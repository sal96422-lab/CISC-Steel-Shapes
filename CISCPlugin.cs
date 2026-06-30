using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(CISCSections.CISCPlugin))]
[assembly: ExtensionApplication(typeof(CISCSections.CISCPlugin))]

namespace CISCSections
{
    public class CISCPlugin : IExtensionApplication
    {
        private const string TAB_ID = "CISC_SECTIONS_TAB";

        private static readonly string[] _typeLabels = {
            "W Shapes", "S Shapes", "C Shapes", "L Shapes", "HSS Rectangular", "HSS Circular"
        };
        private static readonly string[] _viewLabels = {
            "Cross Section", "Side View", "Top View"
        };
        private static readonly string[] _refLabels = { "Centre", "Top", "Bottom" };

        // Ribbon controls
        private RibbonCombo  _ribbonTypeCombo;
        private RibbonCombo  _ribbonSizeCombo;
        private RibbonCombo  _ribbonViewCombo;
        private RibbonCombo  _ribbonRefCombo;
        private RibbonButton _ribbonHiddenBtn;

        // Persistent state — loaded from / saved to disk
        private static int    _lastTypeIndex  = 0;
        private static string _lastSecName    = null;
        private static int    _lastViewIndex  = 0;
        private static int    _lastRefIndex   = 0;
        private static bool   _showHidden     = false;

        private static string SettingsPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "CISCSections", "settings.cfg");

        private static void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                foreach (var line in File.ReadAllLines(SettingsPath))
                {
                    var parts = line.Split('=');
                    if (parts.Length != 2) continue;
                    switch (parts[0].Trim())
                    {
                        case "type":   if (int.TryParse(parts[1], out int t)) _lastTypeIndex = t; break;
                        case "size":   _lastSecName   = parts[1].Trim(); break;
                        case "view":   if (int.TryParse(parts[1], out int v)) _lastViewIndex = v; break;
                        case "ref":    if (int.TryParse(parts[1], out int r)) _lastRefIndex  = r; break;
                        case "hidden": if (bool.TryParse(parts[1],  out bool h)) _showHidden  = h; break;
                    }
                }
            }
            catch { /* ignore corrupt settings */ }
        }

        private static void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllLines(SettingsPath, new[]
                {
                    $"type={_lastTypeIndex}",
                    $"size={_lastSecName ?? ""}",
                    $"view={_lastViewIndex}",
                    $"ref={_lastRefIndex}",
                    $"hidden={_showHidden}"
                });
            }
            catch { }
        }

        public void Initialize()
        {
            LoadSettings();

            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            doc?.Editor.WriteMessage(
                "\nCISC Metric Sections loaded. Select options in the CISC Sections ribbon tab, then click Insert Section.\n");

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

            var tab      = new RibbonTab { Title = "CISC Sections", Id = TAB_ID };
            var panelSrc = new RibbonPanelSource { Title = "Insert Section" };
            var panel    = new RibbonPanel { Source = panelSrc };

            // ── Large Insert button ───────────────────────────────────────────
            var btn = new RibbonButton
            {
                Text           = "Insert\nSection",
                ShowText       = true,
                ShowImage      = true,
                LargeImage     = BuildIcon(),
                Size           = RibbonItemSize.Large,
                Orientation    = System.Windows.Controls.Orientation.Vertical,
                ToolTip        = "Insert the selected CISC metric steel section (CISCINSERT)",
                CommandHandler = new RelayCommand(() =>
                    AcadApp.DocumentManager.MdiActiveDocument
                           ?.SendStringToExecute("CISCINSERT\n", true, false, true))
            };
            panelSrc.Items.Add(btn);
            panelSrc.Items.Add(new RibbonSeparator());

            // ── Row panel: all selection controls ─────────────────────────────
            var rows = new RibbonRowPanel();

            // Row 1 — Section Type
            rows.Items.Add(new RibbonLabel { Text = "Type" });
            _ribbonTypeCombo = MakeCombo(_typeLabels, _lastTypeIndex, 155);
            _ribbonTypeCombo.CurrentChanged += OnRibbonTypeChanged;
            rows.Items.Add(_ribbonTypeCombo);
            rows.Items.Add(new RibbonRowBreak());

            // Row 2 — Section size combo (RibbonCommandItem items for proper text matching)
            rows.Items.Add(new RibbonLabel { Text = "Size" });
            _ribbonSizeCombo = new RibbonCombo { Width = 155, ToolTip = "Select or type to jump to a section" };
            PopulateSizeCombo(_lastTypeIndex);
            _ribbonSizeCombo.CurrentChanged += (s, e) =>
            {
                if (_ribbonSizeCombo.Current is RibbonCommandItem item)
                { _lastSecName = item.Text; SaveSettings(); }
            };
            rows.Items.Add(_ribbonSizeCombo);
            rows.Items.Add(new RibbonRowBreak());

            // Row 3 — View Type
            rows.Items.Add(new RibbonLabel { Text = "View" });
            _ribbonViewCombo = MakeCombo(_viewLabels, _lastViewIndex, 155);
            _ribbonViewCombo.CurrentChanged += (s, e) =>
            {
                int i = _ribbonViewCombo.Items.IndexOf(_ribbonViewCombo.Current);
                if (i >= 0) { _lastViewIndex = i; SaveSettings(); }
            };
            rows.Items.Add(_ribbonViewCombo);
            rows.Items.Add(new RibbonRowBreak());

            // Row 4 — Reference Point
            rows.Items.Add(new RibbonLabel { Text = "Ref " });
            _ribbonRefCombo = MakeCombo(_refLabels, _lastRefIndex, 155);
            _ribbonRefCombo.CurrentChanged += (s, e) =>
            {
                int i = _ribbonRefCombo.Items.IndexOf(_ribbonRefCombo.Current);
                if (i >= 0) { _lastRefIndex = i; SaveSettings(); }
            };
            rows.Items.Add(_ribbonRefCombo);
            rows.Items.Add(new RibbonRowBreak());

            // Row 5 — Hidden Lines toggle button
            _ribbonHiddenBtn = new RibbonButton
            {
                Text           = HiddenLabel(),
                ShowText       = true,
                ShowImage      = false,
                Size           = RibbonItemSize.Standard,
                ToolTip        = "Toggle hidden lines for flanges and inner walls",
                CommandHandler = new RelayCommand(ToggleHidden)
            };
            rows.Items.Add(_ribbonHiddenBtn);

            panelSrc.Items.Add(rows);
            tab.Panels.Add(panel);
            ribbon.Tabs.Add(tab);
        }

        private void OnRibbonTypeChanged(object sender, EventArgs e)
        {
            int i = _ribbonTypeCombo.Items.IndexOf(_ribbonTypeCombo.Current);
            if (i < 0) return;
            _lastTypeIndex = i;
            PopulateSizeCombo(i);
            SaveSettings();
        }

        private void PopulateSizeCombo(int typeIndex)
        {
            if (_ribbonSizeCombo == null) return;
            _ribbonSizeCombo.Items.Clear();
            var type = TypeForIndex(typeIndex);
            foreach (var sec in CISCDatabase.Sections.Where(s => s.Type == type))
                _ribbonSizeCombo.Items.Add(new RibbonCommandItem { Text = sec.Name });

            var match = _ribbonSizeCombo.Items.OfType<RibbonCommandItem>()
                            .FirstOrDefault(i => i.Text == _lastSecName);
            _ribbonSizeCombo.Current = match
                ?? (_ribbonSizeCombo.Items.Count > 0 ? _ribbonSizeCombo.Items[0] : null);

            if (_ribbonSizeCombo.Current is RibbonCommandItem cur)
                _lastSecName = cur.Text;
        }

        private void ToggleHidden()
        {
            _showHidden = !_showHidden;
            if (_ribbonHiddenBtn != null)
                _ribbonHiddenBtn.Text = HiddenLabel();
            SaveSettings();
        }

        private static string HiddenLabel() =>
            _showHidden ? "Hidden Lines: ON" : "Hidden Lines: OFF";

        private static RibbonCombo MakeCombo(string[] labels, int selectedIndex, double width)
        {
            var combo = new RibbonCombo { Width = width };
            foreach (var lbl in labels)
                combo.Items.Add(new RibbonButton { Text = lbl, ShowText = true });
            if (selectedIndex >= 0 && selectedIndex < combo.Items.Count)
                combo.Current = combo.Items[selectedIndex];
            return combo;
        }

        private static int TypeIndexFor(SectionType t)
        {
            switch (t)
            {
                case SectionType.SShape:         return 1;
                case SectionType.Channel:        return 2;
                case SectionType.Angle:          return 3;
                case SectionType.HSSRectangular: return 4;
                case SectionType.HSSCircular:    return 5;
                default:                         return 0;
            }
        }

        private static SectionType TypeForIndex(int idx)
        {
            switch (idx)
            {
                case 1: return SectionType.SShape;
                case 2: return SectionType.Channel;
                case 3: return SectionType.Angle;
                case 4: return SectionType.HSSRectangular;
                case 5: return SectionType.HSSCircular;
                default: return SectionType.WShape;
            }
        }

        private static ViewType ViewForIndex(int idx)
        {
            switch (idx)
            {
                case 1: return ViewType.SideView;
                case 2: return ViewType.TopView;
                default: return ViewType.CrossSection;
            }
        }

        private static ImageSource BuildIcon()
        {
            var dv = new System.Windows.Media.DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var fill = new SolidColorBrush(Color.FromRgb(90, 40, 140));
                var pen  = new Pen(fill, 0);
                dc.DrawRectangle(fill, pen, new System.Windows.Rect(2, 2, 28, 6));
                dc.DrawRectangle(fill, pen, new System.Windows.Rect(12, 8, 8, 16));
                dc.DrawRectangle(fill, pen, new System.Windows.Rect(2, 24, 28, 6));
            }
            var bmp = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Freeze();
            return bmp;
        }

        public void Terminate() { }

        // ─── Main command ─────────────────────────────────────────────────────

        [CommandMethod("CISCINSERT", CommandFlags.Modal)]
        public void InsertSection()
        {
            var doc = AcadApp.DocumentManager.MdiActiveDocument;
            var db  = doc.Database;
            var ed  = doc.Editor;

            // Resolve section from size combo or last saved name
            string secName = (_ribbonSizeCombo?.Current as RibbonCommandItem)?.Text ?? _lastSecName;

            var sec = secName != null
                ? CISCDatabase.Sections.FirstOrDefault(
                      s => s.Name.Equals(secName, StringComparison.OrdinalIgnoreCase))
                : null;

            if (sec == null)
            {
                ed.WriteMessage(string.IsNullOrEmpty(secName)
                    ? "\nNo section entered. Type a section name in the Size field on the CISC Sections ribbon tab.\n"
                    : $"\nSection '{secName}' not found. Check the name and try again.\n");
                return;
            }

            // Sync type combo and persist
            int resolvedType = TypeIndexFor(sec.Type);
            if (resolvedType != _lastTypeIndex)
            {
                _lastTypeIndex = resolvedType;
                if (_ribbonTypeCombo != null && resolvedType < _ribbonTypeCombo.Items.Count)
                    _ribbonTypeCombo.Current = _ribbonTypeCombo.Items[resolvedType];
            }
            _lastSecName = sec.Name;
            if (_ribbonSizeCombo != null)
            {
                var match = _ribbonSizeCombo.Items.OfType<RibbonCommandItem>()
                                .FirstOrDefault(i => i.Text == sec.Name);
                if (match != null) _ribbonSizeCombo.Current = match;
            }
            SaveSettings();

            ViewType view  = ViewForIndex(_lastViewIndex);
            string   refPt = _refLabels[Math.Max(0, Math.Min(_lastRefIndex, _refLabels.Length - 1))];
            bool     showH = _showHidden;

            // Pick start point
            var ppr = ed.GetPoint("\nSpecify start point: ");
            if (ppr.Status != PromptStatus.OK) return;
            Point3d startPt = ppr.Value;

            // Pick end / direction point
            string prompt2 = view == ViewType.CrossSection
                ? "\nSpecify direction point: "
                : "\nSpecify end point: ";
            var opts = new PromptPointOptions(prompt2)
            {
                UseBasePoint = true,
                BasePoint    = startPt
            };
            var ppr2 = ed.GetPoint(opts);
            if (ppr2.Status != PromptStatus.OK) return;

            Point3d endPt = ppr2.Value;
            double  dx    = endPt.X - startPt.X;
            double  dy    = endPt.Y - startPt.Y;
            double  dist  = Math.Sqrt(dx * dx + dy * dy);
            double  angle = Math.Atan2(dy, dx);

            if (dist < 1.0)
            {
                ed.WriteMessage("\nSecond point too close to start — cancelled.\n");
                return;
            }

            double L = view != ViewType.CrossSection ? dist : 0;

            Point3d drawPt = ApplyRefOffset(sec, view, startPt, refPt);
            Point3d pivot  = view == ViewType.CrossSection ? drawPt : startPt;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var drawer = new SectionDrawer(db, tr);
                drawer.Draw(sec, view, drawPt, showH, L, angle, pivot);
                tr.Commit();
            }

            ed.WriteMessage($"\nInserted {sec.Name}  [{view}]  ref={refPt}  angle={angle * 180 / Math.PI:F1}°");
            if (view != ViewType.CrossSection) ed.WriteMessage($"  L={L:F0} mm");
            if (showH) ed.WriteMessage("  (hidden lines on)");
            ed.WriteMessage("\n");
        }

        private static Point3d ApplyRefOffset(SectionProperties sec, ViewType view,
                                              Point3d ip, string refPt)
        {
            double fullH = ViewFullHeight(sec, view);
            double yOff;

            if (view == ViewType.CrossSection)
            {
                bool isCentred = sec.Type != SectionType.Angle;
                if (isCentred)
                    yOff = refPt == "Top"    ? -fullH / 2
                         : refPt == "Bottom" ?  fullH / 2
                         : 0;
                else
                    yOff = refPt == "Top"    ? -fullH
                         : refPt == "Centre" ? -fullH / 2
                         : 0;
            }
            else
            {
                yOff = refPt == "Top"    ? -fullH
                     : refPt == "Centre" ? -fullH / 2
                     : 0;
            }

            return new Point3d(ip.X, ip.Y + yOff, ip.Z);
        }

        private static double ViewFullHeight(SectionProperties sec, ViewType view)
        {
            switch (view)
            {
                case ViewType.TopView:
                    switch (sec.Type)
                    {
                        case SectionType.WShape:
                        case SectionType.SShape:
                        case SectionType.Channel:        return sec.bf;
                        case SectionType.Angle:          return sec.Vleg;
                        case SectionType.HSSRectangular: return sec.B;
                        default:                         return sec.D;
                    }
                default:
                    switch (sec.Type)
                    {
                        case SectionType.WShape:
                        case SectionType.SShape:
                        case SectionType.Channel:        return sec.d;
                        case SectionType.Angle:          return sec.Hleg;
                        case SectionType.HSSRectangular: return sec.H;
                        default:                         return sec.D;
                    }
            }
        }
    }

    internal class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) { _execute = execute; }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}
