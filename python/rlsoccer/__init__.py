"""
RL-Soccer 训练工具包 v2.3
=========================
基于 ML-Agents 插件系统的集成方案:
  - RewardShaper:      奖励塑形 (Potential Shaping + 课程衰减)
  - TrainingPatches:   5项补丁 (梯度裁剪·奖励归一·优势归一·熵衰减·价值裁剪)
  - RedisStatsWriter:  训练指标 → Redis (通过 entry_points)
  - SoccerStepChannel: Unity → Python 步级数据 (通过 SideChannel)
"""

__version__ = "2.3.0"
