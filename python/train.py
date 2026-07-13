#!/usr/bin/env python3
"""
============================================================================
  RL-Soccer POCA 训练启动器 v2.0
  SideChannel + StatsWriter 正确集成 (单进程版)
============================================================================

关键修复: 不使用 subprocess。直接调用 ML-Agents 的 run_training() API,
通过 monkey-patch 将 SoccerStepChannel 注入到环境创建流程中,
确保 SideChannel 与训练循环在同一进程中运行。

用法:
  pip install -e .                     # 安装 rlsoccer 包 (首次)
  python train.py --run-id soccer-v1   # 启动训练
============================================================================
"""

import argparse
import os
import sys
from pathlib import Path
from typing import List, Callable, Optional

# ── POCA 注册 (entry_points 在 dev install 下可能不生效) ──
os.environ.setdefault("PROTOCOL_BUFFERS_PYTHON_IMPLEMENTATION", "python")
from mlagents.plugins.trainer_type import get_default_trainer_types
get_default_trainer_types()

# ── 路径设置 ──
PROJECT_ROOT = Path(__file__).resolve().parents[1]
ML_AGENTS_ROOT = PROJECT_ROOT.parent / "ml-agents"
PYTHON_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(PYTHON_DIR))
sys.path.insert(0, str(ML_AGENTS_ROOT))
sys.path.insert(0, str(ML_AGENTS_ROOT / "ml-agents"))
sys.path.insert(0, str(ML_AGENTS_ROOT / "ml-agents-envs"))

from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel import SideChannel
from mlagents.trainers.learn import run_training
from mlagents.trainers.settings import RunOptions
from mlagents.trainers.cli_utils import load_config


def resolve_config(name: str) -> Path:
    """查找 YAML 配置文件"""
    p = Path(name)
    if p.is_absolute() and p.exists():
        return p
    for base in [PYTHON_DIR / "config", PROJECT_ROOT / "config" / "poca"]:
        candidate = base / name
        if candidate.exists():
            return candidate
    raise FileNotFoundError(f"找不到配置文件: {name}")


def parse_args():
    p = argparse.ArgumentParser(
        description="RL-Soccer POCA 训练启动器 v2.0 (单进程)",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
示例:
  pip install -e .                        # 首次: 安装 rlsoccer 包
  python train.py --run-id soccer-v1      # 启动训练
  python train.py --run-id soccer-v2 --num-envs 4
  python train.py --run-id soccer-v1 --resume
  python train.py --run-id soccer-v1 --inference
        """,
    )
    p.add_argument("--run-id", required=True)
    p.add_argument("--config", default="poca_soccer.yaml")
    p.add_argument("--num-envs", type=int, default=1)
    p.add_argument("--base-port", type=int, default=5005)
    p.add_argument("--resume", action="store_true")
    p.add_argument("--inference", action="store_true")
    p.add_argument("--force", action="store_true")
    p.add_argument("--no-graphics", action="store_true")
    p.add_argument("--env-args", default=None)
    p.add_argument("--timeout-wait", type=int, default=60)
    return p.parse_args()


def _inject_side_channel_into_env_factory():
    """
    Monkey-patch: 将 SoccerStepChannel 注入到 ML-Agents 的环境创建流程中。
    必须在 run_training() 之前调用。

    原理:
      create_environment_factory() 返回一个 factory(worker_id, side_channels) -> UnityEnvironment。
      我们在 factory 外面包一层, 在调用时把 SoccerStepChannel 追加到 side_channels 列表中,
      这样 Unity 连接后自动启用步级数据通道。
    """
    import mlagents.trainers.learn as learn_module

    _original = learn_module.create_environment_factory

    def patched_create_env_factory(
        env_path: Optional[str],
        no_graphics: bool,
        no_graphics_monitor: bool,
        seed: int,
        num_areas: int,
        timeout_wait: int,
        start_port: Optional[int],
        env_args: Optional[List[str]],
        log_folder: str,
    ) -> Callable[[int, List[SideChannel]], UnityEnvironment]:

        base_factory = _original(
            env_path, no_graphics, no_graphics_monitor,
            seed, num_areas, timeout_wait, start_port, env_args, log_folder,
        )

        def wrapped_factory(
            worker_id: int, side_channels: List[SideChannel]
        ) -> UnityEnvironment:
            # 注入我们的 SideChannel
            from rlsoccer.side_channel import get_soccer_step_channel
            channel = get_soccer_step_channel()
            new_channels = list(side_channels)
            if channel not in new_channels:
                new_channels.append(channel)
            return base_factory(worker_id, new_channels)

        return wrapped_factory

    learn_module.create_environment_factory = patched_create_env_factory
    print("🔗 SideChannel 注入成功 (SoccerStepChannel → Unity gRPC)")


def build_run_options(args, config_path: Path) -> RunOptions:
    """从 YAML + CLI 参数构建 RunOptions"""
    # 显式指定 UTF-8 编码，修复 Windows GBK 环境下 load_config 的 UnicodeDecodeError
    import yaml
    with open(config_path, 'r', encoding='utf-8') as f:
        config_dict = yaml.safe_load(f)
    argv = [
        str(config_path),
        f"--run-id={args.run_id}",
        f"--num-envs={args.num_envs}",
        f"--base-port={args.base_port}",
        f"--timeout-wait={args.timeout_wait}",
    ]
    if args.resume:
        argv.append("--resume")
    if args.inference:
        argv.append("--inference")
    if args.force:
        argv.append("--force")
    if args.no_graphics:
        argv.append("--no-graphics")
    if args.env_args:
        argv.append(f"--env-args={args.env_args}")

    # 使用 ML-Agents 的标准参数解析
    from mlagents.trainers.cli_utils import parser as mlagents_parser
    parsed = mlagents_parser.parse_args(argv)
    return RunOptions.from_argparse(parsed)


def main():
    # 修复 Windows 控制台 GBK 编码无法输出 emoji 的问题
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

    args = parse_args()
    config_path = resolve_config(args.config)

    print(f"📄 配置文件: {config_path}")
    redis_host = os.environ.get("REDIS_HOST", "localhost")
    redis_port = os.environ.get("REDIS_PORT", "6379")

    print(f"""
╔══════════════════════════════════════════════════════════════╗
║         ⚽  RL-Soccer POCA 训练 v2.0  ⚽                     ║
╠══════════════════════════════════════════════════════════════╣
║  Run ID:     {args.run_id:<46s}║
║  Config:     {config_path.name:<46s}║
║  Envs:       {args.num_envs:<46d}║
║  Redis:      {redis_host}:{redis_port:<41s}║
║  SideChannel 步级日志: ✅ 注入式                               ║
║  StatsWriter 指标日志: ✅ entry_points 自动注册                ║
╠══════════════════════════════════════════════════════════════╣
║  监控:                                                      ║
║    TensorBoard → http://localhost:6006                       ║
║    Redis CLI   → redis-cli KEYS soccer:*                    ║
║    Redis Bridge→ python redis/redis_bridge.py --port 8000   ║
╚══════════════════════════════════════════════════════════════╝
""")

    # ── 1. 注入 SideChannel ──
    _inject_side_channel_into_env_factory()

    # ── 2. 加载训练优化补丁 (梯度裁剪 + 奖励归一化) ──
    try:
        from rlsoccer.training_patches import apply_all_patches
        apply_all_patches()
    except Exception as e:
        print(f"⚠️  训练补丁加载失败 (不影响训练): {e}")

    # ── 3. 构建 RunOptions ──
    options = build_run_options(args, config_path)

    # ── 3. 启动训练 (单进程, SideChannel + StatsWriter 均在此进程内) ──
    print(f"\n⚠️  请在 Unity Editor 中点击 ▶ Play (确保挂载 SoccerSideChannelRegistrar.cs)")
    print(f"   TensorBoard: tensorboard --logdir results --port 6006\n")

    # run_training 是阻塞调用, 直到训练结束或 Ctrl+C
    run_training(run_seed=42, options=options, num_areas=1)


if __name__ == "__main__":
    main()
