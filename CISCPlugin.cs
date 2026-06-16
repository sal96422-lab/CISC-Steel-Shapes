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

                SectionProperties sec  = dlg.SelectedSection;
                ViewType          view = dlg.SelectedView;
                bool              showH = dlg.ShowHiddenLines;
                double            L    = dlg.BeamLength;

                if (sec == null) return;

                // Pick insertion point in AutoCAD
                var ppr = ed.GetPoint("\nSpecify insertion point: ");
                if (ppr.Status != PromptStatus.OK)
                    return;

                Point3d ip = ppr.Value;

                // Draw inside a transaction
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var drawer = new SectionDrawer(db, tr);
                    drawer.Draw(sec, view, ip, showH, L);
                    tr.Commit();
                }

                ed.WriteMessage($"\nInserted {sec.Name}  [{view}]");
                if (showH) ed.WriteMessage("  (hidden lines on layer CISC-HIDDEN)");
                ed.WriteMessage("\n");
            }
        }
    }
}
