"""
RL-Soccer 训练工具包
====================
基于 ML-Agents 插件系统的正确集成方案:
  - RedisStatsWriter:  训练指标 → Redis (通过 entry_points)
  - SoccerStepChannel: Unity → Python 步级数据 (通过 SideChannel)
  - TrainingPatches:   梯度裁剪 + 奖励归一化 (通过 monkey-patch)
"""

__version__ = "2.1.0"
