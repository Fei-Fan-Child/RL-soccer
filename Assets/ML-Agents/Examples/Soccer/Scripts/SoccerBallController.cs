using UnityEngine;

public class SoccerBallController : MonoBehaviour
{
    public GameObject area;
    [HideInInspector]
    public SoccerEnvController envController;
    public string purpleGoalTag; //will be used to check if collided with purple goal
    public string blueGoalTag; //will be used to check if collided with blue goal

    void Start()
    {
        envController = area.GetComponent<SoccerEnvController>();
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
            // 🆕 惩罚最后触球的队伍
            if (envController != null)
            {
                envController.BallOutOfBounds();
                envController.ResetScene();
            }
        }
    }
}
