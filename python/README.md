# RL-Soccer Python 训练工具包

## 📁 目录结构

```
python/
├── train.py                  # 训练启动器 (主入口)
├── requirements.txt          # Python 依赖
├── config/
│   └── custom_soccer.yaml    # 自定义训练配置 (增强版)
├── networks/
│   ├── __init__.py
│   └── custom_poca.py        # 自定义 Actor/Critic 网络
└── utils/
    ├── __init__.py
    ├── redis_logger.py       # Redis 训练日志
    └── reward_tracker.py     # 奖励函数追踪
```

## 🚀 快速开始

### 1. 安装依赖

```powershell
cd D:\MyUnity\RL-soccer\python
pip install -r requirements.txt
```

### 2. 启动 Redis (可选，用于日志)

```powershell
cd D:\MyUnity\RL-soccer
docker compose -f redis/docker-compose.yml up -d
```

### 3. 启动训练

```powershell
# 基础训练
python train.py --run-id soccer-v1

# 带 Redis 日志
python train.py --run-id soccer-v1 --redis

# 恢复训练
python train.py --run-id soccer-v1 --resume

# 仅推理
python train.py --run-id soccer-v1 --inference
```

### 4. 在 Unity Editor 中点击 ▶ Play

## 📊 监控

```powershell
# TensorBoard (训练曲线)
tensorboard --logdir results --port 6006

# Redis Bridge (日志查询 API)
python ../redis/redis_bridge.py --port 8000
# 打开 http://localhost:8000/docs 查看 API
```

## 🏗️ 自定义网络架构

```python
from networks.custom_poca import SoccerRLModel

model = SoccerRLModel(
    obs_dim=336,        # 观察维度
    hidden_size=512,    # 隐藏层 (可调大)
    num_layers=3,       # 深度 (可加深)
    discrete_branches=[3, 3, 3],
)
```

## 🎯 自定义训练配置

编辑 `config/custom_soccer.yaml` 调整:
- `hyperparameters.learning_rate` — 学习率
- `network_settings.hidden_units` — 隐藏层大小
- `reward_signals.curiosity.strength` — 好奇心探索强度
- `self_play.window` — Self-play 对手池大小
