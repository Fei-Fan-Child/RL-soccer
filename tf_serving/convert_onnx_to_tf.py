#!/usr/bin/env python3
"""
============================================================================
  ONNX → TensorFlow SavedModel 转换工具
  参照 no20.txt 实训文档 — 第4步: 模型导出与 TF Serving 部署
============================================================================

将 ML-Agents 训练出的 ONNX 模型转换为 TensorFlow SavedModel 格式，
以便通过 TensorFlow Serving (gRPC) 提供推理服务。

用法:
  # 转换 2v2 模型
  python convert_onnx_to_tf.py \
      --onnx ../Assets/ML-Agents/Examples/Soccer/TFModels/SoccerTwos.onnx \
      --output ./models/soccer_twos

  # 转换 Striker 模型
  python convert_onnx_to_tf.py \
      --onnx ../Assets/ML-Agents/Examples/Soccer/TFModels/Striker.onnx \
      --output ./models/striker/1

  # 指定输入/输出名称
  python convert_onnx_to_tf.py \
      --onnx model.onnx \
      --output ./models/soccer/1 \
      --inputs obs_0 \
      --outputs discrete_actions,value
============================================================================
"""

import argparse
import os
import sys
from pathlib import Path
import numpy as np

# ============================================================
# 依赖检查
# ============================================================
REQUIRED_PACKAGES = {
    "onnx": "onnx",
    "onnxruntime": "onnxruntime",
    "tf2onnx": "tf2onnx",
    "tensorflow": "tensorflow",
}

missing = []
for module_name, pip_name in REQUIRED_PACKAGES.items():
    try:
        __import__(module_name)
    except ImportError:
        missing.append(pip_name)

if missing:
    print(f"❌ 缺少依赖: {', '.join(missing)}")
    print(f"   安装: pip install {' '.join(missing)}")
    # 非强制退出，因为主要转换不依赖所有包
    # sys.exit(1)


# ============================================================
# 方法 1: ONNX → TF SavedModel (通过 onnx-tf)
# ============================================================
def convert_onnx_to_tf_via_onnx_tf(onnx_path: str, output_dir: str) -> bool:
    """
    使用 onnx-tf 将 ONNX 转为 TensorFlow SavedModel。
    这是最可靠的方法，完整保留模型结构。
    """
    try:
        import onnx
        from onnx_tf.backend import prepare
        import tensorflow as tf

        print(f"📥 加载 ONNX 模型: {onnx_path}")
        onnx_model = onnx.load(onnx_path)
        onnx.checker.check_model(onnx_model)
        print(f"✅ ONNX 模型验证通过")

        print(f"🔄 转换为 TensorFlow...")
        tf_rep = prepare(onnx_model)
        tf_rep.export_graph(output_dir)
        print(f"✅ SavedModel 已导出到: {output_dir}")
        return True

    except ImportError:
        print("⚠️  onnx-tf 未安装，尝试备用方法...")
        return False
    except Exception as e:
        print(f"⚠️  onnx-tf 转换失败: {e}")
        return False


# ============================================================
# 方法 2: ONNX → TF SavedModel (通过 onnx2tf)
# ============================================================
def convert_onnx_to_tf_via_onnx2tf(onnx_path: str, output_dir: str) -> bool:
    """
    使用 onnx2tf 将 ONNX 转换为 TF SavedModel。
    onnx2tf 是更新的工具，兼容性更好。
    """
    try:
        import subprocess

        cmd = [
            "onnx2tf",
            "-i", onnx_path,
            "-o", output_dir,
            "-osd",  # output signature defs
        ]
        print(f"🔧 运行: {' '.join(cmd)}")
        result = subprocess.run(cmd, capture_output=True, text=True)

        if result.returncode == 0:
            print(f"✅ SavedModel 已导出到: {output_dir}")
            return True
        else:
            print(f"❌ onnx2tf 失败:\n{result.stderr}")
            return False

    except FileNotFoundError:
        print("⚠️  onnx2tf 未安装 (pip install onnx2tf)")
        return False


# ============================================================
# 方法 3: ONNX → TF via PyTorch → ONNX → tf2onnx
# ============================================================
def convert_pytorch_to_tf(
    model_path: str, output_dir: str, obs_dim: int = 336
) -> bool:
    """
    从自定义 PyTorch 模型导出到 TF SavedModel。
    链路: PyTorch → ONNX → TF
    """
    try:
        sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "python"))
        from networks.custom_poca import SoccerRLModel
        import torch

        print(f"📥 加载 PyTorch 模型...")
        model = SoccerRLModel(obs_dim=obs_dim)
        if os.path.exists(model_path):
            model.load_state_dict(torch.load(model_path, map_location="cpu"))
        model.eval()

        # Step 1: PyTorch → ONNX
        onnx_path = os.path.join(output_dir, "model.onnx")
        dummy_input = torch.randn(1, obs_dim)
        torch.onnx.export(
            model, dummy_input, onnx_path,
            input_names=["obs"],
            output_names=["discrete_branch_0", "discrete_branch_1",
                          "discrete_branch_2", "value"],
            dynamic_axes={"obs": {0: "batch"}},
            opset_version=11,
        )
        print(f"✅ PyTorch → ONNX: {onnx_path}")

        # Step 2: ONNX → TF SavedModel
        import onnx
        from onnx_tf.backend import prepare
        onnx_model = onnx.load(onnx_path)
        tf_rep = prepare(onnx_model)
        tf_rep.export_graph(output_dir)
        print(f"✅ ONNX → TF SavedModel: {output_dir}")
        return True

    except Exception as e:
        print(f"❌ PyTorch→TF 转换失败: {e}")
        return False


# ============================================================
# 验证输出的 SavedModel
# ============================================================
def verify_saved_model(output_dir: str) -> bool:
    """验证 SavedModel 是否可以正常加载和推理"""
    try:
        import tensorflow as tf

        print(f"🔍 验证 SavedModel: {output_dir}")
        loaded = tf.saved_model.load(output_dir)

        # 打印签名信息
        infer = loaded.signatures["serving_default"]
        print(f"   输入签名: {infer.structured_input_signature}")
        print(f"   输出签名: {infer.structured_output_signature}")
        print(f"✅ SavedModel 验证通过")
        return True

    except Exception as e:
        print(f"❌ SavedModel 验证失败: {e}")
        return False


# ============================================================
# 生成模型配置
# ============================================================
def generate_model_config(model_name: str, output_dir: str):
    """生成 TF Serving 的 models.config"""
    config = f"""
model_config_list {{
  config {{
    name: '{model_name}'
    base_path: '/models/{model_name}'
    model_platform: 'tensorflow'
    model_version_policy {{
      latest {{
        num_versions: 1
      }}
    }}
  }}
}}
"""
    config_path = os.path.join(output_dir, "models.config")
    with open(config_path, "w") as f:
        f.write(config.strip())
    print(f"📝 模型配置已生成: {config_path}")


# ============================================================
# CLI 入口
# ============================================================
def main():
    parser = argparse.ArgumentParser(
        description="ONNX/PyTorch → TensorFlow SavedModel 转换工具"
    )
    parser.add_argument("--onnx", type=str, help="输入 ONNX 模型路径")
    parser.add_argument("--pytorch", type=str, help="输入 PyTorch .pt 模型路径")
    parser.add_argument("--output", type=str, required=True, help="输出目录")
    parser.add_argument("--model-name", type=str, default="soccer_rl",
                        help="模型名称 (用于 TF Serving 配置)")
    parser.add_argument("--obs-dim", type=int, default=336,
                        help="观察空间维度 (PyTorch 模式)")
    parser.add_argument("--verify", action="store_true", default=True,
                        help="验证导出的 SavedModel")

    args = parser.parse_args()

    # 确保输出目录存在
    os.makedirs(args.output, exist_ok=True)

    success = False

    # 按优先级尝试转换方法
    if args.pytorch:
        print("=" * 60)
        print("📦 PyTorch → ONNX → TF SavedModel")
        print("=" * 60)
        success = convert_pytorch_to_tf(args.pytorch, args.output, args.obs_dim)

    elif args.onnx:
        if not os.path.exists(args.onnx):
            print(f"❌ ONNX 文件不存在: {args.onnx}")
            sys.exit(1)

        print("=" * 60)
        print("📦 ONNX → TF SavedModel")
        print("=" * 60)

        # 方法 1: onnx-tf
        success = convert_onnx_to_tf_via_onnx_tf(args.onnx, args.output)

        # 方法 2: onnx2tf 作为备用
        if not success:
            success = convert_onnx_to_tf_via_onnx2tf(args.onnx, args.output)

        if not success:
            print("\n❌ 所有转换方法均失败")
            print("   请尝试:")
            print("   1. pip install onnx-tf tensorflow-probability")
            print("   2. pip install onnx2tf")
            sys.exit(1)

    else:
        print("❌ 请指定 --onnx 或 --pytorch 输入模型")
        sys.exit(1)

    # 验证
    if success and args.verify:
        verify_saved_model(args.output)

    # 生成配置
    generate_model_config(args.model_name, os.path.dirname(args.output.rstrip("/")))

    print("\n" + "=" * 60)
    print("✅ 转换完成!")
    print(f"  模型目录: {args.output}")
    print(f"  启动 TF Serving:")
    print(f"    docker compose -f tf_serving/docker-compose.yml up -d")
    print(f"  测试推理:")
    print(f'    curl -d \'{{"instances": [[...]]}}\' \\')
    print(f'      http://localhost:8501/v1/models/{args.model_name}:predict')
    print("=" * 60)


if __name__ == "__main__":
    main()
