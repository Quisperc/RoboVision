// CameraController.cs
using DirectShowLib;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using FormatType = DirectShowLib.FormatType;
using Size = System.Drawing.Size;

namespace RoboVision
{
    public class CameraController : IDisposable
    {
        private VideoCapture _videoCapture;
        private Mat _currentFrame;
        private Thread _captureThread;
        private volatile bool _isRunning;
        private bool _isDisposed;
        private Size _frameSize;
        private int _selectedCameraIndex;
        private readonly object _captureLock = new object();

        public event EventHandler<Bitmap> FrameUpdated;
        public event EventHandler<string> CaptureCompleted;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<string> ConnectingCamera;

        public List<string> AvailableCameras { get; } = new List<string>();
        public List<Size> AvailableResolutions { get; } = new List<Size>();
        public string DeviceName { get; private set; } = "未选择设备";
        public bool IsPreviewing => _isRunning;
        public Size CurrentResolution => _frameSize;

        public CameraController()
        {
            RefreshDevices();
        }

        public void RefreshDevices()
        {
            try
            {
                // 释放之前的摄像头资源
                if (_videoCapture != null)
                {
                    StopCamera();
                    _videoCapture.Dispose();
                    _videoCapture = null;
                }
                
                _videoCapture = new VideoCapture();
                AvailableCameras.Clear();
                
                // 使用 DirectShow 获取设备名称
                DsDevice[] devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
                foreach (DsDevice device in devices)
                {
                    AvailableCameras.Add(device.Name);
                }
                
                // 如果没有找到设备，使用备用方法
                if (AvailableCameras.Count == 0)
                {
                    FallbackDeviceEnumeration();
                }
            }
            catch (Exception ex)
            {
                // 记录错误并使用备用方法
                Debug.WriteLine($"DirectShow设备枚举失败: {ex.Message}");
                FallbackDeviceEnumeration();
            }
        }
        
        private void FallbackDeviceEnumeration()
        {
            // 备用方法：使用索引尝试打开摄像头
            for (int i = 0; i < 10; i++)
            {
                using (var testCapture = new VideoCapture(i, VideoCaptureAPIs.DSHOW))
                {
                    if (testCapture.IsOpened())
                    {
                        AvailableCameras.Add($"摄像头 {i + 1}");
                        testCapture.Release();
                    }
                }
            }
        }

        public void SelectCamera(int cameraIndex)
        {
            if (cameraIndex < 0 || cameraIndex >= AvailableCameras.Count)
                throw new ArgumentException("无效的摄像头索引");

            StopCamera();
            _selectedCameraIndex = cameraIndex;
            DeviceName = AvailableCameras[cameraIndex];
            ConnectingCamera?.Invoke(this, DeviceName);
            
            try
            {
                // 获取设备支持的分辨率
                InitializeResolutions();

                // 释放旧的VideoCapture实例
                _videoCapture?.Dispose();
                
                // 使用DirectShow后端初始化摄像头
                _videoCapture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);
                
                if (!_videoCapture.IsOpened())
                {
                    throw new InvalidOperationException($"无法打开摄像头 {DeviceName}");
                }

                // 尝试设置最高分辨率（可选）
                if (AvailableResolutions.Count > 0)
                {
                    var maxRes = AvailableResolutions[0];
                    _videoCapture.Set(VideoCaptureProperties.FrameWidth, maxRes.Width);
                    _videoCapture.Set(VideoCaptureProperties.FrameHeight, maxRes.Height);
                }

                // 更新当前分辨率
                _frameSize = new Size(
                    (int)_videoCapture.Get(VideoCaptureProperties.FrameWidth),
                    (int)_videoCapture.Get(VideoCaptureProperties.FrameHeight)
                );
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"选择摄像头失败: {ex.Message}");
                throw;
            }
        }

        private void InitializeResolutions()
        {
            AvailableResolutions.Clear();

            DsDevice[] devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
            if (_selectedCameraIndex >= devices.Length)
            {
                OnErrorOccurred("选中的摄像头索引无效。");
                return;
            }

            DsDevice device = devices[_selectedCameraIndex];
            object sourceObj = null;
            IAMStreamConfig streamConfig = null;
            IPin pin = null;

            try
            {
                // 创建设备的Filter
                Guid iid = typeof(IBaseFilter).GUID;
                device.Mon.BindToObject(null, null, ref iid, out sourceObj);
                IBaseFilter filter = (IBaseFilter)sourceObj;

                // 获取输出Pin
                pin = DsFindPin.ByDirection(filter, PinDirection.Output, 0);
                if (pin == null)
                {
                    OnErrorOccurred("无法找到输出Pin。");
                    return;
                }

                // 获取IAMStreamConfig接口
                streamConfig = pin as IAMStreamConfig;
                if (streamConfig == null)
                {
                    OnErrorOccurred("设备不支持流配置接口。");
                    return;
                }

                // 获取能力数量
                int count, size;
                int hr = streamConfig.GetNumberOfCapabilities(out count, out size);
                if (hr != 0) Marshal.ThrowExceptionForHR(hr);

                IntPtr capsPtr = Marshal.AllocCoTaskMem(size);
                HashSet<Size> uniqueResolutions = new HashSet<Size>();

                try
                {
                    for (int i = 0; i < count; i++)
                    {
                        AMMediaType mediaType = null;
                        try
                        {
                            hr = streamConfig.GetStreamCaps(i, out mediaType, capsPtr);
                            if (hr != 0) continue;

                            if (mediaType.formatType == FormatType.VideoInfo)
                            {
                                VideoInfoHeader videoInfo = (VideoInfoHeader)Marshal.PtrToStructure(
                                    mediaType.formatPtr, typeof(VideoInfoHeader));
                                Size res = new Size(videoInfo.BmiHeader.Width, videoInfo.BmiHeader.Height);
                                uniqueResolutions.Add(res);
                            }
                        }
                        finally
                        {
                            if (mediaType != null) DsUtils.FreeAMMediaType(mediaType);
                        }
                    }

                    AvailableResolutions.AddRange(uniqueResolutions
                        .OrderByDescending(s => s.Width)
                        .ThenByDescending(s => s.Height));
                }
                finally
                {
                    Marshal.FreeCoTaskMem(capsPtr);
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"获取分辨率失败: {ex.Message}");
                // 添加默认回退分辨率
                AddDefaultResolutions();
            }
            finally
            {
                // 释放COM对象
                if (pin != null) Marshal.ReleaseComObject(pin);
                if (streamConfig != null) Marshal.ReleaseComObject(streamConfig);
                if (sourceObj != null) Marshal.ReleaseComObject(sourceObj);
            }
            
            // 确保至少有一个分辨率可用
            if (AvailableResolutions.Count == 0)
            {
                AddDefaultResolutions();
            }
        }
        
        private void AddDefaultResolutions()
        {
            // 添加一些常用分辨率作为备选
            if (_videoCapture != null && _videoCapture.IsOpened())
            {
                AvailableResolutions.Add(new Size(
                    (int)_videoCapture.Get(VideoCaptureProperties.FrameWidth),
                    (int)_videoCapture.Get(VideoCaptureProperties.FrameHeight)));
            }
            
            // 添加常见分辨率
            HashSet<Size> defaultResolutions = new HashSet<Size>
            {
                new Size(640, 480),   // VGA
                new Size(1280, 720),  // HD
                new Size(1920, 1080)  // Full HD
            };
            
            foreach (var res in defaultResolutions)
            {
                if (!AvailableResolutions.Any(r => r.Width == res.Width && r.Height == res.Height))
                {
                    AvailableResolutions.Add(res);
                }
            }
        }

        public void StartPreview()
        {
            if (_isRunning)
                return;

            if (_videoCapture == null || !_videoCapture.IsOpened())
            {
                OnErrorOccurred("摄像头未初始化或未打开");
                return;
            }

            _isRunning = true;
            _captureThread = new Thread(CaptureLoop)
            {
                IsBackground = true,
                Name = "CameraCapture"
            };
            _captureThread.Start();
        }

        private void CaptureLoop()
        {
            try
            {
                using (var frame = new Mat())
                {
                    while (_isRunning && !_isDisposed)
                    {
                        // 防止视频捕获设备被意外释放
                        if (_videoCapture == null || !_videoCapture.IsOpened())
                        {
                            OnErrorOccurred("摄像头连接已丢失");
                            break;
                        }

                        // 尝试读取帧
                        bool hasFrame;
                        lock (_captureLock)
                        {
                            hasFrame = _videoCapture.Read(frame);
                        }

                        if (!hasFrame || frame.Empty())
                        {
                            Thread.Sleep(10);
                            continue;
                        }

                        // 创建Bitmap用于UI显示（避免跨线程访问同一个Mat对象）
                        try
                        {
                            // 修复内存泄漏：确保Bitmap被正确释放
                            using (Bitmap frameBitmap = BitmapConverter.ToBitmap(frame))
                            {
                                // 使用克隆防止跨线程访问问题
                                Bitmap displayBitmap = (Bitmap)frameBitmap.Clone();
                                FrameUpdated?.Invoke(this, displayBitmap);
                                
                                // 注意：这里异步传递了displayBitmap，必须确保它在UI线程中被正确释放
                                // 由事件处理器负责释放displayBitmap
                            }
                        }
                        catch (Exception ex)
                        {
                            OnErrorOccurred($"帧转换错误: {ex.Message}");
                        }

                        // 控制帧率
                        Thread.Sleep(15); // ~60fps
                        
                        // 强制GC收集，减轻内存压力(仅在debug模式使用，生产环境移除)
                        #if DEBUG
                        if (DateTime.Now.Second % 10 == 0) // 每10秒执行一次
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                        #endif
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"预览循环异常: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
            }
        }

        public void StopCamera()
        {
            _isRunning = false;

            if (_captureThread != null && _captureThread.IsAlive)
            {
                try
                {
                    _captureThread.Join(500); // 等待线程结束
                    if (_captureThread.IsAlive)
                    {
                        // 如果线程仍在运行，记录但不中止（避免不安全的线程终止）
                        Debug.WriteLine("警告: 相机捕获线程未能在500ms内停止");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"停止捕获线程异常: {ex.Message}");
                }
                _captureThread = null;
            }
        }

        public void SetResolution(Size resolution)
        {
            if (_videoCapture == null || !_videoCapture.IsOpened())
            {
                OnErrorOccurred("设置分辨率失败：摄像头未初始化或未打开");
                return;
            }

            lock (_captureLock)
            {
                try
                {
                    // 停止预览
                    bool wasRunning = _isRunning;
                    StopCamera();

                    // 设置分辨率
                    _videoCapture.Set(VideoCaptureProperties.FrameWidth, resolution.Width);
                    _videoCapture.Set(VideoCaptureProperties.FrameHeight, resolution.Height);

                    // 验证实际设置的分辨率
                    int actualWidth = (int)_videoCapture.Get(VideoCaptureProperties.FrameWidth);
                    int actualHeight = (int)_videoCapture.Get(VideoCaptureProperties.FrameHeight);
                    _frameSize = new Size(actualWidth, actualHeight);

                    // 记录实际分辨率与请求分辨率的差异
                    if (actualWidth != resolution.Width || actualHeight != resolution.Height)
                    {
                        Debug.WriteLine($"警告: 请求分辨率 {resolution.Width}x{resolution.Height} " +
                                       $"实际设置为 {actualWidth}x{actualHeight}");
                    }

                    // 重新启动预览
                    if (wasRunning)
                    {
                        StartPreview();
                    }
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"设置分辨率失败: {ex.Message}");
                    throw;
                }
            }
        }

        private readonly object _frameLock = new object(); // 新增锁对象
        
        public Mat CaptureFrame()
        {
            if (_videoCapture == null || !_videoCapture.IsOpened())
            {
                OnErrorOccurred("摄像头未初始化");
                return null;
            }

            lock (_captureLock)
            {
                try
                {
                    var frame = new Mat();
                    
                    // 尝试读取多帧以跳过缓冲区中的旧帧
                    for (int i = 0; i < 3; i++)
                    {
                        if (!_videoCapture.Read(frame) || frame.Empty())
                        {
                            Thread.Sleep(10);
                        }
                    }
                    
                    // 读取最新帧
                    if (!_videoCapture.Read(frame) || frame.Empty())
                    {
                        frame.Dispose();
                        OnErrorOccurred("无法捕获帧");
                        return null;
                    }

                    // 拍照时保存到文件
                    SaveFrameToFile(frame);

                    return frame;
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"捕获帧失败: {ex.Message}");
                    return null;
                }
            }
        }
        
        private void SaveFrameToFile(Mat frame)
        {
            if (frame == null || frame.Empty())
                return;

            try
            {
                // 获取唯一文件路径
                string filePath = GetUniqueFilePath();
                EnsureDirectoryExists(filePath);
                
                // 保存图像
                using (Bitmap bitmap = BitmapConverter.ToBitmap(frame))
                {
                    // 使用高质量JPEG编码保存
                    using (var encoderParams = new EncoderParameters(1))
                    using (var qualityParam = new EncoderParameter(Encoder.Quality, 95L))
                    {
                        encoderParams.Param[0] = qualityParam;
                        ImageCodecInfo jpegEncoder = GetJpegEncoder();
                        
                        if (jpegEncoder != null)
                        {
                            bitmap.Save(filePath, jpegEncoder, encoderParams);
                        }
                        else
                        {
                            bitmap.Save(filePath, ImageFormat.Jpeg);
                        }
                    }
                }
                
                // 通知完成
                CaptureCompleted?.Invoke(this, filePath);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"保存图像失败: {ex.Message}");
            }
        }
        
        private ImageCodecInfo GetJpegEncoder()
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == ImageFormat.Jpeg.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private static string GetUniqueFilePath()
        {
            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CapturedImages");
            string filename = $"Capture_{DateTime.Now:yyyyMMdd_HHmmss_fff}.jpg";
            return Path.Combine(directory, filename);
        }

        private static void EnsureDirectoryExists(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        private void OnErrorOccurred(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    StopCamera();
                    
                    lock (_captureLock)
                    {
                        if (_videoCapture != null)
                        {
                            try
                            {
                                _videoCapture.Release();
                                _videoCapture.Dispose();
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"释放视频捕获资源时出错: {ex.Message}");
                            }
                            finally
                            {
                                _videoCapture = null;
                            }
                        }
                        
                        _currentFrame?.Dispose();
                        _currentFrame = null;
                    }
                }
                
                _isDisposed = true;
            }
        }
        
        ~CameraController()
        {
            Dispose(false);
        }
    }
}
