// MainForm.cs
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RoboVision
{
    public partial class MainForm : Form
    {
        private CameraController _camera;
        private DeepLearningModel _dlModel;
        private CommunicationModule _comms;
        private readonly object _imageLock = new object();
        private bool _isClosing;
        private bool _isCameraReady = false;
        private bool _isProcessing;
        private Mat _currentFrame; // 用于存储当前帧

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
            _comms.StartServer("192.168.0.104", 8000);
        }

        private void SetupEventHandlers()
        {
            // 相机事件
            _camera.FrameUpdated += Camera_FrameUpdated;
            _camera.CaptureCompleted += Camera_CaptureCompleted;
            _camera.ErrorOccurred += Camera_ErrorOccurred;
            _camera.ConnectingCamera += Camera_ConnectingCamera;

            // 深度学习事件
            _dlModel.ModelLoaded += DlModel_ModelLoaded;
            _dlModel.InferenceCompleted += DlModel_InferenceCompleted;
            _dlModel.ProceedCompleted += Deeplearning_ProceedCompleted;
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
            //ShowMessage($"图片已保存至：{savePath}");
            UpdateStatus($"拍摄结果已保存至：{savePath}", false, LogLevel.Info);
        }
        // 相机选择事件
        private void Camera_ConnectingCamera(object sender, string e)
        {
            UpdateStatus($"正在连接相机：{e}......", false, LogLevel.Info);
        }
        // 处理图像文件保存完成事件
        private void Deeplearning_ProceedCompleted(object sender, string savePath)
        {
            UpdateStatus($"检测结果已保存至：{savePath}", false, LogLevel.Info);
        }

        private void Camera_ErrorOccurred(object sender, string error)
        {
            HandleError($"相机错误：{error}", false);
        }

        private void DlModel_ModelLoaded(object sender, string msg)
        {
            UpdateStatus($"模型加载：{msg}", false, LogLevel.Info);
        }

        private void DlModel_InferenceCompleted(object sender, string msg)
        {
            UpdateStatus(msg, false, LogLevel.Info);
        }

        private void DlModel_ErrorOccurred(object sender, string msg)
        {
            HandleError($"模型错误：{msg}", false);
            //UpdateStatus($"模型错误：{msg}", false, LogLevel.Error);
        }

        private void Comms_DataReceived(object sender, string data)
        {
            UpdateStatus($"收到：{data}", false, LogLevel.Info);
        }

        private void Comms_StatusChanged(object sender, string msg)
        {
            UpdateStatus($"通信状态：{msg}", false, LogLevel.Info);
        }
        #endregion

        #region UI控件事件
        private void btnDetectCamera_Click(object sender, EventArgs e)
        {
            DetectCameraDevices();
        }

        private void btnSetParameters_Click(object sender, EventArgs e)
        {
            try
            {
                ShowResolutionOptions();
                _isCameraReady = true;
            }
            catch
            {
                HandleError("请先选择一个摄像头", false);
            }
        }

        private void btnCapture_Click(object sender, EventArgs e)
        {
            if (_isCameraReady)
            {
                GetCurrentFrame();
                // 删除之前保留的帧，避免内存泄漏
                //_currentFrame?.Dispose();
            }
            else
                HandleError("请先连接相机并设置参数", false);
        }

        private void btnProcess_Click(object sender, EventArgs e)
        {
            if (_isCameraReady)
            {
                //CaptureFrame();
                GetCurrentFrame();
                if(_currentFrame==null)
                {
                    UpdateStatus("当前帧 _currentFrame 为空", false, LogLevel.Warning);
                }
                else
                    ProcessCurrentFrame();
                // 删除之前保留的帧，避免内存泄漏
                //_currentFrame?.Dispose();
            }
            else
                HandleError("请先连接相机并设置参数", false);
        }

        private void btnLoadModel__Click(object sender, EventArgs e)
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
                UpdateStatus("开始检测相机......", false, LogLevel.Info);
                if (!_camera.AvailableCameras.Any())
                {
                    UpdateStatus("未检测到可用相机设备！", false, LogLevel.Warning);
                    return;
                }

                var csForm = new CameraSelection(_camera.AvailableCameras);
                UpdateStatus($"正在选择相机......", false, LogLevel.Info);
                if (csForm.ShowDialog() == DialogResult.OK)
                {
                    _camera.SelectCamera(csForm.SelectedCameraIndex);
                    UpdateStatus($"已连接：{_camera.DeviceName}", false, LogLevel.Info);
                    //ShowResolutionOptions();
                }
            }
            catch (Exception ex)
            {
                HandleError($"相机初始化失败：{ex.Message}", false, LogLevel.Error);
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
                UpdateStatus($"当前分辨率：{_camera.CurrentResolution}", false, LogLevel.Info);
            }
        }

        private void StartPreview()
        {
            try
            {
                _camera.StartPreview();
                UpdateStatus($"实时预览中 - 分辨率：{_camera.CurrentResolution}", false, LogLevel.Info);
            }
            catch (Exception ex)
            {
                HandleError(ex.Message, false);
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
                HandleError(ex.Message, false);
            }
        }

        private void ProcessCurrentFrame()
        {
            Task.Run(() =>
            {
                if (_currentFrame == null)
                {
                    UpdateStatus("当前帧 _currentFrame 为空", false, LogLevel.Warning);
                }
                try
                {
                    using (var frame = _currentFrame.ToBitmap())
                    // 使用CaptureFrame()替换GetCurrentFrame()
                    //using (var frame = _camera.CaptureFrame().ToBitmap())
                    {
                        if (frame == null)
                        {
                            UpdateStatus("当前帧 frame 为空", false, LogLevel.Warning);
                        }
                        var result = _dlModel.ProcessFrame(frame);
                        if (result != null)
                        {
                            // 创建需要显示的图像副本
                            if (result.ProcessedImage == null)
                            {
                                UpdateStatus($"处理后的图像为空", false, LogLevel.Warning);
                                return;
                            }
                            var displayImage = new Bitmap(result.ProcessedImage);
                            SendDetectionResults(result.Detections);
                            UpdateProcessedImage(displayImage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateStatus($"处理图片错误：{ex.Message}", false, LogLevel.Error);
                }
                finally
                {
                    // 删除之前保留的帧，避免内存泄漏
                    _currentFrame?.Dispose();
                    _currentFrame = null;
                }
            });
        }
        private void GetCurrentFrame()
        {
            // 使用SafeInvoke确保线程安全, 但是这里不需要，使用之后_currentFrame为空，UI线程没有成功参与？
            //SafeInvoke(() =>
            //{
            //lock (_imageLock)
            //    {
            //        if (pictureBoxDisplay.Image != null)
            //        {
            //            using (var bitmap = new Bitmap(pictureBoxDisplay.Image))
            //            {
            //                _currentFrame = BitmapConverter.ToMat(bitmap); // 使用BitmapConverter将Bitmap转换为Mat
            //            }
            //        }
            //    }
            //});
            //return pictureBoxDisplay.Image != null
            //    ? new Bitmap(pictureBoxDisplay.Image)
            //    : null;
            _currentFrame = _camera.CaptureFrame();
            //_camera.CaptureFrame(_currentFrame);
            if (_currentFrame == null)
            {
                UpdateStatus("当前帧为空", false, LogLevel.Warning);
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
                    //_currentFrame?.Dispose(); // 释放旧图像
                    //_currentFrame = BitmapConverter.ToMat(frame);
                    //_currentFrame = BitmapConverter.ToMat(new Bitmap(old)); // 创建独立副本
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
                    var old = pictureBoxProcessed.Image;
                    pictureBoxProcessed.Image = image; // 直接使用传入的Bitmap
                    old?.Dispose(); // 安全释放旧图像
                }
            });
        }

        private void SendDetectionResults(List<BoundingBox> detections)
        {
            try
            {
                var sb = new StringBuilder();
                detections.ForEach(box =>
                {
                    var dataLine = $"{box.Label},{box.Confidence:F2},{box.Rect.X},{box.Rect.Y},{box.Rect.Width},{box.Rect.Height}";
                    sb.AppendLine(dataLine);
                    UpdateStatus($"准备发送：{dataLine}", false, LogLevel.Info);
                });

                UpdateStatus("发送至服务器 127.0.0.1:8000 中......", false, LogLevel.Info);

                // 使用Task避免阻塞UI线程
                Task.Run(() => _comms.SendToClient("192.168.0.104", 8000, sb.ToString()))
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            UpdateStatus($"发送失败：{t.Exception?.InnerException?.Message}", false, LogLevel.Error);
                        }
                    });
            }
            catch (Exception ex)
            {
                UpdateStatus($"发送过程异常：{ex.Message}", false, LogLevel.Error);
            }
        }

        private void UpdateStatus(string message, bool status, LogLevel level = LogLevel.Info)
        {
            if (status)
                SafeInvoke(() => labelStatus.Text = message);
            LogData(message, level);
        }

        // 消息类型枚举定义
        public enum Mymsg
        {
            // 定义可能出现的消息类型
            None = 0,
            Info = 1,
            Warning = 2,
            Error = 3,
            Success = 4
        }

        // 日志等级枚举定义
        public enum LogLevel { Info, Warning, Error }
        private void LogData(string message, LogLevel level = LogLevel.Info)
        {
            SafeInvoke(() =>
            {
                // 确保控件是 RichTextBox（C# 7.3 兼容写法）
                var rtb = textBoxCoordinates as RichTextBox;
                if (rtb != null)
                {
                    // 记录原始颜色
                    Color originalColor = rtb.SelectionColor;

                    // 设置颜色（传统 switch 写法）
                    switch (level)
                    {
                        case LogLevel.Error:
                            rtb.SelectionColor = Color.Red;
                            break;
                        case LogLevel.Warning:
                            rtb.SelectionColor = Color.Orange;
                            break;
                        default:
                            rtb.SelectionColor = Color.Black;
                            break;
                    }

                    // 追加带时间戳的消息
                    rtb.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");

                    // 恢复默认颜色
                    rtb.SelectionColor = originalColor;
                }
                else
                {
                    // 回退方案（理论上不会执行）
                    textBoxCoordinates.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
                }

                // 自动滚动到底部
                textBoxCoordinates.SelectionStart = textBoxCoordinates.Text.Length;
                textBoxCoordinates.ScrollToCaret();
            });
        }

        private void ShowMessage(string message)
        {
            SafeInvoke(() => MessageBox.Show(message));
        }

        private void HandleError(string error, bool status, LogLevel level = LogLevel.Error)
        {
            SafeInvoke(() =>
            {
                MessageBox.Show(error, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                UpdateStatus($"错误：{error}", false, LogLevel.Error);
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
                _currentFrame?.Dispose();

                lock (_imageLock)
                {
                    var img = pictureBoxDisplay.Image;
                    pictureBoxDisplay.Image = null;
                    img?.Dispose();
                    var imgPro = pictureBoxProcessed .Image;
                    pictureBoxProcessed.Image = null;
                    imgPro?.Dispose();
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