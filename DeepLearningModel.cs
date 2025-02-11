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
        private const float ConfidenceThreshold = 0.8f;
        private const float NmsThreshold = 0.8f;
        private static readonly string[] Labels = LoadLabels(); // COCO数据集标签

        // 修改为动态获取的标签列表
        private List<string> _labels = new List<string>();
        private int _numClasses;

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
                //ModelLoaded?.Invoke(this, $"成功加载模型：{modelPath}");

                // 获取模型输出维度信息
                var outputName = _session.OutputNames[0];
                var outputShape = _session.OutputMetadata[outputName].Dimensions;
                _numClasses = outputShape[1] - 4; // 4代表xywh

                // 尝试从模型元数据获取标签
                if (_session.ModelMetadata?.CustomMetadataMap.TryGetValue("names", out var namesStr) ?? false)
                {
                    _labels = namesStr.Split(',').ToList();
                    if (_labels.Count != _numClasses)
                    {
                        ErrorOccurred?.Invoke(this, $"元数据标签数量不匹配，使用默认标签");
                        GenerateDefaultLabels();
                    }
                }
                else
                {
                    GenerateDefaultLabels();
                    ErrorOccurred?.Invoke(this, $"未找到标签元数据，使用默认标签");
                }

                ModelLoaded?.Invoke(this, $"成功加载模型：{modelPath}，检测类别数：{_numClasses}");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"模型加载失败：{ex.Message}");
            }
        }

        private void GenerateDefaultLabels()
        {
            _labels = Enumerable.Range(0, _numClasses)
                .Select(i => $"Class_{i + 1}")
                .ToList();
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

            int dimensionsPerDetection = output.Dimensions[1];
            int numDetections = output.Dimensions[2];

            for (int i = 0; i < numDetections; i++)
            {
                int offset = i * dimensionsPerDetection;

                // 解析坐标
                float x = outputData[offset];
                float y = outputData[offset + 1];
                float w = outputData[offset + 2];
                float h = outputData[offset + 3];

                // 查找最大类别分数
                float maxScore = 0;
                int classId = -1;
                for (int c = 4; c < dimensionsPerDetection; c++)
                {
                    var score = outputData[offset + c];
                    if (score > maxScore)
                    {
                        maxScore = score;
                        classId = c - 4;
                    }
                }

                // 过滤低置信度检测
                if (maxScore < ConfidenceThreshold) continue;

                // 转换到原始坐标
                x = (x - pad.left) / ratio;
                y = (y - pad.top) / ratio;
                w /= ratio;
                h /= ratio;

                // 计算边界框坐标并限制范围
                var (x1, y1, width, height) = SanitizeCoordinates(
                    x, y, w, h,
                    origWidth, origHeight);

                boxes.Add(new BoundingBox
                {
                    Label = _labels[classId],
                    Confidence = maxScore,
                    Rect = new Rectangle(x1, y1, width, height)
                });
            }

            return ApplyNMS(boxes);
        }
        private (int x, int y, int w, int h) SanitizeCoordinates(float xCenter, float yCenter,
            float width, float height, int maxWidth, int maxHeight)
        {
            // 转换为角点坐标
            float x = xCenter - width / 2;
            float y = yCenter - height / 2;

            // 限制坐标范围
            x = Clamp(x, 0, maxWidth - 1);
            y = Clamp(y, 0, maxHeight - 1);
            width = Clamp(width, 1, maxWidth - x);
            height = Clamp(height, 1, maxHeight - y);

            return (
                (int)Math.Round(x),
                (int)Math.Round(y),
                (int)Math.Round(width),
                (int)Math.Round(height)
            );
        }

        private float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        // 改进的NMS方法
        private List<BoundingBox> ApplyNMS(List<BoundingBox> boxes)
        {
            var result = new List<BoundingBox>();
            var ordered = boxes.OrderByDescending(b => b.Confidence).ToList();

            while (ordered.Count > 0)
            {
                // 取出当前最高置信度的检测结果
                var current = ordered[0];
                result.Add(current);
                ordered.RemoveAt(0);

                // 计算与剩余检测结果的IOU并过滤
                ordered.RemoveAll(b => CalculateIOU(current.Rect, b.Rect) > NmsThreshold);
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
