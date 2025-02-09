// MainForm.cs
using System;
using System.Drawing;
using System.Windows.Forms;
using OpenCvSharp;// 没用上
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoboVision
{
    public partial class MainForm : Form
    {
        private CameraController _camera;
        private DeepLearningModel _dlModel;
        private CommunicationModule _comms;
        private readonly object _imageLock = new object();
        private bool _isClosing;

        public MainForm()
        {
            InitializeComponent();
            InitializeModules();
            SetupEventHandlers();
        }

        private void InitializeModules()
        {
            // 初始化相机模块
            _camera = new CameraController();

            // 初始化深度学习模块
            _dlModel = new DeepLearningModel();

            // 初始化通信模块
            _comms = new CommunicationModule();
            _comms.StartServer("127.0.0.1", 8000);
        }

        private void SetupEventHandlers()
        {
            // 相机事件
            _camera.FrameUpdated += Camera_FrameUpdated;
            _camera.CaptureCompleted += Camera_CaptureCompleted;
            _camera.ErrorOccurred += Camera_ErrorOccurred;

            // 深度学习事件
            _dlModel.ModelLoaded += DlModel_ModelLoaded;
            _dlModel.InferenceCompleted += DlModel_InferenceCompleted;
            _dlModel.ErrorOccurred += DlModel_ErrorOccurred;

            // 通信事件
            _comms.DataReceived += Comms_DataReceived;
            _comms.StatusChanged += Comms_StatusChanged;
        }

        #region 事件处理方法
        private void Camera_FrameUpdated(object sender, Bitmap frame)
        {
            UpdatePreview(frame);
        }

        private void Camera_CaptureCompleted(object sender, string savePath)
        {
            ShowMessage($"图片已保存至：{savePath}");
        }

        private void Camera_ErrorOccurred(object sender, string error)
        {
            HandleError($"相机错误：{error}");
        }

        private void DlModel_ModelLoaded(object sender, string msg)
        {
            UpdateStatus($"模型加载：{msg}");
        }

        private void DlModel_InferenceCompleted(object sender, string msg)
        {
            UpdateStatus(msg);
        }

        private void DlModel_ErrorOccurred(object sender, string msg)
        {
            HandleError($"模型错误：{msg}");
        }

        private void Comms_DataReceived(object sender, string data)
        {
            LogData($"收到：{data}");
        }

        private void Comms_StatusChanged(object sender, string msg)
        {
            UpdateStatus($"通信状态：{msg}");
        }
        #endregion

        #region UI控件事件
        private void btnDetectCamera_Click(object sender, EventArgs e)
        {
            DetectCameraDevices();
        }

        private void btnSetParameters_Click(object sender, EventArgs e)
        {
            ShowResolutionOptions();
        }

        private void btnCapture_Click(object sender, EventArgs e)
        {
            CaptureFrame();
        }

        private void btnProcess_Click(object sender, EventArgs e)
        {
            _dlModel.LoadModel(@"models/yolov8s.onnx");
            ProcessCurrentFrame();
        }

        private void btnLoadModel_Click(object sender, EventArgs e)
        {
            var dlg = new OpenFileDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _dlModel.LoadModel(dlg.FileName);
            }
        }
        #endregion

        #region 核心业务方法
        private void DetectCameraDevices()
        {
            try
            {
                _camera.StopCamera();
                _camera.RefreshDevices();

                if (!_camera.AvailableCameras.Any())
                {
                    ShowMessage("未检测到可用相机设备！");
                    return;
                }

                var csForm = new CameraSelection(_camera.AvailableCameras);
                if (csForm.ShowDialog() == DialogResult.OK)
                {
                    _camera.SelectCamera(csForm.SelectedCameraIndex);
                    UpdateStatus($"已连接 - {_camera.DeviceName}");
                    //ShowResolutionOptions();
                }
            }
            catch (Exception ex)
            {
                HandleError($"相机初始化失败：{ex.Message}");
            }
        }

        private void ShowResolutionOptions()
        {
            // 直接使用转换后的分辨率列表
            var paramForm = new ParameterInputForm(_camera.AvailableResolutions);

            if (paramForm.ShowDialog() == DialogResult.OK)
            {
                // 这里会调用我们新增的System.Drawing.Size参数重载方法
                _camera.SetResolution(paramForm.SelectedResolution);

                StartPreview();
                ShowMessage($"当前分辨率：{_camera.CurrentResolution}");
            }
        }

        private void StartPreview()
        {
            try
            {
                _camera.StartPreview();
                UpdateStatus($"实时预览中 - {_camera.CurrentResolution}");
            }
            catch (Exception ex)
            {
                HandleError(ex.Message);
            }
        }

        private void CaptureFrame()
        {
            try
            {
                _camera.CaptureFrame();
            }
            catch (Exception ex)
            {
                HandleError(ex.Message);
            }
        }

        private void ProcessCurrentFrame()
        {
            var frame = GetCurrentFrame();
            if (frame == null) return;

            Task.Run(() =>
            {
                using (frame) // 使用 using 确保释放
                {
                    var result = _dlModel.ProcessFrame(frame);
                    if (result != null)
                    {
                        // 确保 ProcessedImage 在必要时释放
                        using (var processedImage = result.ProcessedImage)
                        {
                            UpdateProcessedImage(processedImage);
                        }
                        SendDetectionResults(result.Detections);
                    }
                }
            });
        }
        #endregion

        #region 辅助方法
        private Bitmap GetCurrentFrame()
        {
            lock (_imageLock)
            {
                return pictureBoxDisplay.Image != null
                    ? new Bitmap(pictureBoxDisplay.Image)
                    : null;
            }
        }

        private void UpdatePreview(Bitmap frame)
        {
            SafeInvoke(() =>
            {
                if (frame == null)
                    return;

                // 检查 Bitmap 是否已被释放（通过异常捕获）
                try
                {
                    if (frame.Width <= 0 || frame.Height <= 0)
                        return;
                }
                catch (ArgumentException)
                {
                    return; // 如果已释放，直接返回
                }

                lock (_imageLock)
                {
                    var old = pictureBoxDisplay.Image;
                    pictureBoxDisplay.Image = new Bitmap(frame); // 创建独立副本
                    old?.Dispose(); // 释放旧图像
                }

                frame?.Dispose(); // 安全释放传入的 Bitmap
            });
        }

        private void UpdateProcessedImage(Bitmap image)
        {
            SafeInvoke(() =>
            {
                lock (_imageLock)
                {
                    var old = pictureBoxDisplay.Image;
                    pictureBoxDisplay.Image = new Bitmap(image);
                    old?.Dispose();
                }
            });
        }

        private void SendDetectionResults(List<BoundingBox> detections)
        {
            var sb = new StringBuilder();
            foreach (var box in detections)
            {
                sb.AppendLine($"{box.Label},{box.Confidence:F2},{box.Rect.X},{box.Rect.Y},{box.Rect.Width},{box.Rect.Height}");
            }
            _comms.SendToClient("127.0.0.1", 8000, sb.ToString());
        }

        private void UpdateStatus(string message)
        {
            SafeInvoke(() => labelStatus.Text = message);
        }

        private void LogData(string message)
        {
            //SafeInvoke(() => listBoxCoordinates.Items.Add($"{DateTime.Now:T} {message}"));
        }

        private void ShowMessage(string message)
        {
            SafeInvoke(() => MessageBox.Show(message));
        }

        private void HandleError(string error)
        {
            SafeInvoke(() =>
            {
                MessageBox.Show(error, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus($"错误：{error}");
            });
        }

        private void SafeInvoke(Action action)
        {
            if (InvokeRequired)
                BeginInvoke(action);
            else
                action();
        }
        #endregion

        #region 资源清理
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_isClosing) return;
            _isClosing = true;

            try
            {
                _camera?.Dispose();
                _dlModel?.Dispose();
                _comms?.Dispose();

                lock (_imageLock)
                {
                    var img = pictureBoxDisplay.Image;
                    pictureBoxDisplay.Image = null;
                    img?.Dispose();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"资源释放异常：{ex}");
            }

            base.OnFormClosing(e);
        }
        #endregion
    }
}