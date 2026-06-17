using System;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Disambiguate: both Autodesk and WinForms define a class named "Application"
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

// Register command class and extension application
[assembly: CommandClass(typeof(CISCSections.CISCPlugin))]
[assembly: ExtensionApplication(typeof(CISCSections.CISCPlugin))]

namespace CISCSections
{
    public class CISCPlugin : IExtensionApplication
    {
        private const string TAB_ID = "CISC_SECTIONS_TAB";

        // Called once when AutoCAD loads the DLL
        public void Initialize()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage(
                "\nCISC Metric Sections v1.0 loaded.  " +
                "Use the CISC Sections ribbon tab or type CISCINSERT.\n");

            // Add ribbon tab — ribbon may not be ready yet at startup
            if (ComponentManager.Ribbon != null)
                AddRibbonTab();
            else
                ComponentManager.ItemInitialized += OnRibbonReady;
        }

        private void OnRibbonReady(object sender, RibbonItemEventArgs e)
        {
            if (ComponentManager.Ribbon != null)
            {
                ComponentManager.ItemInitialized -= OnRibbonReady;
                AddRibbonTab();
            }
        }

        private void AddRibbonTab()
        {
            var ribbon = ComponentManager.Ribbon;
            if (ribbon == null || ribbon.FindTab(TAB_ID) != null) return;

            // ── Tab ──────────────────────────────────────────────────────────
            var tab = new RibbonTab { Title = "CISC Sections", Id = TAB_ID };

            // ── Panel ─────────────────────────────────────────────────────────
            var panelSrc = new RibbonPanelSource { Title = "Insert Section" };
            var panel    = new RibbonPanel { Source = panelSrc };

            // ── Insert button ─────────────────────────────────────────────────
            var btn = new RibbonButton
            {
                Text           = "Insert\nSection",
                ShowText       = true,
                ShowImage      = true,
                LargeImage     = BuildIcon(),
                Size           = RibbonItemSize.Large,
                Orientation    = System.Windows.Controls.Orientation.Vertical,
                ToolTip        = "Insert a CISC metric steel section (CISCINSERT)",
                CommandHandler = new RelayCommand(() =>
                    AcadApp.DocumentManager.MdiActiveDocument
                           ?.SendStringToExecute("CISCINSERT\n", true, false, true))
            };

            panelSrc.Items.Add(btn);
            tab.Panels.Add(panel);
            ribbon.Tabs.Add(tab);
        }

        // Draws a simple I-beam icon at 32×32
        private static ImageSource BuildIcon()
        {
            var dv = new System.Windows.Media.DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var fill = new SolidColorBrush(Color.FromRgb(90, 40, 140)); // purple
                var pen  = new Pen(fill, 0);
                // Top flange
                dc.DrawRectangle(fill, pen, new System.Windows.Rect(2, 2, 28, 6));
                // Web
                dc.DrawRectangle(fill, pen, new System.Windows.Rect(12, 8, 8, 16));
                // Bottom flange
                dc.DrawRectangle(fill, pen, new System.Windows.Rect(2, 24, 28, 6));
            }
            var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Freeze();
            return bmp;
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
                string            refPt  = dlg.RefPoint;

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

                // Offset the draw origin so the reference face (Top/Centre/Bottom) sits on startPt.
                // For cross-section: rotation pivots around the centroid (drawPt).
                // For side/top: rotation pivots around startPt so the beam swings from the picked point.
                Point3d drawPt = ApplyRefOffset(sec, view, startPt, refPt);
                Point3d pivot  = view == ViewType.CrossSection ? drawPt : startPt;

                // Draw inside a transaction
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var drawer = new SectionDrawer(db, tr);
                    drawer.Draw(sec, view, drawPt, showH, L, angle, pivot);
                    tr.Commit();
                }

                ed.WriteMessage($"\nInserted {sec.Name}  [{view}]  angle={angle * 180 / Math.PI:F1}°");
                if (view != ViewType.CrossSection)
                    ed.WriteMessage($"  L={L:F0} mm");
                if (showH) ed.WriteMessage("  (hidden lines on layer CISC-HIDDEN)");
                ed.WriteMessage("\n");
            }
        }
        // Returns the draw-origin point so that the reference face (Top/Centre/Bottom) sits on ip.
        //
        // Cross-section: W/S/C/HSS are centred at draw-origin; L is drawn from bottom-left.
        // Side/Top view: all shapes are drawn with bottom edge at draw-origin (ip = bottom-left corner).
        private static Point3d ApplyRefOffset(SectionProperties sec, ViewType view,
                                              Point3d ip, string refPt)
        {
            double fullH = ViewFullHeight(sec, view);
            double yOff;

            if (view == ViewType.CrossSection)
            {
                bool isCentred = sec.Type != SectionType.Angle; // L drawn from bottom
                if (isCentred)
                {
                    // centred: bottom = ip.Y - fullH/2, top = ip.Y + fullH/2
                    yOff = refPt == "Top"    ? -fullH / 2
                         : refPt == "Bottom" ?  fullH / 2
                         : 0;
                }
                else
                {
                    // L angle: bottom at draw-origin, top at draw-origin + fullH
                    yOff = refPt == "Top"    ? -fullH
                         : refPt == "Centre" ? -fullH / 2
                         : 0;
                }
            }
            else
            {
                // Side / Top views: bottom edge at draw-origin, extends up by fullH
                yOff = refPt == "Top"    ? -fullH
                     : refPt == "Centre" ? -fullH / 2
                     : 0;
            }

            return new Point3d(ip.X, ip.Y + yOff, ip.Z);
        }

        // The full height (depth) of a section in the given view direction.
        private static double ViewFullHeight(SectionProperties sec, ViewType view)
        {
            switch (view)
            {
                case ViewType.TopView:
                    switch (sec.Type)
                    {
                        case SectionType.WShape:
                        case SectionType.SShape:
                        case SectionType.Channel:    return sec.bf;
                        case SectionType.Angle:      return sec.Vleg;
                        case SectionType.HSSRectangular: return sec.B;
                        default:                     return sec.D;   // circular
                    }
                default: // SideView + CrossSection share the depth axis
                    switch (sec.Type)
                    {
                        case SectionType.WShape:
                        case SectionType.SShape:
                        case SectionType.Channel:    return sec.d;
                        case SectionType.Angle:      return sec.Hleg;
                        case SectionType.HSSRectangular: return sec.H;
                        default:                     return sec.D;
                    }
            }
        }
    }

    // Minimal ICommand implementation for ribbon buttons
    internal class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) { _execute = execute; }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
