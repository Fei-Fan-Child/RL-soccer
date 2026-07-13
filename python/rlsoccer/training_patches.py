"""
训练优化补丁
============
在训练启动时通过 monkey-patch 注入以下优化:

1. 梯度裁剪 (Gradient Clipping)
   - 防止训练后期 loss 爆炸
   - max_norm=0.5 对 Actor 和 Critic 分别裁剪

2. 奖励归一化 (Reward Normalization)
   - 用 running mean/std 归一化 returns
   - 消除不同 reward signal 之间的量级差异
   - 提升训练稳定性

用法:
  from rlsoccer.training_patches import apply_all_patches
  apply_all_patches()  # 在 run_training() 之前调用
"""

from typing import Dict, Optional
from collections import defaultdict

import numpy as np
from mlagents_envs import logging_util

logger = logging_util.get_logger(__name__)

_PATCHES_APPLIED = False


# ============================================================
# 1. 梯度裁剪补丁
# ============================================================
def _patch_gradient_clipping(max_norm: float = 0.5):
    """
    在 TorchPOCAOptimizer.update() 的 loss.backward() 之后、
    optimizer.step() 之前插入梯度裁剪。
    """
    from mlagents.trainers.poca.optimizer_torch import TorchPOCAOptimizer

    original_update = TorchPOCAOptimizer.update

    def patched_update(self, batch, num_sequences):
        result = original_update(self, batch, num_sequences)

        # 裁剪 actor 梯度 (self.policy.actor 是 SimpleActor)
        if hasattr(self.policy, 'actor'):
            torch_params = list(self.policy.actor.parameters())
            self._clip_gradients(torch_params, max_norm=max_norm)
        # 裁剪 critic 梯度
        if hasattr(self, '_critic'):
            torch_params = list(self._critic.parameters())
            self._clip_gradients(torch_params, max_norm=max_norm)

        return result

    # 为 TorchPOCAOptimizer 添加 _clip_gradients 方法
    def _clip_gradients(self, params, max_norm=0.5):
        import torch
        total_norm = torch.nn.utils.clip_grad_norm_(params, max_norm)
        return total_norm

    TorchPOCAOptimizer._clip_gradients = _clip_gradients
    TorchPOCAOptimizer.update = patched_update

    logger.info(f"✅ 梯度裁剪补丁已应用 (max_norm={max_norm})")


# ============================================================
# 2. 奖励归一化补丁
# ============================================================
class RunningMeanStd:
    """追踪 running mean/std"""

    def __init__(self, momentum=0.01):
        self.mean = 0.0
        self.var = 1.0
        self.count = 0
        self.momentum = momentum

    def update(self, x: np.ndarray):
        batch_mean = np.mean(x)
        batch_var = np.var(x)
        batch_count = x.size

        if self.count == 0:
            self.mean = batch_mean
            self.var = batch_var
        else:
            delta = batch_mean - self.mean
            self.mean += self.momentum * delta
            self.var = (1 - self.momentum) * self.var + self.momentum * batch_var

        self.count += batch_count

    def normalize(self, x: np.ndarray) -> np.ndarray:
        return (x - self.mean) / (np.sqrt(self.var) + 1e-8)


def _patch_reward_normalization(momentum: float = 0.01):
    """
    在 _process_trajectory 中，对 returns 进行归一化。
    维护 running stats，不改变 reward signal 的数量。
    """
    from mlagents.trainers.poca.trainer import POCATrainer

    original_process = POCATrainer._process_trajectory
    # 全局 running stats, 按 reward signal name 分
    _reward_stats: Dict[str, RunningMeanStd] = defaultdict(
        lambda: RunningMeanStd(momentum=momentum)
    )

    def patched_process_trajectory(self, trajectory):
        # 先调用原始处理
        original_process(self, trajectory)

        # 对 buffer 中的 returns 做归一化
        agent_buffer = trajectory.to_agentbuffer()
        for name in self.optimizer.reward_signals:
            # name 可能是 RewardSignal enum 或直接是 str（取决于 ML-Agents 版本）
            name_str = name.value if hasattr(name, 'value') else str(name)
            returns_key = f"RewardSignal/{name_str}/Returns"
            if returns_key in agent_buffer:
                returns = np.array(agent_buffer[returns_key].get_batch(), dtype=np.float32).flatten()
                _reward_stats[name_str].update(returns)
                normalized = _reward_stats[name_str].normalize(returns)
                agent_buffer[returns_key].set(normalized.tolist())

    POCATrainer._process_trajectory = patched_process_trajectory
    logger.info(f"✅ 奖励归一化补丁已应用 (momentum={momentum})")


# ============================================================
# 批量应用
# ============================================================
def apply_all_patches(
    gradient_clip: bool = True,
    gradient_max_norm: float = 0.5,
    reward_norm: bool = True,
    reward_norm_momentum: float = 0.01,
):
    """
    应用所有训练优化补丁。

    Args:
        gradient_clip: 启用梯度裁剪
        gradient_max_norm: 梯度裁剪的 max_norm
        reward_norm: 启用奖励归一化
        reward_norm_momentum: running stats 的指数移动平均系数
    """
    global _PATCHES_APPLIED
    if _PATCHES_APPLIED:
        logger.warning("补丁已应用, 跳过重复调用")
        return

    if gradient_clip:
        _patch_gradient_clipping(max_norm=gradient_max_norm)

    if reward_norm:
        _patch_reward_normalization(momentum=reward_norm_momentum)

    _PATCHES_APPLIED = True
    logger.info("🎯 所有训练优化补丁已加载")
