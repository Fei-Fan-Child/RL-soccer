# RL-Soccer 环境配置与训练指南

> **已验证环境**: Windows 11 + Unity 6000.3.18f1 + Python 3.10 + ML-Agents 1.2.0.dev0

---

## 1. 目录结构

```
D:\MyUnity\
├── RL-soccer              ← 本项目 (git clone 到这里)
└── ml-agents              ← ML-Agents 源码 (手动复制)
    ├── ml-agents/           训练器
    └── ml-agents-envs/      通信库
```

---

## 2. Unity 环境

| 组件 | 说明 |
|------|------|
| Unity Editor | 6000.3.18f1 |
| ML-Agents 包 | `file:../../ml-agents/com.unity.ml-agents` (本地引用) |
| 场景 | `Assets/ML-Agents/Examples/Soccer/Scenes/SoccerTwos.unity` |

打开项目后 Unity 会自动解析 `Packages/manifest.json` 中的本地包引用。

---

## 3. Python 环境

### 3.1 安装 Python 3.10

```powershell
winget install Python.Python.3.10
```

验证：

```powershell
py -0
# 应看到 -V:3.10  Python 3.10 (64-bit)
```

### 3.2 安装 ML-Agents 依赖

> **注意**：ML-Agents 源码不在本仓库中，需要复制 `D:\MyUnity\ml-agents` 文件夹到新电脑同位置。

```powershell
cd D:\MyUnity\RL-soccer\python

# 步骤 1: grpcio (先单独装，避免 hash 校验问题)
py -3.10 -m pip install --no-cache-dir grpcio

# 步骤 2: ml-agents-envs
py -3.10 -m pip install -e D:\MyUnity\ml-agents\ml-agents-envs

# 步骤 3: ml-agents (训练器，含 PyTorch，下载较大)
py -3.10 -m pip install -e D:\MyUnity\ml-agents\ml-agents

# 步骤 4: 版本对齐 — 降级 protobuf / numpy / tensorboard
py -3.10 -m pip uninstall onnx -y
py -3.10 -m pip install "protobuf==3.20.3" "tensorboard>=2.13,<2.14" "numpy>=1.23.5,<1.24.0"

# 步骤 5: 安装 rlsoccer 包 (SideChannel + StatsWriter 插件)
py -3.10 -m pip install -e .

# 步骤 6: 再次版本对齐 (rlsoccer 依赖会覆盖)
py -3.10 -m pip uninstall onnx -y
py -3.10 -m pip install "protobuf==3.20.3" "tensorboard>=2.13,<2.14"

# 步骤 7: onnxscript (模型导出需要)
py -3.10 -m pip install onnxscript
py -3.10 -m pip install "protobuf==3.20.3"

# 步骤 8: 验证
py -3.10 -c "from mlagents.trainers.learn import run_training; from rlsoccer.side_channel import get_soccer_step_channel; print('OK')"
```

---

## 4. ML-Agents 兼容性补丁

> Python 3.10 的 `importlib.metadata.entry_points()` 返回 dict，而 ML-Agents 代码按 Python 3.12+ 的 list 接口调用，会导致 `AttributeError: 'str' object has no attribute 'group'`。

需要修改 `D:\MyUnity\ml-agents\ml-agents\mlagents\plugins\stats_writer.py` 中的 `register_stats_writer_plugins` 函数。

找到这段代码（约第 38-46 行）：

```python
    if ML_AGENTS_STATS_WRITER not in [ep.group for ep in importlib_metadata.entry_points()]:
        ...
    entry_points = [ep for ep in importlib_metadata.entry_points() if ep.group == ML_AGENTS_STATS_WRITER]
```

替换为：

```python
    eps = importlib_metadata.entry_points()
    if isinstance(eps, dict):
        if ML_AGENTS_STATS_WRITER not in eps:
            logger.warning(...)
            return get_default_stats_writers(run_options)
        entry_points = eps[ML_AGENTS_STATS_WRITER]
    else:
        if ML_AGENTS_STATS_WRITER not in [ep.group for ep in eps]:
            logger.warning(...)
            return get_default_stats_writers(run_options)
        entry_points = [ep for ep in eps if ep.group == ML_AGENTS_STATS_WRITER]
```

---

## 5. 训练

### 5.1 启动训练

```powershell
cd D:\MyUnity\RL-soccer\python
py -3.10 train.py --run-id soccer-v1 --force --timeout-wait 300
```

看到 "Listening on port 5005" 后：

1. 打开 Unity Editor
2. 加载 `Assets/ML-Agents/Examples/Soccer/Scenes/SoccerTwos.unity`
3. 确认场景中挂载了 `SoccerSideChannelRegistrar` 脚本
4. 确认每个 Agent 的 `BehaviorParameters` → `VectorObservationSize` = **10**
5. 确认每个 Agent 的 `BehaviorParameters` → `BehaviorType` = **Default**
6. 点击 ▶ **Play**

### 5.2 监控

```powershell
# 新终端 — TensorBoard
tensorboard --logdir D:\MyUnity\RL-soccer\python\results --port 6006
# 浏览器打开 http://localhost:6006
```

### 5.3 常用参数

| 参数 | 默认值 | 说明 |
|------|-------|------|
| `--run-id` | 必填 | 训练名称 |
| `--config` | `poca_soccer.yaml` | 配置文件（可选 `poca_soccer_optimized.yaml`） |
| `--resume` | false | 从检查点恢复 |
| `--force` | false | 覆盖已有数据 |
| `--no-graphics` | false | 无图形模式 |
| `--num-envs` | 1 | 并行环境数 |
| `--timeout-wait` | 60 | Unity 连接超时（秒） |

### 5.4 恢复训练

```powershell
py -3.10 train.py --run-id soccer-v1 --resume
```

---

## 6. 配置说明

### 训练配置

- `python/config/poca_soccer.yaml` — 基础版（512 hidden, lr=3e-4）
- `python/config/poca_soccer_optimized.yaml` — 优化版（1024 hidden, lr=1e-4, 更大对手池）
- `python/config/soccer_curriculum.json` — 4 阶段课程学习

### 关键 YAML 参数

```yaml
behaviors:
  SoccerTwos:
    trainer_type: poca          # POCA 仅支持 extrinsic 奖励
    hyperparameters:
      batch_size: 2048
      learning_rate: 0.0003
      beta: 0.005               # 熵正则化
    network_settings:
      hidden_units: 512
      num_layers: 2
    max_steps: 50000000         # 总训练步数
```

---

## 7. 常见问题

**Q: `UnicodeEncodeError: 'gbk' codec can't encode character`**
A: Windows 控制台 GBK 编码不兼容 emoji。已在 `train.py` 中修复（强制 UTF-8 输出）。

**Q: `AttributeError: module 'numpy' has no attribute 'float'`**
A: NumPy 2.x 移除了 `np.float`。降级到 1.23.x：`py -3.10 -m pip install "numpy>=1.23.5,<1.24.0"`

**Q: `ModuleNotFoundError: No module named 'onnxscript'`**
A: 安装 onnxscript：`py -3.10 -m pip install onnxscript`，然后降级 protobuf：`py -3.10 -m pip install "protobuf==3.20.3"`

**Q: Unity 报 "Couldn't connect to trainer"**
A: 必须先启动 `train.py`，再点 Unity Play。顺序反了会连接失败。

**Q: 训练时 Unity 报 NullReferenceException**
A: 检查每个 Agent 的 `BehaviorParameters` → `VectorObservationSize` 设为 10。

**Q: protobuf 版本冲突**
A: ML-Agents 编译的 pb2 文件只兼容 protobuf 3.x。确保 `protobuf==3.20.3`。
