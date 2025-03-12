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
        private bool _isRunning;
        private bool _isDisposed;
        private Size _frameSize;

        private int _selectedCameraIndex;


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
            _videoCapture?.Dispose();
            _videoCapture = null;
            _videoCapture = new VideoCapture();
            AvailableCameras.Clear();
            try
            {
                // 使用 DirectShow 获取设备名称
                DsDevice[] devices = DsDevice.GetDevicesOfCat(FilterCategory.VideoInputDevice);
                foreach (DsDevice device in devices)
                {
                    AvailableCameras.Add(device.Name);
                }
            }
            catch (Exception ex)
            {
                // 记录错误但不要吞掉异常
                Debug.WriteLine($"DirectShow设备枚举失败: {ex.Message}");
                // 回退方法需要明确说明索引不可靠
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
        }

        public void SelectCamera(int cameraIndex)
        {
            if (cameraIndex < 0 || cameraIndex >= AvailableCameras.Count)
                throw new ArgumentException("无效的摄像头索引");

            StopCamera();
            _selectedCameraIndex = cameraIndex;
            DeviceName = AvailableCameras[cameraIndex];
            ConnectingCamera?.Invoke(this, DeviceName);
            // 获取设备支持的分辨率
            InitializeResolutions();

            // 使用DirectShow后端初始化摄像头
            _videoCapture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);

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

            try
            {
                // 创建设备的Filter
                Guid iid = typeof(IBaseFilter).GUID;
                device.Mon.BindToObject(null, null, ref iid, out sourceObj);
                IBaseFilter filter = (IBaseFilter)sourceObj;

                // 获取输出Pin
                IPin pin = DsFindPin.ByDirection(filter, PinDirection.Output, 0);
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
                if (AvailableResolutions.Count == 0)
                {
                    AvailableResolutions.Add(new Size(
                        (int)_videoCapture.Get(VideoCaptureProperties.FrameWidth),
                        (int)_videoCapture.Get(VideoCaptureProperties.FrameHeight)));
                }
            }
            finally
            {
                if (streamConfig != null) Marshal.ReleaseComObject(streamConfig);
                if (sourceObj != null) Marshal.ReleaseComObject(sourceObj);
            }
        }

        public void StartPreview()
        {
            if (_videoCapture == null || !_videoCapture.IsOpened())
                throw new InvalidOperationException("摄像头未初始化");

            if (_isRunning) return;

            _isRunning = true;
            _captureThread = new Thread(CaptureLoop)
            {
                IsBackground = true
            };
            _captureThread.Start();
        }

        private void CaptureLoop()
        {
            while (_isRunning)
            {
                try
                {
                    using (var frame = new Mat()) // 使用 using 确保 Mat 释放
                    {
                        if (_videoCapture.Read(frame) && !frame.Empty())
                        {
                            _currentFrame?.Dispose();
                            _currentFrame = frame.Clone();
                            _frameSize = new Size(frame.Width, frame.Height);

                            using (var bitmap = BitmapConverter.ToBitmap(frame))
                            {
                                var clonedBitmap = (Bitmap)bitmap.Clone();
                                FrameUpdated?.Invoke(this, clonedBitmap);
                            }
                        }
                    }
                    Thread.Sleep(33);
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"捕获错误: {ex.Message}");
                    _isRunning = false;
                }
            }
        }

        public void StopCamera()
        {
            _isRunning = false;

            // 停止捕获线程
            if (_captureThread != null && _captureThread.IsAlive)
            {
                if (!_captureThread.Join(2000))
                {
                    try { _captureThread.Interrupt(); }
                    catch { /* Ignore thread state exceptions */ }
                }
            }

            // 仅释放当前帧，保留摄像头实例
            _currentFrame?.Dispose();
            _currentFrame = null;
        }

        public void SetResolution(Size resolution)
        {
            if (!AvailableResolutions.Contains(resolution))
                throw new ArgumentException("不支持的分辨率");

            bool wasRunning = IsPreviewing;

            try
            {
                if (wasRunning)
                    StopCamera();

                _videoCapture.Set(VideoCaptureProperties.FrameWidth, resolution.Width);
                _videoCapture.Set(VideoCaptureProperties.FrameHeight, resolution.Height);

                // 验证分辨率设置
                double actualWidth = _videoCapture.Get(VideoCaptureProperties.FrameWidth);
                double actualHeight = _videoCapture.Get(VideoCaptureProperties.FrameHeight);

                if (actualWidth != resolution.Width || actualHeight != resolution.Height)
                    throw new ArgumentException("分辨率设置失败");

                _frameSize = resolution;

            }
            catch (Exception ex)
            {
                OnErrorOccurred($"分辨率设置失败: {ex.Message}");
            }
            finally
            {
                if (wasRunning)
                    StartPreview();
            }
        }

        private readonly object _frameLock = new object(); // 新增锁对象
        public Mat CaptureFrame()
        {
            Mat frameCopy = null;
            Mat frameCopyuse = null;
            try
            {
                lock (_frameLock) // 加锁保证线程安全
                {
                    // 检查对象有效性
                    if (_currentFrame == null || _currentFrame.IsDisposed || _currentFrame.Empty())
                        return null;

                    // 创建深度拷贝
                    frameCopy = _currentFrame.Clone();
                    // 用于保存用来深度学习的对象
                    frameCopyuse?.Dispose();
                    frameCopyuse = _currentFrame.Clone();
                }

                var savePath = GetUniqueFilePath();
                using (var bitmap = BitmapConverter.ToBitmap(frameCopy))
                {
                    EnsureDirectoryExists(savePath);
                    bitmap.Save(savePath, ImageFormat.Jpeg);
                }
                CaptureCompleted?.Invoke(this, savePath);
                return frameCopyuse;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"捕获失败: {ex.Message}");
                return null; // 确保在异常情况下返回值
            }
            finally
            {
                frameCopy?.Dispose(); // 确保临时拷贝被释放
                //frameCopyuse?.Dispose(); // 不能释放，否则返回的对象无效
            }
        }

        private static string GetUniqueFilePath()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "input", $"Capture_{timestamp}.jpg");
        }

        private static void EnsureDirectoryExists(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directory);
        }

        private void OnErrorOccurred(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isRunning = false;
            //_captureThread?.Join(1000);
            if (_captureThread != null && _captureThread.IsAlive)
            {
                if (!_captureThread.Join(2000))
                {
                    _captureThread.Interrupt();
                }
            }

            _currentFrame?.Dispose();
            _videoCapture?.Dispose();
            _videoCapture = null;
            _currentFrame = null;

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        ~CameraController()
        {
            Dispose();
        }
    }
}
