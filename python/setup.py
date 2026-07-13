"""
RL-Soccer Python 包
====================
安装后 ML-Agents 会自动发现 RedisStatsWriter 插件。
"""

from setuptools import setup, find_packages

setup(
    name="rlsoccer",
    version="2.0.0",
    description="RL-Soccer training toolkit with Redis logging",
    packages=find_packages(),
    python_requires=">=3.8",
    install_requires=[
        "mlagents>=1.0.0",
        "redis>=4.0.0",
        "numpy>=1.21.0",
    ],
    entry_points={
        # ML-Agents 插件: 自动注册 RedisStatsWriter
        "mlagents.stats_writer": [
            "redis = rlsoccer.redis_writer:get_redis_stats_writer",
        ],
    },
)
