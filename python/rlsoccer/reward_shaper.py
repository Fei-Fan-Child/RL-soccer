"""
奖励塑形与归一化模块
====================
在训练过程中动态调整奖励信号，解决以下问题:

1. 奖励稀疏 — 进球 ±1 但 99% 时间无大事件 → 用潜力函数提供稠密引导
2. 量级不统一 — 进球=±1, 角落=-0.15, 触球=+0.2 → 按事件类型分别归一化
3. 课程学习 — 从稠密奖励过渡到稀疏奖励，让策略最终依赖自然信号

用法:
  from rlsoccer.reward_shaper import RewardShaper
  shaper = RewardShaper(config_path="config/reward_weights.yaml")

  # 在训练循环中:
  shaped_reward = shaper.shape(episode_data)

核心算法:
  Potential-Based Reward Shaping (Ng, Harada, Russell 1999)
  F(s, a, s') = γ * Φ(s') - Φ(s)
  保证最优策略不变，同时提供稠密引导信号。
"""

import math
from pathlib import Path
from typing import Dict, Optional, Tuple
from collections import defaultdict, deque

import numpy as np
import yaml

from mlagents_envs import logging_util

logger = logging_util.get_logger(__name__)


# ============================================================
# 默认奖励事件权重
# ============================================================
DEFAULT_EVENT_WEIGHTS = {
    # 队伍级事件 (大奖励/惩罚)
    "goal_scored": 1.0,         # 进球
    "goal_conceded": -1.0,      # 丢球
    "corner_trap": -0.15,       # 球被逼入角落
    "ball_out": -0.15,          # 球出界 (已废弃，改为反弹)
    "danger_zone": -0.001,      # 禁区危险 (每帧)

    # Agent 级事件 (触球时)
    "ball_touch": 0.2,          # 触球基础
    "interception": 0.25,       # 拦截
    "pass_receive": 0.08,       # 传球配合
    "shot_on_target": 0.5,      # 射正 (球朝球门方向)
    "own_goal_risk": -0.25,     # 乌龙风险
    "clearance": 0.2,           # 解围

    # 稠密奖励权重 (每帧)
    "approach_ball": 0.02,      # 靠近球
    "face_ball": 0.008,         # 面朝球
    "possess_ball": 0.005,      # 控球
    "defensive_position": 0.004,# 防守站位
    "offensive_half": 0.002,    # 进攻半场
    "dribble_forward": 0.003,   # 带球推进
    "pressure": 0.002,          # 压迫对手

    # 惩罚
    "wall_collision": -0.15,    # 撞墙
    "stuck": -0.008,            # 卡住不动 (每3秒)
    "away_from_ball": -0.005,   # 远离球 (>5秒)
    "teammate_bump": -0.03,     # 队友碰撞
}


# ============================================================
# 潜力函数 (Potential Function)
# ============================================================

class PotentialShaper:
    """
    基于球位置的潜力函数，提供稠密引导而不改变最优策略。

    潜力定义:
      Φ(s) = w_attack * (-dist_to_opp_goal) + w_defense * (-dist_to_own_goal)

    直觉:
      - 球越靠近对方球门 → 潜力越高 (进攻有利)
      - 球越远离己方球门 → 潜力越高 (防守安全)
    """

    def __init__(
        self,
        attack_weight: float = 0.01,
        defense_weight: float = 0.005,
        gamma: float = 0.99,
    ):
        self.attack_weight = attack_weight
        self.defense_weight = defense_weight
        self.gamma = gamma
        self._last_potential: Dict[int, float] = {}  # per episode

    def potential(self, ball_pos: np.ndarray, opp_goal_pos: np.ndarray, own_goal_pos: np.ndarray) -> float:
        """计算当前状态的潜力值"""
        dist_to_opp = np.linalg.norm(ball_pos - opp_goal_pos)
        dist_to_own = np.linalg.norm(ball_pos - own_goal_pos)
        return self.attack_weight * (-dist_to_opp) + self.defense_weight * (-dist_to_own)

    def shape(self, episode_id: int, ball_pos: np.ndarray, opp_goal_pos: np.ndarray, own_goal_pos: np.ndarray) -> float:
        """
        计算塑形奖励: F = γ * Φ(s') - Φ(s)
        首次调用时自动初始化上次潜力值。
        """
        current = self.potential(ball_pos, opp_goal_pos, own_goal_pos)
        last = self._last_potential.get(episode_id, current)
        self._last_potential[episode_id] = current
        return self.gamma * current - last

    def reset_episode(self, episode_id: int):
        """回合结束时清理"""
        self._last_potential.pop(episode_id, None)


# ============================================================
# 奖励归一化 (Per-Event Running Statistics)
# ============================================================

class RunningNormalizer:
    """追踪每个奖励事件的 running mean/std，按类型归一化"""

    def __init__(self, momentum: float = 0.001, epsilon: float = 1e-8):
        self.momentum = momentum
        self.epsilon = epsilon
        self.stats: Dict[str, Tuple[float, float]] = {}  # name -> (mean, var)

    def update(self, name: str, value: float):
        """更新某类事件的 running statistics"""
        if name not in self.stats:
            self.stats[name] = (value, 1.0)  # 初始: mean=value, var=1.0
        else:
            mean, var = self.stats[name]
            delta = value - mean
            new_mean = mean + self.momentum * delta
            new_var = (1 - self.momentum) * var + self.momentum * delta * delta
            self.stats[name] = (new_mean, new_var)

    def normalize(self, name: str, value: float) -> float:
        """Z-score 归一化"""
        if name not in self.stats:
            self.stats[name] = (0.0, 1.0)
        mean, var = self.stats[name]
        return (value - mean) / (math.sqrt(var) + self.epsilon)

    def get_stats(self, name: str) -> Tuple[float, float]:
        return self.stats.get(name, (0.0, 1.0))


# ============================================================
# 奖励塑形器 (主入口)
# ============================================================

class RewardShaper:
    """
    奖励塑形器 — 统一管理所有奖励调整逻辑。

    三种塑形模式 (可组合):
      1. potential  — 基于潜力的稠密引导
      2. normalize  — 按事件类型归一化
      3. curriculum — 课程学习，逐步减少人工奖励

    用法:
      shaper = RewardShaper()
      shaped = shaper.shape_event("goal_scored", 1.0, episode_id=5)
    """

    def __init__(
        self,
        event_weights: Optional[Dict[str, float]] = None,
        enable_potential: bool = True,
        enable_normalize: bool = True,
        enable_curriculum: bool = True,
        potential_attack: float = 0.01,
        potential_defense: float = 0.005,
        gamma: float = 0.99,
        normalize_momentum: float = 0.001,
        curriculum_total_steps: int = 50_000_000,
        curriculum_anneal_steps: int = 20_000_000,
    ):
        # 事件权重
        self.weights = DEFAULT_EVENT_WEIGHTS.copy()
        if event_weights:
            self.weights.update(event_weights)

        # 潜力塑形
        self._use_potential = enable_potential
        self._potential = PotentialShaper(potential_attack, potential_defense, gamma)

        # 归一化
        self._use_normalize = enable_normalize
        self._normalizer = RunningNormalizer(momentum=normalize_momentum)

        # 课程学习
        self._use_curriculum = enable_curriculum
        self._total_steps = curriculum_total_steps
        self._anneal_steps = curriculum_anneal_steps
        self._current_step = 0

        # 统计
        self._episode_rewards: Dict[int, float] = defaultdict(float)

        logger.info(
            f"RewardShaper 初始化: potential={enable_potential}, "
            f"normalize={enable_normalize}, curriculum={enable_curriculum}"
        )

    # ── 事件级塑形 ──

    def shape_event(self, event_name: str, raw_reward: float, episode_id: int = 0) -> float:
        """
        对单个奖励事件进行塑形。

        Args:
            event_name: 事件名称 (如 "goal_scored", "ball_touch")
            raw_reward: Unity 端给出的原始奖励值
            episode_id: 回合 ID

        Returns:
            塑形后的奖励值
        """
        # 1. 应用事件权重
        weight = self.weights.get(event_name, 1.0)
        shaped = raw_reward * weight

        # 2. 归一化
        if self._use_normalize:
            self._normalizer.update(event_name, shaped)
            shaped = self._normalizer.normalize(event_name, shaped)

        # 3. 课程衰减
        if self._use_curriculum:
            shaped *= self._curriculum_factor()

        # 4. 跟踪
        self._episode_rewards[episode_id] += shaped

        return shaped

    # ── 潜力塑形 (位置引导) ──

    def shape_potential(
        self,
        episode_id: int,
        ball_pos: np.ndarray,
        opp_goal_pos: np.ndarray,
        own_goal_pos: np.ndarray,
    ) -> float:
        """
        计算基于潜力的稠密引导奖励。
        应在每帧调用。
        """
        if not self._use_potential:
            return 0.0

        shaped = self._potential.shape(episode_id, ball_pos, opp_goal_pos, own_goal_pos)

        if self._use_curriculum:
            shaped *= self._curriculum_factor()

        return shaped

    # ── 批量塑形 (用于训练 loop) ──

    def shape_batch(self, events: Dict[str, float], episode_id: int = 0) -> Dict[str, float]:
        """批量塑形多个事件"""
        return {k: self.shape_event(k, v, episode_id) for k, v in events.items()}

    # ── 课程学习 ──

    def _curriculum_factor(self) -> float:
        """
        计算当前课程衰减因子。
        前 anneal_steps 步保持 1.0，之后线性衰减到 0.1。
        让策略逐步依赖环境自然奖励 (进球等) 而非人工稠密奖励。
        """
        if self._current_step < self._anneal_steps:
            return 1.0
        progress = (self._current_step - self._anneal_steps) / (self._total_steps - self._anneal_steps)
        progress = min(progress, 1.0)
        return 1.0 - 0.9 * progress  # 1.0 → 0.1

    def step(self, n: int = 1):
        """推进训练步数 (用于课程衰减)"""
        self._current_step += n

    def get_curriculum_factor(self) -> float:
        return self._curriculum_factor()

    # ── 回合管理 ──

    def end_episode(self, episode_id: int) -> float:
        """回合结束，返回累计奖励"""
        total = self._episode_rewards.pop(episode_id, 0.0)
        self._potential.reset_episode(episode_id)
        return total

    # ── 统计查询 ──

    def get_event_stats(self) -> Dict[str, Dict[str, float]]:
        """获取各事件当前统计"""
        result = {}
        for name, (mean, var) in self._normalizer.stats.items():
            result[name] = {"mean": mean, "std": math.sqrt(var)}
        return result

    def get_summary(self) -> Dict[str, float]:
        """获取摘要"""
        return {
            "curriculum_factor": self._curriculum_factor(),
            "total_steps": self._current_step,
        }

    # ── 持久化 ──

    def save(self, path: str):
        """保存运行时统计"""
        import json
        data = {
            "weights": self.weights,
            "stats": self.get_event_stats(),
            "curriculum_factor": self._curriculum_factor(),
            "current_step": self._current_step,
        }
        with open(path, "w") as f:
            json.dump(data, f, indent=2)
        logger.info(f"RewardShaper 状态已保存到 {path}")

    def load(self, path: str):
        """加载运行时统计"""
        import json
        with open(path, "r") as f:
            data = json.load(f)
        self.weights.update(data.get("weights", {}))
        self._current_step = data.get("current_step", 0)
        logger.info(f"RewardShaper 状态已从 {path} 恢复 (step={self._current_step})")


# ============================================================
# 便捷函数: 从 YAML 配置加载 RewardShaper
# ============================================================

def load_reward_shaper(config_path: str) -> RewardShaper:
    """
    从 YAML 配置文件创建 RewardShaper。

    配置文件格式 (reward_shaping 段):
      reward_shaping:
        event_weights:
          goal_scored: 1.0
          ball_touch: 0.2
          ...
        potential:
          attack_weight: 0.01
          defense_weight: 0.005
        normalize:
          momentum: 0.001
        curriculum:
          total_steps: 50000000
          anneal_steps: 20000000
    """
    import yaml
    with open(config_path, 'r', encoding='utf-8') as f:
        config = yaml.safe_load(f)

    rs_config = config.get("reward_shaping", {})

    return RewardShaper(
        event_weights=rs_config.get("event_weights"),
        enable_potential=rs_config.get("potential", {}).get("enabled", True),
        enable_normalize=rs_config.get("normalize", {}).get("enabled", True),
        enable_curriculum=rs_config.get("curriculum", {}).get("enabled", True),
        potential_attack=rs_config.get("potential", {}).get("attack_weight", 0.01),
        potential_defense=rs_config.get("potential", {}).get("defense_weight", 0.005),
        gamma=rs_config.get("gamma", 0.99),
        normalize_momentum=rs_config.get("normalize", {}).get("momentum", 0.001),
        curriculum_total_steps=rs_config.get("curriculum", {}).get("total_steps", 50_000_000),
        curriculum_anneal_steps=rs_config.get("curriculum", {}).get("anneal_steps", 20_000_000),
    )
