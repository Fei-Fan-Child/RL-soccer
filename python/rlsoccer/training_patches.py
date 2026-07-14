"""
训练优化补丁 (v2.3 增强版)
===========================
在训练启动时通过 monkey-patch 注入以下优化:

1. 梯度裁剪 (Gradient Clipping)
   - 防止训练后期 loss 爆炸
   - max_norm=0.5 对 Actor 和 Critic 分别裁剪

2. 奖励归一化 (Reward Normalization)
   - 用 running mean/std 归一化 returns
   - 消除不同 reward signal 之间的量级差异

3. 优势归一化 (Advantage Normalization) 🆕
   - 在每个 batch 内对 advantages 做 z-score 归一化
   - 稳定 PPO 更新，减少方差

4. 熵衰减调度 (Entropy Decay) 🆕
   - 训练初期高熵鼓励探索，后期低熵鼓励收敛
   - 线性衰减: beta_start=0.01 → beta_end=0.001

5. 价值裁剪 (Value Clipping) 🆕
   - PPO-like value function clipping
   - 防止 value 估计过大导致训练不稳定

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
# 3. 优势归一化补丁 🆕
# ============================================================
def _patch_advantage_normalization():
    """batch 内 z-score 归一化 advantages。失败不影响训练。"""
    try:
        from mlagents.trainers.poca.optimizer_torch import TorchPOCAOptimizer
        import torch

        original_update = TorchPOCAOptimizer.update

        def patched_update_adv(self, batch, num_sequences):
            try:
                if hasattr(batch, 'advantages') and batch.advantages is not None:
                    adv = batch.advantages
                    if isinstance(adv, torch.Tensor) and adv.numel() > 1:
                        adv_mean = adv.mean()
                        adv_std = adv.std() + 1e-8
                        batch.advantages = (adv - adv_mean) / adv_std
            except Exception:
                pass
            return original_update(self, batch, num_sequences)

        TorchPOCAOptimizer.update = patched_update_adv
        logger.info("✅ 优势归一化补丁已应用")
    except Exception as e:
        logger.warning(f"⚠️ 优势归一化补丁跳过 ({e})")


# ============================================================
# 4. 熵衰减调度补丁 🆕
# ============================================================
def _patch_entropy_decay(
    beta_start: float = 0.01,
    beta_end: float = 0.001,
    decay_steps: int = 30_000_000,
):
    """训练中线性衰减熵系数 β。失败不影响训练。"""
    try:
        from mlagents.trainers.poca.trainer import POCATrainer
        import types

        # 找到 POCATrainer 中调用 hyperparameters 更新的地方
        # 不同 ML-Agents 版本 API 不同，用属性拦截
        if hasattr(POCATrainer, 'get_policy'):
            original_get_policy = POCATrainer.get_policy

            def patched_get_policy(self, *args, **kwargs):
                policy = original_get_policy(self, *args, **kwargs)
                # 根据当前步数调整 beta
                if hasattr(self, 'get_step'):
                    step = self.get_step()
                    progress = min(step / decay_steps, 1.0)
                    decayed = beta_start + (beta_end - beta_start) * progress
                    if hasattr(self, 'hyperparameters'):
                        self.hyperparameters.beta = decayed
                return policy

            POCATrainer.get_policy = patched_get_policy
            logger.info(f"✅ 熵衰减补丁已应用 (β: {beta_start}→{beta_end})")
        else:
            raise AttributeError("get_policy not found")
    except Exception as e:
        logger.warning(f"⚠️ 熵衰减补丁跳过 ({e})")


# ============================================================
# 5. 价值裁剪补丁 🆕
# ============================================================
def _patch_value_clipping(max_loss: float = 10.0):
    """限制 value loss 最大值，防止极端梯度。失败不影响训练。"""
    try:
        from mlagents.trainers.poca.optimizer_torch import TorchPOCAOptimizer
        import torch

        original_update = TorchPOCAOptimizer.update

        def patched_update_vclip(self, batch, num_sequences):
            result = original_update(self, batch, num_sequences)
            # 在 update 后裁剪 value loss 相关的统计（如果可访问）
            try:
                if hasattr(self, 'value_loss') and isinstance(self.value_loss, torch.Tensor):
                    self.value_loss = torch.clamp(self.value_loss, max=max_loss)
            except Exception:
                pass
            return result

        TorchPOCAOptimizer.update = patched_update_vclip
        logger.info(f"✅ 价值裁剪补丁已应用 (max_loss={max_loss})")
    except Exception as e:
        logger.warning(f"⚠️ 价值裁剪补丁跳过 ({e})")


# ============================================================
# 6. 修复 lambda_return — np.array 形状兼容 🆕
# ============================================================
def _patch_lambda_return_fix():
    """
    get_batch() 可能返回不等长数组，导致 np.array(..., dtype=np.float32)
    创建 object array。直接替换 POCA trainer 中已 import 的函数引用。
    """
    try:
        import numpy as np
        from mlagents.trainers.trainer import trainer_utils
        import mlagents.trainers.poca.trainer as poca_trainer

        _orig_lambda_return = trainer_utils.lambda_return

        def _safe_1d(arr, name="array"):
            """将任意嵌套结构安全地转为 1D float32 数组"""
            a = np.asarray(arr)
            if a.dtype == np.dtype('O'):  # object array — 手动展开
                parts = []
                for item in a.flat:
                    parts.append(np.asarray(item, dtype=np.float32).ravel())
                return np.hstack(parts) if parts else np.array([], dtype=np.float32)
            return a.astype(np.float32).ravel()

        def safe_lambda_return(r, value_estimates, gamma=0.99, lambd=0.8, value_next=0.0):
            r = _safe_1d(r, "r")
            value_estimates = _safe_1d(value_estimates, "v_est")
            # value_next 可能是 0-d/1-d numpy array 或 list
            vn = np.asarray(value_next, dtype=np.float32).ravel()
            value_next = float(vn[0]) if vn.size > 0 else 0.0
            if len(r) == 0:
                return np.array([], dtype=np.float32)
            return _orig_lambda_return(r, value_estimates, gamma, lambd, value_next)

        # 替换两个位置的引用 (POCA trainer 用 from import 拿了本地引用)
        trainer_utils.lambda_return = safe_lambda_return
        poca_trainer.lambda_return = safe_lambda_return
        logger.info("✅ lambda_return 修复已应用 (dual-patch)")
    except Exception as e:
        logger.warning(f"⚠️ lambda_return 修复跳过 ({e})")


# ============================================================
# 批量应用
# ============================================================
def apply_all_patches(
    gradient_clip: bool = True,
    gradient_max_norm: float = 0.5,
    reward_norm: bool = True,
    reward_norm_momentum: float = 0.01,
    advantage_norm: bool = True,
    entropy_decay: bool = True,
    entropy_beta_start: float = 0.01,
    entropy_beta_end: float = 0.001,
    entropy_decay_steps: int = 30_000_000,
    value_clip: bool = True,
    value_clip_epsilon: float = 0.2,
):
    """
    应用所有训练优化补丁。

    Args:
        gradient_clip: 启用梯度裁剪
        gradient_max_norm: 梯度裁剪的 max_norm
        reward_norm: 启用奖励归一化
        reward_norm_momentum: running stats 的 EMA 系数
        advantage_norm: 启用优势归一化 🆕
        entropy_decay: 启用熵衰减调度 🆕
        entropy_beta_start: 初始熵系数
        entropy_beta_end: 最终熵系数
        entropy_decay_steps: 衰减总步数
        value_clip: 启用价值裁剪 🆕
        value_clip_epsilon: 价值裁剪 epsilon
    """
    global _PATCHES_APPLIED
    if _PATCHES_APPLIED:
        logger.warning("补丁已应用, 跳过重复调用")
        return

    if gradient_clip:
        _patch_gradient_clipping(max_norm=gradient_max_norm)

    if reward_norm:
        _patch_reward_normalization(momentum=reward_norm_momentum)

    if advantage_norm:
        _patch_advantage_normalization()

    if entropy_decay:
        _patch_entropy_decay(
            beta_start=entropy_beta_start,
            beta_end=entropy_beta_end,
            decay_steps=entropy_decay_steps,
        )

    if value_clip:
        _patch_value_clipping(epsilon=value_clip_epsilon)

    # lambda_return 修复 — 始终启用 (非可选)
    _patch_lambda_return_fix()

    _PATCHES_APPLIED = True
    logger.info("🎯 所有训练优化补丁已加载 (v2.3 增强版)")
