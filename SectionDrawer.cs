using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CISCSections
{
    /// <summary>
    /// Draws 2-D representations of CISC metric sections in AutoCAD model space.
    ///
    /// Coordinate convention:
    ///   Cross-section : centroid placed at insertPt; X = width, Y = depth
    ///   Side / Top    : insertPt is the bottom-left corner; X = beam length, Y = depth/width
    ///
    /// Layers created on first use:
    ///   CISC-VISIBLE  – colour 7 (white/black), continuous
    ///   CISC-HIDDEN   – colour 3 (green),  HIDDEN2 linetype (dashed)
    /// </summary>
    public class SectionDrawer
    {
        private const string VIS_LAYER    = "CISC-VISIBLE";
        private const string HIDDEN_LAYER = "CISC-HIDDEN";
        private const string HIDDEN_LT    = "DASHED";

        private readonly Database         _db;
        private readonly Transaction      _tr;
        private readonly BlockTableRecord _ms;
        private readonly List<ObjectId>   _created = new List<ObjectId>();
        public SectionDrawer(Database db, Transaction tr)
        {
            _db = db;
            _tr = tr;
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            _ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            EnsureLayers();
        }

        // ─── Public entry point ─────────────────────────────────────────────

        // pivot: point to rotate around (defaults to ip when not supplied)
        public void Draw(SectionProperties s, ViewType view, Point3d ip,
                         bool showHidden, double beamLength, double angle = 0,
                         Point3d? pivot = null)
        {
            _created.Clear();

            switch (s.Type)
            {
                case SectionType.WShape:         DrawW    (s, view, ip, showHidden, beamLength); break;
                case SectionType.SShape:         DrawS    (s, view, ip, showHidden, beamLength); break;
                case SectionType.Channel:        DrawC    (s, view, ip, showHidden, beamLength); break;
                case SectionType.Angle:          DrawL    (s, view, ip, showHidden, beamLength); break;
                case SectionType.HSSRectangular: DrawHSSR (s, view, ip, showHidden, beamLength); break;
                case SectionType.HSSCircular:    DrawHSSC (s, view, ip, showHidden, beamLength); break;
            }

            if (Math.Abs(angle) > 1e-10)
            {
                var rotPt = pivot ?? ip;
                var mat   = Matrix3d.Rotation(angle, Vector3d.ZAxis, rotPt);
                foreach (var id in _created)
                    ((Entity)_tr.GetObject(id, OpenMode.ForWrite)).TransformBy(mat);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  W / I  S H A P E S
        // ────────────────────────────────────────────────────────────────────

        private void DrawW(SectionProperties s, ViewType view, Point3d ip,
                           bool hidden, double L)
        {
            double d = s.d, bf = s.bf, tf = s.tf, tw = s.tw;
            double r = Math.Max(1.0, s.k - tf);   // CISC k: outer-face-of-flange to fillet toe

            if (view == ViewType.CrossSection)
            {
                const double bF = -0.41421356; // CW 90° concave fillets
                AddPolyline(ip, VIS_LAYER,
                    (-bf/2,       -d/2,       0.0),
                    ( bf/2,       -d/2,       0.0),
                    ( bf/2,       -d/2+tf,    0.0),
                    ( tw/2+r,     -d/2+tf,    bF),
                    ( tw/2,       -d/2+tf+r,  0.0),
                    ( tw/2,        d/2-tf-r,  bF),
                    ( tw/2+r,      d/2-tf,    0.0),
                    ( bf/2,        d/2-tf,    0.0),
                    ( bf/2,        d/2,       0.0),
                    (-bf/2,        d/2,       0.0),
                    (-bf/2,        d/2-tf,    0.0),
                    (-(tw/2+r),    d/2-tf,    bF),
                    (-tw/2,        d/2-tf-r,  0.0),
                    (-tw/2,       -d/2+tf+r,  bF),
                    (-(tw/2+r),   -d/2+tf,    0.0),
                    (-bf/2,       -d/2+tf,    0.0)
                );
            }
            else if (view == ViewType.SideView)
            {
                double x0 = ip.X, y0 = ip.Y;
                AddRect(x0, y0, L, d, VIS_LAYER);
                string jLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+tf,   jLyr);
                HLine(x0, x0+L, y0+d-tf, jLyr);
            }
            else // TopView
            {
                double x0 = ip.X, y0 = ip.Y;
                AddRect(x0, y0, L, bf, VIS_LAYER);
                string wLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+(bf-tw)/2, wLyr);
                HLine(x0, x0+L, y0+(bf+tw)/2, wLyr);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  S  S H A P E S   (American Standard — tapered flanges)
        // ────────────────────────────────────────────────────────────────────

        private void DrawS(SectionProperties s, ViewType view, Point3d ip,
                           bool hidden, double L)
        {
            double d = s.d, bf = s.bf, tf = s.tf, tw = s.tw;

            // Standard 1:6 taper slope; tabulated tf is the average flange thickness.
            // tfRoot = thickness at the web face, tfTip = thickness at the flange toe.
            double drop   = bf / 12.0;
            double tfRoot = tf + drop;
            double tfTip  = Math.Max(2.0, tf - drop);
            // k is measured from average-thickness flange face; cap so arc fits inside tfRoot
            double r      = Math.Max(1.0, Math.Min(s.k - tf, tfRoot - 1.0));

            if (view == ViewType.CrossSection)
            {
                const double bF = -0.41421356;
                AddPolyline(ip, VIS_LAYER,
                    (-bf/2,         -d/2,           0.0),
                    ( bf/2,         -d/2,           0.0),
                    ( bf/2,         -d/2+tfTip,     0.0),
                    ( tw/2+r,       -d/2+tfRoot,    bF),
                    ( tw/2,         -d/2+tfRoot+r,  0.0),
                    ( tw/2,          d/2-tfRoot-r,  bF),
                    ( tw/2+r,        d/2-tfRoot,    0.0),
                    ( bf/2,          d/2-tfTip,     0.0),
                    ( bf/2,          d/2,           0.0),
                    (-bf/2,          d/2,           0.0),
                    (-bf/2,          d/2-tfTip,     0.0),
                    (-(tw/2+r),      d/2-tfRoot,    bF),
                    (-tw/2,          d/2-tfRoot-r,  0.0),
                    (-tw/2,         -d/2+tfRoot+r,  bF),
                    (-(tw/2+r),     -d/2+tfRoot,    0.0),
                    (-bf/2,         -d/2+tfTip,     0.0)
                );
            }
            else if (view == ViewType.SideView)
            {
                double x0 = ip.X, y0 = ip.Y;
                AddRect(x0, y0, L, d, VIS_LAYER);
                string jLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+tf,   jLyr);
                HLine(x0, x0+L, y0+d-tf, jLyr);
            }
            else // TopView
            {
                double x0 = ip.X, y0 = ip.Y;
                AddRect(x0, y0, L, bf, VIS_LAYER);
                string wLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+(bf-tw)/2, wLyr);
                HLine(x0, x0+L, y0+(bf+tw)/2, wLyr);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  C  C H A N N E L S   (tapered flanges)
        // ────────────────────────────────────────────────────────────────────

        private void DrawC(SectionProperties s, ViewType view, Point3d ip,
                           bool hidden, double L)
        {
            double d = s.d, bf = s.bf, tf = s.tf, tw = s.tw;
            double wx = tw / 2;

            // Standard 1:6 taper slope; tabulated tf is the average flange thickness.
            double drop   = bf / 12.0;
            double tfRoot = tf + drop;
            double tfTip  = Math.Max(2.0, tf - drop);
            // k is measured from average-thickness flange face; cap so arc fits inside tfRoot
            double r      = Math.Max(1.0, Math.Min(s.k - tf, tfRoot - 1.0));

            if (view == ViewType.CrossSection)
            {
                const double bF = 0.41421356; // CCW 90° fillets for channel trace
                AddPolyline(ip, VIS_LAYER,
                    (-wx,       -d/2,           0.0),
                    (-wx,        d/2,           0.0),
                    ( bf-wx,     d/2,           0.0),
                    ( bf-wx,     d/2-tfTip,     0.0),
                    ( wx+r,      d/2-tfRoot,    bF),
                    ( wx,        d/2-tfRoot-r,  0.0),
                    ( wx,       -d/2+tfRoot+r,  bF),
                    ( wx+r,     -d/2+tfRoot,    0.0),
                    ( bf-wx,    -d/2+tfTip,     0.0),
                    ( bf-wx,    -d/2,           0.0)
                );
            }
            else if (view == ViewType.SideView)
            {
                double x0 = ip.X, y0 = ip.Y;
                AddRect(x0, y0, L, d, VIS_LAYER);
                string jLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+tf,   jLyr);
                HLine(x0, x0+L, y0+d-tf, jLyr);
            }
            else // TopView
            {
                double x0 = ip.X, y0 = ip.Y;
                AddRect(x0, y0, L, bf, VIS_LAYER);
                string wLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+tw, wLyr);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  L  A N G L E S
        // ────────────────────────────────────────────────────────────────────

        private void DrawL(SectionProperties s, ViewType view, Point3d ip,
                           bool hidden, double L)
        {
            double H = s.Hleg, B = s.Vleg, t = s.t;
            // r   = root fillet (large, inner concave corner between the two legs)
            // rto = toe radius (moderate, outer corner at each leg tip only)
            // Heel corner at (0,0) is sharp — no radius
            double r   = t;
            double rto = t;

            if (view == ViewType.CrossSection)
            {
                const double bToe  =  0.41421356; // CCW 90° toe arcs (convex)
                const double bRoot = -0.41421356; // CW  90° root fillet (concave)
                AddPolyline(ip, VIS_LAYER,
                    ( 0,      0,      0.0),
                    ( B,      0,      0.0),
                    ( B,      t-rto,  bToe),
                    ( B-rto,  t,      0.0),
                    ( t+r,    t,      bRoot),
                    ( t,      t+r,    0.0),
                    ( t,      H-rto,  bToe),
                    ( t-rto,  H,      0.0),
                    ( 0,      H,      0.0)
                );
            }
            else if (view == ViewType.SideView)
            {
                double x0 = ip.X, y0 = ip.Y;
                AddRect(x0, y0, L, H, VIS_LAYER);
                string iLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+t, iLyr);
            }
            else // TopView
            {
                double x0 = ip.X, y0 = ip.Y;
                AddRect(x0, y0, L, B, VIS_LAYER);
                string iLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+t, iLyr);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  H S S   R E C T A N G U L A R
        // ────────────────────────────────────────────────────────────────────

        private void DrawHSSR(SectionProperties s, ViewType view, Point3d ip,
                              bool hidden, double L)
        {
            double H = s.H, B = s.B, t = s.t;
            double r = Math.Max(3.0, t * 0.5); // corner radius

            if (view == ViewType.CrossSection)
            {
                AddRoundedPolyline(ip, -B/2, -H/2, B, H, r, VIS_LAYER);
                string innerLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                AddRoundedPolyline(ip, -B/2+t, -H/2+t, B-2*t, H-2*t, Math.Max(1.0, r-t), innerLyr);
            }
            else if (view == ViewType.SideView)
            {
                double x0 = ip.X, y0 = ip.Y;
                AddRect(x0, y0, L, H, VIS_LAYER);
                string iLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+t,   iLyr);
                HLine(x0, x0+L, y0+H-t, iLyr);
            }
            else // TopView
            {
                double x0 = ip.X, y0 = ip.Y;
                AddRect(x0, y0, L, B, VIS_LAYER);
                string iLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+t,   iLyr);
                HLine(x0, x0+L, y0+B-t, iLyr);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  H S S   C I R C U L A R   ( C H S )
        // ────────────────────────────────────────────────────────────────────

        private void DrawHSSC(SectionProperties s, ViewType view, Point3d ip,
                              bool hidden, double L)
        {
            double D = s.D, t = s.t;

            if (view == ViewType.CrossSection)
            {
                Circle(ip, D / 2,     VIS_LAYER);
                string innerLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                Circle(ip, D / 2 - t, innerLyr);
            }
            else
            {
                double x0 = ip.X, y0 = ip.Y;
                AddRect(x0, y0, L, D, VIS_LAYER);
                string iLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+t,   iLyr);
                HLine(x0, x0+L, y0+D-t, iLyr);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  P r i m i t i v e   h e l p e r s
        // ────────────────────────────────────────────────────────────────────


        private void HLine(double x1, double x2, double y, string layer)
            => AddLine(new Point3d(x1, y, 0), new Point3d(x2, y, 0), layer);

        private void VLine(double x, double y1, double y2, string layer)
            => AddLine(new Point3d(x, y1, 0), new Point3d(x, y2, 0), layer);

        private void AddRect(double x0, double y0, double w, double h, string layer)
        {
            var pl = new Polyline(4);
            pl.AddVertexAt(0, new Point2d(x0,   y0),   0, 0, 0);
            pl.AddVertexAt(1, new Point2d(x0+w, y0),   0, 0, 0);
            pl.AddVertexAt(2, new Point2d(x0+w, y0+h), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(x0,   y0+h), 0, 0, 0);
            pl.Closed = true;
            pl.Layer  = layer;
            _ms.AppendEntity(pl);
            _tr.AddNewlyCreatedDBObject(pl, true);
            _created.Add(pl.ObjectId);
        }

        // Closed polyline with arc bulges. pts = (dx, dy, bulge) relative to ip.
        // Bulge: +tan(θ/4) for CCW arc, -tan(θ/4) for CW arc; 0 for straight.
        private void AddPolyline(Point3d ip, string layer,
                                 params (double dx, double dy, double b)[] pts)
        {
            var pl = new Polyline(pts.Length);
            for (int i = 0; i < pts.Length; i++)
                pl.AddVertexAt(i, new Point2d(ip.X + pts[i].dx, ip.Y + pts[i].dy),
                               pts[i].b, 0.0, 0.0);
            pl.Closed = true;
            pl.Layer  = layer;
            _ms.AppendEntity(pl);
            _tr.AddNewlyCreatedDBObject(pl, true);
            _created.Add(pl.ObjectId);
        }

        // Closed rounded-rectangle polyline offset from ip.
        private void AddRoundedPolyline(Point3d ip, double dx, double dy,
                                        double w, double h, double r, string layer)
        {
            const double bA = 0.41421356; // CCW 90° arc
            double x = ip.X + dx, y = ip.Y + dy;
            var pl = new Polyline(8);
            pl.AddVertexAt(0, new Point2d(x+r,   y),     0.0, 0.0, 0.0);
            pl.AddVertexAt(1, new Point2d(x+w-r, y),     bA,  0.0, 0.0);
            pl.AddVertexAt(2, new Point2d(x+w,   y+r),   0.0, 0.0, 0.0);
            pl.AddVertexAt(3, new Point2d(x+w,   y+h-r), bA,  0.0, 0.0);
            pl.AddVertexAt(4, new Point2d(x+w-r, y+h),   0.0, 0.0, 0.0);
            pl.AddVertexAt(5, new Point2d(x+r,   y+h),   bA,  0.0, 0.0);
            pl.AddVertexAt(6, new Point2d(x,     y+h-r), 0.0, 0.0, 0.0);
            pl.AddVertexAt(7, new Point2d(x,     y+r),   bA,  0.0, 0.0);
            pl.Closed = true;
            pl.Layer  = layer;
            _ms.AppendEntity(pl);
            _tr.AddNewlyCreatedDBObject(pl, true);
            _created.Add(pl.ObjectId);
        }

        private void AddLine(Point3d start, Point3d end, string layer)
        {
            if (start.IsEqualTo(end)) return;
            var ln = new Line(start, end) { Layer = layer };
            _ms.AppendEntity(ln);
            _tr.AddNewlyCreatedDBObject(ln, true);
            _created.Add(ln.ObjectId);
        }

        private void Circle(Point3d centre, double radius, string layer)
        {
            if (radius <= 0) return;
            var c = new Circle(centre, Vector3d.ZAxis, radius) { Layer = layer };
            _ms.AppendEntity(c);
            _tr.AddNewlyCreatedDBObject(c, true);
            _created.Add(c.ObjectId);
        }

        // ────────────────────────────────────────────────────────────────────
        //  L a y e r  /  L i n e t y p e   s e t u p
        // ────────────────────────────────────────────────────────────────────

        private void EnsureLayers()
        {
            EnsureLayer(VIS_LAYER,    Color.FromColorIndex(ColorMethod.ByAci, 6),  ObjectId.Null);
            EnsureLayer(HIDDEN_LAYER, Color.FromColorIndex(ColorMethod.ByAci, 5),  GetOrLoadLinetype(HIDDEN_LT));
        }

        private void EnsureLayer(string name, Color color, ObjectId linetypeId)
        {
            var lt = (LayerTable)_tr.GetObject(_db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(name)) return;

            lt.UpgradeOpen();
            var ltr = new LayerTableRecord { Name = name, Color = color };
            if (linetypeId != ObjectId.Null)
                ltr.LinetypeObjectId = linetypeId;
            lt.Add(ltr);
            _tr.AddNewlyCreatedDBObject(ltr, true);
        }

        private ObjectId GetOrLoadLinetype(string ltName)
        {
            var ltt = (LinetypeTable)_tr.GetObject(_db.LinetypeTableId, OpenMode.ForRead);
            if (ltt.Has(ltName)) return ltt[ltName];

            try { _db.LoadLineTypeFile(ltName, "acad.lin"); } catch { }

            ltt = (LinetypeTable)_tr.GetObject(_db.LinetypeTableId, OpenMode.ForRead);
            return ltt.Has(ltName) ? ltt[ltName] : ObjectId.Null;
        }
    }
}
