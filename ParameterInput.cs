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
        public int SelectedResolutionIndex { get; private set; }
        public bool ParameterSet { get; private set; }

        public ParameterInputForm(VideoCaptureDevice camera)
        {
            InitializeComponent();

            if (camera == null)
            {
                MessageBox.Show("请先选择相机！");
                this.Close();
                return;
            }

            var capabilities = camera.VideoCapabilities;
            int foundIndex = -1;
            for (int i = 0; i < capabilities.Length; i++)
            {
                var cap = capabilities[i];
                comboBoxResolutions.Items.Add($"{cap.FrameSize.Width}x{cap.FrameSize.Height} {cap.AverageFrameRate}fps");

                // 如果当前相机已设置分辨率，则查找匹配的索引
                if (camera.VideoResolution != null)
                {
                    if (cap.FrameSize.Width == camera.VideoResolution.FrameSize.Width &&
                        cap.FrameSize.Height == camera.VideoResolution.FrameSize.Height)
                    {
                        foundIndex = i;
                    }
                }
            }

            if (foundIndex != -1)
            {
                comboBoxResolutions.SelectedIndex = foundIndex;
            }
            else if (comboBoxResolutions.Items.Count > 0)
            {
                comboBoxResolutions.SelectedIndex = 0;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (comboBoxResolutions.SelectedIndex >= 0)
            {
                SelectedResolutionIndex = comboBoxResolutions.SelectedIndex;
                ParameterSet = true;
                DialogResult = DialogResult.OK;
            }
            else
            {
                MessageBox.Show("请选择一个分辨率！");
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            ParameterSet = false;
            DialogResult = DialogResult.Cancel;
        }
    }
}
