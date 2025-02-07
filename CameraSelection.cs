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
    public partial class CameraSelection : Form
    {
        public string SelectedCamera { get; private set; }

        public CameraSelection(List<string> cameras)
        {
            InitializeComponent();
            listBoxCameras.DataSource = cameras;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (listBoxCameras.SelectedItem != null)
            {
                SelectedCamera = listBoxCameras.SelectedItem.ToString();
                DialogResult = DialogResult.OK;
            }
            else
            {
                MessageBox.Show("请先选择一个相机。");
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }
    }
}
