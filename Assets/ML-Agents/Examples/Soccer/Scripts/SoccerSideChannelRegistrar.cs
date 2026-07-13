// ================================================================
// SoccerStepSideChannel 自动注册器
// ================================================================
// 将此脚本挂载到场景中的任意 GameObject (如 SoccerField)，
// 即可在 Unity 启动时自动注册 SideChannel，无需手动编码。
//
// 如果你不想自动注册，也可以在任何地方手动调用:
//   var channel = new SoccerStepSideChannel();
//   SideChannelManager.RegisterSideChannel(channel);
// ================================================================

using Unity.MLAgents.SideChannels;
using UnityEngine;

/// <summary>
/// 在 Awake 时自动创建并注册 SoccerStepSideChannel。
/// 挂到场景中任意一个 GameObject 上即可。
/// </summary>
public class SoccerSideChannelRegistrar : MonoBehaviour
{
    [Tooltip("是否在 Awake 时自动注册 SideChannel")]
    public bool registerOnAwake = true;

    private SoccerStepSideChannel m_Channel;

    void Awake()
    {
        if (registerOnAwake)
        {
            m_Channel = new SoccerStepSideChannel();
            SideChannelManager.RegisterSideChannel(m_Channel);
            Debug.Log("[SideChannel] SoccerStepSideChannel 已注册 (步级数据将发送到 Python → Redis)");
        }
    }

    void OnDestroy()
    {
        if (m_Channel != null)
        {
            SideChannelManager.UnregisterSideChannel(m_Channel);
            Debug.Log($"[SideChannel] 已注销 (共发送 {m_Channel.TotalStepsSent} 步)");
        }
    }
}
