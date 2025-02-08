using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace RoboVision
{
    public class DeepLearningModel : IDisposable
    {
        private InferenceSession session;

        public DeepLearningModel()
        {
            //InitializeComponent();
        }

        /// <summary>
        /// 将 .pt 模型转换为 ONNX 模型，调用 Python 脚本完成转换。
        /// 请确保已准备好转换脚本 convert_to_onnx.py，并且系统中可调用 python 命令。
        /// </summary>
        /// <param name="ptModelPath">.pt 模型文件路径</param>
        /// <param name="onnxOutputPath">生成的 ONNX 模型保存路径</param>
        public void ConvertPtToOnnx(string ptModelPath, string onnxOutputPath)
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = "python", // 或 "python3"，取决于您的环境
                    Arguments = $"convert_to_onnx.py --pt_model \"{ptModelPath}\" --onnx_output \"{onnxOutputPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(start))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string err = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        MessageBox.Show("转换失败: " + err, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        MessageBox.Show("转换成功: " + output, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("转换时出错: " + ex.Message, "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 加载 ONNX 模型文件，创建推理会话。
        /// </summary>
        /// <param name="modelPath">ONNX 模型文件路径</param>
        public void LoadModel(string modelPath)
        {
            try
            {
                var sessionOptions = SessionOptions.MakeSessionOptionWithCudaProvider(0);
                session = new InferenceSession(modelPath, sessionOptions);
            }
            catch (Exception ex)
            {
                // 回退到 CPU 模式
                try
                {
                    session = new InferenceSession(modelPath);
                    MessageBox.Show("GPU 初始化失败，已回退到 CPU 模式：" + ex.Message);
                }
                catch (Exception cpuEx)
                {
                    MessageBox.Show("模型加载完全失败：" + cpuEx.Message);
                }
            }
        }

        /// <summary>
        /// 对传入的图片进行深度学习处理（例如目标检测、分割），并返回处理结果图像。
        /// </summary>
        /// <param name="imagePath">原始图像路径</param>
        /// <returns>处理后图像</returns>
        public Bitmap ProcessImage(string imagePath)
        {
            // 加载原始图像
            Bitmap original = new Bitmap(imagePath);

            // 预处理：缩放并归一化到 [0,1]，转换为 [1, 3, H, W] 的张量
            Tensor<float> inputTensor = PreprocessImage(original, 640, 640);

            // 根据您的模型输入名称调整此处（示例中假设输入节点名称为 "images"）
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", inputTensor)
            };
            if (session == null)
            {
                MessageBox.Show("导入模型出错: seesion 为空！", "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            try
            {
                using (var results = session.Run(inputs))
                {
                    // 后处理：解析模型输出，示例中绘制一个固定矩形
                    Bitmap outputImage = PostProcessResults(original, results);
                    return outputImage;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("处理图像出错: " + ex.Message, "异常", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return original;
            }
        }

        /// <summary>
        /// 将图像缩放到指定尺寸，并转换为张量，归一化到 [0,1]。
        /// </summary>
        private Tensor<float> PreprocessImage(Bitmap image, int targetWidth, int targetHeight)
        {
            Bitmap resized = new Bitmap(image, new Size(targetWidth, targetHeight));
            var tensor = new DenseTensor<float>(new int[] { 1, 3, targetHeight, targetWidth });
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    Color pixel = resized.GetPixel(x, y);
                    // 这里以 RGB 顺序为例，实际可能需要转换为 BGR 顺序
                    tensor[0, 0, y, x] = pixel.R / 255.0f;
                    tensor[0, 1, y, x] = pixel.G / 255.0f;
                    tensor[0, 2, y, x] = pixel.B / 255.0f;
                }
            }

            return tensor;
        }

        /// <summary>
        /// 解析模型输出，本示例在原图上绘制了一个固定矩形。
        /// 实际应用中需根据模型输出格式解析目标框、类别等信息。
        /// </summary>
        private Bitmap PostProcessResults(Bitmap original, IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results)
        {
            Bitmap output = new Bitmap(original);
            using (Graphics g = Graphics.FromImage(output))
            {
                Pen pen = new Pen(Color.Red, 2);
                // 示例：绘制一个固定矩形，实际应使用解析结果绘制检测框
                Rectangle detectionRect = new Rectangle(50, 50, 100, 100);
                g.DrawRectangle(pen, detectionRect);
            }
            return output;
        }
        public void Dispose()
        {
            if (session != null)
            {
                session.Dispose();
                session = null;
            }
        }
    }
}
