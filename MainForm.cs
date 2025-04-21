// MainForm.cs
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
        private volatile bool _isClosing;
        private bool _isCameraReady = false;
        private volatile bool _isProcessing;
        private Mat _currentFrame; // 用于存储当前帧
        
        // 使用配置选项
        private string _commsTargetIP = "127.0.0.1";
        private int _commsTargetPort = 8001;
        private int _commsSourcePort = 8000;
        
        // 添加取消令牌源
        private CancellationTokenSource _processingCts;

        // 定期清理内存
        private System.Windows.Forms.Timer _memoryCleanupTimer;
        
        private void InitializeMemoryCleanup()
        {
            _memoryCleanupTimer = new System.Windows.Forms.Timer();
            _memoryCleanupTimer.Interval = 30000; // 30秒
            _memoryCleanupTimer.Tick += (s, e) => 
            {
                // 在UI线程上执行清理
                GC.Collect();
                GC.WaitForPendingFinalizers();
                
                // 记录内存使用情况
                long memoryUsed = GC.GetTotalMemory(true) / (1024 * 1024);
                Debug.WriteLine($"Memory cleanup completed. Current usage: {memoryUsed} MB");
            };
            _memoryCleanupTimer.Start();
        }

        public MainForm()
        {
            InitializeComponent();
            _processingCts = new CancellationTokenSource();
            InitializeModules();
            SetupEventHandlers();
            InitializeMemoryCleanup(); // 添加内存清理初始化
            
            // 添加应用程序域未处理异常处理
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.ThreadException += Application_ThreadException;
        }

        private void InitializeModules()
        {
            try
            {
                // 初始化相机模块
                _camera = new CameraController();

                // 初始化深度学习模块
                _dlModel = new DeepLearningModel();

                // 初始化通信模块
                _comms = new CommunicationModule();
                Task.Run(async () => 
                {
                    try 
                    {
                        await _comms.StartReceiveServerAsync();
                    }
                    catch (Exception ex)
                    {
                        SafeInvoke(() => HandleError($"通信服务器启动失败: {ex.Message}", false));
                    }
                });
            }
            catch (Exception ex)
            {
                HandleError($"模块初始化失败：{ex.Message}", false);
            }
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

        #region 全局异常处理
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogUnhandledException(e.ExceptionObject as Exception, "AppDomain未处理异常");
        }

        private void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            LogUnhandledException(e.Exception, "线程未处理异常");
        }

        private void LogUnhandledException(Exception ex, string source)
        {
            if (ex == null) return;

            try
            {
                string errorMessage = $"[{source}] {DateTime.Now}: {ex.Message}\r\n{ex.StackTrace}";
                
                // 记录到日志文件
                string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                Directory.CreateDirectory(logDirectory);
                string logPath = Path.Combine(logDirectory, $"error_{DateTime.Now:yyyyMMdd}.log");
                
                File.AppendAllText(logPath, errorMessage + Environment.NewLine + Environment.NewLine);
                
                // 显示消息
                SafeInvoke(() => HandleError($"发生未处理异常: {ex.Message}", true, LogLevel.Error));
            }
            catch
            {
                // 即使日志记录失败，也不应抛出新异常
            }
        }
        #endregion

        #region 事件处理方法
        private void Camera_FrameUpdated(object sender, Bitmap frame)
        {
            if (!_isClosing)
            {
                try
                {
                    // 重要：我们接收的frame由UI负责释放
                    UpdatePreview(frame);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"处理相机帧时出错: {ex.Message}");
                    // 确保在错误情况下也释放资源
                    frame?.Dispose();
                }
            }
            else
            {
                // 如果窗体正在关闭，仍然需要释放资源
                frame?.Dispose();
            }
        }

        private void Camera_CaptureCompleted(object sender, string savePath)
        {
            UpdateStatus($"拍摄结果已保存至：{savePath}", myMsg.none, LogLevel.Info);
        }
        
        private void Camera_ConnectingCamera(object sender, string e)
        {
            UpdateStatus($"正在连接相机：{e}......", myMsg.none, LogLevel.Info);
        }
        
        private void Deeplearning_ProceedCompleted(object sender, string savePath)
        {
            UpdateStatus($"检测结果已保存至：{savePath}", myMsg.none, LogLevel.Info);
        }

        private void Camera_ErrorOccurred(object sender, string error)
        {
            HandleError($"相机错误：{error}", false);
        }

        private void DlModel_ModelLoaded(object sender, string msg)
        {
            UpdateStatus($"模型加载：{msg}", myMsg.none, LogLevel.Info);
        }

        private void DlModel_InferenceCompleted(object sender, string msg)
        {
            UpdateStatus(msg, myMsg.none, LogLevel.Info);
        }

        private void DlModel_ErrorOccurred(object sender, string msg)
        {
            HandleError($"模型错误：{msg}", false);
        }

        private void Comms_DataReceived(object sender, string data)
        {
            UpdateStatus($"收到：{data}", myMsg.none, LogLevel.Info);
        }

        private void Comms_StatusChanged(object sender, string msg)
        {
            UpdateStatus($"通信状态：{msg}", myMsg.none, LogLevel.Info);
        }
        #endregion

        #region UI控件事件
        private void btnDetectCamera_Click(object sender, EventArgs e)
        {
            try
            {
                DetectCameraDevices();
            }
            catch (Exception ex)
            {
                HandleError($"检测相机失败: {ex.Message}", false);
            }
        }

        private void btnSetParameters_Click(object sender, EventArgs e)
        {
            try
            {
                ShowResolutionOptions();
                _isCameraReady = true;
            }
            catch (Exception ex)
            {
                HandleError($"设置参数失败: {ex.Message}", false);
            }
        }

        private void btnCapture_Click(object sender, EventArgs e)
        {
            if (_isCameraReady)
            {
                try
                {
                    GetCurrentFrame();
                }
                catch (Exception ex)
                {
                    HandleError($"拍照失败: {ex.Message}", false);
                }
            }
            else
                HandleError("请先连接相机并设置参数", false);
        }

        private void btnProcess_Click(object sender, EventArgs e)
        {
            if (_isCameraReady)
            {
                try
                {
                    // 避免重复处理
                    if (_isProcessing)
                    {
                        UpdateStatus("正在处理中，请稍候...", myMsg.none, LogLevel.Warning);
                        return;
                    }
                    
                    GetCurrentFrame();
                    if (_currentFrame == null)
                    {
                        UpdateStatus("当前帧 _currentFrame 为空", myMsg.none, LogLevel.Warning);
                    }
                    else
                    {
                        ProcessCurrentFrame();
                    }
                }
                catch (Exception ex)
                {
                    HandleError($"处理图像失败: {ex.Message}", false);
                    _isProcessing = false;
                }
            }
            else
                HandleError("请先连接相机并设置参数", false);
        }

        private void btnLoadModel__Click(object sender, EventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog();
                dlg.Filter = "ONNX模型文件 (*.onnx)|*.onnx|所有文件 (*.*)|*.*";
                dlg.Title = "选择深度学习模型";
                
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    UpdateStatus($"正在加载模型: {Path.GetFileName(dlg.FileName)}...", myMsg.none, LogLevel.Info);
                    _dlModel.LoadModel(dlg.FileName);
                }
            }
            catch (Exception ex)
            {
                HandleError($"加载模型失败: {ex.Message}", false);
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
                
                if (_camera.AvailableCameras.Count == 0)
                {
                    UpdateStatus("未检测到摄像头设备", myMsg.none, LogLevel.Warning);
                    return;
                }

                // 使用正确的构造函数创建CameraSelection
                var csForm = new CameraSelection(_camera.AvailableCameras);
                
                if (csForm.ShowDialog() == DialogResult.OK)
                {
                    int selectedIndex = csForm.SelectedCameraIndex;
                    if (selectedIndex >= 0)
                    {
                        _camera.SelectCamera(selectedIndex);
                        StartPreview();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"检测相机设备失败: {ex.Message}", ex);
            }
        }

        private void ShowResolutionOptions()
        {
            if (_camera.AvailableResolutions.Count == 0)
            {
                throw new InvalidOperationException("请先选择一个摄像头");
            }

            // 使用正确的类名和构造函数
            using (ParameterInputForm dialog = new ParameterInputForm(_camera.AvailableResolutions))
            {
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _camera.SetResolution(dialog.SelectedResolution);
                    StartPreview();
                }
            }
        }

        private void StartPreview()
        {
            try
            {
                if (!_camera.IsPreviewing)
                {
                    _camera.StartPreview();
                    UpdateStatus($"摄像头预览已启动：{_camera.CurrentResolution.Width}x{_camera.CurrentResolution.Height}", myMsg.Camera_connected, LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                HandleError($"启动预览失败：{ex.Message}", false);
            }
        }

        private void CaptureFrame()
        {
            try
            {
                if (_camera != null && _camera.IsPreviewing)
                {
                    _camera.CaptureFrame();
                }
            }
            catch (Exception ex)
            {
                HandleError($"拍照失败：{ex.Message}", false);
            }
        }

        private async void ProcessCurrentFrame()
        {
            if (_currentFrame == null || _isProcessing) return;

            try
            {
                _isProcessing = true;
                UpdateStatus("正在处理图像...", myMsg.none, LogLevel.Info);
                
                // 重置取消令牌
                _processingCts?.Cancel();
                _processingCts = new CancellationTokenSource();
                
                // 使用Task.Run在后台线程执行处理
                await Task.Run(() => 
                {
                    try
                    {
                        // 转换为位图
                        Bitmap frameBitmap = null;
                        lock (_imageLock)
                        {
                            if (_currentFrame != null && !_currentFrame.Empty())
                            {
                                frameBitmap = BitmapConverter.ToBitmap(_currentFrame);
                            }
                        }

                        if (frameBitmap == null)
                        {
                            SafeInvoke(() => UpdateStatus("无法处理空图像", myMsg.none, LogLevel.Warning));
                            return;
                        }

                        using (frameBitmap)
                        {
                            // 检查模型是否已加载
                            if (!_dlModel.IsInitialized)
                            {
                                SafeInvoke(() => HandleError("请先加载深度学习模型", false));
                                return;
                            }

                            // 处理图像
                            var result = _dlModel.ProcessFrame(frameBitmap);
                            if (result != null)
                            {
                                // 更新UI显示
                                SafeInvoke(() => UpdateProcessedImage(result.ProcessedImage));
                                
                                // 发送检测结果
                                if (result.Detections.Count > 0)
                                {
                                    SafeInvoke(() => SendDetectionResults(result.Detections));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        SafeInvoke(() => HandleError($"处理图像时出错: {ex.Message}", false));
                    }
                }, _processingCts.Token);
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("图像处理已取消", myMsg.none, LogLevel.Info);
            }
            catch (Exception ex)
            {
                HandleError($"处理图像失败: {ex.Message}", false);
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private void GetCurrentFrame()
        {
            try
            {
                lock (_imageLock)
                {
                    Mat frame = null;
                    try
                    {
                        frame = _camera.CaptureFrame();
                    }
                    catch (Exception ex)
                    {
                        HandleError($"获取帧失败：{ex.Message}", false);
                        return;
                    }

                    if (frame != null && !frame.Empty())
                    {
                        try
                        {
                            // 释放旧帧
                            _currentFrame?.Dispose();
                            _currentFrame = frame;
                            
                            // 转换当前帧为Bitmap并更新预览
                            using (Bitmap frameBitmap = BitmapConverter.ToBitmap(_currentFrame))
                            {
                                // 创建克隆用于UI显示，原始对象在using块结束时释放
                                Bitmap displayBitmap = (Bitmap)frameBitmap.Clone();
                                UpdatePreview(displayBitmap);
                            }
                        }
                        catch (Exception ex)
                        {
                            HandleError($"处理帧失败: {ex.Message}", false);
                            // 确保释放资源
                            frame?.Dispose();
                        }
                    }
                    else
                    {
                        // 释放无效帧
                        frame?.Dispose();
                        UpdateStatus("获取到空帧", myMsg.none, LogLevel.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError($"获取当前帧失败: {ex.Message}", false);
            }
        }

        private void UpdatePreview(Bitmap frame)
        {
            if (frame == null || _isClosing) return;

            try
            {
                // 将UI更新和资源处理放在同一个线程上下文中
                SafeInvoke(() =>
                {
                    try
                    {
                        // 直接使用传入的Bitmap，无需再次克隆
                        // 释放之前的图像
                        if (pictureBoxDisplay.Image != null)
                        {
                            Image oldImage = pictureBoxDisplay.Image;
                            pictureBoxDisplay.Image = null;
                            oldImage.Dispose();
                        }

                        // 设置新图像 - 直接使用，无需克隆，所有权转移
                        pictureBoxDisplay.Image = frame;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"更新预览图像失败: {ex.Message}");
                        // 确保在错误情况下释放资源
                        frame?.Dispose();
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新预览失败: {ex.Message}");
                // 确保在任何情况下都释放资源
                frame?.Dispose();
            }
        }

        private void UpdateProcessedImage(Bitmap image)
        {
            if (image == null || _isClosing) return;

            try
            {
                SafeInvoke(() =>
                {
                    try
                    {
                        // 释放之前的图像
                        if (pictureBoxProcessed.Image != null)
                        {
                            var oldImage = pictureBoxProcessed.Image;
                            pictureBoxProcessed.Image = null;
                            oldImage.Dispose();
                        }

                        // 设置新图像（使用克隆以避免原对象被释放）
                        pictureBoxProcessed.Image = new Bitmap(image);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"更新处理后图像失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"更新处理后图像失败: {ex.Message}");
            }
        }

        private void GetTargetServer(out string targetIp, out int targetPort)
        {
            targetIp = _commsTargetIP;
            targetPort = _commsTargetPort;
        }

        private async void SendDetectionResults(List<BoundingBox> detections)
        {
            if (detections == null || detections.Count == 0) return;

            try
            {
                // 构建JSON格式的检测结果
                var json = new StringBuilder("{\"detections\":[");
                
                for (int i = 0; i < detections.Count; i++)
                {
                    if (i > 0) json.Append(",");
                    
                    var box = detections[i];
                    json.Append("{")
                        .Append($"\"label\":\"{box.Label}\",")
                        .Append($"\"confidence\":{box.Confidence.ToString("F4")},")
                        .Append($"\"x\":{box.Rect.X},")
                        .Append($"\"y\":{box.Rect.Y},")
                        .Append($"\"width\":{box.Rect.Width},")
                        .Append($"\"height\":{box.Rect.Height}")
                        .Append("}");
                }
                
                json.Append("]}");

                // 获取目标服务器
                GetTargetServer(out string targetIp, out int targetPort);
                
                // 发送到服务器
                try
                {
                    await _comms.SendDataAsync(targetIp, targetPort, json.ToString());
                }
                catch (SocketException ex)
                {
                    UpdateStatus($"通信错误: {ex.Message}", myMsg.error, LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                HandleError($"发送检测结果失败: {ex.Message}", false);
            }
        }

        private void UpdateStatus(string message, myMsg mymsg = myMsg.none, LogLevel level = LogLevel.Info)
        {
            SafeInvoke(() => 
            {
                // 记录日志
                LogData(message, level);
                
                // 更新状态栏
                labelStatus.Text = message;
                
                // 根据消息类型设置状态栏颜色
                switch (mymsg)
                {
                    case myMsg.Camera_connected:
                        labelStatus.BackColor = Color.LightGreen;
                        break;
                    case myMsg.disconnected:
                        labelStatus.BackColor = Color.Yellow;
                        break;
                    case myMsg.error:
                        labelStatus.BackColor = Color.LightPink;
                        break;
                    default:
                        labelStatus.BackColor = SystemColors.Control;
                        break;
                }
            });
        }

        public enum myMsg
        {
            // 定义可能出现的消息类型
            none, Camera_connected, disconnected, error
        }

        public enum LogLevel { Info, Warning, Error }

        private void LogData(string message, LogLevel level = LogLevel.Info)
        {
            try
            {
                // 设置日志级别颜色
                Color textColor = Color.Black;
                switch (level)
                {
                    case LogLevel.Warning:
                        textColor = Color.Orange;
                        break;
                    case LogLevel.Error:
                        textColor = Color.Red;
                        break;
                    default:
                        textColor = Color.Black;
                        break;
                }

                // 格式化日志条目
                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                string logEntry = $"[{timestamp}] [{level.ToString()}] {message}";
                
                // 在UI线程中更新日志控件
                SafeInvoke(() =>
                {
                    try
                    {
                        // 添加新日志条目
                        textBoxCoordinates.SelectionColor = textColor;
                        textBoxCoordinates.AppendText(logEntry + Environment.NewLine);
                        
                        // 自动滚动到最新
                        textBoxCoordinates.ScrollToCaret();
                        
                        // 限制日志行数，避免内存过度使用
                        const int MaxLogLines = 500;
                        if (textBoxCoordinates.Lines.Length > MaxLogLines)
                        {
                            // 保留最后MaxLogLines行
                            var lastLines = textBoxCoordinates.Lines.Skip(textBoxCoordinates.Lines.Length - MaxLogLines).ToArray();
                            textBoxCoordinates.Clear();
                            foreach (var line in lastLines)
                            {
                                textBoxCoordinates.AppendText(line + Environment.NewLine);
                            }
                        }

                        // 写入日志文件
                        string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                        Directory.CreateDirectory(logDir);
                        string logPath = Path.Combine(logDir, $"log_{DateTime.Now:yyyyMMdd}.txt");
                        
                        File.AppendAllText(logPath, logEntry + Environment.NewLine);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"写入日志失败: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"记录日志异常: {ex.Message}");
            }
        }

        private void ShowMessage(string message)
        {
            MessageBox.Show(message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void HandleError(string error, bool status, LogLevel level = LogLevel.Error)
        {
            // 记录错误日志
            LogData(error, level);
            if (status)
            {
                UpdateStatus(error, myMsg.error, level);
            }
        }

        private void SafeInvoke(Action action)
        {
            try
            {
                if (IsDisposed || _isClosing) return;

                if (InvokeRequired)
                {
                    BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SafeInvoke失败: {ex.Message}");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _isClosing = true;
            
            // 停止内存清理计时器
            _memoryCleanupTimer?.Stop();
            _memoryCleanupTimer?.Dispose();
            
            // 取消所有正在进行的处理
            _processingCts?.Cancel();
            
            // 停止所有异步操作
            try
            {
                _camera?.StopCamera();
                _camera?.Dispose();
                
                // 释放当前帧资源
                lock (_imageLock)
                {
                    _currentFrame?.Dispose();
                    _currentFrame = null;
                }
                
                // 释放其他模块
                _dlModel?.Dispose();
                _comms?.Dispose();
                
                // 释放图像资源
                if (pictureBoxDisplay.Image != null)
                {
                    Image img = pictureBoxDisplay.Image;
                    pictureBoxDisplay.Image = null;
                    img.Dispose();
                }
                
                if (pictureBoxProcessed.Image != null)
                {
                    Image img = pictureBoxProcessed.Image;
                    pictureBoxProcessed.Image = null;
                    img.Dispose();
                }
                
                _processingCts?.Dispose();
                
                // 强制GC回收
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"关闭表单时清理资源出错: {ex.Message}");
            }
            
            base.OnFormClosing(e);
        }
        #endregion
    }
}