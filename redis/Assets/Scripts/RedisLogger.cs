// ================================================================
// Redis 训练日志客户端 (Unity 端)
// 参照 no20.txt 实训文档 — 第5步: Redis 缓存与可视化
// ================================================================
// 功能:
//   1. 记录每步决策 (状态-动作-奖励) 到 Redis
//   2. 按时间序列存储，TTL=600s
//   3. 热力图数据: Agent 活动轨迹密度
//   4. 决策概率分布推送
//
// 通信方式:
//   方案A (训练时):   Unity → ML-Agents SideChannel → Python → Redis
//   方案B (独立):     Unity → HTTP POST → Python Bridge → Redis (本脚本)
//   方案C (直接):     Unity → StackExchange.Redis → Redis (需 NuGet)
//
// 本实现使用方案B (HTTP Bridge)，因为:
//   - 无需额外 C# Redis 库依赖
//   - 与 Python 训练脚本的 Redis 工具解耦
//   - 易于调试 (可用 curl 测试)
// ================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Redis 日志客户端 — 通过 HTTP Bridge 将训练数据推送到 Redis
/// </summary>
public class RedisLogger : MonoBehaviour
{
    [Header("Redis Bridge 配置")]
    [Tooltip("Python Redis Bridge 地址")]
    public string bridgeUrl = "http://localhost:8000";

    [Tooltip("是否启用日志")]
    public bool enableLogging = true;

    [Tooltip("批量发送: 积累多少步后一次性发送")]
    public int batchSize = 10;

    [Tooltip("发送超时 (秒)")]
    public float timeout = 3f;

    [Header("调试")]
    [Tooltip("打印每次日志内容")]
    public bool verboseLogging = false;

    // 批量缓冲区
    private List<StepLogEntry> m_Buffer = new List<StepLogEntry>();
    private int m_EpisodeId = 0;
    private int m_StepCount = 0;

    // 性能统计
    private int m_TotalLogsSent = 0;
    private int m_FailedLogs = 0;

    public int TotalLogsSent => m_TotalLogsSent;
    public int FailedLogs => m_FailedLogs;


    private void Start()
    {
        StartCoroutine(CheckBridgeHealth());
    }

    /// <summary>
    /// 检查 Redis Bridge 是否可用
    /// </summary>
    private IEnumerator CheckBridgeHealth()
    {
        using (var req = UnityWebRequest.Get($"{bridgeUrl}/health"))
        {
            req.timeout = 3;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                Debug.Log($"[Redis] ✅ Bridge 可用: {bridgeUrl}");
            else
                Debug.LogWarning($"[Redis] ⚠️ Bridge 不可用: {bridgeUrl} — 日志仅本地缓存");
        }
    }

    /// <summary>
    /// 记录一步决策 (状态-动作-奖励)
    /// 在 Agent.OnActionReceived 或 FixedUpdate 中调用
    /// </summary>
    public void LogStep(
        float[] observations,
        int[] actions,
        float reward,
        Vector3 agentPosition,
        int team = 0,
        int position = 0
    )
    {
        if (!enableLogging) return;

        var entry = new StepLogEntry
        {
            episode_id = m_EpisodeId,
            step = m_StepCount++,
            observations = observations,
            actions = actions,
            reward = reward,
            pos_x = agentPosition.x,
            pos_z = agentPosition.z,
            team = team,
            position = position,
        };

        m_Buffer.Add(entry);

        // 批量发送
        if (m_Buffer.Count >= batchSize)
        {
            FlushBuffer();
        }
    }

    /// <summary>
    /// 记录回合摘要
    /// </summary>
    public void LogEpisodeSummary(
        float totalReward,
        int steps,
        int goalsScored = 0,
        int goalsConceded = 0,
        string result = "draw"
    )
    {
        if (!enableLogging) return;

        var summary = new EpisodeSummary
        {
            episode_id = m_EpisodeId,
            total_reward = totalReward,
            steps = steps,
            goals_scored = goalsScored,
            goals_conceded = goalsConceded,
            result = result,
        };

        StartCoroutine(SendEpisodeSummary(summary));

        // 新回合开始
        m_EpisodeId++;
        m_StepCount = 0;
    }

    /// <summary>
    /// 刷新缓冲区 (发送累积的步骤数据)
    /// </summary>
    public void FlushBuffer()
    {
        if (m_Buffer.Count == 0) return;

        var batch = new List<StepLogEntry>(m_Buffer);
        m_Buffer.Clear();
        StartCoroutine(SendBatch(batch));
    }

    // ============================================================
    // HTTP 发送
    // ============================================================

    private IEnumerator SendBatch(List<StepLogEntry> batch)
    {
        var json = SerializeBatch(batch);
        var url = $"{bridgeUrl}/api/log/steps";

        using (var req = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = (int)timeout;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                m_TotalLogsSent += batch.Count;
                if (verboseLogging)
                    Debug.Log($"[Redis] ✅ 发送 {batch.Count} 条日志 (总计: {m_TotalLogsSent})");
            }
            else
            {
                m_FailedLogs += batch.Count;
                Debug.LogWarning($"[Redis] ❌ 发送失败: {req.error}");
            }
        }
    }

    private IEnumerator SendEpisodeSummary(EpisodeSummary summary)
    {
        var json = JsonUtility.ToJson(summary);
        var url = $"{bridgeUrl}/api/log/episode";

        using (var req = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = (int)timeout;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                Debug.Log($"[Redis] 📊 回合 {summary.episode_id} 摘要已记录 " +
                          $"(reward={summary.total_reward:F2}, result={summary.result})");
        }
    }

    /// <summary>
    /// 推送决策概率分布 (用于可视化)
    /// </summary>
    public void LogActionProbabilities(
        int episodeId, int step, float[][] branchProbs
    )
    {
        if (!enableLogging) return;

        var probData = new ActionProbEntry
        {
            episode_id = episodeId,
            step = step,
            branch_0 = branchProbs.Length > 0 ? branchProbs[0] : null,
            branch_1 = branchProbs.Length > 1 ? branchProbs[1] : null,
            branch_2 = branchProbs.Length > 2 ? branchProbs[2] : null,
        };

        StartCoroutine(SendActionProb(probData));
    }

    private IEnumerator SendActionProb(ActionProbEntry entry)
    {
        var json = JsonUtility.ToJson(entry);
        var url = $"{bridgeUrl}/api/log/action_probs";

        using (var req = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = (int)timeout;

            yield return req.SendWebRequest();
        }
    }

    // ============================================================
    // 序列化
    // ============================================================

    private string SerializeBatch(List<StepLogEntry> batch)
    {
        var sb = new StringBuilder();
        sb.Append("{\"steps\":[");
        for (int i = 0; i < batch.Count; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append(SerializeStepEntry(batch[i]));
        }
        sb.Append("]}");
        return sb.ToString();
    }

    private string SerializeStepEntry(StepLogEntry entry)
    {
        var sb = new StringBuilder();
        sb.Append("{");
        sb.Append($"\"episode_id\":{entry.episode_id},");
        sb.Append($"\"step\":{entry.step},");
        sb.Append($"\"reward\":{entry.reward:F6},");
        sb.Append($"\"pos_x\":{entry.pos_x:F4},");
        sb.Append($"\"pos_z\":{entry.pos_z:F4},");
        sb.Append($"\"team\":{entry.team},");
        sb.Append($"\"position\":{entry.position},");

        // actions
        sb.Append("\"actions\":[");
        for (int i = 0; i < entry.actions.Length; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append(entry.actions[i]);
        }
        sb.Append("],");

        // observations (可以很大，只记录摘要)
        sb.Append("\"obs_summary\":{");
        sb.Append($"\"length\":{entry.observations?.Length ?? 0}");
        sb.Append("}");

        sb.Append("}");
        return sb.ToString();
    }

    private void OnDestroy()
    {
        // 确保退出前刷新
        FlushBuffer();
    }

    // ============================================================
    // 数据结构
    // ============================================================

    private class StepLogEntry
    {
        public int episode_id;
        public int step;
        public float[] observations;
        public int[] actions;
        public float reward;
        public float pos_x;
        public float pos_z;
        public int team;
        public int position;
    }

    [System.Serializable]
    private class EpisodeSummary
    {
        public int episode_id;
        public float total_reward;
        public int steps;
        public int goals_scored;
        public int goals_conceded;
        public string result;
    }

    [System.Serializable]
    private class ActionProbEntry
    {
        public int episode_id;
        public int step;
        public float[] branch_0;
        public float[] branch_1;
        public float[] branch_2;
    }
}
