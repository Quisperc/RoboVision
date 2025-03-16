// CameraSelection.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace RailwayRoadSectionDetection
{
    public partial class CameraSelection : Form
    { 
        public int SelectedCameraIndex { get; private set; } = 1;
        public event EventHandler<string> selectingCamera;

        public CameraSelection(IEnumerable<string> cameras)
        {
            InitializeComponent();
            selectingCamera?.Invoke(this, $"正在选择相机......");
            listBoxCameras.DataSource = cameras.ToList();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (listBoxCameras.SelectedIndex >= 0)
            {
                SelectedCameraIndex = listBoxCameras.SelectedIndex;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                MessageBox.Show("请先选择一个摄像头");
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
