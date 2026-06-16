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
        // "Centre" | "Top" | "Bottom"
        public string            CrossRefPoint     { get; private set; } = "Centre";

        public SectionDialog()
        {
            BuildUI();
            PopulateTypeCombo();
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

            // Section Size
            AddLabel("Section Size:", lx, y + 3);
            _cboSize = AddCombo(cx, y, cw);
            y += 34;

            // View Type
            AddLabel("View Type:", lx, y);
            y += 22;

            _rbCross = AddRadio("Cross Section  (end-on profile)", cx, y);
            _rbCross.Checked = true;
            y += 24;

            // Reference point — only relevant for cross-section
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

            // note: length is now picked by clicking two points in AutoCAD

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
            _rbCross.CheckedChanged += (s, e) => UpdateRefPoint();
            _rbSide.CheckedChanged  += (s, e) => UpdateRefPoint();
            _rbTop.CheckedChanged   += (s, e) => UpdateRefPoint();
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

        private void UpdateRefPoint()
        {
            bool isCross = _rbCross.Checked;
            _lblCrossRef.Enabled  = isCross;
            _cboCrossRef.Enabled  = isCross;
        }

        // ─── OK validation ──────────────────────────────────────────────────

        private void OnOKClick(object sender, EventArgs e)
        {
            SelectedSection = _cboSize.SelectedItem as SectionProperties;
            if (SelectedSection == null)
            {
                MessageBox.Show("Please select a section size.", "CISC Sections", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            SelectedView    = _rbSide.Checked ? ViewType.SideView : _rbTop.Checked ? ViewType.TopView : ViewType.CrossSection;
            ShowHiddenLines = _chkHidden.Checked;
            CrossRefPoint   = _cboCrossRef.SelectedItem?.ToString() ?? "Centre";
        }
    }
}
