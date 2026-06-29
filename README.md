# RL-soccer

Standalone Unity project containing the ML-Agents Soccer example extracted from the local ML-Agents workspace.

## Contents

- Unity scenes: `Assets/ML-Agents/Examples/Soccer/Scenes/`
- Prefabs, materials, meshes, scripts and ONNX models: `Assets/ML-Agents/Examples/Soccer/`
- Training configs:
  - `config/poca/SoccerTwos.yaml`
  - `config/poca/StrikersVsGoalie.yaml`

## Open

Open this folder in Unity:

```text
D:\MyUnity\RL-soccer
```

The project references the local ML-Agents Unity package at:

```text
D:\MyUnity\ml-agents\com.unity.ml-agents
```

## Training examples

Run from `D:\MyUnity\ml-agents` after opening the scene in Unity:

```powershell
mlagents-learn config\poca\SoccerTwos.yaml --run-id=rl-soccer-twos
mlagents-learn config\poca\StrikersVsGoalie.yaml --run-id=rl-soccer-strikers-goalie
```

Or copy/adapt the configs from this project's `config/poca` folder.
