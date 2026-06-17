using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CISCSections
{
    public class SectionDialog : Form
    {
        // Controls
        private ComboBox    _cboType;
        private ComboBox    _cboSize;
        private RadioButton _rbCross;
        private RadioButton _rbSide;
        private RadioButton _rbTop;
        private ComboBox    _cboCrossRef;
        private Label       _lblCrossRef;
        private CheckBox    _chkHidden;
        private Label       _lblProps;
        private Button      _btnOK;
        private Button      _btnCancel;

        // Results read by the command after OK
        public SectionProperties SelectedSection  { get; private set; }
        public ViewType          SelectedView      { get; private set; }
        public bool              ShowHiddenLines   { get; private set; }
        public string            RefPoint          { get; private set; } = "Centre";

        // Remember last used section across dialog instances
        private static int    _lastTypeIndex    = 0;
        private static string _lastSectionName  = null;
        private static int    _lastRefIndex     = 0;
        private static int    _lastViewIndex    = 0; // 0=Cross, 1=Side, 2=Top

        public SectionDialog()
        {
            BuildUI();
            PopulateTypeCombo();
            RestoreLastSelection();
        }

        // ─── UI construction ────────────────────────────────────────────────

        private void BuildUI()
        {
            Text            = "CISC Metric Section Insert";
            Size            = new Size(440, 440);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;

            int lx = 16, cx = 160, cw = 240, y = 14;

            // Section Type
            AddLabel("Section Type:", lx, y + 3);
            _cboType = AddCombo(cx, y, cw);
            y += 34;

            // Section Size — typeable with auto-complete across all section types
            AddLabel("Section Size:", lx, y + 3);
            _cboSize = new ComboBox
            {
                Location           = new Point(cx, y),
                Size               = new Size(cw, 21),
                DropDownStyle      = ComboBoxStyle.DropDown,
                AutoCompleteMode   = AutoCompleteMode.Suggest,
                AutoCompleteSource = AutoCompleteSource.CustomSource
            };
            // Populate custom source with every section name from all types
            var allNames = new System.Windows.Forms.AutoCompleteStringCollection();
            allNames.AddRange(CISCDatabase.Sections.Select(s => s.Name).ToArray());
            _cboSize.AutoCompleteCustomSource = allNames;
            Controls.Add(_cboSize);
            y += 34;

            // View Type
            AddLabel("View Type:", lx, y);
            y += 22;

            _rbCross = AddRadio("Cross Section  (end-on profile)", cx, y);
            _rbCross.Checked = true;
            y += 24;

            // Reference point — applies to all views
            _lblCrossRef = AddLabel("Pick point:", cx + 18, y + 3);
            _cboCrossRef = new ComboBox
            {
                Location      = new Point(cx + 88, y),
                Size          = new Size(110, 21),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cboCrossRef.Items.AddRange(new object[] { "Centre", "Top", "Bottom" });
            _cboCrossRef.SelectedIndex = 0;
            Controls.Add(_cboCrossRef);
            y += 28;

            _rbSide = AddRadio("Side / Elevation View", cx, y);
            y += 24;

            _rbTop = AddRadio("Top / Plan View", cx, y);
            y += 32;

            // Hidden lines
            _chkHidden = new CheckBox
            {
                Text     = "Show hidden lines for flanges / inner walls",
                Location = new Point(cx, y),
                AutoSize = true
            };
            Controls.Add(_chkHidden);
            y += 32;

            // Properties read-out
            AddLabel("Dimensions:", lx, y);
            y += 20;
            _lblProps = new Label
            {
                Location  = new Point(cx, y),
                Size      = new Size(cw, 60),
                ForeColor = Color.DarkBlue,
                Font      = new Font("Consolas", 8.5f)
            };
            Controls.Add(_lblProps);
            y += 70;

            // Buttons
            _btnOK     = new Button { Text = "Insert", Location = new Point(cx, y), Size = new Size(110, 30), DialogResult = DialogResult.OK };
            _btnCancel = new Button { Text = "Cancel", Location = new Point(cx + 120, y), Size = new Size(110, 30), DialogResult = DialogResult.Cancel };
            Controls.Add(_btnOK);
            Controls.Add(_btnCancel);
            AcceptButton = _btnOK;
            CancelButton = _btnCancel;

            // Wire events
            _cboType.SelectedIndexChanged += (s, e) => OnTypeChanged();
            _cboSize.SelectedIndexChanged += (s, e) => OnSizeChanged();
            _cboSize.TextChanged          += (s, e) => OnSizeTextChanged();
            _btnOK.Click += OnOKClick;
        }

        private Label AddLabel(string text, int x, int y)
        {
            var lbl = new Label { Text = text, Location = new Point(x, y), AutoSize = true };
            Controls.Add(lbl);
            return lbl;
        }

        private ComboBox AddCombo(int x, int y, int width)
        {
            var cb = new ComboBox { Location = new Point(x, y), Size = new Size(width, 21), DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(cb);
            return cb;
        }

        private RadioButton AddRadio(string text, int x, int y)
        {
            var rb = new RadioButton { Text = text, Location = new Point(x, y), AutoSize = true };
            Controls.Add(rb);
            return rb;
        }

        // ─── Populate combos ────────────────────────────────────────────────

        private void PopulateTypeCombo()
        {
            _cboType.Items.AddRange(new object[]
            {
                "W Shapes (Wide Flange)",
                "S Shapes (American Standard)",
                "C Shapes (Channels)",
                "L Shapes (Angles)",
                "HSS Rectangular",
                "HSS Circular"
            });
            _cboType.SelectedIndex = 0;
        }

        private SectionType CurrentSectionType()
        {
            switch (_cboType.SelectedIndex)
            {
                case 1: return SectionType.SShape;
                case 2: return SectionType.Channel;
                case 3: return SectionType.Angle;
                case 4: return SectionType.HSSRectangular;
                case 5: return SectionType.HSSCircular;
                default: return SectionType.WShape;
            }
        }

        private void OnTypeChanged()
        {
            _cboSize.Items.Clear();
            foreach (var s in CISCDatabase.Sections.Where(x => x.Type == CurrentSectionType()))
                _cboSize.Items.Add(s);
            if (_cboSize.Items.Count > 0) _cboSize.SelectedIndex = 0;
        }

        private void OnSizeChanged()
        {
            var sec = _cboSize.SelectedItem as SectionProperties;
            if (sec == null) { _lblProps.Text = ""; return; }
            UpdatePropsLabel(sec);
        }

        // When the user types, try to find a matching section across ALL types
        private void OnSizeTextChanged()
        {
            string txt = _cboSize.Text.Trim();
            if (string.IsNullOrEmpty(txt)) return;

            // Already matched via SelectedItem
            if (_cboSize.SelectedItem is SectionProperties sel && sel.Name == txt) return;

            // Search all sections for a match
            var match = CISCDatabase.Sections.FirstOrDefault(
                s => s.Name.Equals(txt, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                // Switch type combo to match, which repopulates size list
                int typeIdx = TypeIndexFor(match.Type);
                if (_cboType.SelectedIndex != typeIdx)
                {
                    _cboType.SelectedIndex = typeIdx; // triggers OnTypeChanged → repopulates
                }
                // Select the matching item in the size list
                for (int i = 0; i < _cboSize.Items.Count; i++)
                {
                    if ((_cboSize.Items[i] as SectionProperties)?.Name == match.Name)
                    {
                        _cboSize.SelectedIndex = i;
                        break;
                    }
                }
                UpdatePropsLabel(match);
            }
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

        private void UpdatePropsLabel(SectionProperties sec)
        {
            switch (sec.Type)
            {
                case SectionType.WShape:
                case SectionType.SShape:
                case SectionType.Channel:
                    _lblProps.Text = $"d  = {sec.d} mm\nbf = {sec.bf} mm\ntf = {sec.tf} mm\ntw = {sec.tw} mm";
                    break;
                case SectionType.Angle:
                    _lblProps.Text = $"H-leg = {sec.Hleg} mm\nV-leg = {sec.Vleg} mm\nt     = {sec.t} mm";
                    break;
                case SectionType.HSSRectangular:
                    _lblProps.Text = $"H = {sec.H} mm\nB = {sec.B} mm\nt = {sec.t} mm";
                    break;
                case SectionType.HSSCircular:
                    _lblProps.Text = $"D = {sec.D} mm\nt = {sec.t} mm";
                    break;
            }
        }

        // ─── Restore / save last selection ──────────────────────────────────

        private void RestoreLastSelection()
        {
            // Restore type
            if (_lastTypeIndex >= 0 && _lastTypeIndex < _cboType.Items.Count)
                _cboType.SelectedIndex = _lastTypeIndex;

            // Restore section name if it exists in the repopulated list
            if (_lastSectionName != null)
            {
                for (int i = 0; i < _cboSize.Items.Count; i++)
                {
                    if ((_cboSize.Items[i] as SectionProperties)?.Name == _lastSectionName)
                    {
                        _cboSize.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Restore view
            if      (_lastViewIndex == 1) _rbSide.Checked  = true;
            else if (_lastViewIndex == 2) _rbTop.Checked   = true;
            else                          _rbCross.Checked  = true;

            // Restore reference point
            if (_lastRefIndex >= 0 && _lastRefIndex < _cboCrossRef.Items.Count)
                _cboCrossRef.SelectedIndex = _lastRefIndex;
        }

        // ─── OK validation ──────────────────────────────────────────────────

        private void OnOKClick(object sender, EventArgs e)
        {
            // Accept typed name or selected item
            SelectedSection = _cboSize.SelectedItem as SectionProperties;
            if (SelectedSection == null)
            {
                // Try matching typed text against all sections
                string txt = _cboSize.Text.Trim();
                SelectedSection = CISCDatabase.Sections.FirstOrDefault(
                    s => s.Name.Equals(txt, StringComparison.OrdinalIgnoreCase));
            }

            if (SelectedSection == null)
            {
                MessageBox.Show("Section not found. Please select or type a valid section name.",
                    "CISC Sections", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            SelectedView    = _rbSide.Checked ? ViewType.SideView : _rbTop.Checked ? ViewType.TopView : ViewType.CrossSection;
            ShowHiddenLines = _chkHidden.Checked;
            RefPoint        = _cboCrossRef.SelectedItem?.ToString() ?? "Centre";

            // Save for next time
            _lastTypeIndex   = TypeIndexFor(SelectedSection.Type);
            _lastSectionName = SelectedSection.Name;
            _lastRefIndex    = _cboCrossRef.SelectedIndex;
            _lastViewIndex   = _rbSide.Checked ? 1 : _rbTop.Checked ? 2 : 0;
        }
    }
}
