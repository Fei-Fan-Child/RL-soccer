# RL-Soccer — 基于强化学习的足球 AI 训练项目

> Unity ML-Agents + POCA + TF Serving + Redis  
> **v2.3** — 6 项训练 Bug 修复 | Python 3.10 推荐 | 环境配置指南独立为 [Environment.md](Environment.md)

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
- [附录](#附录)

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

> **完整的环境搭建与训练步骤已独立为 [Environment.md](Environment.md)**，包含：
> - Python 3.10 安装
> - ML-Agents 依赖安装 (8 步命令)
> - ML-Agents 兼容性补丁
> - 训练启动与监控
> - 常见问题排查

### 2.1 目录结构

```text
D:\MyUnity\
├── RL-soccer         ← 本项目 (git clone)
└── ml-agents          ← ML-Agents 源码 (手动复制)
    ├── ml-agents/       训练器
    └── ml-agents-envs/  通信库
```

### 2.2 依赖一览

| 组件 | 版本 | 说明 |
|------|------|------|
| Unity Editor | 6000.3.18f1 | 3D 物理足球环境 |
| Python | **3.10** (推荐) | ML-Agents 原生支持 |
| ML-Agents 包 | `file:../../ml-agents/com.unity.ml-agents` | Unity 本地包引用 |
| ML-Agents Python | 1.2.0.dev0 (本地安装) | POCA + Self-Play |
| Input System | 1.19.0 | 人类操控 |
| protobuf | 3.20.3 | **必须降级到此版本** |
| numpy | 1.23.x | **不可用 2.x** |
| onnxscript | 最新 | 模型导出需要 |
| TensorBoard | 2.13.x | 训练监控 |

### 2.3 Docker (可选)

```powershell
# Redis (步级日志 + 指标缓存)
docker compose -f redis/docker-compose.yml up -d

# TF Serving (模型远程推理)
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
│   │   ├── AutoModeSwitcher.cs              # 🆕 自动模式切换 (Human↔AI)
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
│   └── rlsoccer/                           #   ★ Python 包 (v2.3)
│       ├── __init__.py
│       ├── reward_shaper.py                 #   🆕 奖励塑形器 (Potential + Curriculum + Normalize)
│       ├── redis_writer.py                 #   RedisStatsWriter (entry_points 插件)
│       ├── side_channel.py                 #   SoccerStepChannel (Unity↔Python gRPC)
│       └── training_patches.py             #   🆕 5项补丁 (梯度裁剪·奖励归一·优势归一·熵衰减·价值裁剪)
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

### 5.5 🆕 Python 端奖励塑形 (v2.3)

| 模块 | 功能 | 文件 |
|------|------|------|
| `RewardShaper` | 统一管理三种塑形模式 | `reward_shaper.py` |
| `PotentialShaper` | 基于球位置的潜力函数: Φ(s) = -dist_to_opp + -dist_to_own | 同上 |
| `RunningNormalizer` | 按事件类型 Z-score 归一化 | 同上 |
| 课程衰减 | 前 20M 步不变，之后线性衰减到 0.1× | 同上 |
| 事件权重 | 20 种事件权重可在 YAML 中配置 | `poca_soccer.yaml` |

### 5.6 🆕 训练补丁 (v2.3)

| # | 补丁 | 说明 |
|---|------|------|
| 1 | 梯度裁剪 | max_norm=0.5, 防止 loss 爆炸 |
| 2 | 奖励归一化 | running mean/std Z-score |
| 3 | **优势归一化** 🆕 | batch 内归一化 advantages, 减少方差 |
| 4 | **熵衰减** 🆕 | β: 0.01→0.001 线性衰减 30M 步 |
| 5 | **价值裁剪** 🆕 | 防止 value 估计过大 |

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

> **完整步骤见 [Environment.md](Environment.md)**，下方为快速参考。

### 7.1 快速开始

```powershell
# 步骤 1: 启动训练 (先运行这个)
cd D:\MyUnity\RL-soccer\python
py -3.10 train.py --run-id soccer-v1 --force --timeout-wait 300

# 步骤 2: Unity Editor 中打开 SoccerTwos 场景，点击 ▶ Play
#   - 确保挂载 SoccerSideChannelRegistrar
#   - 确保 BehaviorParameters → VectorObservationSize = 10
#   - 确保 BehaviorParameters → BehaviorType = Default

# 步骤 3: 监控
tensorboard --logdir results --port 6006
# 浏览器打开 http://localhost:6006
```

> **⚠️ 必须先 train.py 再 Unity Play，顺序反了会连接失败。**

### 7.2 命令参数

| 参数 | 默认值 | 说明 |
|------|-------|------|
| `--run-id` | 必填 | 训练名称 |
| `--config` | `poca_soccer.yaml` | 配置文件 |
| `--resume` | false | 从检查点恢复 |
| `--force` | false | 覆盖已有数据 |
| `--no-graphics` | false | 无图形模式 (服务器) |
| `--num-envs` | 1 | 并行 Unity 环境数 |
| `--timeout-wait` | 60 | Unity 连接超时 (秒) |

```powershell
# 使用优化版配置
py -3.10 train.py --run-id soccer-v1 --config poca_soccer_optimized.yaml --force

# 恢复训练
py -3.10 train.py --run-id soccer-v1 --resume
```

### 7.3 训练配置版本

| 配置文件 | 网络 | 适用场景 |
|---------|------|---------|
| `poca_soccer.yaml` | 512×2 | 基础版，一般 GPU |
| `poca_soccer_optimized.yaml` | 1024×3 | ★ 推荐，更大容量 |
| `poca_soccer_v100.yaml` | 2048×3 + LSTM | V100 32GB 专用 |

### 7.4 TensorBoard 关键指标

| 指标 | 期望趋势 | 说明 |
|------|---------|------|
| `Environment/Cumulative Reward` | 📈 上升 | 综合奖励（最重要） |
| `Self-Play/ELO` | 📈 上升 | 相对能力评分 |
| `Losses/Policy Loss` | 📉 下降 | 策略损失 |
| `Policy/Learning Rate` | ➡ 按 schedule | 学习率 |

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

### A. v2.3 更新日志 (2026-07-14)

#### Unity 端 (C#)
| 类型 | 文件 | 改动 |
|------|------|------|
| 🐛 Bug | `AgentSoccer.cs` | `m_BallTouch` 默认值 0→1.0，首帧奖励尖峰修复 |
| 🐛 Bug | `AgentSoccer.cs` | 拦截/传球检测顺序修复，`m_SoccerSettings` null 检查 |
| 🐛 Bug | `SoccerEnvController.cs` | `m_ResetParams` null fallback |
| 🆕 新增 | `SoccerBallController.cs` | 球碰墙反弹 (0.92 系数) 替代重置 |
| 🆕 新增 | `SoccerBallController.cs` | 四角惩罚 (-0.15) + 四角对方奖励 (+0.05) |
| 🆕 新增 | `SoccerEnvController.cs` | `AddTeamReward()` 队伍级奖励方法 |
| 🔧 修复 | 球门物理 | GoalNet 标签清理 + Soccergoal 碰撞体匹配模型 + Cage 框架 (仅正面进球) |
| 🔧 修复 | Agent 配置 | BlueStriker `isHumanControlled=true`, P1/P2 键位 |
| 🆕 新增 | `AutoModeSwitcher.cs` | 自动模式切换: Human ↔ AI 无缝切换 |

#### Python 端
| 类型 | 文件 | 改动 |
|------|------|------|
| 🆕 新增 | `reward_shaper.py` | 奖励塑形器 (Potential + Curriculum + Normalize, 383行) |
| 🆕 增强 | `training_patches.py` | 从 2 项→5 项: 优势归一化 + 熵衰减 + 价值裁剪 |
| 🔧 更新 | `poca_soccer.yaml` | `reward_shaping` 段 (20 种事件权重), `lr_schedule: linear` |
| 🔧 更新 | `train.py` | 自动加载 5 项补丁 + 奖励塑形器, v2.3 横幅 |
| 📄 文档 | `Environment.md` | **新增** 环境配置与训练完整指南 |

### B. 预训练模型

| 模型 | 路径 | 用途 |
|------|------|------|
| SoccerTwos.onnx | `Assets/.../TFModels/` | 2v2 通用球员 |
| Striker.onnx | `Assets/.../TFModels/` | 前锋专用 |
| Goalie.onnx | `Assets/.../TFModels/` | 门将专用 |

### C. 常见问题

**Q: 训练时 Unity 报 "Communicator" 错误？**
A: 先启动 `train.py`，再点 Unity ▶ Play。检查端口 5005 未被占用。

**Q: `UnicodeEncodeError: 'gbk' codec`？**
A: Windows 控制台编码问题，v2.3 已修复 (`train.py` 强制 UTF-8)。

**Q: `AttributeError: module 'numpy' has no attribute 'float'`？**
A: NumPy 2.x 移除了 `np.float`。降级：`py -3.10 -m pip install "numpy>=1.23.5,<1.24.0"`

**Q: `ModuleNotFoundError: No module named 'onnxscript'`？**
A: `py -3.10 -m pip install onnxscript`，然后 `py -3.10 -m pip install "protobuf==3.20.3"`

**Q: protobuf 相关错误 (Descriptors/TypeError/ImportError)？**
A: ML-Agents 的 pb2 文件只兼容 protobuf 3.x：`py -3.10 -m pip install "protobuf==3.20.3"`

**Q: Python 3.14 能用吗？**
A: **推荐 Python 3.10**。3.14 需手动改 ml-agents 的 setup.py 版本约束，且 numpy/torch 兼容性差。详见 [Environment.md](Environment.md)。

**Q: 显存不足 (OOM)？**
A: 减小 `batch_size` 至 1024/512，降低 `buffer_size`。

**Q: 不能用 curiosity/gail/rnd？**
A: POCA 训练器**不支持**，只能使用 `extrinsic` 奖励。

**Q: Unity 报 NullReferenceException？**
A: 检查 Agent 的 `BehaviorParameters` → `VectorObservationSize` = 10，`BehaviorType` = Default。
