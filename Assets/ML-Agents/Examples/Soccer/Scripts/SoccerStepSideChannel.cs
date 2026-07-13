// ================================================================
// Soccer 步级数据 SideChannel (Unity C# 端)
// ================================================================
// 每步将 (状态-动作-奖励) 通过 SideChannel 发送到 Python，
// Python 端再写入 Redis，实现实时训练日志。
//
// UUID 必须与 Python 端 rlsoccer/side_channel.py 一致:
//   8b7d5e3a-1c2f-4d6e-9a8b-3c4d5e6f7a8b
//
// 使用方式:
//   1. 将此脚本挂载到场景中的任意 GameObject
//   2. 在 AgentSoccer.OnActionReceived 中调用:
//        SoccerStepSideChannel.SendStep(episodeId, step, reward, actions, pos, team, posEnum);
// ================================================================

using System;
using Unity.MLAgents.SideChannels;
using UnityEngine;

/// <summary>
/// 通过 SideChannel 将每一步的训练数据发送到 Python，再由 Python 写入 Redis。
/// 这是 ML-Agents 原生的高性能通信通道 (gRPC, <5ms)。
/// </summary>
public class SoccerStepSideChannel : SideChannel
{
    // ⚠️ 必须与 Python 端 SoccerStepChannel 的 UUID 完全一致
    private static readonly Guid k_ChannelId =
        new Guid("8b7d5e3a-1c2f-4d6e-9a8b-3c4d5e6f7a8b");

    /// <summary>单例</summary>
    public static SoccerStepSideChannel Instance { get; private set; }

    /// <summary>已发送的总步数</summary>
    public int TotalStepsSent { get; private set; }

    private bool m_Enabled = true;

    public SoccerStepSideChannel()
    {
        ChannelId = k_ChannelId;
        Instance = this;
    }

    /// <summary>
    /// 发送一步的训练数据。
    /// 建议在 Agent.OnActionReceived() 末尾调用。
    /// </summary>
    public void SendStep(
        int episodeId,
        int step,
        float reward,
        int[] actions,
        Vector3 agentPosition,
        int team,
        int position
    )
    {
        if (!m_Enabled) return;

        using (var msg = new OutgoingMessage())
        {
            msg.WriteInt32(episodeId);
            msg.WriteInt32(step);
            msg.WriteFloat32(reward);
            msg.WriteInt32(actions.Length > 0 ? actions[0] : 0);
            msg.WriteInt32(actions.Length > 1 ? actions[1] : 0);
            msg.WriteInt32(actions.Length > 2 ? actions[2] : 0);
            msg.WriteFloat32(agentPosition.x);
            msg.WriteFloat32(agentPosition.z);
            msg.WriteInt32(team);
            msg.WriteInt32(position);

            QueueMessageToSend(msg);
            TotalStepsSent++;
        }
    }

    /// <summary>
    /// 发送回合摘要。
    /// </summary>
    public void SendEpisodeSummary(
        int episodeId,
        float totalReward,
        int steps,
        int goalsScored,
        int goalsConceded
    )
    {
        if (!m_Enabled) return;

        using (var msg = new OutgoingMessage())
        {
            msg.WriteInt32(-1); // 标记: 回合摘要 (episodeId = -1)
            msg.WriteInt32(episodeId);
            msg.WriteFloat32(totalReward);
            msg.WriteInt32(steps);
            msg.WriteInt32(goalsScored);
            msg.WriteInt32(goalsConceded);
            // padding
            msg.WriteInt32(0);
            msg.WriteInt32(0);
            msg.WriteInt32(0);
            msg.WriteFloat32(0f);
            msg.WriteFloat32(0f);

            QueueMessageToSend(msg);
        }
    }

    /// <summary>
    /// 启用/禁用发送 (训练时可关闭节省带宽)
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        m_Enabled = enabled;
    }

    /// <summary>
    /// 接收 Python 发来的消息 (当前不需要，预留)
    /// </summary>
    protected override void OnMessageReceived(IncomingMessage msg)
    {
        // Python → Unity: 可用于接收推理动作 (远程推理模式)
        // 当前不需要，预留接口
    }
}
