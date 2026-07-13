# RL-Soccer — 基于强化学习的足球 AI 训练项目

> 石河子大学信息科学与技术学院 · 人工智能综合实训  
> Unity ML-Agents + POCA + TF Serving + Redis  
> **v2.2** — Python 3.14 兼容 | NullReferenceException 修复 | 环境配置文档更新

---

## 目录

- [1. 项目概述](#1-项目概述)
- [2. 环境配置](#2-环境配置)
- [3. 项目结构](#3-项目结构)
- [4. 数据架构](#4-数据架构)
- [5. 奖励函数设计](#5-奖励函数设计)
- [6. 游戏机制](#6-游戏机制)
- [7. 训练指南](#7-训练指南)
- [8. TF Serving 部署](#8-tf-serving-部署)
- [9. Redis 日志与可视化](#9-redis-日志与可视化)
- [10. 人类 vs AI 模式](#10-人类-vs-ai-模式)

---

## 1. 项目概述

基于 Unity ML-Agents 的足球对抗强化学习项目。使用 **POCA** (POsthumous Credit Assignment) 算法进行 self-play 多智能体训练，实现 2v2 和 Strikers-vs-Goalie 两种足球对战模式。

### 技术栈

| 层级 | 技术 | 说明 |
|------|------|------|
| 游戏引擎 | Unity 6000.3.18f1 | 3D 物理足球环境 |
| 训练框架 | ML-Agents (本地包) | POCA + Self-Play |
| 神经网络 | PyTorch (SimpleActor) | ML-Agents 内置, YAML 配置 |
| 步级日志 | **SideChannel (gRPC)** | Unity → Python → Redis, <5ms |
| 指标日志 | **StatsWriter 插件** | 训练指标自动写入 Redis |
| 训练优化 | **梯度裁剪 + 奖励归一化** | monkey-patch 注入, 提升稳定性 |
| 课程学习 | **4 阶段 Curriculum** | 静态球 → 慢速 → 正常 → 快速 |
| 模型推理 | ONNX → TF Serving | gRPC 远程推理 |
| 缓存 | Redis 7 | 2GB, LRU, RDB |
| 监控 | TensorBoard | 训练曲线实时查看 |
| 输入 | Unity Input System 1.19 | 人类操控支持 |

---

## 2. 环境配置

### 2.1 依赖关系

```text
D:\MyUnity\RL-soccer         ← 本项目 (Unity)
D:\MyUnity\ml-agents          ← ML-Agents 源码 (Unity 本地包引用)
```

### 2.2 Unity 环境

| 组件 | 版本/路径 |
|------|----------|
| Unity Editor | 6000.3.18f1 |
| ML-Agents 包 | `file:../../ml-agents/com.unity.ml-agents` |
| Input System | 1.19.0 |
| Barracuda (ONNX) | 2.6.1 |
| Unity MCP | 0.82.4 |

### 2.3 Python 环境 (首次安装)

> **⚠️ Python 3.14+ 用户注意**: 本地 `ml-agents` 仓库限制 `python_requires="<=3.10.12"`。  
> 安装前需要先修改 `D:\MyUnity\ml-agents\ml-agents-envs\setup.py` 和  
> `D:\MyUnity\ml-agents\ml-agents\setup.py` 中的版本约束（详见下方步骤）。

```powershell
cd D:\MyUnity\RL-soccer\python

# ============================================================
# 步骤 1: 安装 ml-agents-envs (Unity 通信库)
# ============================================================
# 首次安装前，修改 D:\MyUnity\ml-agents\ml-agents-envs\setup.py:
#   python_requires=">=3.10.1"          (原: ">=3.10.1,<=3.10.12")
#   "numpy>=1.23.5"                     (原: "numpy>=1.23.5,<1.24.0")
#   "protobuf>=3.6"                     (原: "protobuf>=3.6,<3.21")
#   "grpcio>=1.11.0"                    (原: "grpcio>=1.11.0,<=1.53.2")
#   "pettingzoo>=1.15.0"                (原: "pettingzoo==1.15.0")

pip install -e D:\MyUnity\ml-agents\ml-agents-envs

# ============================================================
# 步骤 2: 安装 ml-agents (训练器)
# ============================================================
# 首次安装前，修改 D:\MyUnity\ml-agents\ml-agents\setup.py:
#   python_requires=">=3.10.1"          (原: ">=3.10.1,<=3.10.12")
#   "numpy>=1.23.5"                     (原: "numpy>=1.23.5,<1.24.0")
#   "protobuf>=3.6"                     (原: "protobuf>=3.6,<3.21")
#   "grpcio>=1.11.0"                    (原: "grpcio>=1.11.0,<=1.53.2")
#   "torch>=2.1.1"                      (原: "torch>=2.1.1,<=2.8.0")
#   "onnx>=1.15.0"                      (原: "onnx==1.15.0")

pip install -e D:\MyUnity\ml-agents\ml-agents

# ============================================================
# 步骤 3: 版本对齐 (关键！解决 protobuf/tensorboard 冲突)
# ============================================================
# ml-agents 的 pb2 文件只兼容 protobuf 3.x，但新版 tensorboard 需要 6.x+
# 解决: 降级 protobuf + tensorboard，移除 onnx（训练不需要）

pip uninstall onnx -y
pip install "protobuf==3.20.3" "tensorboard>=2.13,<2.14"

# ============================================================
# 步骤 4: 安装 rlsoccer 包 (RedisStatsWriter 插件 + 依赖)
# ============================================================
pip install -e .

# ============================================================
# 步骤 5: 重复版本对齐 (rlsoccer 依赖会覆盖 protobuf/tensorboard)
# ============================================================
pip uninstall onnx -y
pip install "protobuf==3.20.3" "tensorboard>=2.13,<2.14"

# ============================================================
# 验证
# ============================================================
python -c "from mlagents.trainers.learn import run_training; from rlsoccer.side_channel import get_soccer_step_channel; print('OK')"
```

> **为什么这么复杂？** Python 3.14 是 2026 年最新版本，ML-Agents 仓库的  
> protobuf/torch/numpy 版本约束跟不上。上述步骤已在实际环境中验证通过。

### 2.4 Docker 服务

```powershell
# 仅 Redis
docker compose -f redis/docker-compose.yml up -d

# TF Serving + Redis 全套
docker compose -f tf_serving/docker-compose.yml up -d
```

---

## 3. 项目结构

```
RL-soccer/
├── Assets/ML-Agents/Examples/Soccer/
│   ├── Scenes/
│   │   ├── SoccerTwos.unity                # 2v2 场景
│   │   └── StrikersVsGoalie.unity          # 前锋 vs 门将
│   ├── Scripts/
│   │   ├── AgentSoccer.cs                  # ★ 球员 Agent (20 维奖励 + 10 维观察)
│   │   ├── SoccerEnvController.cs          # 环境管理器 (进球/重置/curriculum)
│   │   ├── SoccerBallController.cs         # 足球碰撞检测
│   │   ├── SoccerSettings.cs               # 全局配置
│   │   ├── SoccerStepSideChannel.cs        # ★ SideChannel: 步级数据 → Python
│   │   ├── SoccerSideChannelRegistrar.cs   # ★ 自动注册 SideChannel (挂场景即可)
│   │   └── Billboard.cs                    # UI 始终面向摄像机
│   ├── Prefabs/                            # 球场 + 球 + Agent 预制体
│   └── TFModels/                           # 预训练 ONNX 模型
│       ├── SoccerTwos.onnx
│       ├── Striker.onnx
│       └── Goalie.onnx
│
├── config/poca/
│   ├── SoccerTwos.yaml                     # 2v2 POCA 配置 (原始)
│   └── StrikersVsGoalie.yaml              # 前锋 vs 门将配置 (原始)
│
├── python/                                 # 🔵 Python 训练工具包 v2.1
│   ├── setup.py                            #   ★ 包安装 (entry_points 注册 StatsWriter)
│   ├── train.py                            #   ★ 训练启动器 (单进程: SideChannel + 优化补丁)
│   ├── requirements.txt                    #   Python 依赖清单
│   ├── README.md
│   ├── config/
│   │   ├── poca_soccer.yaml                #   基础训练配置 (仅 extrinsic)
│   │   ├── poca_soccer_optimized.yaml      #   ★ 优化版配置 (1024 hidden, 0.01 beta, window 20)
│   │   └── soccer_curriculum.json          #   ★ 4 阶段课程学习 (静态→慢速→正常→快速)
│   └── rlsoccer/                           #   ★ Python 包
│       ├── __init__.py
│       ├── redis_writer.py                 #   RedisStatsWriter (entry_points 插件)
│       ├── side_channel.py                 #   SoccerStepChannel (Unity↔Python gRPC)
│       └── training_patches.py             #   ★ 梯度裁剪 + 奖励归一化 (monkey-patch)
│
├── tf_serving/                             # 🟠 TF Serving 部署
│   ├── Dockerfile
│   ├── docker-compose.yml                  # 一键部署 (TF + Redis)
│   ├── convert_onnx_to_tf.py               # ONNX → TF SavedModel
│   └── Assets/Scripts/TFServingClient.cs   # Unity REST 推理客户端
│
└── redis/                                  # 🔴 Redis 缓存
    ├── redis.conf                          # Redis 配置 (2GB, LRU, RDB)
    ├── docker-compose.yml                  # Redis 独立部署
    ├── redis_bridge.py                     # FastAPI 桥接 (查询 + 兜底写入)
    └── Assets/Scripts/RedisLogger.cs       # Unity HTTP 日志客户端 (备用)
```

---

## 4. 数据架构

### 4.1 两条独立通道

```
┌──────────────────────────────────────────────────────────────────┐
│                        Unity 训练时                               │
│                                                                  │
│  ┌─ 通道 1: 步级数据 (SideChannel) ──────────────────────────┐  │
│  │                                                            │  │
│  │  AgentSoccer.OnActionReceived()                            │  │
│  │    └→ SoccerStepSideChannel.SendStep(s,a,r,pos,team)       │  │
│  │         │                                                  │  │
│  │         ▼ gRPC (<5ms, ML-Agents 原生)                      │  │
│  │    Python SoccerStepChannel.on_message_received()          │  │
│  │         │                                                  │  │
│  │         ▼                                                  │  │
│  │    Redis:                                                  │  │
│  │      soccer:ep:{id}:step:{n}     Hash (TTL 600s)          │  │
│  │      soccer:heatmap:{team}       SortedSet                  │  │
│  │                                                            │  │
│  └────────────────────────────────────────────────────────────┘  │
│                                                                  │
│  ┌─ 通道 2: 训练指标 (StatsWriter 插件) ────────────────────┐  │
│  │                                                            │  │
│  │  mlagents-learn 训练循环                                   │  │
│  │    └→ 每 summary_freq (10000) 步                           │  │
│  │    └→ RedisStatsWriter.write_stats()                      │  │
│  │         │  (通过 setup.py entry_points 自动注册)           │  │
│  │         ▼                                                  │  │
│  │    Redis:                                                  │  │
│  │      soccer:metrics:reward       List (cap 500)            │  │
│  │      soccer:metrics:elo          List (cap 200)            │  │
│  │      soccer:metrics:loss         Hash                       │  │
│  │      soccer:metrics:latest       Hash (完整快照)           │  │
│  │                                                            │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

### 4.2 为什么是 SideChannel 而不是 HTTP？

| 方案 | 延迟 | 可靠性 | 实现 |
|------|------|--------|------|
| **SideChannel (gRPC)** | <5ms | ✅ ML-Agents 原生, 训练进程内 | 本方案 |
| HTTP Bridge | ~10-50ms | ⚠️ 额外进程, 需手动批量 | 备用 (`RedisLogger.cs`) |

SideChannel 复用了 ML-Agents 与 Unity 之间已有的 gRPC 连接，零额外开销。

### 4.3 Redis Key 一览

| Key 模式 | 来源 | 类型 | TTL | 内容 |
|----------|------|------|-----|------|
| `soccer:ep:{id}:step:{n}` | SideChannel | Hash | 600s | 单步 (s,a,r,pos,team) |
| `soccer:heatmap:{team}` | SideChannel | SortedSet | — | 位置热力图 |
| `soccer:metrics:reward` | StatsWriter | List (500) | — | 累积奖励曲线 |
| `soccer:metrics:elo` | StatsWriter | List (200) | — | ELO 历史 |
| `soccer:metrics:loss` | StatsWriter | Hash | — | 训练损失最新值 |
| `soccer:metrics:latest` | StatsWriter | Hash | — | 全部指标快照 |

---

## 5. 奖励函数设计

> 核心原则：**奖励塑形 (Reward Shaping)** — 为每一个中间行为设计稠密奖励信号，  
> 避免稀疏奖励导致的训练崩溃。参照 OpenAI Five / AlphaStar 的工业级实践。

### 5.1 设计哲学

```
稠密引导 (每帧):     靠近球 + 面朝球 + 移动速度 + 控球 + 站位 + 边界意识
半稠密反馈 (每几秒):  踢球方向 + 拦截抢断 + 传球配合 + 卡住检测
稀疏事件 (每回合几次): 触球 + 进球 + 失球 + 碰墙 + 球出界
事件级 (每局):        得分 + 回合结束
```

### 5.2 完整奖励信号表

#### 5.2.1 稠密引导 (每帧触发, Dense)

| # | 奖励信号 | 公式 | 量级 | 设计意图 |
|---|---------|------|------|---------|
| 1 | **靠近球** | `Δ距离 × 0.015` | ±0.02/帧 | 🏆 最核心的稠密引导——每一步都能感知方向对错 |
| 2 | **远离球** | 同上（自动取负） | ±0.02/帧 | 惩罚与球拉开距离的无效移动 |
| 3 | **面朝球** | `max(0, (dot-0.3) × 0.005)` | 0~0.0035/帧 | 视觉注意力引导 |
| 4 | **移动速度** | `速度 × 0.0003` | 0~0.002/帧 | 鼓励活跃，防止挂机 |
| 5 | **卡住检测** | `-0.005/帧` (>3秒不动) | -0.25/秒 | 惩罚策略退化 |
| 6 | **持续控球** | `+0.003/帧` (距球<2) | 0.15/秒 | 粘球奖励——带球是进攻基础 |
| 7 | **长时间不碰球** | `-0.002/帧` (>10秒) | -0.1/秒 | 催促参与比赛 |
| 8 | **Striker 前压** | `-dist × 0.0001` | 0~+0.005/帧 | 进攻位置意识 |
| 9 | **Goalie 站位** | `+0.001/帧` (距球门1.5~5) | 0.05/秒 | 防守选位 |
| 10 | **Goalie 失位** | `-0.003/帧` (距球门>8) | -0.15/秒 | 防止门将乱跑 |
| 11 | **边界意识** 🆕 | `(1-dist/3) × -0.003/帧` (\(距边线<3\)) | 0~-0.15/秒 | 防止跑出球场 |
| 12 | **越界强惩罚** 🆕 | `-0.01/帧` (已在边线外) | -0.5/秒 | 立即拉回场内 |

#### 5.2.2 半稠密反馈 (动作触发, Semi-Dense)

| # | 奖励信号 | 公式 | 量级 | 设计意图 |
|---|---------|------|------|---------|
| 13 | **踢球方向质量** | `+0.4 × shotAlignment` (dot>0.5) | +0.2~+0.4 | 鼓励向对方球门射门 |
| 14 | **乌龙球预防** | `-0.25` (dot<-0.3) | -0.25 | 惩罚踢向己方球门 |
| 15 | **拦截抢断** 🆕 | `+0.25` (从对方脚下抢到球) | +0.25 | 鼓励防守抢断 |
| 16 | **传球配合** 🆕 | `+0.08` (队友间传递) | +0.08 | 鼓励团队配合 |
| 17 | **踢球力度** | `力量 × 0.00002` | ~0.04 | 鼓励大力射门 |
| 18 | **碰墙** (Agent) | `-0.15` | -0.15 | 边界意识 |

#### 5.2.3 交互反馈

| # | 奖励信号 | 公式 | 量级 | 设计意图 |
|---|---------|------|------|---------|
| 15 | **触球基础** | `+0.2 × ball_touch` (curriculum) | 0~0.2 | 触球即奖励 |
| 16 | **带球推进** | `+0.001/帧` (Striker触球时) | ~0.05/秒 | 盘带鼓励 |
| 17 | **队友碰撞** | `-0.02` | -0.02 | 控制间距 |

#### 5.2.4 事件级奖励 (全局)

| # | 奖励信号 | 公式 | 量级 | 位置 |
|---|---------|------|------|------|
| 19 | **进球** | `+(1 - t/T)` | 0~+1.0 | `SoccerEnvController.GoalTouched` |
| 20 | **失球** | `-1.0` | -1.0 | 同上 |
| 21 | **球出界 (责任方)** 🆕 | `-0.15` (全队) | -0.15 | `SoccerEnvController.BallOutOfBounds` |
| 22 | **球出界 (对方)** 🆕 | `+0.05` (全队) | +0.05 | 同上 |
| 23 | **Goalie 生存** | `+Existential × 0.3` | ~0.00001/帧 | `AgentSoccer.OnActionReceived` |

### 5.3 旧版 vs 新版对比

| 维度 | 旧版 (v1) | 新版 (v2.2) |
|------|---------|------------|
| 奖励信号数 | **4 种** | **23 种** |
| 稠密引导 | 0 种 | **12 种** |
| 出界惩罚 | ❌ 直接重置 | ✅ 团队惩罚 + 对方奖励 |
| 拦截奖励 | ❌ 无 | ✅ +0.25 抢断奖励 |
| 传球奖励 | ❌ 无 | ✅ +0.08 配合奖励 |
| 每回合总奖励量级 | 0.01 ~ 0.5 | **2.0 ~ 15.0** |
| 边界意识 | ❌ 无 | ✅ 距边线<3 渐近惩罚 |

### 5.4 代码位置

```text
AgentSoccer.cs:
  FixedUpdate()          ← 稠密奖励 (每帧, #1~#10)
  OnActionReceived()     ← 生存激励 (#20) + SideChannel SendStep
  OnCollisionEnter()     ← 触球/墙壁/队友 (#11~#17)
  OnCollisionStay()      ← 带球推进 (#16)

SoccerEnvController.cs:
  GoalTouched()          ← 进球/失球 (#18, #19) + SideChannel SendSummary
```

---

## 6. 游戏机制

### 6.1 两个场景

| 场景 | 模式 | Agent 配置 |
|------|------|-----------|
| `SoccerTwos` | 2v2 对称对抗 | 每队 2 Striker |
| `StrikersVsGoalie` | 前锋 vs 门将 | 每队 1 Striker + 1 Goalie |

### 6.2 动作空间

```text
Discrete, 3 Branches × 3 Actions:

  Branch 0 (Forward):  0=停 / 1=前进 / 2=后退
  Branch 1 (Lateral):  0=停 / 1=右移 / 2=左移
  Branch 2 (Rotate):   0=停 / 1=左转 / 2=右转

总组合: 3³ = 27 种动作
```

### 6.3 观察空间

Agent 通过 **双重感知** 获取环境信息：

**RayPerceptionSensor (3D 射线):**
- 球、己方/对方球门、墙壁、队友、对手的位置

**VectorSensor (10 维额外向量观察, v2.1 新增):**

| # | 观察 | 维度 | 说明 |
|---|------|------|------|
| 1-3 | 球相对速度 | 3 | 球的运动方向和速度 (Agent 坐标系) |
| 4 | 球距对方球门 | 1 | 进攻机会评估 |
| 5 | Agent 自身速度 | 1 | 当前移动速率 |
| 6 | 面朝球程度 | 1 | dot(forward, dirToBall) |
| 7-8 | 归一化位置 | 2 | 球场坐标 / 20 |
| 9-10 | 队伍身份 | 2 | one-hot (Blue/Purple) |

**总观察维度 ≈ 336 (射线) + 10 (向量) = 346 维**

### 6.4 环境参数 (Curriculum)

| 参数 | 默认值 | 课程阶段 | 说明 |
|------|-------|---------|------|
| `ball_touch` | 0.0 | 1.0 → 0.5 → 0.0 | 触球奖励系数 (逐步降低) |
| `goal_reward` | 1.0 | 0.5 → 0.7 → 1.0 | 进球奖励基值 (逐步提高) |
| `ball_movement_speed` | 5.0 | 0 → 2 → 5 → 8 | 球的初速度 (逐步加快, v2.1 新增) |

课程学习配置: `python/config/soccer_curriculum.json`

```json
// 4 阶段渐进训练
{
  "lessons": [
    {"ball_touch": 1.0, "ball_movement_speed": 0},    // 入门: 静态球
    {"ball_touch": 0.5, "ball_movement_speed": 2},    // 初级: 慢速带球
    {"ball_touch": 0.0, "ball_movement_speed": 5},    // 中级: 正常比赛
    {"ball_touch": 0.0, "ball_movement_speed": 8}     // 高级: 快速球
  ]
}
```

---

## 7. 训练指南

### 7.1 快速开始

```powershell
# 前置: 完成 [2.3 Python 环境配置](#23-python-环境-首次安装)

# 1. 启动训练 (先运行这个！)
cd D:\MyUnity\RL-soccer\python
python train.py --run-id soccer-v1

# 2. 看到 "Listening on port 5005" 后，在 Unity Editor 中点击 ▶ Play
#    确保场景中 SoccerSideChannelRegistrar 已挂载到任意 GameObject

# 3. 监控训练
tensorboard --logdir results --port 6006
```

> **⚠️ 重要**: 必须先启动 `python train.py`，再点 Unity ▶ Play。  
> 顺序反了会报 "Couldn't connect to trainer" 警告（Agent 退化为推理模式）。

> **⚠️ VectorObservationSize**: 如果 AI Agent 需要向量观察（球速、位置等 10 维），  
> 在 Unity Inspector 中将 Agent 的 `BehaviorParameters` → `VectorObservationSize` 设为 `10`。  
> 设为 `0`（默认）也不会报错——`CollectObservations` 已内置 null guard 安全跳过。

### 7.2 启动时发生了什么

```
python train.py --run-id soccer-v1
  │
  ├─ 1. 注册 SoccerStepChannel (等待 Unity 连接)
  │      └→ 步级数据将写入 Redis: soccer:ep:*:step:*
  │
  ├─ 2. 加载训练优化补丁
  │      ├→ 梯度裁剪 (max_norm=0.5)
  │      └→ 奖励归一化 (running mean/std)
  │
  ├─ 3. 注入 SideChannel 到环境工厂
  │      └→ Python 单进程直连, 非 subprocess
  │
  ├─ 4. 启动 run_training()
  │      └→ 自动加载 RedisStatsWriter (entry_points 插件)
  │      └→ 指标数据将写入 Redis: soccer:metrics:*
  │
  └─ 5. 等待 Unity 连接...
         └→ Unity ▶ Play → gRPC 握手 → 训练开始
```

### 7.3 环境变量配置

| 变量 | 默认值 | 说明 |
|------|-------|------|
| `REDIS_HOST` | `localhost` | Redis 主机地址 |
| `REDIS_PORT` | `6379` | Redis 端口 |

```powershell
# 指定 Redis 地址
$env:REDIS_HOST="192.168.1.100"
python train.py --run-id soccer-v1
```

### 7.4 命令参数

| 参数 | 默认值 | 说明 |
|------|-------|------|
| `--run-id` | 必填 | 训练运行 ID |
| `--config` | `poca_soccer_optimized.yaml` | YAML 配置文件 (可选 optimized 版) |
| `--num-envs` | 1 | 并行 Unity 环境数量 |
| `--resume` | false | 从检查点恢复训练 |
| `--inference` | false | 仅推理模式 (不更新权重) |
| `--force` | false | 覆盖已有 run-id |
| `--no-graphics` | false | 无图形模式 (服务器训练) |
| `--base-port` | 5005 | Unity 通信端口 |
| `--env-args` | — | 传递给 Unity 的额外参数 |

### 7.5 训练监控指标

TensorBoard 关键指标：

| 指标 | 期望趋势 | 说明 |
|------|---------|------|
| `Environment/Cumulative Reward` | 📈 持续上升 | 综合奖励（最重要） |
| `Self-Play/ELO` | 📈 缓慢上升 | 相对能力评分 |
| `Losses/Policy Loss` | 📉 稳定下降 | 策略损失 |
| `Losses/Value Loss` | 📉 稳定下降 | 价值损失 |
| `Policy/Learning Rate` | ➡ 按 schedule | 学习率衰减 |

Redis 实时查看：

```powershell
redis-cli LRANGE soccer:metrics:reward 0 5   # 最近 5 条累积奖励
redis-cli HGETALL soccer:metrics:loss         # 最新训练损失
redis-cli ZREVRANGE soccer:heatmap:0 0 9 WITHSCORES  # 蓝队热力图 Top 10
```

### 7.6 训练配置 (YAML 关键参数)

三个版本可用:

```yaml
# python/config/poca_soccer_v100.yaml (🆕 V100-32GB 专用)
behaviors:
  SoccerTwos:
    trainer_type: poca
    network_settings:
      hidden_units: 2048       # 1024→2048  V100 32GB 驾驭
      num_layers: 3
      memory:                  # 🆕 LSTM 记忆单元
        memory_size: 128
        sequence_length: 16
    hyperparameters:
      batch_size: 2048         # 1024→2048  更稳定梯度
      buffer_size: 20480
      learning_rate: 0.0001
      beta: 0.015              # 更强探索正则化
      num_epoch: 5
      learning_rate_schedule: linear
    self_play:
      window: 30               # 20→30  更大对手池
      save_steps: 50000
      swap_steps: 4000
      play_against_latest_model_ratio: 0.3
```

```yaml
# python/config/poca_soccer_optimized.yaml (★ 推荐 — GPU 通用)
behaviors:
  SoccerTwos:
    trainer_type: poca          # ⚠️ POCA 仅支持 extrinsic 奖励信号
    hyperparameters:
      learning_rate: 0.0001    # 3e-4→1e-4  更精细更新
      beta: 0.01               # 0.005→0.01 更多探索
      epsilon: 0.2             # PPO clip 范围
      lambd: 0.95              # GAE λ
      batch_size: 1024         # 2048→1024  更稳定
      num_epoch: 5             # 3→5        更充分学习
    network_settings:
      hidden_units: 1024       # 512→1024   更高容量
      num_layers: 3            # 2→3        更深网络
    reward_signals:
      extrinsic:
        gamma: 0.99
    self_play:
      window: 20               # 10→20      更大对手池
      swap_steps: 4000         # 2000→4000  更稳定适应
      play_against_latest_model_ratio: 0.3
```

| 参数 | 基础值 | 优化值 | 影响 |
|------|-------|-------|------|
| `learning_rate` | 0.0003 | **0.0001** | 更精细的策略更新 |
| `beta` | 0.005 | **0.01** | 更多探索, 防早熟 |
| `batch_size` | 2048 | **1024** | 更稳定的梯度 |
| `hidden_units` | 512 | **1024** | 更强的表达能力 |
| `num_layers` | 2 | **3** | 更深的非线性 |
| `num_epoch` | 3 | **5** | 每批数据更充分学习 |
| `self_play.window` | 10 | **20** | 更多样化的对手 |
| `swap_steps` | 2000 | **4000** | 策略更稳定适应 |

### 7.7 原始方式训练 (不使用 Python 工具包)

```powershell
cd D:\MyUnity\ml-agents

# 2v2 训练
mlagents-learn D:\MyUnity\RL-soccer\config\poca\SoccerTwos.yaml --run-id=rl-soccer-twos

# Strikers vs Goalie
mlagents-learn D:\MyUnity\RL-soccer\config\poca\StrikersVsGoalie.yaml --run-id=rl-soccer-strikers-goalie

# 使用修复版配置
mlagents-learn D:\MyUnity\RL-soccer\python\config\poca_soccer.yaml --run-id=rl-soccer-v2
```

> **注意**: 原始方式不会启用 SideChannel 和 RedisStatsWriter。使用 `python train.py` 启动可以获得完整的 Redis 日志功能。

---

## 8. TF Serving 部署

### 8.1 架构

```
Unity (C#) ──REST/gRPC──→ TF Serving (:8501) ──→ TF SavedModel
                              │
                              ├── 推理延迟: ~5ms (gRPC)
                              └── vs 本地 Barracuda: ~15ms (参考值)
```

### 8.2 转换模型

```powershell
cd tf_serving

# 方法 1: ONNX → TF SavedModel
python convert_onnx_to_tf.py \
    --onnx ../Assets/ML-Agents/Examples/Soccer/TFModels/SoccerTwos.onnx \
    --output ./models/soccer_twos/1 \
    --model-name soccer_twos

# 方法 2: 从训练结果直接转换 (ONNX 模型在 results/<run-id>/ 下)
python convert_onnx_to_tf.py \
    --onnx ../results/soccer-v1/SoccerTwos.onnx \
    --output ./models/soccer_twos/1
```

### 8.3 启动服务

```powershell
# 构建并启动 TF Serving + Redis
docker compose -f tf_serving/docker-compose.yml up -d --build

# 测试 REST API
curl -d '{"instances": [[...]]}' \
    http://localhost:8501/v1/models/soccer_twos:predict
```

### 8.4 Unity 端调用

```csharp
// 挂载 TFServingClient.cs 到 Agent GameObject
// Inspector 配置: ServerUrl = "http://localhost:8501", ModelName = "soccer_twos"

var client = GetComponent<TFServingClient>();
int[] actions = client.PredictSync(observations);
// → [forward, lateral, rotate]
```

---

## 9. Redis 日志与可视化

### 9.1 数据流总览

```
训练时自动写入 (无需额外操作):

  Unity ──gRPC SideChannel──→ Python ──→ Redis  (步级数据, <5ms)
  mlagents-learn ──StatsWriter插件──→ Redis      (训练指标, 每10000步)

查询时:

  Redis Bridge (:8000) ──HTTP──→ 可视化面板 / curl / 自定义前端
```

### 9.2 启动 Bridge (查询 API)

```powershell
pip install fastapi uvicorn redis
python redis/redis_bridge.py --port 8000
# API 文档: http://localhost:8000/docs
```

API 端点：

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/health` | 健康检查 |
| GET | `/api/stats/reward_curve` | 最近 100 局累积奖励 |
| GET | `/api/stats/heatmap/{team}` | 热力图数据 (`?top_n=100`) |
| GET | `/api/stats/summary` | 训练全局统计 |
| POST | `/api/log/steps` | 批量写入步骤 (备用) |
| POST | `/api/log/episode` | 写入回合摘要 (备用) |

### 9.3 手动查询 Redis

```powershell
redis-cli KEYS "soccer:*"                      # 查看所有 key
redis-cli LRANGE soccer:metrics:reward 0 -1    # 完整奖励曲线
redis-cli HGETALL soccer:metrics:latest        # 最新全部指标
redis-cli ZREVRANGE soccer:heatmap:0 0 19 WITHSCORES  # 蓝队热力图 Top 20
```

---

## 10. 人类 vs AI 模式

### 10.1 启用方式

在 Unity Editor 中：

1. 选中 Agent GameObject
2. `AgentSoccer` 组件:
   - `isHumanControlled = true`
   - `playerSlot = P1` 或 `P2`
3. `BehaviorParameters` 组件:
   - `BehaviorType = HeuristicOnly`

### 10.2 键位映射

| 操作 | P1 (WASD) | P2 (方向键) |
|------|----------|------------|
| 前进 | W | ↑ |
| 后退 | S | ↓ |
| 左转 | A | ← |
| 右转 | D | → |

---

## 附录

### A. 预训练模型

| 模型 | 路径 | 用途 |
|------|------|------|
| SoccerTwos.onnx | `Assets/.../TFModels/` | 2v2 通用球员 |
| Striker.onnx | `Assets/.../TFModels/` | 前锋专用 |
| Goalie.onnx | `Assets/.../TFModels/` | 门将专用 |

### B. 常见问题

**Q: 训练时 Unity 报 "Communicator" 错误？**
A: `mlagents-learn` 必须先启动，再点 Unity ▶ Play。检查端口 5005 未被占用。

**Q: Unity 报 NullReferenceException: CollectObservations → sensor.AddObservation？**
A: `BehaviorParameters` 的 `VectorObservationSize` 设为 0 时，ML-Agents 不会创建
`VectorSensor`，导致 `CollectObservations` 收到 null sensor。已在 `AgentSoccer.cs` 中添加
null guard (`if (sensor == null) return`)，设为 0 也不会崩溃。如需启用向量观察，设为 10。

**Q: Python 报 "Cannot import mlagents" / 安装失败？**
A: 检查 Python 版本。Python 3.14 用户必须按 [2.3 节](#23-python-环境-首次安装) 手动修改
`ml-agents` 的 `setup.py` 版本约束、降级 protobuf 到 3.20.3、移除 onnx。

**Q: Python 报 protobuf 相关错误 (Descriptors/TypeError/ImportError)？**
A: 版本冲突。ML-Agents 的 protobuf 生成文件只兼容 3.x，但新版 onnx/tensorboard
需要 protobuf 4.x+。解决: `pip install "protobuf==3.20.3" "tensorboard>=2.13,<2.14"`
并 `pip uninstall onnx -y`（训练不需要 onnx）。

**Q: Redis 里没有步级数据？**
A: 检查 Unity 场景中是否挂载了 `SoccerStepSideChannel.cs` 脚本（挂到任意 GameObject 即可）。

**Q: Redis 里没有训练指标？**
A: 确认已按 [2.3 节](#23-python-环境-首次安装) 完成 rlsoccer 包安装
（`pip install -e .` 后 entry_points 才会注册 RedisStatsWriter 插件）。

**Q: 显存不足 (OOM)？**
A: 减小 `batch_size` 至 1024/512，降低 `buffer_size`。

**Q: 奖励曲线震荡不收敛？**
A: 增大 `beta` (熵正则化) → 增加探索；降低 `learning_rate` → 更稳定。

**Q: 不能用 curiosity/gail/rnd 吗？**
A: POCA 训练器**不支持**。只能使用 `extrinsic` 奖励信号。如需好奇心驱动探索，改用 PPO trainer。

**Q: 优化版配置和基础版有什么区别？**
A: 优化版降低了 lr、增大了 beta/网络容量/对手池。预期训练效率提升 20-40%。

**Q: 如何启用课程学习？**
A: `python/config/soccer_curriculum.json` 已配置好 4 阶段课程。训练时 Unity 端自动读取 `ball_movement_speed` 参数调节球速。

**Q: 梯度裁剪和奖励归一化怎么工作的？**
A: `rlsoccer/training_patches.py` 在训练启动时通过 monkey-patch 注入到 POCA 训练循环中，无需修改 ML-Agents 源码。
