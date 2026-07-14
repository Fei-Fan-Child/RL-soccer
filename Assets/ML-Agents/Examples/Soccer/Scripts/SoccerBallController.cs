using UnityEngine;

public class SoccerBallController : MonoBehaviour
{
    public GameObject area;
    [HideInInspector]
    public SoccerEnvController envController;
    public string purpleGoalTag; //will be used to check if collided with purple goal
    public string blueGoalTag; //will be used to check if collided with blue goal

    private Rigidbody m_BallRb;

    [Header("反弹设置")]
    [Tooltip("球撞墙反弹系数 (0~1, 1=完美反弹)")]
    public float wallBounceFactor = 0.92f;

    [Header("四角惩罚")]
    [Tooltip("离场地边缘多少单位内算角落")]
    public float cornerZoneThreshold = 2.0f;
    [Tooltip("球撞入角落时给全队的惩罚")]
    public float cornerPenalty = -0.08f;
    [Tooltip("对方队伍在角落反弹时的微奖励")]
    public float cornerOpponentReward = 0.02f;

    void Start()
    {
        envController = area.GetComponent<SoccerEnvController>();
        m_BallRb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision col)
    {
        if (!string.IsNullOrEmpty(purpleGoalTag) && col.gameObject.CompareTag(purpleGoalTag))
        {
            envController.GoalTouched(Team.Blue);
        }
        if (!string.IsNullOrEmpty(blueGoalTag) && col.gameObject.CompareTag(blueGoalTag))
        {
            envController.GoalTouched(Team.Purple);
        }
        if (col.gameObject.CompareTag("wall"))
        {
            // 反弹
            if (m_BallRb != null)
            {
                Vector3 incomingVelocity = m_BallRb.linearVelocity;
                Vector3 normal = col.contacts[0].normal;
                Vector3 reflectedVelocity = Vector3.Reflect(incomingVelocity, normal);
                m_BallRb.linearVelocity = reflectedVelocity * wallBounceFactor;
            }

            // 四角检测：球在场地边角区域撞墙 → 惩罚最后触球队伍
            if (envController != null)
            {
                CheckCornerPenalty(col.contacts[0].point);
            }
        }
    }

    /// <summary>
    /// 检测碰撞点是否在场地四角区域，若是则惩罚责任队伍。
    /// 角落定义：碰撞点同时靠近 X 和 Z 边界。
    /// </summary>
    private void CheckCornerPenalty(Vector3 hitPoint)
    {
        float absX = Mathf.Abs(hitPoint.x);
        float absZ = Mathf.Abs(hitPoint.z);

        // 需要同时接近两个方向的边界才算角落
        // 场地大约 ±6 范围，用阈值判断
        bool nearXEdge = absX > (6.0f - cornerZoneThreshold);
        bool nearZEdge = absZ > (6.0f - cornerZoneThreshold);

        if (!nearXEdge || !nearZEdge) return;

        int lastTeam = envController.LastBallTouchedByTeam;
        if (lastTeam < 0) return;

        if (lastTeam == (int)Team.Blue)
        {
            envController.AddTeamReward(Team.Blue, cornerPenalty);
            envController.AddTeamReward(Team.Purple, cornerOpponentReward);
        }
        else
        {
            envController.AddTeamReward(Team.Purple, cornerPenalty);
            envController.AddTeamReward(Team.Blue, cornerOpponentReward);
        }
    }
}
