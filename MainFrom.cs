using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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
        // 当前选中的相机名称
        private string currentCamera;
        // 当前帧
        private Bitmap currentFrame;
        //Bitmap frame;

        public MainFrom()
        {
            InitializeComponent();
            // 初始化各模块
            // 初始化相机控制器
            camera = new CameraController();
            // 初始化深度学习模型
            deepLearning = new DeepLearningModel();
            // 启动通信模块
            commModule = new CommunicationModule("127.0.0.1", 8000);
            currentCamera = "未选择";
            UpdateCameraStatus();
        }
        // 更新显示当前相机状态的Label
        private void UpdateCameraStatus()
        {
            labelCurrentCamera.Text = $"当前相机：{currentCamera}";
        }
        // 检测相机并弹出选择窗口
        private void btnDetectCamera_Click(object sender, EventArgs e)
        {
            // 停止之前的预览
            camera.StopCamera();
            if (camera.DetectCamera())
            {
                var cameras = camera.GetAvailableCameras();
                if (cameras.Count > 0)
                {
                    using (CameraSelection csForm = new CameraSelection(cameras))
                    {
                        if (csForm.ShowDialog() == DialogResult.OK)
                        {
                            currentCamera = csForm.SelectedCamera;
                            // 设置选中的相机
                            camera.SetSelectedCamera(currentCamera);
                            UpdateCameraStatus();
                            MessageBox.Show("选择的相机：" + currentCamera);

                            // 启动相机预览，将 NewFrame 事件委托给 Video_NewFrame 方法
                            //camera.StartCamera(new NewFrameEventHandler(Video_NewFrame));
                        }
                    }
                }
                else
                {
                    MessageBox.Show("未检测到任何相机！");
                }
            }
            else
            {
                MessageBox.Show("检测相机失败！");
            }
        }
        // 使用锁确保线程安全
        private readonly object imageLock = new object();
        // 更新预览框
        private void Video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            // 如果已有 currentFrame，则先释放
            currentFrame?.Dispose();
            // 复制当前帧
            currentFrame = (Bitmap)eventArgs.Frame.Clone();
            // 如果 PictureBox 中已有图像，释放掉旧图像
            if (pictureBoxDisplay.Image != null)
            {
                var oldImage = pictureBoxDisplay.Image;
                pictureBoxDisplay.Image = null;
                oldImage.Dispose();
            }
            // 确保线程安全更新 UI
            if (pictureBoxDisplay.InvokeRequired)
            {
                pictureBoxDisplay.BeginInvoke(new Action(() =>
                {
                    lock (imageLock)
                    {
                        pictureBoxDisplay.Image = currentFrame;
                    }
                }));
            }
            else
            {
                lock (imageLock)
                {
                    pictureBoxDisplay.Image = currentFrame;
                }
            }
        }

        // 设置参数：弹出参数设置对话框
        private void btnSetParameters_Click(object sender, EventArgs e)
        {
            var currentDevice = camera.GetCurrentCamera();
            if (currentDevice == null)
            {
                MessageBox.Show("请先检测并选择相机！");
                return;
            }
            using (ParameterInputForm paramForm = new ParameterInputForm(currentDevice))
            {
                if (paramForm.ShowDialog() == DialogResult.OK && paramForm.ParameterSet)
                {
                    bool success = camera.SetParameter("Resolution", paramForm.SelectedResolutionIndex);
                    if (success)
                    {
                        MessageBox.Show("参数设置成功！");
                        // 重新启动预览，新设置的分辨率生效
                        camera.StopCamera();
                        camera.StartCamera(new NewFrameEventHandler(Video_NewFrame));
                        camera.StartCamera(new NewFrameEventHandler(camera.CaptureImage));
                    }
                    else
                        MessageBox.Show("参数设置失败！");
                }
                else
                {   // 若未修改参数，重新启动预览
                    camera.StartCamera(new NewFrameEventHandler(Video_NewFrame));
                    camera.StartCamera(new NewFrameEventHandler(camera.CaptureImage));
                }
            }
        }

        // 拍摄图片并显示保存结果
        private void btnCapture_Click(object sender, EventArgs e)
        {
            camera.CaptureCurrentFrame();
            //Bitmap capturedImage = camera.CaptureImage();
            //MessageBox.Show("图片已保存!");
            pictureBoxDisplay.Image = camera.GetCurrentFrame();

            //if (currentFrame != null)
            //{
            //    using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            //    {
            //        saveFileDialog.Filter = "JPEG Image|*.jpg|PNG Image|*.png|Bitmap Image|*.bmp";
            //        saveFileDialog.Title = "保存捕获的图像";
            //        saveFileDialog.FileName = "CapturedImage";

            //        if (saveFileDialog.ShowDialog() == DialogResult.OK)
            //        {
            //            // 获取所选文件的扩展名
            //            string fileExtension = System.IO.Path.GetExtension(saveFileDialog.FileName).ToLower();
            //            // 根据扩展名设置图像格式
            //            ImageFormat imgFormat = ImageFormat.Jpeg;
            //            if (fileExtension == ".png")
            //            {
            //                imgFormat = ImageFormat.Png;
            //            }
            //            else if (fileExtension == ".bmp")
            //            {
            //                imgFormat = ImageFormat.Bmp;
            //            }

            //            // 保存图像
            //            currentFrame.Save(saveFileDialog.FileName, imgFormat);
            //            MessageBox.Show("图像已成功保存。", "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //        }
            //    }
            //}
            //else
            //{
            //    MessageBox.Show("没有可保存的图像。", "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            //}
        }

        // 处理图像、计算坐标并发送数据（与之前示例一致）
        private void btnProcess_Click(object sender, EventArgs e)
        {
            commModule.SendData(12, 34);
            MessageBox.Show("没做好！不可以点！");
            //string imagePath = @"C:\Images\captured.jpg";
            //Bitmap segmentedImage = deepLearning.ProcessImage(imagePath);
            //string segmentedPath = @"C:\Images\segmented.jpg";
            //segmentedImage.Save(segmentedPath);
            //pictureBoxDisplay.Image = segmentedImage;

            //// 计算物体坐标（示例：取中心点）
            //Point coordinate = ImageProcessor.ComputeObjectPosition(segmentedImage);
            //textBoxCoordinates.Text = $"X: {coordinate.X}\r\nY: {coordinate.Y}";
            //MessageBox.Show($"目标物体位置：X={coordinate.X}, Y={coordinate.Y}");

            //// 将坐标数据发送给 Roboguide 模块
            //commModule.SendData(coordinate.X, coordinate.Y);
        }
        // 在窗体关闭前停止预览
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 关闭服务器
            commModule.StopServer();
            // 停止相机预览
            camera.StopCamera();
        }
    }
}
