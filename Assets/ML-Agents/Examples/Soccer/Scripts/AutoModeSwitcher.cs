using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;

/// <summary>
/// 自动切换训练/游戏模式。
/// 有训练器连接 → 全部 Default (Self-Play)
/// 无训练器连接 → Blue HeuristicOnly (人类控制), Purple Default (AI)
/// </summary>
public class AutoModeSwitcher : MonoBehaviour
{
    [Header("人类控制的 Agent")]
    public AgentSoccer[] humanAgents;

    [Header("游戏速度")]
    public float trainingTimeScale = 20f;
    public float gameTimeScale = 1f;

    private bool m_WasTraining = false;

    void Start()
    {
        ApplyMode();
    }

    void Update()
    {
        // 每秒检测一次模式变化
        if (Time.frameCount % 60 == 0)
        {
            bool isTraining = Academy.Instance.IsCommunicatorOn;
            if (isTraining != m_WasTraining)
            {
                ApplyMode();
            }
            m_WasTraining = isTraining;
        }
    }

    public void ApplyMode()
    {
        bool isTraining = Academy.Instance.IsCommunicatorOn;

        if (isTraining)
        {
            // 训练模式: 全部 Default, 20x 速度
            Time.timeScale = trainingTimeScale;
            foreach (var agent in humanAgents)
            {
                if (agent != null)
                {
                    var bp = agent.GetComponent<BehaviorParameters>();
                    if (bp != null) bp.BehaviorType = BehaviorType.Default;
                    agent.isHumanControlled = false;
                }
            }
            Debug.Log("[AutoMode] 训练模式: 全部 Default, timeScale=" + trainingTimeScale);
        }
        else
        {
            // 游戏模式: Blue HeuristicOnly, Purple Default, 1x 速度
            Time.timeScale = gameTimeScale;
            foreach (var agent in humanAgents)
            {
                if (agent != null)
                {
                    var bp = agent.GetComponent<BehaviorParameters>();
                    if (bp != null) bp.BehaviorType = BehaviorType.HeuristicOnly;
                    agent.isHumanControlled = true;
                }
            }
            Debug.Log("[AutoMode] 游戏模式: Blue HeuristicOnly, timeScale=" + gameTimeScale);
        }
    }
}
