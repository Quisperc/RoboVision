using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;

namespace RoboVision
{
    public partial class MainFrom : Form
    {
        private CameraController camera;
        private DeepLearningModel deepLearning;
        private CommunicationModule commModule;
        private readonly object imageLock = new object();

        public MainFrom()
        {
            InitializeComponent();
            InitializeModules();
        }
        private void InitializeModules()
        {
            // 初始化相机控制器并订阅事件
            camera = new CameraController();
            camera.FrameUpdated += Camera_FrameUpdated;
            camera.CaptureCompleted += Camera_CaptureCompleted;
            camera.ErrorOccurred += Camera_ErrorOccurred;

            // 初始化深度学习模型
            deepLearning = new DeepLearningModel();
            string modelPath = @"models/yolov8s.onnx";
            deepLearning.LoadModel(modelPath);

            // 启动通信模块
            commModule = new CommunicationModule("127.0.0.1", 8000);
            UpdateCameraStatus("未连接");
        }
        // 更新显示当前相机状态的Label
        private void UpdateCameraStatus(string status)
        {
            if (labelCurrentCamera.InvokeRequired)
            {
                labelCurrentCamera.BeginInvoke(new Action(() =>
                    labelCurrentCamera.Text = $"相机状态：{status}"));
            }
            else
            {
                labelCurrentCamera.Text = $"相机状态：{status}";
            }
        }
        // 检测相机并弹出选择窗口
        private void btnDetectCamera_Click(object sender, EventArgs e)
        {
            try
            {
                camera.StopCamera();
                camera.RefreshDevices();

                if (!camera.CameraExists)
                {
                    MessageBox.Show("未检测到可用相机设备！");
                    return;
                }

                var cameras = new List<string>(camera.AvailableCameras);
                using (var csForm = new CameraSelection(cameras))
                {
                    if (csForm.ShowDialog() == DialogResult.OK)
                    {
                        camera.SelectCamera(csForm.SelectedCamera);
                        UpdateCameraStatus($"已连接 - {csForm.SelectedCamera}");
                        StartPreview();
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError($"相机初始化失败：{ex.Message}");
            }
        }
        // 视频预览
        private void StartPreview()
        {
            try
            {
                camera.StartPreview();
                UpdateCameraStatus("实时预览中...");
            }
            catch (InvalidOperationException ex)
            {
                HandleError(ex.Message);
            }
        }
        private void Camera_FrameUpdated(object sender, Bitmap frame)
        {
            try
            {
                UpdatePreview(frame);
            }
            catch (Exception ex)
            {
                HandleError($"画面更新失败：{ex.Message}");
            }
        }
        private void UpdatePreview(Bitmap frame)
        {
            if (pictureBoxDisplay.InvokeRequired)
            {
                pictureBoxDisplay.BeginInvoke(new Action(() =>
                {
                    lock (imageLock)
                    {
                        UpdateImageSafe(frame.Clone() as Bitmap);
                    }
                }));
            }
            else
            {
                lock (imageLock)
                {
                    UpdateImageSafe(frame.Clone() as Bitmap);
                }
            }
        }
        // 安全更新 PictureBox 中的图像
        private void UpdateImageSafe(Bitmap newImage)
        {
            var old = pictureBoxDisplay.Image;
            pictureBoxDisplay.Image = newImage;
            old?.Dispose();
        }
        private void Camera_CaptureCompleted(object sender, string savePath)
        {
            ShowMessage($"图片已保存至：{savePath}");
        }

        private void Camera_ErrorOccurred(object sender, string error)
        {
            HandleError(error);
        }

        private void btnSetParameters_Click(object sender, EventArgs e)
        {
            try
            {
                // [修正点1] 直接从CameraController获取分辨率参数
                var capabilities = camera.AvailableResolutions; // 替换GetCurrentCamera()

                // [修正点2] 检查是否存在可用分辨率
                if (capabilities.Length == 0)
                {
                    MessageBox.Show("请先选择可用相机");
                    return;
                }

                // [修正点3] 传入正确的参数类型
                using (var paramForm = new ParameterInputForm(capabilities))
                {
                    if (paramForm.ShowDialog() == DialogResult.OK)
                    {
                        if (camera.TrySetResolution(paramForm.SelectedResolutionIndex))
                        {
                            RestartPreview();
                            ShowMessage($"当前分辨率：{camera.CurrentResolution}");
                        }
                        else
                        {
                            HandleError("分辨率设置失败");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError($"参数设置错误：{ex.Message}");
            }
        }
        private void RestartPreview()
        {
            camera.StopCamera();
            StartPreview();
        }

        private void btnCapture_Click(object sender, EventArgs e)
        {
            try
            {
                camera.CaptureFrame();
            }
            catch (InvalidOperationException ex)
            {
                HandleError(ex.Message);
            }
        }

        private void btnProcess_Click(object sender, EventArgs e)
        {
            // 处理逻辑保持不变...
        }

        private void ShowMessage(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => MessageBox.Show(message)));
            }
            else
            {
                MessageBox.Show(message);
            }
        }

        private void HandleError(string error)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    MessageBox.Show(error, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    UpdateCameraStatus("错误状态");
                }));
            }
            else
            {
                MessageBox.Show(error, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateCameraStatus("错误状态");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            camera.Dispose();
            commModule.StopServer();
        }
    }
}
