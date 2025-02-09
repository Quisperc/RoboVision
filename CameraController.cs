// CameraController.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Threading;
using Size = System.Drawing.Size;
using DirectShowLib;

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

        public event EventHandler<Bitmap> FrameUpdated;
        public event EventHandler<string> CaptureCompleted;
        public event EventHandler<string> ErrorOccurred;

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
            catch
            {
                // 回退方法
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

            // 使用 DirectShow 后端打开摄像头
            _videoCapture = new VideoCapture(cameraIndex, VideoCaptureAPIs.DSHOW);
            DeviceName = AvailableCameras[cameraIndex];
            InitializeResolutions();
        }

        private void InitializeResolutions()
        {
            AvailableResolutions.Clear();
            var standardResolutions = new List<Size>
            {
                new Size(1920, 1080),
                new Size(1280, 720),
                new Size(800, 600),
                new Size(640, 480),
                new Size(320, 240)
            };

            foreach (var res in standardResolutions)
            {
                _videoCapture.Set(VideoCaptureProperties.FrameWidth, res.Width);
                _videoCapture.Set(VideoCaptureProperties.FrameHeight, res.Height);

                var actualWidth = _videoCapture.Get(VideoCaptureProperties.FrameWidth);
                var actualHeight = _videoCapture.Get(VideoCaptureProperties.FrameHeight);

                if (actualWidth == res.Width && actualHeight == res.Height)
                {
                    AvailableResolutions.Add(res);
                }
            }

            if (!AvailableResolutions.Any())
            {
                AvailableResolutions.Add(new Size(
                    (int)_videoCapture.Get(VideoCaptureProperties.FrameWidth),
                    (int)_videoCapture.Get(VideoCaptureProperties.FrameHeight)
                ));
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
            _captureThread?.Join(1000);
            //_videoCapture?.Release();
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
            finally
            {
                if (wasRunning)
                    StartPreview();
            }
        }

        public void CaptureFrame()
        {
            try
            {
                if (_currentFrame == null || _currentFrame.Empty()) return;

                var savePath = GetUniqueFilePath();
                using (var bitmap = BitmapConverter.ToBitmap(_currentFrame))
                {
                    EnsureDirectoryExists(savePath);
                    bitmap.Save(savePath, ImageFormat.Jpeg);
                }
                CaptureCompleted?.Invoke(this, savePath);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"捕获失败: {ex.Message}");
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
            _captureThread?.Join(1000);

            _currentFrame?.Dispose();
            _videoCapture?.Dispose();

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        ~CameraController()
        {
            Dispose();
        }
    }
}
