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
//相机使用AForge.Video库
using AForge.Video;
using AForge.Video.DirectShow;

namespace RoboVision
{
    public partial class CameraController : Form
    {
        private FilterInfoCollection videoDevices;
        private VideoCaptureDevice videoSource;
        private Bitmap currentFrame;               // 当前帧图像
        // 标识是否需要在下一个新帧时捕获图像
        private bool isCaptureRequested = false;

        public CameraController()
        {
            // 初始化时获取所有视频输入设备
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
        }

        // 检测是否存在相机
        public bool DetectCamera()
        {
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            return videoDevices.Count > 0;
        }

        // 返回可用相机名称列表
        public List<string> GetAvailableCameras()
        {
            List<string> cameras = new List<string>();
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo device in videoDevices)
            {
                cameras.Add(device.Name);
            }
            return cameras;
        }

        // 设置当前选中的相机（通过设备名称匹配）
        public void SetSelectedCamera(string cameraName)
        {
            videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            foreach (FilterInfo device in videoDevices)
            {
                if (device.Name.Equals(cameraName, StringComparison.OrdinalIgnoreCase))
                {
                    videoSource = new VideoCaptureDevice(device.MonikerString);
                    break;
                }
            }
        }

        // 获取当前选中的相机对象
        public VideoCaptureDevice GetCurrentCamera()
        {
            return videoSource;
        }

        // 启动相机预览（通过 NewFrame 事件返回图像）
        public void StartCamera(NewFrameEventHandler frameHandler)
        {
            if (videoSource != null)
            {
                videoSource.NewFrame += frameHandler;
                videoSource.Start();
            }
        }

        // 请求捕获当前帧图像
        public void CaptureCurrentFrame()
        {
            isCaptureRequested = true;
        }
        // 返回当前帧图像
        public Bitmap GetCurrentFrame()
        {
            return currentFrame;
        }
        // 新帧事件处理方法：更新 currentFrame，并在请求捕获时保存图像
        public void CaptureImage(object sender, NewFrameEventArgs eventArgs)
        {
            // 释放之前的帧
            currentFrame?.Dispose();
            // 克隆新帧
            currentFrame = (Bitmap)eventArgs.Frame.Clone();

            // 判断是否请求捕获
            if (isCaptureRequested)
            {
                // 重置捕获请求标志
                isCaptureRequested = false;

                // 为避免当前帧后续修改影响保存结果，复制一份
                Bitmap capturedFrame = (Bitmap)currentFrame.Clone();

                // 构造文件名（注意：确保 "/input" 路径存在并具备写权限）
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"CapturedImage_{timestamp}.jpg";

                //保存问题
                string folderPath = Path.Combine(Application.StartupPath, "input");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                string savePath = Path.Combine(folderPath, fileName);
                capturedFrame.Save(savePath, ImageFormat.Jpeg);
                // 保存图像为 JPEG 格式
                try
                {
                    capturedFrame.Save(savePath, ImageFormat.Jpeg);
                }
                catch (ExternalException ex)
                {
                    MessageBox.Show("保存图像错误: " + ex.Message);
                }
                // 释放之前的帧
                capturedFrame?.Dispose();
                // 如果在非UI线程调用 MessageBox.Show，可能需要调度到UI线程（示例中直接调用）
                MessageBox.Show("图片已保存至：" + savePath);

                // 根据需求，可在这里对 capturedFrame 做进一步处理，或者释放它
            }
        }

        // 停止相机预览
        public void StopCamera()
        {
            if (videoSource != null && videoSource.IsRunning)
            {
                videoSource.SignalToStop();
                videoSource.WaitForStop();
            }
        }

        // 设置相机参数示例：以“Resolution”为例，根据传入的分辨率索引来设置视频参数
        // value 应为 int 类型的索引
        public bool SetParameter(string paramName, object value)
        {
            if (videoSource == null)
                return false;

            if (paramName == "Resolution")
            {
                int index = Convert.ToInt32(value);
                VideoCapabilities[] capabilities = videoSource.VideoCapabilities;
                if (capabilities != null && index >= 0 && index < capabilities.Length)
                {
                    videoSource.VideoResolution = capabilities[index];
                    return true;
                }
            }
            // 可根据需要扩展其他参数
            return false;
        }
    }
}
