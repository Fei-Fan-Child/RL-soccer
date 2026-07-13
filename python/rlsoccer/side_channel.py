"""
Soccer 步级数据 SideChannel (Python 端)
========================================
通过 ML-Agents SideChannel 机制接收 Unity 发送的每一步
(状态-动作-奖励) 数据, 写入 Redis。

通信协议:
  UUID: 8b7d5e3a-1c2f-4d6e-9a8b-3c4d5e6f7a8b
  消息格式 (每个 OutgoingMessage):
    1. int    episode_id
    2. int    step
    3. float  reward
    4. int    action_0
    5. int    action_1
    6. int    action_2
    7. float  pos_x
    8. float  pos_z
    9. int    team       (0=Blue, 1=Purple)
    10. int    position  (0=Striker, 1=Goalie)

使用方式:
  from rlsoccer.side_channel import SoccerStepChannel, register_step_channel
  channel = register_step_channel()
"""

import uuid
import json
import time
from typing import Optional

from mlagents_envs.side_channel import SideChannel, IncomingMessage
from mlagents_envs import logging_util

logger = logging_util.get_logger(__name__)

# 与 C# 端保持一致的 UUID
SOCCER_STEP_CHANNEL_ID = uuid.UUID("8b7d5e3a-1c2f-4d6e-9a8b-3c4d5e6f7a8b")


class SoccerStepChannel(SideChannel):
    """
    接收 Unity Agent 每一步的 (s,a,r) 数据，写入 Redis。
    """

    def __init__(self):
        super().__init__(SOCCER_STEP_CHANNEL_ID)
        self._redis = None
        self._episode_buffer = []
        self._total_steps = 0
        self._init_redis()

    def _init_redis(self):
        """初始化 Redis 连接"""
        try:
            import redis
            import os
            host = os.environ.get("REDIS_HOST", "localhost")
            port = int(os.environ.get("REDIS_PORT", "6379"))
            self._redis = redis.Redis(
                host=host, port=port,
                decode_responses=True,
                socket_connect_timeout=3,
            )
            self._redis.ping()
            logger.info(f"SoccerStepChannel: Redis 就绪 {host}:{port}")
        except ImportError:
            logger.warning("SoccerStepChannel: redis-py 未安装, 步级日志仅输出到控制台")
        except Exception as e:
            logger.warning(f"SoccerStepChannel: Redis 不可用 ({e})")

    def on_message_received(self, msg: IncomingMessage) -> None:
        """
        Unity 每发送一步数据，这里就被调用一次。
        频率: ~50次/秒 (Unity FixedUpdate)
        """
        try:
            episode_id = msg.read_int32()
            step = msg.read_int32()
            reward = msg.read_float32()
            action_0 = msg.read_int32()
            action_1 = msg.read_int32()
            action_2 = msg.read_int32()
            pos_x = msg.read_float32()
            pos_z = msg.read_float32()
            team = msg.read_int32()
            position = msg.read_int32()
        except Exception as e:
            logger.error(f"SideChannel 消息解析失败: {e}")
            return

        self._total_steps += 1

        # 写入 Redis
        if self._redis is not None:
            self._write_to_redis(
                episode_id, step, reward,
                [action_0, action_1, action_2],
                pos_x, pos_z, team, position,
            )

        # 每 1000 步汇总一次
        if self._total_steps % 1000 == 0:
            logger.info(
                f"[SideChannel] 已接收 {self._total_steps} 步 | "
                f"最新: ep={episode_id} step={step} "
                f"reward={reward:+.4f} actions={[action_0,action_1,action_2]}"
            )

    def _write_to_redis(
        self,
        episode_id: int,
        step: int,
        reward: float,
        actions: list,
        pos_x: float,
        pos_z: float,
        team: int,
        position: int,
    ) -> None:
        """将一步数据写入 Redis"""
        if self._redis is None:
            return

        try:
            pipe = self._redis.pipeline()

            # 1. 单步详情 → Hash (TTL=600s)
            key = f"soccer:ep:{episode_id}:step:{step}"
            pipe.hset(key, mapping={
                "reward": str(reward),
                "actions": json.dumps(actions),
                "pos_x": str(round(pos_x, 4)),
                "pos_z": str(round(pos_z, 4)),
                "team": str(team),
                "position": str(position),
                "ts": time.strftime("%H:%M:%S"),
            })
            pipe.expire(key, 600)

            # 2. 热力图 → SortedSet
            grid_x = round(pos_x * 2) / 2
            grid_z = round(pos_z * 2) / 2
            pipe.zincrby(f"soccer:heatmap:{team}", 1, f"{grid_x:.2f},{grid_z:.2f}")

            pipe.execute()
        except Exception as e:
            logger.debug(f"Redis 写入失败: {e}")

    def get_total_steps(self) -> int:
        return self._total_steps


# ============================================================
# 便捷注册函数
# ============================================================
_side_channel: Optional[SoccerStepChannel] = None


def get_soccer_step_channel() -> SoccerStepChannel:
    """获取或创建全局 SoccerStepChannel 实例"""
    global _side_channel
    if _side_channel is None:
        _side_channel = SoccerStepChannel()
    return _side_channel


def register_step_channel() -> SoccerStepChannel:
    """
    创建并注册 SoccerStepChannel 到 SideChannelManager。
    Unity 连接后即可自动接收步级数据。
    """
    from mlagents_envs.side_channel import SideChannelManager

    channel = get_soccer_step_channel()
    # 重复注册是安全的 (ML-Agents 内部会去重)
    SideChannelManager.register_side_channel(channel)
    logger.info("SoccerStepChannel 已注册 (等待 Unity 连接...)")
    return channel
