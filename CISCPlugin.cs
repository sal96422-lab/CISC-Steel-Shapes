using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System.Windows.Forms;

// Disambiguate: both Autodesk and WinForms define a class named "Application"
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

// Register command class and extension application
[assembly: CommandClass(typeof(CISCSections.CISCPlugin))]
[assembly: ExtensionApplication(typeof(CISCSections.CISCPlugin))]

namespace CISCSections
{
    public class CISCPlugin : IExtensionApplication
    {
        // Called once when AutoCAD loads the DLL
        public void Initialize()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage(
                "\nCISC Metric Sections v1.0 loaded.  " +
                "Type  CISCINSERT  to insert a section.\n");
        }

        public void Terminate() { }

        // ─── Main command ────────────────────────────────────────────────────

        [CommandMethod("CISCINSERT", CommandFlags.Modal)]
        public void InsertSection()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            var db  = doc.Database;
            var ed  = doc.Editor;

            // Show selection dialog
            using (var dlg = new SectionDialog())
            {
                if (AcadApp.ShowModalDialog(dlg) != DialogResult.OK)
                    return;

                SectionProperties sec    = dlg.SelectedSection;
                ViewType          view   = dlg.SelectedView;
                bool              showH  = dlg.ShowHiddenLines;
                string            refPt  = dlg.CrossRefPoint;

                if (sec == null) return;

                // Pick start point
                var ppr = ed.GetPoint("\nSpecify start point: ");
                if (ppr.Status != PromptStatus.OK) return;
                Point3d startPt = ppr.Value;

                double L     = 0;
                double angle = 0;

                // Always pick a second point — defines end point (side/top) or direction (cross-section)
                string prompt2 = view == ViewType.CrossSection
                    ? "\nSpecify direction point: "
                    : "\nSpecify end point: ";
                var opts = new PromptPointOptions(prompt2);
                opts.UseBasePoint = true;
                opts.BasePoint    = startPt;
                var ppr2 = ed.GetPoint(opts);
                if (ppr2.Status != PromptStatus.OK) return;

                Point3d endPt = ppr2.Value;
                double  dx    = endPt.X - startPt.X;
                double  dy    = endPt.Y - startPt.Y;
                double  dist  = Math.Sqrt(dx * dx + dy * dy);
                angle = Math.Atan2(dy, dx);

                if (dist < 1.0)
                {
                    ed.WriteMessage("\nSecond point too close to start — cancelled.\n");
                    return;
                }

                if (view != ViewType.CrossSection)
                    L = dist;

                // For cross-section, offset insertion point by reference choice
                // Rotation in SectionDrawer is around drawPt, so apply offset first
                Point3d drawPt = view == ViewType.CrossSection
                    ? ApplyCrossRefOffset(sec, startPt, refPt)
                    : startPt;

                // Draw inside a transaction
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var drawer = new SectionDrawer(db, tr);
                    drawer.Draw(sec, view, drawPt, showH, L, angle);
                    tr.Commit();
                }

                ed.WriteMessage($"\nInserted {sec.Name}  [{view}]  angle={angle * 180 / Math.PI:F1}°");
                if (view != ViewType.CrossSection)
                    ed.WriteMessage($"  L={L:F0} mm");
                if (showH) ed.WriteMessage("  (hidden lines on layer CISC-HIDDEN)");
                ed.WriteMessage("\n");
            }
        }
        // Shifts the draw centre so that the chosen face of the cross-section lands on ip.
        // W/S/C/HSS are drawn centred at ip; L angles are drawn from bottom-left at ip.
        private static Point3d ApplyCrossRefOffset(SectionProperties sec, Point3d ip, string refPt)
        {
            if (refPt == "Centre")
            {
                // L angle is drawn from bottom-left — shift up so centroid sits at ip
                if (sec.Type == SectionType.Angle)
                    return new Point3d(ip.X, ip.Y - sec.Hleg / 2, ip.Z);
                return ip;
            }

            double halfH;
            switch (sec.Type)
            {
                case SectionType.WShape:
                case SectionType.SShape:
                case SectionType.Channel:    halfH = sec.d / 2;  break;
                case SectionType.HSSRectangular: halfH = sec.H / 2; break;
                case SectionType.HSSCircular:    halfH = sec.D / 2; break;
                default: halfH = sec.Hleg / 2; break; // L angle fallback
            }

            if (sec.Type == SectionType.Angle)
            {
                // L is drawn from bottom at ip.Y
                double yOff = refPt == "Top" ? -sec.Hleg : 0; // Bottom = no shift
                return new Point3d(ip.X, ip.Y + yOff, ip.Z);
            }
            else
            {
                // All others centred at ip
                double yOff = refPt == "Top" ? -halfH : halfH; // Top = shift centre down; Bottom = shift up
                return new Point3d(ip.X, ip.Y + yOff, ip.Z);
            }
        }
    }
}
