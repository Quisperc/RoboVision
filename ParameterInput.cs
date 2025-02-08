    using AForge.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RoboVision
{
    public partial class ParameterInputForm : Form
    {
        // 修改构造函数参数类型
        public ParameterInputForm(IEnumerable<VideoCapabilities> capabilities)
        {
            InitializeComponent();
            LoadResolutions(capabilities);
        }

        public int SelectedResolutionIndex { get; private set; } = -1;

        private void LoadResolutions(IEnumerable<VideoCapabilities> capabilities)
        {
            comboBoxResolutions.Items.Clear();

            foreach (var cap in capabilities.OrderBy(c => c.FrameSize.Width))
            {
                comboBoxResolutions.Items.Add($"{cap.FrameSize.Width}x{cap.FrameSize.Height}");
            }

            if (comboBoxResolutions.Items.Count > 0)
                comboBoxResolutions.SelectedIndex = 0;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            SelectedResolutionIndex = comboBoxResolutions.SelectedIndex;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
