using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;

public class SoccerEnvController : MonoBehaviour
{
    [System.Serializable]
    public class PlayerInfo
    {
        public AgentSoccer Agent;
        [HideInInspector]
        public Vector3 StartingPos;
        [HideInInspector]
        public Quaternion StartingRot;
        [HideInInspector]
        public Rigidbody Rb;
    }


    /// <summary>
    /// Max Academy steps before this platform resets
    /// </summary>
    [Tooltip("Max Environment Steps")] public int MaxEnvironmentSteps = 25000;

    /// <summary>
    /// The area bounds.
    /// </summary>

    /// <summary>
    /// We will be changing the ground material based on success/failue
    /// </summary>

    public GameObject ball;
    [HideInInspector]
    public Rigidbody ballRb;
    Vector3 m_BallStartingPos;

    //List of Agents On Platform
    public List<PlayerInfo> AgentsList = new List<PlayerInfo>();

    private SoccerSettings m_SoccerSettings;
    private EnvironmentParameters m_ResetParams;


    private SimpleMultiAgentGroup m_BlueAgentGroup;
    private SimpleMultiAgentGroup m_PurpleAgentGroup;

    private int m_ResetTimer;
    private int m_BlueScore;
    private int m_PurpleScore;
    private TMPro.TextMeshPro m_ScoreTextWest;
    private TMPro.TextMeshPro m_ScoreTextEast;

    /// <summary>
    /// 最后一次触球的队伍: -1=无人, 0=Blue, 1=Purple。
    /// Agent 触球时设置，球出界时用于惩罚责任方。
    /// </summary>
    [HideInInspector]
    public int LastBallTouchedByTeam = -1;

    void Start()
    {

        m_SoccerSettings = FindFirstObjectByType<SoccerSettings>();
        m_ResetParams = Academy.Instance.EnvironmentParameters;
        // Initialize TeamManager
        m_BlueAgentGroup = new SimpleMultiAgentGroup();
        m_PurpleAgentGroup = new SimpleMultiAgentGroup();

        // Find scoreboard TextMeshPro objects
        var sbWest = GameObject.Find("Score_West");
        if (sbWest != null) m_ScoreTextWest = sbWest.GetComponent<TMPro.TextMeshPro>();
        var sbEast = GameObject.Find("Score_East");
        if (sbEast != null) m_ScoreTextEast = sbEast.GetComponent<TMPro.TextMeshPro>();
        UpdateScoreDisplay();
        ballRb = ball.GetComponent<Rigidbody>();
        m_BallStartingPos = new Vector3(ball.transform.position.x, ball.transform.position.y, ball.transform.position.z);
        foreach (var item in AgentsList)
        {
            item.StartingPos = item.Agent.transform.position;
            item.StartingRot = item.Agent.transform.rotation;
            item.Rb = item.Agent.GetComponent<Rigidbody>();
            if (item.Agent.team == Team.Blue)
            {
                m_BlueAgentGroup.RegisterAgent(item.Agent);
            }
            else
            {
                m_PurpleAgentGroup.RegisterAgent(item.Agent);
            }
        }
        ResetScene();
    }

    void FixedUpdate()
    {
        m_ResetTimer += 1;
        if (m_ResetTimer >= MaxEnvironmentSteps && MaxEnvironmentSteps > 0)
        {
            m_BlueAgentGroup.GroupEpisodeInterrupted();
            m_PurpleAgentGroup.GroupEpisodeInterrupted();
            ResetScene();
        }
    }


    public void ResetBall()
    {
        var randomPosX = Random.Range(-2.5f, 2.5f);
        var randomPosZ = Random.Range(-2.5f, 2.5f);

        ball.transform.position = m_BallStartingPos + new Vector3(randomPosX, 0f, randomPosZ);
        ballRb.linearVelocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;

        // 🆕 课程学习: 根据 curriculum 参数控制球的初始速度
        //    如果 m_ResetParams 尚未初始化 (Academy 未就绪)，跳过球速设置
        var resetParams = m_ResetParams ?? Academy.Instance.EnvironmentParameters;
        if (resetParams != null)
        {
            float ballSpeed = resetParams.GetWithDefault("ball_movement_speed", 5.0f);
            if (ballSpeed > 0.01f)
            {
                Vector3 randomDir = new Vector3(
                    Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
                ballRb.linearVelocity = randomDir * ballSpeed;
            }
        }
    }

    /// <summary>
    /// 🆕 球出界处理: 惩罚最后触球的队伍，奖励对方队伍。
    /// 由 SoccerBallController 在球撞墙时调用。
    /// </summary>
    public void BallOutOfBounds()
    {
        // 只有明确知道谁最后触球才惩罚
        if (LastBallTouchedByTeam < 0) return;

        if (LastBallTouchedByTeam == (int)Team.Blue)
        {
            // 蓝队踢出界 → 蓝队全体受罚，紫队微奖励
            m_BlueAgentGroup.AddGroupReward(-0.15f);
            m_PurpleAgentGroup.AddGroupReward(0.05f);
        }
        else
        {
            // 紫队踢出界 → 紫队全体受罚，蓝队微奖励
            m_PurpleAgentGroup.AddGroupReward(-0.15f);
            m_BlueAgentGroup.AddGroupReward(0.05f);
        }

        // 出界后重置追踪
        LastBallTouchedByTeam = -1;
    }

    /// <summary>
    /// 🆕 给指定队伍全体奖励 (用于四角反弹等局部事件)。
    /// </summary>
    public void AddTeamReward(Team team, float reward)
    {
        if (team == Team.Blue)
            m_BlueAgentGroup.AddGroupReward(reward);
        else
            m_PurpleAgentGroup.AddGroupReward(reward);
    }

    public void GoalTouched(Team scoredTeam)
    {
        if (scoredTeam == Team.Blue)
        {
            m_BlueScore++;
            m_BlueAgentGroup.AddGroupReward(1.5f - (float)m_ResetTimer / MaxEnvironmentSteps);
            m_PurpleAgentGroup.AddGroupReward(-1.5f);
        }
        else
        {
            m_PurpleScore++;
            m_PurpleAgentGroup.AddGroupReward(1 - (float)m_ResetTimer / MaxEnvironmentSteps);
            m_BlueAgentGroup.AddGroupReward(-1);
        }
        UpdateScoreDisplay();

        // 🆕 发送回合摘要到 Python → Redis
        SendEpisodeSummaryToSideChannel(scoredTeam);

        m_PurpleAgentGroup.EndGroupEpisode();
        m_BlueAgentGroup.EndGroupEpisode();
        ResetScene();

    }

    /// <summary>
    /// 通过 SideChannel 发送回合摘要到 Python 端 Redis
    /// </summary>
    private void SendEpisodeSummaryToSideChannel(Team scoredTeam)
    {
        var channel = SoccerStepSideChannel.Instance;
        if (channel == null) return;

        // 找到任意 Agent 获取 episode ID
        int episodeId = 0;
        if (AgentsList.Count > 0 && AgentsList[0].Agent != null)
            episodeId = AgentsList[0].Agent.CompletedEpisodes;

        channel.SendEpisodeSummary(
            episodeId: episodeId,
            totalReward: 0f, // 奖励由各 Agent 独立累计
            steps: m_ResetTimer,
            goalsScored: scoredTeam == Team.Blue ? 1 : 0,
            goalsConceded: scoredTeam == Team.Blue ? 0 : 1
        );
    }

    void UpdateScoreDisplay()
    {
        string scoreText = "Blue: " + m_BlueScore + "  Vs  Purple: " + m_PurpleScore;
        if (m_ScoreTextWest != null)
            m_ScoreTextWest.text = scoreText;
        if (m_ScoreTextEast != null)
            m_ScoreTextEast.text = scoreText;
    }


    public void ResetScene()
    {
        m_ResetTimer = 0;

        //Reset Agents
        foreach (var item in AgentsList)
        {
            // Human-controlled agents keep their current rotation (no random spin).
            if (item.Agent.isHumanControlled)
            {
                var newStartPos = item.Agent.initialPos;
                item.Agent.transform.position = newStartPos;
            }
            else
            {
                var randomPosX = Random.Range(-5f, 5f);
                var newStartPos = item.Agent.initialPos + new Vector3(randomPosX, 0f, 0f);
                var rot = item.Agent.rotSign * Random.Range(80.0f, 100.0f);
                var newRot = Quaternion.Euler(0, rot, 0);
                item.Agent.transform.SetPositionAndRotation(newStartPos, newRot);
            }

            item.Rb.linearVelocity = Vector3.zero;
            item.Rb.angularVelocity = Vector3.zero;
        }

        //Reset Ball
        ResetBall();
    }
}
