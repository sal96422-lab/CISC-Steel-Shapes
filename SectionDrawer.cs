using System;
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
        private const string HIDDEN_LT    = "HIDDEN2";

        private readonly Database         _db;
        private readonly Transaction      _tr;
        private readonly BlockTableRecord _ms;
        private          BlockTableRecord _target; // redirected to block BTR during Draw()

        public SectionDrawer(Database db, Transaction tr)
        {
            _db = db;
            _tr = tr;
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            _ms     = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            _target = _ms;
            EnsureLayers();
        }

        // ─── Public entry point ─────────────────────────────────────────────

        public void Draw(SectionProperties s, ViewType view, Point3d ip,
                         bool showHidden, double beamLength)
        {
            // Create an anonymous block so the whole section is one selectable object
            var bt  = (BlockTable)_tr.GetObject(_db.BlockTableId, OpenMode.ForWrite);
            var btr = new BlockTableRecord { Name = "*U" };
            var btrId = bt.Add(btr);
            _tr.AddNewlyCreatedDBObject(btr, true);

            _target = btr; // redirect entity additions into the block

            switch (s.Type)
            {
                case SectionType.WShape:         DrawW    (s, view, ip, showHidden, beamLength); break;
                case SectionType.SShape:         DrawS    (s, view, ip, showHidden, beamLength); break;
                case SectionType.Channel:        DrawC    (s, view, ip, showHidden, beamLength); break;
                case SectionType.Angle:          DrawL    (s, view, ip, showHidden, beamLength); break;
                case SectionType.HSSRectangular: DrawHSSR (s, view, ip, showHidden, beamLength); break;
                case SectionType.HSSCircular:    DrawHSSC (s, view, ip, showHidden, beamLength); break;
            }

            _target = _ms; // restore

            // Insert block reference at world origin (geometry already at absolute coords)
            var bref = new BlockReference(Point3d.Origin, btrId) { Layer = VIS_LAYER };
            _ms.AppendEntity(bref);
            _tr.AddNewlyCreatedDBObject(bref, true);
        }

        // ────────────────────────────────────────────────────────────────────
        //  W / I  S H A P E S
        // ────────────────────────────────────────────────────────────────────

        private void DrawW(SectionProperties s, ViewType view, Point3d ip,
                           bool hidden, double L)
        {
            double d = s.d, bf = s.bf, tf = s.tf, tw = s.tw;
            // Estimated fillet radius — approximates CISC k value
            double r = Math.Max(5.0, tf * 0.8);

            if (view == ViewType.CrossSection)
            {
                // ── Outer flange edges (full width, no fillet needed) ──────
                Seg(ip, -bf/2,  d/2,    +bf/2,  d/2,    VIS_LAYER); // top flange top
                Seg(ip, +bf/2,  d/2,    +bf/2,  d/2-tf, VIS_LAYER); // top flange right tip
                Seg(ip, -bf/2,  d/2,    -bf/2,  d/2-tf, VIS_LAYER); // top flange left tip
                Seg(ip, -bf/2, -d/2,    +bf/2, -d/2,    VIS_LAYER); // bottom flange bottom
                Seg(ip, +bf/2, -d/2,    +bf/2, -d/2+tf, VIS_LAYER); // bottom flange right tip
                Seg(ip, -bf/2, -d/2,    -bf/2, -d/2+tf, VIS_LAYER); // bottom flange left tip

                // ── Inner flange faces — shortened to leave room for fillets ──
                // Top flange inner face, right half: from right tip to start of fillet
                Seg(ip, +bf/2, d/2-tf,  +(tw/2+r), d/2-tf, VIS_LAYER);
                // Top flange inner face, left half
                Seg(ip, -bf/2, d/2-tf,  -(tw/2+r), d/2-tf, VIS_LAYER);
                // Bottom flange inner face, right half
                Seg(ip, +bf/2, -d/2+tf, +(tw/2+r), -d/2+tf, VIS_LAYER);
                // Bottom flange inner face, left half
                Seg(ip, -bf/2, -d/2+tf, -(tw/2+r), -d/2+tf, VIS_LAYER);

                // ── Web — shortened at both ends for fillets ───────────────
                Seg(ip, +tw/2,  d/2-tf-r, +tw/2, -d/2+tf+r, VIS_LAYER); // right web
                Seg(ip, -tw/2,  d/2-tf-r, -tw/2, -d/2+tf+r, VIS_LAYER); // left web

                // ── Fillet arcs at the 4 inner web-flange corners ──────────
                // Angles in radians, drawn CCW.
                // Top-right  corner: arc from 90° → 180°
                FilletArc(ip, +(tw/2+r),  d/2-tf-r, r, Math.PI/2, Math.PI);
                // Top-left   corner: arc from 0°  → 90°
                FilletArc(ip, -(tw/2+r),  d/2-tf-r, r, 0,         Math.PI/2);
                // Bottom-right corner: arc from 180° → 270°
                FilletArc(ip, +(tw/2+r), -d/2+tf+r, r, Math.PI,   3*Math.PI/2);
                // Bottom-left  corner: arc from 270° → 360°
                FilletArc(ip, -(tw/2+r), -d/2+tf+r, r, 3*Math.PI/2, 2*Math.PI);
            }
            else if (view == ViewType.SideView)
            {
                double x0 = ip.X, y0 = ip.Y;

                // Outer rectangle — always solid
                HLine(x0, x0+L, y0,    VIS_LAYER);
                HLine(x0, x0+L, y0+d,  VIS_LAYER);
                VLine(x0,   y0, y0+d,  VIS_LAYER);
                VLine(x0+L, y0, y0+d,  VIS_LAYER);

                // Flange-web junction lines:
                //   solid when no hidden lines, dashed when hidden lines on
                //   (these represent the inner faces of the flanges)
                string jLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+tf,   jLyr);
                HLine(x0, x0+L, y0+d-tf, jLyr);
            }
            else // TopView
            {
                double x0 = ip.X, y0 = ip.Y;

                // Outer rectangle — flange footprint
                HLine(x0, x0+L, y0,    VIS_LAYER);
                HLine(x0, x0+L, y0+bf, VIS_LAYER);
                VLine(x0,   y0, y0+bf, VIS_LAYER);
                VLine(x0+L, y0, y0+bf, VIS_LAYER);

                // Web edges — always shown; solid when no hidden lines, dashed when on
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
            double r      = Math.Max(5.0, tfRoot * 0.25);

            if (view == ViewType.CrossSection)
            {
                // ── Top flange ───────────────────────────────────────────────
                Seg(ip, -bf/2,  d/2,    +bf/2,  d/2,    VIS_LAYER); // outer top
                Seg(ip, +bf/2,  d/2,    +bf/2,  d/2-tfTip, VIS_LAYER); // right tip
                Seg(ip, -bf/2,  d/2,    -bf/2,  d/2-tfTip, VIS_LAYER); // left tip
                // Inner faces — sloped from toe (thin) to root (thick), shortened for fillet
                Seg(ip, +bf/2, d/2-tfTip, +(tw/2+r), d/2-tfRoot, VIS_LAYER);
                Seg(ip, -bf/2, d/2-tfTip, -(tw/2+r), d/2-tfRoot, VIS_LAYER);
                // Fillet arcs at top corners (same quarter-circles as W but at tfRoot)
                FilletArc(ip, +(tw/2+r),  d/2-tfRoot-r, r, Math.PI/2,   Math.PI);
                FilletArc(ip, -(tw/2+r),  d/2-tfRoot-r, r, 0,            Math.PI/2);

                // ── Web ──────────────────────────────────────────────────────
                Seg(ip, +tw/2,  d/2-tfRoot-r, +tw/2, -d/2+tfRoot+r, VIS_LAYER);
                Seg(ip, -tw/2,  d/2-tfRoot-r, -tw/2, -d/2+tfRoot+r, VIS_LAYER);

                // ── Bottom flange ─────────────────────────────────────────────
                FilletArc(ip, +(tw/2+r), -d/2+tfRoot+r, r, Math.PI,   3*Math.PI/2);
                FilletArc(ip, -(tw/2+r), -d/2+tfRoot+r, r, 3*Math.PI/2, 2*Math.PI);
                // Inner sloped faces — from root (thick) to toe (thin)
                Seg(ip, +(tw/2+r), -d/2+tfRoot, +bf/2, -d/2+tfTip, VIS_LAYER);
                Seg(ip, -(tw/2+r), -d/2+tfRoot, -bf/2, -d/2+tfTip, VIS_LAYER);
                Seg(ip, +bf/2, -d/2+tfTip, +bf/2, -d/2, VIS_LAYER); // right tip
                Seg(ip, -bf/2, -d/2+tfTip, -bf/2, -d/2, VIS_LAYER); // left tip
                Seg(ip, -bf/2, -d/2, +bf/2, -d/2, VIS_LAYER);       // outer bottom
            }
            else if (view == ViewType.SideView)
            {
                double x0 = ip.X, y0 = ip.Y;
                HLine(x0, x0+L, y0,    VIS_LAYER);
                HLine(x0, x0+L, y0+d,  VIS_LAYER);
                VLine(x0,   y0, y0+d,  VIS_LAYER);
                VLine(x0+L, y0, y0+d,  VIS_LAYER);

                string jLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+tf,   jLyr);
                HLine(x0, x0+L, y0+d-tf, jLyr);
            }
            else // TopView
            {
                double x0 = ip.X, y0 = ip.Y;
                HLine(x0, x0+L, y0,    VIS_LAYER);
                HLine(x0, x0+L, y0+bf, VIS_LAYER);
                VLine(x0,   y0, y0+bf, VIS_LAYER);
                VLine(x0+L, y0, y0+bf, VIS_LAYER);

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
            double r      = Math.Max(3.0, tfRoot * 0.25);

            if (view == ViewType.CrossSection)
            {
                // Outer web left edge (full height)
                Seg(ip, -wx, -d/2, -wx, d/2, VIS_LAYER);
                // Top flange outer top
                Seg(ip, -wx, d/2, bf-wx, d/2, VIS_LAYER);
                // Top flange tip — only tfTip tall
                Seg(ip, bf-wx, d/2, bf-wx, d/2-tfTip, VIS_LAYER);
                // Top flange inner — sloped from toe (thin) to root (thick), shortened for fillet
                Seg(ip, bf-wx, d/2-tfTip, wx+r, d/2-tfRoot, VIS_LAYER);
                // Fillet arc at top-inner corner (90°→180°)
                FilletArc(ip, wx+r, d/2-tfRoot-r, r, Math.PI/2, Math.PI);
                // Inner web — shortened at both ends
                Seg(ip, wx, d/2-tfRoot-r, wx, -d/2+tfRoot+r, VIS_LAYER);
                // Fillet arc at bottom-inner corner (180°→270°)
                FilletArc(ip, wx+r, -d/2+tfRoot+r, r, Math.PI, 3*Math.PI/2);
                // Bottom flange inner — sloped from root (thick) to toe (thin)
                Seg(ip, wx+r, -d/2+tfRoot, bf-wx, -d/2+tfTip, VIS_LAYER);
                // Bottom flange tip — only tfTip tall
                Seg(ip, bf-wx, -d/2+tfTip, bf-wx, -d/2, VIS_LAYER);
                // Bottom outer
                Seg(ip, bf-wx, -d/2, -wx, -d/2, VIS_LAYER);
            }
            else if (view == ViewType.SideView)
            {
                double x0 = ip.X, y0 = ip.Y;
                HLine(x0, x0+L, y0,    VIS_LAYER);
                HLine(x0, x0+L, y0+d,  VIS_LAYER);
                VLine(x0,   y0, y0+d,  VIS_LAYER);
                VLine(x0+L, y0, y0+d,  VIS_LAYER);

                string jLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+tf,   jLyr);
                HLine(x0, x0+L, y0+d-tf, jLyr);
            }
            else // TopView
            {
                double x0 = ip.X, y0 = ip.Y;
                HLine(x0, x0+L, y0,    VIS_LAYER);
                HLine(x0, x0+L, y0+bf, VIS_LAYER);
                VLine(x0,   y0, y0+bf, VIS_LAYER);
                VLine(x0+L, y0, y0+bf, VIS_LAYER);

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
            double ipx = ip.X, ipy = ip.Y;

            if (view == ViewType.CrossSection)
            {
                // ── Outer faces — all sharp corners, no arcs ───────────────
                Seg(ip, 0, H, 0, 0,  VIS_LAYER); // outer left face (full height)
                Seg(ip, 0, 0, B, 0,  VIS_LAYER); // outer bottom face (full width)

                // ── Right (toe) face — shortened at top for inner arc ──────
                Seg(ip, B, 0, B, t-rto, VIS_LAYER);

                // Inner toe arc at (B, t) — concave corner inside horizontal leg tip
                // Centre (B-rto, t-rto): 0°→90° CCW: from (B,t-rto) to (B-rto,t)
                AddCornerArc(ipx+B-rto, ipy+t-rto, rto, 0, Math.PI/2, VIS_LAYER);

                // ── Inner horizontal face ──────────────────────────────────
                Seg(ip, B-rto, t, t+r, t, VIS_LAYER);

                // Root fillet arc: 180°→270° CCW from (t,t+r) to (t+r,t)
                FilletArc(ip, t+r, t+r, r, Math.PI, 3*Math.PI/2);

                // ── Inner vertical face — shortened at top for inner arc ───
                Seg(ip, t, t+r, t, H-rto, VIS_LAYER);

                // Inner toe arc at (t, H) — concave corner inside vertical leg tip
                // Centre (t-rto, H-rto): 0°→90° CCW: from (t,H-rto) to (t-rto,H)
                AddCornerArc(ipx+t-rto, ipy+H-rto, rto, 0, Math.PI/2, VIS_LAYER);

                // ── Top face of vertical leg ───────────────────────────────
                Seg(ip, t-rto, H, 0, H, VIS_LAYER);
            }
            else if (view == ViewType.SideView)
            {
                double x0 = ip.X, y0 = ip.Y;
                HLine(x0, x0+L, y0,    VIS_LAYER);
                HLine(x0, x0+L, y0+H,  VIS_LAYER);
                VLine(x0,   y0, y0+H,  VIS_LAYER);
                VLine(x0+L, y0, y0+H,  VIS_LAYER);
                HLine(x0, x0+L, y0+t,  VIS_LAYER);

                if (hidden) HLine(x0, x0+L, y0+H-t, HIDDEN_LAYER);
            }
            else // TopView
            {
                double x0 = ip.X, y0 = ip.Y;
                HLine(x0, x0+L, y0,    VIS_LAYER);
                HLine(x0, x0+L, y0+B,  VIS_LAYER);
                VLine(x0,   y0, y0+B,  VIS_LAYER);
                VLine(x0+L, y0, y0+B,  VIS_LAYER);
                // Single inner line at y=t — top face of horizontal leg (no symmetric counterpart on an L)
                string innerLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0+t, innerLyr);
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
                // Outer rectangle (with rounded corners)
                RoundedRect(ip, -B/2, -H/2, B, H, r, VIS_LAYER);
                // Inner rectangle
                string innerLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                RoundedRect(ip, -B/2+t, -H/2+t, B-2*t, H-2*t, Math.Max(1, r-t), innerLyr);
            }
            else if (view == ViewType.SideView)
            {
                double x0 = ip.X, y0 = ip.Y;
                string iLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0,      VIS_LAYER);
                HLine(x0, x0+L, y0+H,    VIS_LAYER);
                VLine(x0,   y0, y0+H,    VIS_LAYER);
                VLine(x0+L, y0, y0+H,    VIS_LAYER);
                HLine(x0, x0+L, y0+t,    iLyr);
                HLine(x0, x0+L, y0+H-t,  iLyr);
            }
            else // TopView
            {
                double x0 = ip.X, y0 = ip.Y;
                string iLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0,      VIS_LAYER);
                HLine(x0, x0+L, y0+B,    VIS_LAYER);
                VLine(x0,   y0, y0+B,    VIS_LAYER);
                VLine(x0+L, y0, y0+B,    VIS_LAYER);
                HLine(x0, x0+L, y0+t,    iLyr);
                HLine(x0, x0+L, y0+B-t,  iLyr);
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
                string iLyr = hidden ? HIDDEN_LAYER : VIS_LAYER;
                HLine(x0, x0+L, y0,      VIS_LAYER);
                HLine(x0, x0+L, y0+D,    VIS_LAYER);
                VLine(x0,   y0, y0+D,    VIS_LAYER);
                VLine(x0+L, y0, y0+D,    VIS_LAYER);
                HLine(x0, x0+L, y0+t,    iLyr);
                HLine(x0, x0+L, y0+D-t,  iLyr);
            }
        }

        // ────────────────────────────────────────────────────────────────────
        //  P r i m i t i v e   h e l p e r s
        // ────────────────────────────────────────────────────────────────────

        /// Line segment offset from origin point op by (dx1,dy1)→(dx2,dy2).
        private void Seg(Point3d op, double dx1, double dy1, double dx2, double dy2, string layer)
        {
            AddLine(new Point3d(op.X+dx1, op.Y+dy1, op.Z),
                    new Point3d(op.X+dx2, op.Y+dy2, op.Z), layer);
        }

        private void HLine(double x1, double x2, double y, string layer)
            => AddLine(new Point3d(x1, y, 0), new Point3d(x2, y, 0), layer);

        private void VLine(double x, double y1, double y2, string layer)
            => AddLine(new Point3d(x, y1, 0), new Point3d(x, y2, 0), layer);

        /// Fillet arc offset from origin ip, drawn CCW from startAngle to endAngle (radians).
        private void FilletArc(Point3d ip, double cx, double cy, double r,
                               double startAngle, double endAngle)
        {
            var arc = new Arc();
            arc.Center     = new Point3d(ip.X + cx, ip.Y + cy, ip.Z);
            arc.Radius     = r;
            arc.StartAngle = startAngle;
            arc.EndAngle   = endAngle;
            arc.Normal     = Vector3d.ZAxis;
            arc.Layer      = VIS_LAYER;
            _target.AppendEntity(arc);
            _tr.AddNewlyCreatedDBObject(arc, true);
        }

        /// Rounded rectangle — 4 lines with fillet arcs at corners.
        private void RoundedRect(Point3d ip, double dx, double dy, double w, double h,
                                 double r, string layer)
        {
            double x = ip.X + dx, y = ip.Y + dy;

            // Straight segments
            AddLine(new Point3d(x+r,   y,   0), new Point3d(x+w-r, y,   0), layer); // bottom
            AddLine(new Point3d(x+w,   y+r, 0), new Point3d(x+w,   y+h-r, 0), layer); // right
            AddLine(new Point3d(x+w-r, y+h, 0), new Point3d(x+r,   y+h, 0), layer); // top
            AddLine(new Point3d(x,     y+h-r,0), new Point3d(x,     y+r, 0), layer); // left

            // Corner arcs (CCW): bottom-left=180°→270°, bottom-right=270°→360°,
            //                    top-right=0°→90°, top-left=90°→180°
            AddCornerArc(x+r,   y+r,   r, Math.PI,      3*Math.PI/2, layer);
            AddCornerArc(x+w-r, y+r,   r, 3*Math.PI/2,  2*Math.PI,   layer);
            AddCornerArc(x+w-r, y+h-r, r, 0,             Math.PI/2,   layer);
            AddCornerArc(x+r,   y+h-r, r, Math.PI/2,     Math.PI,     layer);
        }

        private void AddCornerArc(double cx, double cy, double r,
                                  double startAngle, double endAngle, string layer)
        {
            var arc = new Arc();
            arc.Center     = new Point3d(cx, cy, 0);
            arc.Radius     = r;
            arc.StartAngle = startAngle;
            arc.EndAngle   = endAngle;
            arc.Normal     = Vector3d.ZAxis;
            arc.Layer      = layer;
            _target.AppendEntity(arc);
            _tr.AddNewlyCreatedDBObject(arc, true);
        }

        private void AddLine(Point3d start, Point3d end, string layer)
        {
            if (start.IsEqualTo(end)) return;
            var ln = new Line(start, end) { Layer = layer };
            _target.AppendEntity(ln);
            _tr.AddNewlyCreatedDBObject(ln, true);
        }

        private void Circle(Point3d centre, double radius, string layer)
        {
            if (radius <= 0) return;
            var c = new Circle(centre, Vector3d.ZAxis, radius) { Layer = layer };
            _target.AppendEntity(c);
            _tr.AddNewlyCreatedDBObject(c, true);
        }

        // ────────────────────────────────────────────────────────────────────
        //  L a y e r  /  L i n e t y p e   s e t u p
        // ────────────────────────────────────────────────────────────────────

        private void EnsureLayers()
        {
            EnsureLayer(VIS_LAYER,    Color.FromColorIndex(ColorMethod.ByAci, 7),  ObjectId.Null);
            EnsureLayer(HIDDEN_LAYER, Color.FromColorIndex(ColorMethod.ByAci, 3),  GetOrLoadLinetype(HIDDEN_LT));
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
