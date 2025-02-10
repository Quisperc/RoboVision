// ======================== 深度学习模块 DeepLearningModule.cs ========================
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace RoboVision
{
    public class DeepLearningModel : IDisposable
    {
        public event EventHandler<string> ModelLoaded;
        public event EventHandler<string> InferenceCompleted;
        public event EventHandler<string> ProceedCompleted;
        public event EventHandler<string> ErrorOccurred;

        private InferenceSession _session;
        private bool _disposed;

        // 新增常量定义
        private const int TargetSize = 640;    // YOLOv8输入尺寸
        private const float ConfidenceThreshold = 0.5f;
        private const float NmsThreshold = 0.5f;
        private static readonly string[] Labels = LoadLabels(); // COCO数据集标签

        private static string[] LoadLabels()
        {
            // COCO数据集80个类别标签
            return new string[]
            {
                "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat",
                "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat",
                "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack",
                "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball",
                "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket",
                "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple",
                "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake", "chair",
                "couch", "potted plant", "bed", "dining table", "toilet", "tv", "laptop", "mouse", "remote",
                "keyboard", "cell phone", "microwave", "oven", "toaster", "sink", "refrigerator", "book",
                "clock", "vase", "scissors", "teddy bear", "hair drier", "toothbrush"
            };
        }

        public bool IsInitialized => _session != null;

        public void LoadModel(string modelPath)
        {
            try
            {
                _session?.Dispose();

                var sessionOptions = new SessionOptions();
                try
                {
                    ModelLoaded?.Invoke(this, $"使用GPU加速中......");
                    sessionOptions.AppendExecutionProvider_CUDA();
                }
                catch
                {
                    ModelLoaded?.Invoke(this, $"无法加载GPU，加载CPU。");
                    sessionOptions.AppendExecutionProvider_CPU();
                }

                _session = new InferenceSession(modelPath, sessionOptions);
                // 关闭
                sessionOptions?.Close();
                ModelLoaded?.Invoke(this, $"成功加载模型：{modelPath}");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"模型加载失败：{ex.Message}");
            }
        }

        public DetectionResult ProcessFrame(Bitmap frame)
        {
            if (_session == null)
            {
                ErrorOccurred?.Invoke(this, "模型未初始化");
                return null;
            }

            try
            {
                float ratio;
                (int top, int left) pad;
                var inputTensor = PreprocessFrame(frame, out ratio, out pad);
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("images", inputTensor)
                };

                using (var results = _session.Run(inputs))
                {
                    var output = results.First().AsTensor<float>();
                    var detections = ParseOutput(output, ratio, pad, frame.Width, frame.Height);

                    InferenceCompleted?.Invoke(this, $"检测到 {detections.Count} 个目标");
                    var detectionResult = new DetectionResult
                    {
                        ProcessedImage = PostProcessFrame((Bitmap)frame.Clone(), detections),
                        Detections = detections
                    };
                    saveProceed(detectionResult.ProcessedImage);
                    return detectionResult;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"推理失败：{ex.Message}");
                return null;
            }
        }

        private Tensor<float> PreprocessFrame(Bitmap frame, out float ratio, out (int top, int left) pad)
        {
            // 计算缩放比例
            ratio = Math.Min((float)TargetSize / frame.Width, (float)TargetSize / frame.Height);
            var newWidth = (int)(frame.Width * ratio);
            var newHeight = (int)(frame.Height * ratio);
            pad = ((TargetSize - newHeight) / 2, (TargetSize - newWidth) / 2);

            // 创建Letterbox图像
            var resized = new Bitmap(TargetSize, TargetSize);
            using (var g = Graphics.FromImage(resized))
            {
                g.Clear(Color.FromArgb(114, 114, 114)); // YOLO标准填充色
                g.DrawImage(frame, pad.left, pad.top, newWidth, newHeight);
            }

            // 创建输入张量 [1,3,640,640]
            var inputTensor = new DenseTensor<float>(new[] { 1, 3, TargetSize, TargetSize });

            // 使用LockBits快速访问像素
            var bitmapData = resized.LockBits(new Rectangle(0, 0, resized.Width, resized.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            unsafe
            {
                byte* p = (byte*)bitmapData.Scan0;
                for (int y = 0; y < bitmapData.Height; y++)
                {
                    for (int x = 0; x < bitmapData.Width; x++)
                    {
                        // 输入通道顺序为RGB，归一化到0-1范围
                        inputTensor[0, 0, y, x] = p[2] / 255f; // R
                        inputTensor[0, 1, y, x] = p[1] / 255f; // G 
                        inputTensor[0, 2, y, x] = p[0] / 255f; // B
                        p += 3;
                    }
                    p += bitmapData.Stride - bitmapData.Width * 3;
                }
            }
            resized.UnlockBits(bitmapData);
            return inputTensor;
        }

        private List<BoundingBox> ParseOutput(Tensor<float> output, float ratio, (int top, int left) pad,
                                            int origWidth, int origHeight)
        {
            var boxes = new List<BoundingBox>();
            var outputData = output.ToArray();

            // YOLOv8输出格式 [1,84,8400]
            for (int i = 0; i < output.Dimensions[2]; i++)
            {
                // 跳过低置信度检测
                float confidence = outputData[4 + i * output.Dimensions[1]];
                if (confidence < ConfidenceThreshold) continue;

                // 获取最大类别分数
                int classId = 0;
                float maxScore = 0;
                for (int c = 4; c < output.Dimensions[1]; c++)
                {
                    float score = outputData[c + i * output.Dimensions[1]];
                    if (score > maxScore)
                    {
                        maxScore = score;
                        classId = c - 4;
                    }
                }

                // 过滤低分检测
                float totalScore = confidence * maxScore;
                if (totalScore < ConfidenceThreshold) continue;

                // 解析坐标 (中心x, 中心y, 宽度, 高度)
                float x = outputData[0 + i * output.Dimensions[1]];
                float y = outputData[1 + i * output.Dimensions[1]];
                float w = outputData[2 + i * output.Dimensions[1]];
                float h = outputData[3 + i * output.Dimensions[1]];

                // 转换到原始图像坐标
                x = (x - pad.left) / ratio;
                y = (y - pad.top) / ratio;
                w /= ratio;
                h /= ratio;

                // 转换为中心点坐标到角点坐标
                boxes.Add(new BoundingBox
                {
                    Label = Labels[classId],
                    Confidence = totalScore,
                    Rect = new Rectangle(
                        (int)(x - w / 2),
                        (int)(y - h / 2),
                        (int)w,
                        (int)h)
                });
            }

            // 应用非极大值抑制
            return ApplyNMS(boxes);
        }

        private List<BoundingBox> ApplyNMS(List<BoundingBox> boxes)
        {
            var result = new List<BoundingBox>();
            var ordered = boxes.OrderByDescending(b => b.Confidence).ToList();

            while (ordered.Count > 0)
            {
                var current = ordered[0];
                result.Add(current);

                // 移除与当前框IOU超过阈值的框
                ordered.RemoveAll(b => CalculateIOU(current.Rect, b.Rect) > NmsThreshold);
                if (ordered.Count > 0) ordered.RemoveAt(0);
            }

            return result;
        }
        private float CalculateIOU(Rectangle a, Rectangle b)
        {
            int areaA = a.Width * a.Height;
            int areaB = b.Width * b.Height;

            int x1 = Math.Max(a.Left, b.Left);
            int y1 = Math.Max(a.Top, b.Top);
            int x2 = Math.Min(a.Right, b.Right);
            int y2 = Math.Min(a.Bottom, b.Bottom);

            if (x2 < x1 || y2 < y1) return 0;

            int intersection = (x2 - x1) * (y2 - y1);
            return (float)intersection / (areaA + areaB - intersection);
        }

        private Bitmap PostProcessFrame(Bitmap frame, List<BoundingBox> boxes)
        {
            using (var g = Graphics.FromImage(frame))
            {
                foreach (var box in boxes)
                {
                    // 绘制边界框
                    using (var pen = new Pen(Color.Red, 2))
                    {
                        g.DrawRectangle(pen, box.Rect);
                    }

                    // 绘制标签
                    string label = $"{box.Label} {box.Confidence:0.00}";
                    var size = g.MeasureString(label, SystemFonts.DefaultFont);
                    g.FillRectangle(Brushes.Red,
                        new RectangleF(box.Rect.Left, box.Rect.Top - size.Height,
                        size.Width, size.Height));
                    g.DrawString(label, SystemFonts.DefaultFont, Brushes.White,
                        box.Rect.Left, box.Rect.Top - size.Height);
                }
            }
            return frame;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _session?.Dispose();
            _disposed = true;
        }
        // 保存处理后的图像
        public void saveProceed(Bitmap detectionResult)
        {
            try
            {
                if (detectionResult == null) return;
                // 获取唯一文件路径
                var savePath = GetUniqueFilePath();
                // 确保目录存在
                EnsureDirectoryExists(savePath);
                detectionResult.Save(savePath, ImageFormat.Jpeg);
                ProceedCompleted?.Invoke(this, savePath);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"处理文件保存失败: {ex.Message}");
            }
        }
        // 获取唯一文件路径
        private static string GetUniqueFilePath()
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "output", $"Capture_{timestamp}.jpg");
        }
        // 确保目录存在
        private static void EnsureDirectoryExists(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            Directory.CreateDirectory(directory);
        }
        // 错误处理
        private void OnErrorOccurred(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }
    }

    public class DetectionResult
    {
        public Bitmap ProcessedImage { get; set; }     // 带标注的图像
        public List<BoundingBox> Detections { get; set; } // 所有检测结果
    }

    public class BoundingBox
    {
        public string Label { get; set; }     // 类别标签（如 "car"）
        public float Confidence { get; set; } // 置信度（0-1）
        public Rectangle Rect { get; set; }   // 检测框位置和大小
    }
}
