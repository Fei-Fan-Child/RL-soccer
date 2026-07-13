#!/usr/bin/env python3
"""
============================================================================
  Redis Bridge — Unity ↔ Redis 通信中间层
  参照 no20.txt 实训文档 — 第5步: Redis 缓存与可视化观察台
============================================================================

功能:
  1. 接收 Unity 端 HTTP POST 的训练日志
  2. 写入 Redis (状态-动作-奖励, TTL=600s)
  3. 提供查询 API 供可视化面板消费
  4. 热力图数据聚合
  5. 奖励曲线实时查询

启动:
  pip install fastapi uvicorn redis
  python redis_bridge.py --port 8000

API 端点:
  POST   /api/log/steps           # 批量写入步骤日志
  POST   /api/log/episode         # 写入回合摘要
  POST   /api/log/action_probs    # 写入动作概率 (决策可视化)
  GET    /api/stats/reward_curve  # 获取奖励曲线
  GET    /api/stats/heatmap/{team}# 获取热力图数据
  GET    /api/stats/summary       # 获取训练摘要统计
  GET    /health                  # 健康检查
============================================================================
"""

import argparse
import json
import time
from typing import List, Optional, Dict
from datetime import datetime

import uvicorn
from fastapi import FastAPI, HTTPException, Query
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel

# ============================================================
# Redis 客户端
# ============================================================
import redis


class RedisClient:
    """Redis 客户端封装"""

    def __init__(self, host="localhost", port=6379, db=0, password=None):
        try:
            self.client = redis.Redis(
                host=host, port=port, db=db, password=password,
                decode_responses=True, socket_connect_timeout=5,
            )
            self.client.ping()
            self.available = True
            print(f"✅ Redis 连接成功: {host}:{port}")
        except Exception as e:
            print(f"⚠️  Redis 不可用 ({e})，使用内存存储")
            self.client = None
            self.available = False
            self._memory_store: List[Dict] = []

    # ---- 步骤日志 ----
    def log_step(self, entry: Dict) -> None:
        ep_id = entry["episode_id"]
        step = entry["step"]
        key = f"soccer:ep:{ep_id}:step:{step}"

        data = {
            "reward": entry.get("reward", 0),
            "actions": json.dumps(entry.get("actions", [])),
            "pos_x": entry.get("pos_x", 0),
            "pos_z": entry.get("pos_z", 0),
            "team": entry.get("team", 0),
            "position": entry.get("position", 0),
            "timestamp": datetime.now().isoformat(),
        }

        if self.available:
            pipe = self.client.pipeline()
            pipe.hset(key, mapping={k: str(v) for k, v in data.items()})
            pipe.expire(key, 600)
            # 热力图: 按团队记录位置
            team = entry.get("team", 0)
            grid_x = round(entry.get("pos_x", 0) * 2) / 2
            grid_z = round(entry.get("pos_z", 0) * 2) / 2
            pipe.zincrby(f"soccer:heatmap:{team}", 1, f"{grid_x:.2f},{grid_z:.2f}")
            pipe.execute()
        else:
            self._memory_store.append(data)

    def log_batch(self, entries: List[Dict]) -> int:
        count = 0
        for entry in entries:
            self.log_step(entry)
            count += 1
        return count

    # ---- 回合摘要 ----
    def log_episode(self, summary: Dict) -> None:
        ep_id = summary["episode_id"]
        key = f"soccer:ep:{ep_id}:summary"

        data = {
            "total_reward": summary.get("total_reward", 0),
            "steps": summary.get("steps", 0),
            "goals_scored": summary.get("goals_scored", 0),
            "goals_conceded": summary.get("goals_conceded", 0),
            "result": summary.get("result", "draw"),
            "timestamp": datetime.now().isoformat(),
        }

        if self.available:
            pipe = self.client.pipeline()
            pipe.hset(key, mapping={k: str(v) for k, v in data.items()})
            pipe.expire(key, 3600)
            # 追加奖励曲线
            pipe.lpush("soccer:stats:reward_curve", summary.get("total_reward", 0))
            pipe.ltrim("soccer:stats:reward_curve", 0, 99)
            pipe.execute()
        else:
            self._memory_store.append({"type": "episode", **data})

    # ---- 动作概率 ----
    def log_action_probs(self, entry: Dict) -> None:
        ep_id = entry["episode_id"]
        step = entry["step"]
        key = f"soccer:ep:{ep_id}:step:{step}:probs"

        data = {
            "branch_0": json.dumps(entry.get("branch_0", [])),
            "branch_1": json.dumps(entry.get("branch_1", [])),
            "branch_2": json.dumps(entry.get("branch_2", [])),
        }

        if self.available:
            pipe = self.client.pipeline()
            pipe.hset(key, mapping=data)
            pipe.expire(key, 600)
            pipe.execute()

    # ---- 查询 ----
    def get_reward_curve(self) -> List[float]:
        if self.available:
            data = self.client.lrange("soccer:stats:reward_curve", 0, -1)
            return [float(x) for x in data]
        return []

    def get_heatmap(self, team: int, top_n: int = 100) -> List[Dict]:
        if self.available:
            raw = self.client.zrevrange(
                f"soccer:heatmap:{team}", 0, top_n - 1, withscores=True
            )
            return [{"pos": k, "count": int(v)} for k, v in raw]
        return []

    def get_summary(self) -> Dict:
        curve = self.get_reward_curve()
        if not curve:
            return {"episodes": 0}
        return {
            "episodes": len(curve),
            "avg_reward": sum(curve) / len(curve),
            "max_reward": max(curve),
        }


# ============================================================
# FastAPI 应用
# ============================================================
app = FastAPI(
    title="RL-Soccer Redis Bridge",
    description="Unity ↔ Redis 训练日志通信中间层",
    version="1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)

# 全局 Redis 客户端 (在 startup 中初始化)
db: Optional[RedisClient] = None


@app.on_event("startup")
def startup():
    global db
    import os
    host = os.environ.get("REDIS_HOST", "localhost")
    port = int(os.environ.get("REDIS_PORT", "6379"))
    db = RedisClient(host=host, port=port)


# ---- 数据模型 ----
class StepEntry(BaseModel):
    episode_id: int
    step: int
    reward: float = 0.0
    actions: List[int] = []
    pos_x: float = 0.0
    pos_z: float = 0.0
    team: int = 0
    position: int = 0
    observations: Optional[List[float]] = None

class BatchSteps(BaseModel):
    steps: List[StepEntry] = []

class EpisodeSummaryModel(BaseModel):
    episode_id: int
    total_reward: float
    steps: int
    goals_scored: int = 0
    goals_conceded: int = 0
    result: str = "draw"

class ActionProbModel(BaseModel):
    episode_id: int
    step: int
    branch_0: Optional[List[float]] = None
    branch_1: Optional[List[float]] = None
    branch_2: Optional[List[float]] = None


# ---- API 端点 ----

@app.get("/health")
def health():
    return {"status": "ok", "redis": db.available if db else False}


@app.post("/api/log/steps")
def log_steps(batch: BatchSteps):
    """批量写入步骤日志"""
    entries = [s.model_dump() for s in batch.steps]
    count = db.log_batch(entries) if db else 0
    return {"status": "ok", "count": count}


@app.post("/api/log/episode")
def log_episode(summary: EpisodeSummaryModel):
    """写入回合摘要"""
    db.log_episode(summary.model_dump())
    return {"status": "ok"}


@app.post("/api/log/action_probs")
def log_action_probs(entry: ActionProbModel):
    """写入动作概率分布 (决策可视化)"""
    db.log_action_probs(entry.model_dump())
    return {"status": "ok"}


@app.get("/api/stats/reward_curve")
def get_reward_curve():
    """获取最近100局的累积奖励曲线"""
    curve = db.get_reward_curve()
    return {"data": curve, "count": len(curve)}


@app.get("/api/stats/heatmap/{team}")
def get_heatmap(team: int, top_n: int = Query(default=100, le=500)):
    """获取热力图数据 (Agent 活动轨迹密度)"""
    data = db.get_heatmap(team, top_n)
    return {"team": team, "data": data}


@app.get("/api/stats/summary")
def get_summary():
    """获取训练全局摘要"""
    summary = db.get_summary()
    return summary


# ============================================================
# 入口
# ============================================================
def main():
    parser = argparse.ArgumentParser(description="Redis Bridge Server")
    parser.add_argument("--host", default="0.0.0.0", help="绑定地址")
    parser.add_argument("--port", type=int, default=8000, help="端口")
    parser.add_argument("--redis-host", default="localhost", help="Redis 主机")
    parser.add_argument("--redis-port", type=int, default=6379, help="Redis 端口")

    args = parser.parse_args()

    # 设置环境变量供 FastAPI startup 使用
    import os
    os.environ["REDIS_HOST"] = args.redis_host
    os.environ["REDIS_PORT"] = str(args.redis_port)

    print("=" * 60)
    print("🔴 Redis Bridge Server")
    print(f"   HTTP:  http://{args.host}:{args.port}")
    print(f"   Redis: {args.redis_host}:{args.redis_port}")
    print(f"   API docs: http://{args.host}:{args.port}/docs")
    print("=" * 60)

    uvicorn.run(app, host=args.host, port=args.port, log_level="info")


if __name__ == "__main__":
    main()
