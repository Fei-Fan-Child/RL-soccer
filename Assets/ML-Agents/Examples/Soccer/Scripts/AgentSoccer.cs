using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum Team
{
    Blue = 0,
    Purple = 1
}

public class AgentSoccer : Agent
{
    // ============================================================
    // 枚举定义
    // ============================================================

    public enum Position
    {
        Striker,
        Goalie,
        Generic
    }

    public enum HumanSlot
    {
        None = 0,
        P1 = 1,
        P2 = 2
    }

    // ============================================================
    // 基本字段
    // ============================================================

    [HideInInspector]
    public Team team;
    float m_KickPower;
    float m_BallTouch;
    public Position position;

    [Header("Human Control")]
    [Tooltip("If true, this agent is driven by keyboard input via Heuristic() instead of the neural network. " +
             "Set BehaviorType to HeuristicOnly on this agent's BehaviorParameters.")]
    public bool isHumanControlled = false;

    [Tooltip("Which human player's keyboard scheme to read. Only used when isHumanControlled is true. " +
             "P1 = WASD + QE (rotate). P2 = Arrow keys + NumpadEnter (rotate).")]
    public HumanSlot playerSlot = HumanSlot.None;

    const float k_Power = 2000f;
    float m_Existential;
    float m_LateralSpeed;
    float m_ForwardSpeed;

    [HideInInspector]
    public Rigidbody agentRb;
    SoccerSettings m_SoccerSettings;
    BehaviorParameters m_BehaviorParameters;
    public Vector3 initialPos;
    public float rotSign;

    EnvironmentParameters m_ResetParams;

    // ============================================================
    // 🆕 奖励塑形字段 (Reward Shaping)
    // ============================================================

    private Transform m_BallTransform;
    private Rigidbody m_BallRb;
    private Transform m_OwnGoal;
    private Transform m_OppGoal;
    private SoccerEnvController m_EnvController;

    // 距离追踪
    private float m_LastDistToBall;
    private float m_LastDistToOwnGoal;
    private float m_LastBallDistToOppGoal;

    // 统计
    private int m_BallTouchCount;
    private float m_TimeWithoutBall;      // 连续未触球时间
    private Vector3 m_LastPosition;
    private float m_StuckTime;            // 持续不动的时间

    // 球权追踪
    private int m_LastTouchedByTeam;      // -1=none, 0=Blue, 1=Purple

    // ============================================================
    // 初始化
    // ============================================================

    public override void Initialize()
    {
        m_EnvController = GetComponentInParent<SoccerEnvController>();
        if (m_EnvController != null)
        {
            m_Existential = 1f / m_EnvController.MaxEnvironmentSteps;
        }
        else
        {
            m_Existential = 1f / MaxStep;
        }

        m_BehaviorParameters = gameObject.GetComponent<BehaviorParameters>();
        if (m_BehaviorParameters.TeamId == (int)Team.Blue)
        {
            team = Team.Blue;
            initialPos = new Vector3(transform.position.x - 5f, .5f, transform.position.z);
            rotSign = 1f;
        }
        else
        {
            team = Team.Purple;
            initialPos = new Vector3(transform.position.x + 5f, .5f, transform.position.z);
            rotSign = -1f;
        }

        // 速度参数
        if (position == Position.Goalie)
        {
            m_LateralSpeed = 1.0f;
            m_ForwardSpeed = 1.0f;
        }
        else if (position == Position.Striker)
        {
            m_LateralSpeed = 0.3f;
            m_ForwardSpeed = 1.3f;
        }
        else
        {
            m_LateralSpeed = 0.3f;
            m_ForwardSpeed = 1.0f;
        }

        m_SoccerSettings = FindFirstObjectByType<SoccerSettings>();
        agentRb = GetComponent<Rigidbody>();
        agentRb.maxAngularVelocity = 500;
        m_ResetParams = Academy.Instance.EnvironmentParameters;

        // 🆕 找到关键物体的 Transform
        CacheReferences();
    }

    /// <summary>
    /// 缓存场景引用 (球、球门等)
    /// </summary>
    private void CacheReferences()
    {
        // 球
        var ball = GameObject.FindGameObjectWithTag("ball");
        if (ball != null)
        {
            m_BallTransform = ball.transform;
            m_BallRb = ball.GetComponent<Rigidbody>();
        }

        // 球门 — 根据 tag 查找
        // SoccerBallController 使用 purpleGoalTag / blueGoalTag
        var purpleGoal = GameObject.FindGameObjectWithTag("purpleGoal");
        var blueGoal = GameObject.FindGameObjectWithTag("blueGoal");

        if (team == Team.Blue)
        {
            m_OwnGoal = purpleGoal?.transform;   // 蓝方被 PurpleGoal 得分
            m_OppGoal = blueGoal?.transform;      // 蓝方攻击 BlueGoal
        }
        else
        {
            m_OwnGoal = blueGoal?.transform;
            m_OppGoal = purpleGoal?.transform;
        }

        // 🆕 初始化距离追踪变量，防止首帧 FixedUpdate 产生巨大奖励跳变
        if (m_BallTransform != null)
            m_LastDistToBall = Vector3.Distance(transform.position, m_BallTransform.position);
        if (m_OwnGoal != null)
            m_LastDistToOwnGoal = Vector3.Distance(transform.position, m_OwnGoal.position);
    }

    // ============================================================
    // 🆕 每帧奖励塑形 (Dense Reward Shaping) — v2.2 增强版
    // ============================================================
    private void FixedUpdate()
    {
        if (m_BallTransform == null) return;

        // ─── 1. 距离奖励：鼓励靠近球 (Dense) ───
        float distToBall = Vector3.Distance(transform.position, m_BallTransform.position);
        float ballApproachDelta = m_LastDistToBall - distToBall; // 正数=靠近
        float ballApproachReward = ballApproachDelta * 0.02f;    // 0.015→0.02 加强靠近引导
        AddReward(Mathf.Clamp(ballApproachReward, -0.03f, 0.03f));
        m_LastDistToBall = distToBall;

        // ─── 2. 移动奖励：鼓励活跃，惩罚静止 (Dense) ───
        float moveSpeed = agentRb.linearVelocity.magnitude;
        float moveReward = moveSpeed * 0.0005f; // 0.0003→0.0005 更鼓励移动
        AddReward(moveReward);

        // 检测卡住 (超过 3 秒几乎不动)
        float movedDist = Vector3.Distance(transform.position, m_LastPosition);
        if (movedDist < 0.01f)
            m_StuckTime += Time.fixedDeltaTime;
        else
            m_StuckTime = 0f;
        if (m_StuckTime > 3.0f)
            AddReward(-0.008f); // -0.005→-0.008 加大卡住惩罚
        m_LastPosition = transform.position;

        // ─── 3. 面朝奖励：鼓励面向球 (Dense) ───
        Vector3 dirToBall = (m_BallTransform.position - transform.position).normalized;
        float facingBall = Vector3.Dot(transform.forward, dirToBall);
        float facingReward = (facingBall - 0.3f) * 0.008f; // 0.005→0.008 加强朝向引导
        AddReward(Mathf.Max(0, facingReward));

        // ─── 4. 球权/控球奖励 (Semi-Dense) ───
        m_TimeWithoutBall += Time.fixedDeltaTime;
        if (distToBall < 2.0f)
        {
            m_TimeWithoutBall = 0f;
            AddReward(0.005f); // 0.003→0.005 加大控球奖励
        }
        else
        {
            if (m_TimeWithoutBall > 10.0f)
                AddReward(-0.003f); // -0.002→-0.003 加大远离球惩罚
        }

        // ─── 5. 边界意识：惩罚靠近边线 (Dense) 🆕 ───
        // 球场约 ±20 范围，超出 ±18 视为危险区域
        float boundaryLimit = 18f;
        float distToBoundaryX = boundaryLimit - Mathf.Abs(transform.position.x);
        float distToBoundaryZ = boundaryLimit - Mathf.Abs(transform.position.z);
        float minDistToBoundary = Mathf.Min(distToBoundaryX, distToBoundaryZ);

        if (minDistToBoundary < 3f && minDistToBoundary > 0f)
        {
            // 越靠近边界惩罚越大 (3 单位内开始惩罚)
            float boundaryPenalty = (1f - minDistToBoundary / 3f) * -0.003f;
            AddReward(boundaryPenalty);
        }
        else if (minDistToBoundary <= 0f)
        {
            // 已经在边界外 — 强惩罚
            AddReward(-0.01f);
        }

        // ─── 6. 位置策略奖励 ───

        if (position == Position.Striker)
        {
            // Striker: 鼓励推向对方半场
            if (m_OppGoal != null)
            {
                float distToOppGoal = Vector3.Distance(transform.position, m_OppGoal.position);
                AddReward(-distToOppGoal * 0.00015f); // 0.0001→0.00015 加强前压引导
            }
        }
        else if (position == Position.Goalie)
        {
            // Goalie: 鼓励站在球和己方球门之间 (防守站位)
            if (m_OwnGoal != null)
            {
                float distToOwnGoal = Vector3.Distance(transform.position, m_OwnGoal.position);
                // 理想距离：离球门 2-5 单位
                if (distToOwnGoal > 1.5f && distToOwnGoal < 5.0f)
                    AddReward(0.0015f); // 0.001→0.0015
                else if (distToOwnGoal > 8.0f)
                    AddReward(-0.005f); // -0.003→-0.005 加大失位惩罚
            }
        }
    }

    // ============================================================
    // 动作处理
    // ============================================================

    public void MoveAgent(ActionSegment<int> act)
    {
        var dirToGo = Vector3.zero;
        var rotateDir = Vector3.zero;

        m_KickPower = 0f;

        var forwardAxis = act[0];
        var rightAxis = act[1];
        var rotateAxis = act[2];

        switch (forwardAxis)
        {
            case 1:
                dirToGo += transform.forward * m_ForwardSpeed;
                m_KickPower = 1f;
                break;
            case 2:
                dirToGo += transform.forward * -m_ForwardSpeed;
                break;
        }

        switch (rightAxis)
        {
            case 1:
                dirToGo += transform.right * m_LateralSpeed;
                break;
            case 2:
                dirToGo += transform.right * -m_LateralSpeed;
                break;
        }

        switch (rotateAxis)
        {
            case 1:
                rotateDir = transform.up * -1f;
                break;
            case 2:
                rotateDir = transform.up * 1f;
                break;
        }

        transform.Rotate(rotateDir, Time.deltaTime * 100f);
        var speed = m_SoccerSettings != null ? m_SoccerSettings.agentRunSpeed : 2.0f;
        agentRb.AddForce(dirToGo * speed, ForceMode.VelocityChange);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        // 🆕 保留轻微的生存激励，但大幅降低权重
        if (position == Position.Goalie)
            AddReward(m_Existential * 0.3f);
        // Striker 不再惩罚 — FixedUpdate 的稠密奖励已经提供足够引导

        MoveAgent(actionBuffers.DiscreteActions);

        // 🆕 通过 SideChannel 发送步级数据到 Python → Redis
        SendStepDataToSideChannel(actionBuffers.DiscreteActions);
    }

    /// <summary>
    /// 通过 SideChannel 发送 (s,a,r) 到 Python 端 Redis。
    /// 频率: ~50次/秒 (FixedUpdate)，性能影响 <0.1ms。
    /// </summary>
    private void SendStepDataToSideChannel(ActionSegment<int> actions)
    {
        var channel = SoccerStepSideChannel.Instance;
        if (channel == null) return;

        // 获取当前步的即时奖励 (FixedUpdate 每帧累积的)
        // 注意: ML-Agents 的 GetCumulativeReward 返回回合累积值
        float stepReward = GetCumulativeReward();

        channel.SendStep(
            episodeId: CompletedEpisodes,
            step: StepCount,
            reward: stepReward,
            actions: new int[] { actions[0], actions[1], actions[2] },
            agentPosition: transform.position,
            team: (int)team,
            position: (int)position
        );
    }

    // ============================================================
    // 🆕 增强观察空间 (补充 RayPerceptionSensor 无法提供的信息)
    // ============================================================
    public override void CollectObservations(Unity.MLAgents.Sensors.VectorSensor sensor)
    {
        if (sensor == null) return; // VectorObservationSize 为 0 时 sensor 为 null

        // ── 球相关信息 ──
        if (m_BallTransform != null && m_BallRb != null)
        {
            // 球相对于 Agent 的速度 (3 维)
            Vector3 relBallVel = transform.InverseTransformVector(m_BallRb.linearVelocity);
            sensor.AddObservation(relBallVel);

            // 球到对方球门的距离 — 进攻机会评估 (1 维)
            if (m_OppGoal != null)
                sensor.AddObservation(
                    Vector3.Distance(m_BallTransform.position, m_OppGoal.position));
            else
                sensor.AddObservation(0f);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
        }

        // ── Agent 自身状态 ──
        // 当前速度大小 (1 维)
        sensor.AddObservation(agentRb.linearVelocity.magnitude);

        // 面朝球的程度 — 视觉注意力 (1 维)
        if (m_BallTransform != null)
        {
            Vector3 dirToBall = (m_BallTransform.position - transform.position).normalized;
            sensor.AddObservation(Vector3.Dot(transform.forward, dirToBall));
        }
        else
        {
            sensor.AddObservation(0f);
        }

        // ── 位置信息 — 球场归一化坐标 (2 维) ──
        sensor.AddObservation(transform.position.x / 20f);
        sensor.AddObservation(transform.position.z / 20f);

        // ── 队伍身份 one-hot (2 维) ──
        sensor.AddObservation(team == Team.Blue ? 1f : 0f);
        sensor.AddObservation(team == Team.Purple ? 1f : 0f);

        // 总计: 3 + 1 + 1 + 1 + 2 + 2 = 10 维额外向量观察
    }

    // ============================================================
    // 人类操控 (保持不变)
    // ============================================================

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        discreteActionsOut[0] = 0;
        discreteActionsOut[1] = 0;
        discreteActionsOut[2] = 0;

        if (!isHumanControlled)
            return;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null)
            return;

        switch (playerSlot)
        {
            case HumanSlot.P1:
                if (kb.wKey.isPressed) discreteActionsOut[0] = 1;
                if (kb.sKey.isPressed) discreteActionsOut[0] = 2;
                if (kb.aKey.isPressed) discreteActionsOut[2] = 1;
                if (kb.dKey.isPressed) discreteActionsOut[2] = 2;
                break;

            case HumanSlot.P2:
                if (kb.upArrowKey.isPressed)    discreteActionsOut[0] = 1;
                if (kb.downArrowKey.isPressed)  discreteActionsOut[0] = 2;
                if (kb.leftArrowKey.isPressed)  discreteActionsOut[2] = 1;
                if (kb.rightArrowKey.isPressed) discreteActionsOut[2] = 2;
                break;
        }
#else
        if (Input.GetKey(KeyCode.W)) discreteActionsOut[0] = 1;
        if (Input.GetKey(KeyCode.S)) discreteActionsOut[0] = 2;
        if (Input.GetKey(KeyCode.A)) discreteActionsOut[2] = 1;
        if (Input.GetKey(KeyCode.D)) discreteActionsOut[2] = 2;
#endif
    }

    // ============================================================
    // 🆕 碰撞奖励 (增强版 v2.2)
    // ============================================================

    void OnCollisionEnter(Collision c)
    {
        // ─── 触球奖励 ───
        if (c.gameObject.CompareTag("ball"))
        {
            m_BallTouchCount++;

            // 🆕 拦截/传球奖励：必须在更新球权之前读取上一次触球者
            if (m_EnvController != null)
            {
                int prevToucher = m_EnvController.LastBallTouchedByTeam;
                // 上次触球是对方，这次是我们 → 成功拦截！
                if (prevToucher >= 0 && prevToucher != (int)team)
                {
                    AddReward(0.25f);
                }
                // 上次触球是队友，这次是我们 → 传球配合
                else if (prevToucher == (int)team && m_BallTouchCount > 1)
                {
                    AddReward(0.08f);
                }
            }

            // 🆕 更新环境级球权追踪 (用于出界惩罚) — 必须在拦截检测之后设置
            if (m_EnvController != null)
                m_EnvController.LastBallTouchedByTeam = (int)team;

            // 基础触球奖励
            float baseReward = 0.2f * m_BallTouch;
            AddReward(baseReward);

            // 🆕 踢球方向质量奖励
            Vector3 kickDir = (c.contacts[0].point - transform.position).normalized;

            if (m_OppGoal != null)
            {
                Vector3 dirToOppGoal = (m_OppGoal.position - c.transform.position).normalized;
                float shotAlignment = Vector3.Dot(kickDir, dirToOppGoal);

                if (shotAlignment > 0.5f)
                {
                    // 球踢向对方球门方向 → 高质量射门/传球
                    AddReward(0.4f * shotAlignment); // 0.3→0.4 加强射门奖励
                }
                else if (shotAlignment < -0.3f)
                {
                    // 球踢向己方球门方向 → 乌龙风险!
                    AddReward(-0.25f); // -0.2→-0.25 加大乌龙惩罚
                }
            }

            // 🆕 踢球力度奖励 (鼓励大力射门)
            float kickForce = k_Power * m_KickPower;
            if (position == Position.Goalie) kickForce = k_Power;
            AddReward(kickForce * 0.00002f); // ~0.04 for 2000 force

            // 🆕 标记球权归属
            m_LastTouchedByTeam = (int)team;

            // 施加物理力
            var force = kickForce;
            var dir = kickDir;
            c.gameObject.GetComponent<Rigidbody>().AddForce(dir * force);
        }

        // ─── 墙壁惩罚 (Agent 撞墙) 🆕 增强 ───
        if (c.gameObject.CompareTag("wall"))
        {
            AddReward(-0.15f); // -0.1→-0.15 加大撞墙惩罚
        }

        // ─── 队友碰撞微惩罚 (避免拥挤) ───
        var otherAgent = c.gameObject.GetComponent<AgentSoccer>();
        if (otherAgent != null && otherAgent.team == team)
        {
            AddReward(-0.03f); // -0.02→-0.03
        }
    }

    // ============================================================
    // 🆕 碰撞停留 (持续接触检测) — 带球奖励
    // ============================================================
    void OnCollisionStay(Collision c)
    {
        // 持续控球: 稳定带球额外奖励
        if (c.gameObject.CompareTag("ball") && position == Position.Striker)
        {
            AddReward(0.0015f); // 0.001→0.0015 每帧, ~0.075/秒
        }
    }

    // ============================================================
    // 回合重置
    // ============================================================

    public override void OnEpisodeBegin()
    {
        // 环境参数
        m_BallTouch = m_ResetParams.GetWithDefault("ball_touch", 1.0f);

        // 🆕 重置追踪变量
        if (m_BallTransform != null)
            m_LastDistToBall = Vector3.Distance(transform.position, m_BallTransform.position);
        if (m_OwnGoal != null)
            m_LastDistToOwnGoal = Vector3.Distance(transform.position, m_OwnGoal.position);

        m_BallTouchCount = 0;
        m_TimeWithoutBall = 0f;
        m_StuckTime = 0f;
        m_LastTouchedByTeam = -1;
        m_LastPosition = transform.position;

        // 🆕 重新缓存引用 (场景重置后引用可能丢失)
        if (m_BallTransform == null || m_OppGoal == null)
            CacheReferences();
    }
}
