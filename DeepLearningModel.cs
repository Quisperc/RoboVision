// ======================== 深度学习模块 DeepLearningModule.cs ========================
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        private const float ConfidenceThreshold = 0.55f;
        private const float NmsThreshold = 0.45f;
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
                // 释放之前的会话资源
                if (_session != null)
                {
                    _session.Dispose();
                    _session = null;
                }

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
                // 关闭并释放会话选项资源
                sessionOptions?.Dispose();
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
                Bitmap resizedBitmap = null;
                DetectionResult detectionResult = null;
                Bitmap processedImage = null;
                Bitmap frameClone = null;

                try
                {
                    // 克隆输入帧，避免修改原始对象
                    frameClone = (Bitmap)frame.Clone();
                    
                    // 预处理并获取输入张量
                    var inputTensor = PreprocessFrame(frameClone, out ratio, out pad, out resizedBitmap);
                    
                    // 创建输入列表
                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor("images", inputTensor)
                    };

                    // 执行模型推理
                    using (var results = _session.Run(inputs))
                    {
                        // 获取输出并解析
                        var output = results.First().AsTensor<float>();
                        var detections = ParseOutput(output, ratio, pad, frameClone.Width, frameClone.Height);

                        // 通知结果
                        InferenceCompleted?.Invoke(this, $"检测到 {detections.Count} 个目标");
                        
                        // 后处理图像
                        processedImage = PostProcessFrame(frameClone, detections);
                        
                        // 创建结果对象
                        detectionResult = new DetectionResult
                        {
                            ProcessedImage = processedImage,
                            Detections = detections
                        };
                        
                        // 所有权转移，防止提前释放
                        processedImage = null;
                        
                        // 保存处理结果
                        SaveProcessedImage(detectionResult.ProcessedImage);
                    }

                    return detectionResult;
                }
                catch (Exception ex)
                {
                    processedImage?.Dispose();
                    throw new Exception($"处理图像失败: {ex.Message}", ex);
                }
                finally
                {
                    // 确保中间资源被释放
                    resizedBitmap?.Dispose();
                    if (frameClone != null && frameClone != processedImage && 
                        (detectionResult == null || frameClone != detectionResult.ProcessedImage))
                    {
                        frameClone.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"推理失败：{ex.Message}");
                return null;
            }
        }

        // 预处理图像，创建适合模型输入的张量
        private Tensor<float> PreprocessFrame(Bitmap frame, out float ratio, out (int top, int left) pad, out Bitmap resized)
        {
            // 计算缩放比例
            ratio = Math.Min((float)TargetSize / frame.Width, (float)TargetSize / frame.Height);
            var newWidth = (int)(frame.Width * ratio);
            var newHeight = (int)(frame.Height * ratio);
            // 使用+1修正边界问题，保证不会出现负数，非对称填充
            pad = ((TargetSize - newHeight + 1) / 2, (TargetSize - newWidth + 1) / 2);

            // 创建Letterbox图像
            resized = new Bitmap(TargetSize, TargetSize);
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; // 高质量插值
                g.Clear(Color.FromArgb(114, 114, 114)); // YOLO标准填充色
                g.DrawImage(frame, pad.left, pad.top, newWidth, newHeight);
            }

            // 创建输入张量 [1,3,640,640]
            var inputTensor = new DenseTensor<float>(new[] { 1, 3, TargetSize, TargetSize });

            // 使用LockBits快速访问像素
            var bitmapData = resized.LockBits(new Rectangle(0, 0, resized.Width, resized.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

            try
            {
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
            }
            finally
            {
                // 确保位图数据解锁
                resized.UnlockBits(bitmapData);
            }
            
            return inputTensor;
        }
        private List<BoundingBox> ParseOutput(Tensor<float> output, float ratio, (int top, int left) pad,
                                int origWidth, int origHeight)
        {
            var boxes = new List<BoundingBox>();

            // 修复维度处理
            var reshaped = output.Reshape(new[] { 1, output.Dimensions[1], output.Dimensions[2] });
            int numAnchors = reshaped.Dimensions[2];
            int numClasses = reshaped.Dimensions[1] - 4;

            for (int i = 0; i < numAnchors; i++)
            {
                float xCenter = reshaped[0, 0, i];
                float yCenter = reshaped[0, 1, i];
                float width = reshaped[0, 2, i];
                float height = reshaped[0, 3, i];

                // 跳过无效预测
                if (width <= 0 || height <= 0) continue;

                // 转换到原始图像坐标
                float xMin = (xCenter - width / 2 - pad.left) / ratio;
                float yMin = (yCenter - height / 2 - pad.top) / ratio;
                float xMax = (xCenter + width / 2 - pad.left) / ratio;
                float yMax = (yCenter + height / 2 - pad.top) / ratio;

                // 限制坐标范围
                xMin = Clamp(xMin, 0, origWidth);
                yMin = Clamp(yMin, 0, origHeight);
                xMax = Clamp(xMax, 0, origWidth);
                yMax = Clamp(yMax, 0, origHeight);

                // 获取类别分数
                float maxScore = 0;
                int classId = -1;
                for (int c = 0; c < numClasses; c++)
                {
                    float score = 1.0f / (1.0f + (float)Math.Exp(-reshaped[0, 4 + c, i]));
                    if (score > maxScore && score > ConfidenceThreshold)
                    {
                        maxScore = score;
                        classId = c;
                    }
                }

                if (classId == -1) continue;

                boxes.Add(new BoundingBox
                {
                    Label = _labels[classId],
                    Confidence = maxScore,
                    Rect = new Rectangle(
                        (int)xMin, (int)yMin,
                        (int)(xMax - xMin),
                        (int)(yMax - yMin))
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
            //width = Clamp(width, 1, maxWidth - x);
            // 当maxWidth - x可能为负数时会导致异常，增加保护性判断
            width = Clamp(width, 1, Math.Max(0, maxWidth - x));
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

        // 改进的NMS方法：计算IOU时使用的是整数坐标的Rectangle，可能导致精度丢失，使用浮点数计算IOU：
        private List<BoundingBox> ApplyNMS(List<BoundingBox> boxes)
        {
            var result = new List<BoundingBox>();
            var ordered = boxes.OrderByDescending(b => b.Confidence).ToList();

            // 按类别分组处理
            var classGroups = ordered.GroupBy(b => b.Label);

            foreach (var group in classGroups)
            {
                var classBoxes = group.ToList();
                while (classBoxes.Count > 0)
                {
                    var current = classBoxes[0];
                    result.Add(current);
                    classBoxes.RemoveAt(0);

                    classBoxes.RemoveAll(b => CalculateIOU(current.Rect, b.Rect) > NmsThreshold);
                }
            }

            return result;
        }

        private float CalculateIOU(RectangleF a, RectangleF b)
        {
            float areaA = a.Width * a.Height;
            float areaB = b.Width * b.Height;

            float x1 = Math.Max(a.Left, b.Left);
            float y1 = Math.Max(a.Top, b.Top);
            float x2 = Math.Min(a.Right, b.Right);
            float y2 = Math.Min(a.Bottom, b.Bottom);

            if (x2 < x1 || y2 < y1) return 0;

            float intersection = (x2 - x1) * (y2 - y1);
            return intersection / (areaA + areaB - intersection);
        }

        private Bitmap PostProcessFrame(Bitmap frame, List<BoundingBox> boxes)
        {
            // 确保使用高质量的绘图
            using (var g = Graphics.FromImage(frame))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                
                foreach (var box in boxes)
                {
                    // 为每个类别分配不同颜色
                    Color boxColor = GetColorForClass(box.Label);
                    using (var pen = new Pen(boxColor, 2))
                    using (var brush = new SolidBrush(Color.FromArgb(128, boxColor)))
                    using (var textBrush = new SolidBrush(Color.White))
                    using (var textFont = new Font("Arial", 10, FontStyle.Bold))
                    {
                        // 画框
                        g.DrawRectangle(pen, box.Rect);
                        
                        // 绘制标签背景
                        string text = $"{box.Label} {box.Confidence:P1}";
                        SizeF textSize = g.MeasureString(text, textFont);
                        g.FillRectangle(brush, box.Rect.X, box.Rect.Y - textSize.Height, textSize.Width, textSize.Height);
                        
                        // 绘制标签文本
                        g.DrawString(text, textFont, textBrush, box.Rect.X, box.Rect.Y - textSize.Height);
                    }
                }
            }
            return frame;
        }

        // 为不同类别分配不同颜色
        private Color GetColorForClass(string label)
        {
            // 根据标签名称生成一个稳定的哈希值作为颜色索引
            int hash = label.GetHashCode();
            // 使用固定的颜色表
            Color[] colors = new Color[] 
            {
                Color.Red, Color.Green, Color.Blue, Color.Yellow, Color.Cyan, 
                Color.Magenta, Color.Orange, Color.Purple, Color.Lime, Color.Brown
            };
            
            int index = Math.Abs(hash) % colors.Length;
            return colors[index];
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托管资源
                    _session?.Dispose();
                }
                
                // 释放非托管资源
                _session = null;
                _disposed = true;
            }
        }

        public void SaveProcessedImage(Bitmap detectionResult)
        {
            if (detectionResult == null) return;

            try
            {
                string filePath = GetUniqueFilePath();
                EnsureDirectoryExists(filePath);
                
                // 使用更高质量的JPEG编码
                using (var encoderParams = new EncoderParameters(1))
                using (var qualityParam = new EncoderParameter(Encoder.Quality, 95L))
                {
                    encoderParams.Param[0] = qualityParam;
                    ImageCodecInfo jpegEncoder = GetEncoder(ImageFormat.Jpeg);
                    detectionResult.Save(filePath, jpegEncoder, encoderParams);
                }
                
                ProceedCompleted?.Invoke(this, filePath);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"保存处理后图像失败：{ex.Message}");
            }
        }

        // 获取指定格式的编码器
        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
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

        ~DeepLearningModel()
        {
            Dispose(false);
        }

        // 资源回收方法，手动触发
        public void CleanupResources()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
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

