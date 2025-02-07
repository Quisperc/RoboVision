# convert_to_onnx.py
import torch
import argparse

def convert(pt_model_path, onnx_output_path):
    # 加载 pt 模型（请根据实际情况加载模型）
    model = torch.load(pt_model_path, map_location=torch.device('0'))
    model.eval()

    # 定义一个虚拟输入（这里假设模型输入为 [1, 3, 640, 640]，根据实际情况修改）
    dummy_input = torch.randn(1, 3, 640, 640)

    # 导出 ONNX 模型
    torch.onnx.export(model, dummy_input, onnx_output_path,
                      input_names=['images'],  # 根据实际模型输入名称修改
                      output_names=['output'], # 根据实际模型输出名称修改
                      opset_version=11)
    print("模型转换成功，ONNX 模型保存至：" + onnx_output_path)

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--pt_model', type=str, required=True, help='pt 模型文件路径')
    parser.add_argument('--onnx_output', type=str, required=True, help='ONNX 模型保存路径')
    args = parser.parse_args()
    convert(args.pt_model, args.onnx_output)
