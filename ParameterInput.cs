// ParameterInputForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace RoboVision
{
    public partial class ParameterInputForm : Form
    {
        public Size SelectedResolution { get; private set; }

        public ParameterInputForm(IEnumerable<Size> resolutions)
        {
            InitializeComponent();
            LoadResolutions(resolutions);
        }

        private void LoadResolutions(IEnumerable<Size> resolutions)
        {
            comboBoxResolutions.Items.Clear();
            foreach (var res in resolutions.OrderBy(r => r.Width))
            {
                comboBoxResolutions.Items.Add($"{res.Width}x{res.Height}");
            }
            comboBoxResolutions.SelectedIndex = 0;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (comboBoxResolutions.SelectedItem is string selected)
            {
                var parts = selected.Split('x');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out int width) &&
                    int.TryParse(parts[1], out int height))
                {
                    SelectedResolution = new Size(width, height);
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
            }
            MessageBox.Show("请选择有效的分辨率");
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
