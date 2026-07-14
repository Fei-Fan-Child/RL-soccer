"""
Redis 训练指标 StatsWriter
===========================
通过 ML-Agents 的 StatsWriter 插件系统，将训练过程中的
所有标量指标实时写入 Redis, 供可视化面板消费。

注册方式 (setup.py entry_points):
  [mlagents.stats_writer]
  redis = rlsoccer.redis_writer:get_redis_stats_writer

Redis Key 设计:
  soccer:metrics:latest     Hash    最新一步的所有指标
  soccer:metrics:reward     List    累积奖励曲线 (cap 500)
  soccer:metrics:elo        List    ELO 评分历史 (cap 200)
  soccer:metrics:loss       Hash    训练损失最新值
"""

import time
import json
from typing import Dict, List, Any, Optional
from collections import deque

from mlagents.trainers.stats import StatsWriter, StatsSummary
from mlagents.trainers.settings import RunOptions
from mlagents_envs import logging_util

logger = logging_util.get_logger(__name__)


# ============================================================
# 轻量级 Redis 客户端 (无依赖版)
# ============================================================
class _RedisClient:
    """
    不依赖 redis 库的轻量客户端。
    如果安装了 redis-py 则使用它，否则仅记日志。
    """

    def __init__(self, host="localhost", port=6379, db=0):
        self._client = None
        self.available = False
        try:
            import redis
            self._client = redis.Redis(
                host=host, port=port, db=db,
                decode_responses=True,
                socket_connect_timeout=3,
            )
            self._client.ping()
            self.available = True
            logger.info(f"RedisStatsWriter: Redis 连接成功 {host}:{port}")
        except ImportError:
            logger.warning(
                "RedisStatsWriter: redis-py 未安装, 指标将仅写入本地 JSON。"
                "  pip install redis 以启用 Redis 日志。"
            )
        except Exception as e:
            logger.warning(f"RedisStatsWriter: Redis 不可用 ({e}), 使用本地 JSON 存储")

    def hset(self, key: str, mapping: Dict[str, str]) -> None:
        if not self.available or self._client is None:
            return
        try:
            self._client.hset(key, mapping=mapping)
        except Exception:
            pass

    def lpush(self, key: str, value: str) -> None:
        if not self.available or self._client is None:
            return
        try:
            self._client.lpush(key, value)
        except Exception:
            pass

    def ltrim(self, key: str, start: int, end: int) -> None:
        if not self.available or self._client is None:
            return
        try:
            self._client.ltrim(key, start, end)
        except Exception:
            pass

    def set(self, key: str, value: str) -> None:
        if not self.available or self._client is None:
            return
        try:
            self._client.set(key, value)
        except Exception:
            pass


# ============================================================
# Redis StatsWriter
# ============================================================
class RedisStatsWriter(StatsWriter):
    """
    将训练标量指标写入 Redis。

    指标分类存储:
      - Environment/* → soccer:metrics:reward  (累积奖励, List)
      - Self-Play/*   → soccer:metrics:elo     (ELO 评分, List)
      - Losses/*      → soccer:metrics:loss    (训练损失, Hash)
      - Policy/*      → soccer:metrics:policy  (策略参数, Hash)
      - 其它          → soccer:metrics:latest  (最新快照, Hash)
    """

    # 主要关注的关键指标
    KEY_METRICS = [
        "Environment/Cumulative Reward",
        "Environment/Group Cumulative Reward",
        "Self-Play/ELO",
        "Losses/Policy Loss",
        "Losses/Value Loss",
        "Losses/Baseline Loss",
        "Policy/Learning Rate",
        "Policy/Epsilon",
        "Policy/Beta",
        "Is Training",
    ]

    def __init__(
        self,
        redis_host: str = "localhost",
        redis_port: int = 6379,
        reward_list_cap: int = 500,
        elo_list_cap: int = 200,
    ):
        super().__init__()
        self._redis = _RedisClient(host=redis_host, port=redis_port)
        self._reward_cap = reward_list_cap
        self._elo_cap = elo_list_cap
        self._step_count = 0

        # 本地 JSON 兜底存储
        self._local_history: List[Dict] = []

    def write_stats(
        self, category: str, values: Dict[str, StatsSummary], step: int
    ) -> None:
        """
        ML-Agents 训练循环会在每个 summary_freq 步调用此方法。
        category: 行为名称 (如 "SoccerTwos")
        """
        self._step_count += 1
        timestamp = time.time()

        # 提取关键指标 (确保转为 Python 原生 float)
        flat: Dict[str, float] = {}
        for key, summary in values.items():
            flat[key] = float(round(summary.mean, 6))

        # ── 按指标类型写入不同 Redis Key ──

        # 1. 累积奖励 → List (用于奖励曲线)
        if "Environment/Cumulative Reward" in flat:
            reward = flat["Environment/Cumulative Reward"]
            self._redis.lpush("soccer:metrics:reward", str(reward))
            self._redis.ltrim("soccer:metrics:reward", 0, self._reward_cap - 1)
            # 同时记录时间戳版本 (用于带时间轴的图表)
            self._redis.lpush(
                "soccer:metrics:reward_ts",
                json.dumps({"step": step, "reward": reward, "ts": timestamp}),
            )
            self._redis.ltrim("soccer:metrics:reward_ts", 0, self._reward_cap - 1)

        # 2. ELO 评分 → List
        if "Self-Play/ELO" in flat:
            self._redis.lpush("soccer:metrics:elo", str(flat["Self-Play/ELO"]))
            self._redis.ltrim("soccer:metrics:elo", 0, self._elo_cap - 1)

        # 3. 训练损失 → Hash (仅最新值)
        loss_data = {}
        for loss_key in ["Losses/Policy Loss", "Losses/Value Loss", "Losses/Baseline Loss"]:
            if loss_key in flat:
                loss_data[loss_key.replace("/", "_")] = str(flat[loss_key])
        if loss_data:
            loss_data["_step"] = str(step)
            loss_data["_ts"] = str(timestamp)
            self._redis.hset("soccer:metrics:loss", loss_data)

        # 4. 策略参数 → Hash
        policy_data = {}
        for pol_key in ["Policy/Learning Rate", "Policy/Epsilon", "Policy/Beta"]:
            if pol_key in flat:
                policy_data[pol_key.replace("/", "_")] = str(flat[pol_key])
        if policy_data:
            self._redis.hset("soccer:metrics:policy", policy_data)

        # 5. 全部指标快照 → Hash (最新)
        all_data = {k.replace("/", "_").replace(" ", ""): str(v) for k, v in flat.items()}
        all_data["_category"] = category
        all_data["_step"] = str(step)
        all_data["_ts"] = str(timestamp)
        self._redis.hset("soccer:metrics:latest", all_data)

        # 6. 本地 JSON 兜底 (每 50 步写一次)
        self._local_history.append({"step": step, "category": category, **flat})
        if len(self._local_history) % 50 == 0:
            self._save_local()

    def _save_local(self) -> None:
        """写入本地 JSON 文件作为兜底"""
        try:
            import os
            path = os.path.join(
                os.path.dirname(__file__), "..", "..", "training_logs", "metrics.json"
            )
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "w") as f:
                json.dump(self._local_history[-500:], f, indent=2)
        except Exception:
            pass


# ============================================================
# 入口点工厂函数 (供 setuptools entry_points 调用)
# ============================================================
def get_redis_stats_writer(run_options: RunOptions) -> List[StatsWriter]:
    """
    ML-Agents 插件入口点。
    系统会在训练启动时自动调用此函数，传入 RunOptions。
    返回的 StatsWriter 列表会被自动注册到训练循环。

    环境变量配置:
      REDIS_HOST: Redis 主机 (默认 localhost)
      REDIS_PORT: Redis 端口 (默认 6379)
    """
    import os
    host = os.environ.get("REDIS_HOST", "localhost")
    port = int(os.environ.get("REDIS_PORT", "6379"))

    writer = RedisStatsWriter(redis_host=host, redis_port=port)
    logger.info(
        f"RedisStatsWriter 已注册: redis://{host}:{port} "
        f"(指标将在训练时实时写入)"
    )
    return [writer]
