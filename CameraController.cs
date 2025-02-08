using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using AForge.Video;
using AForge.Video.DirectShow;
using static System.Net.Mime.MediaTypeNames;

namespace RoboVision
{
    public class CameraController : IDisposable
    {
        private FilterInfoCollection _videoDevices;
        private VideoCaptureDevice _videoSource;
        private Bitmap _currentFrame;
        private bool _isCaptureRequested;
        private bool _isDisposed;

        public event EventHandler<Bitmap> FrameUpdated;
        public event EventHandler<string> CaptureCompleted;
        public event EventHandler<string> ErrorOccurred;

        public CameraController()
        {
            RefreshDevices();
        }

        public void RefreshDevices()
        {
            _videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        }

        public bool CameraExists => _videoDevices.Count > 0;

        public IEnumerable<string> AvailableCameras =>
            _videoDevices.Cast<FilterInfo>().Select(device => device.Name);

        /// <summary>
        /// 获取当前相机支持的分辨率列表
        /// </summary>
        public VideoCapabilities[] AvailableResolutions
            => _videoSource?.VideoCapabilities ?? Array.Empty<VideoCapabilities>();

        /// <summary>
        /// 获取当前使用的分辨率
        /// </summary>
        public string CurrentResolution
            => _videoSource?.VideoResolution?.FrameSize.ToString() ?? "未知";

        // 新增设备信息访问
        // <summary>
        /// 当前设备名称
        /// </summary>
        public string DeviceName { get; private set; } = "未选择设备";
        public bool IsPreviewing => _videoSource?.IsRunning ?? false;

        private void UpdateDeviceInfo()
        {
            DeviceName = _videoSource?.Source ?? "未选择设备";
        }


        public void SelectCamera(string cameraName)
        {
            if (string.IsNullOrWhiteSpace(cameraName))
                throw new ArgumentException("Camera name cannot be empty", nameof(cameraName));

            var device = _videoDevices.Cast<FilterInfo>()
                .FirstOrDefault(d => d.Name.Equals(cameraName, StringComparison.OrdinalIgnoreCase));

            if (device == null)
                throw new ArgumentException($"Camera '{cameraName}' not found");

            if (_videoSource?.IsRunning == true)
                StopCamera();

            _videoSource = new VideoCaptureDevice(device.MonikerString);
            // [新增] 更新设备名称
            DeviceName = device?.Name ?? "未知设备";
        }

        public void StartPreview()
        {
            if (_videoSource == null)
                throw new InvalidOperationException("No camera selected");

            if (!_videoSource.IsRunning)
            {
                _videoSource.NewFrame += OnNewFrameReceived;
                _videoSource.Start();
            }
        }

        public void StopCamera()
        {
            if (_videoSource?.IsRunning == true)
            {
                _videoSource.SignalToStop();
                _videoSource.NewFrame -= OnNewFrameReceived;
                _videoSource.WaitForStop();
            }
        }

        public void CaptureFrame()
        {
            _isCaptureRequested = true;
        }

        public bool TrySetResolution(int index)
        {
            if (_videoSource == null) return false;

            var capabilities = _videoSource.VideoCapabilities;
            if (capabilities == null || index < 0 || index >= capabilities.Length)
                return false;

            _videoSource.VideoResolution = capabilities[index];
            return true;
        }

        private void OnNewFrameReceived(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                // 更新当前帧
                UpdateCurrentFrame(eventArgs.Frame);

                // 处理捕获请求
                if (_isCaptureRequested)
                {
                    _isCaptureRequested = false;
                    SaveCapturedFrame(eventArgs.Frame);
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Frame processing error: {ex.Message}");
            }
        }

        private void UpdateCurrentFrame(Bitmap frame)
        {
            var previous = _currentFrame;
            _currentFrame = (Bitmap)frame.Clone();
            previous?.Dispose();

            FrameUpdated?.Invoke(this, _currentFrame);
        }

        private void SaveCapturedFrame(Bitmap frame)
        {
            try
            {
                var savePath = GetUniqueFilePath();
                using (var clonedFrame = (Bitmap)frame.Clone())
                {
                    EnsureDirectoryExists(savePath);
                    clonedFrame.Save(savePath, ImageFormat.Jpeg);
                }
                CaptureCompleted?.Invoke(this, savePath);
            }
            catch (ExternalException ex)
            {
                OnErrorOccurred($"Image save error: {ex.Message}");
            }
            catch (IOException ex)
            {
                OnErrorOccurred($"File operation error: {ex.Message}");
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Unexpected error: {ex.Message}");
            }
        }

        private static string GetUniqueFilePath()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            var fileName = $"Capture_{timestamp}.jpg";
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "input", fileName);
        }

        private static void EnsureDirectoryExists(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
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
            if (_isDisposed) return;

            StopCamera();
            _videoSource?.SignalToStop();
            _currentFrame?.Dispose();

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        ~CameraController()
        {
            Dispose();
        }
    }
}
