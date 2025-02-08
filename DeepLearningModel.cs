// ======================== 深度学习模块 DeepLearningModule.cs ========================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace RoboVision
{
    public class DeepLearningModel : IDisposable
    {
        public event EventHandler<string> ModelLoaded;
        public event EventHandler<string> InferenceCompleted;
        public event EventHandler<string> ErrorOccurred;

        private InferenceSession _session;
        private bool _disposed;

        public bool IsInitialized => _session != null;

        public void LoadModel(string modelPath)
        {
            try
            {
                _session?.Dispose();

                var sessionOptions = new SessionOptions();
                try
                {
                    sessionOptions.AppendExecutionProvider_CUDA();
                }
                catch
                {
                    sessionOptions.AppendExecutionProvider_CPU();
                }

                _session = new InferenceSession(modelPath, sessionOptions);
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
                var inputTensor = PreprocessFrame(frame);
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("images", inputTensor)
                };

                var results = _session.Run(inputs);
                var output = results.First().AsTensor<float>();
                var detections = ParseOutput(output);

                InferenceCompleted?.Invoke(this, $"检测到 {detections.Count} 个目标");
                return new DetectionResult
                {
                    ProcessedImage = PostProcessFrame(frame, detections),
                    Detections = detections
                };
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"推理失败：{ex.Message}");
                return null;
            }
        }

        private Tensor<float> PreprocessFrame(Bitmap frame)
        {
            // ... 优化后的预处理逻辑（使用LockBits）...
            return new DenseTensor<float>(new[] { 1, 3, frame.Height, frame.Width }); // 示例返回值
        }

        private List<BoundingBox> ParseOutput(Tensor<float> output)
        {
            var boxes = new List<BoundingBox>();
            // ... 输出解析逻辑...
            return boxes;
        }

        private Bitmap PostProcessFrame(Bitmap frame, List<BoundingBox> boxes)
        {
            // 假设这里有一些后处理逻辑
            // 如果没有后处理逻辑，可以返回原始帧
            return frame;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _session?.Dispose();
            _disposed = true;
        }
    }

    public class DetectionResult
    {
        public Bitmap ProcessedImage { get; set; }
        public List<BoundingBox> Detections { get; set; }
    }

    public class BoundingBox
    {
        public string Label { get; set; }
        public float Confidence { get; set; }
        public Rectangle Rect { get; set; }
    }
}
