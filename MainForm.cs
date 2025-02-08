using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Imaging.Filters;
using AForge.Video;
using AForge.Video.DirectShow;

namespace RoboVision
{
    public partial class MainForm : Form
    {
        private CameraController camera;
        private DeepLearningModel deepLearning;
        private CommunicationModule commModule;
        private readonly object imageLock = new object();

        public MainForm()
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
            // 有效性验证（关键修正点1）
            if (frame == null || frame.Width <= 0 || frame.Height <= 0)
                return;

            Bitmap clonedFrame = null;

            try
            {
                // 立即克隆（关键修正点2）
                clonedFrame = (Bitmap)frame.Clone();

                Action updateAction = () =>
                {
                    lock (imageLock)
                    {
                        UpdateImageSafe(clonedFrame);
                    }
                };

                if (pictureBoxDisplay.InvokeRequired)
                {
                    // 传递已克隆的副本（关键修正点3）
                    pictureBoxDisplay.BeginInvoke(updateAction);
                }
                else
                {
                    updateAction();
                }
            }
            catch (ArgumentException ex)
            {
                Debug.WriteLine($"图像处理异常：{ex.Message}");
                clonedFrame?.Dispose();
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine($"跨线程错误：{ex.Message}");
                clonedFrame?.Dispose();
            }
        }

        private void UpdateImageSafe(Bitmap newImage)
        {
            try
            {
                var old = pictureBoxDisplay.Image;
                pictureBoxDisplay.Image = (Bitmap)newImage.Clone();
                old?.Dispose();
            }
            finally
            {
                newImage.Dispose(); // 确保释放克隆的临时对象
            }
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
            MessageBox.Show("没做好！不可以点！");
            // 检查是否有图像
            //if (currentFrame == null)
            //{
            //    MessageBox.Show("请先捕获图像！");
            //    return;
            //}
            // 检查是否有模型
            if (deepLearning == null)
            {
                MessageBox.Show("未加载深度学习模型！");
                string modelPath = @"models/yolov8s.onnx";
                deepLearning.LoadModel(modelPath);
                return;
            }
            // 检查是否有通信模块
            if (camera == null)
            {
                MessageBox.Show("未启动通信模块！");
                return;
            }
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
            //if (isClosing)
            //{
            //    e.Cancel = true; // 直接阻止重复关闭
            //    return;
            //}

            //isClosing = true;

            try
            {
                // 释放资源
                if (camera != null)
                {
                    camera.Dispose();
                    camera = null;
                }

                if (commModule != null)
                {
                    commModule.StopServer();
                    commModule = null;
                }
                if(deepLearning!=null)
                {
                    deepLearning.Dispose();
                    deepLearning = null;
                }
                // 不再需要调用 base.OnFormClosing(e)!
            }
            catch (Exception ex)
            {
                MessageBox.Show("关闭错误: " + ex.Message);
            }
            finally
            {
                //isClosing = false;
            }

            // 关键：手动标记窗体允许关闭
            e.Cancel = false;
        }

    }
}
