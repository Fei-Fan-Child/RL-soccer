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
    // Note that that the detectable tags are different for the blue and purple teams. The order is
    // * ball
    // * own goal
    // * opposing goal
    // * wall
    // * own teammate
    // * opposing player

    public enum Position
    {
        Striker,
        Goalie,
        Generic
    }

    [HideInInspector]
    public Team team;
    float m_KickPower;
    // The coefficient for the reward for colliding with a ball. Set using curriculum.
    float m_BallTouch;
    public Position position;

    public enum HumanSlot
    {
        None = 0,
        P1 = 1,
        P2 = 2
    }

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

    public override void Initialize()
    {
        SoccerEnvController envController = GetComponentInParent<SoccerEnvController>();
        if (envController != null)
        {
            m_Existential = 1f / envController.MaxEnvironmentSteps;
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
    }

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
        agentRb.AddForce(dirToGo * m_SoccerSettings.agentRunSpeed,
            ForceMode.VelocityChange);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)

    {

        if (position == Position.Goalie)
        {
            // Existential bonus for Goalies.
            AddReward(m_Existential);
        }
        else if (position == Position.Striker)
        {
            // Existential penalty for Strikers
            AddReward(-m_Existential);
        }
        MoveAgent(actionBuffers.DiscreteActions);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        // Default all branches to 0 (no-op) every frame.
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
                // P1: W=forward, S=back, A=rotate left, D=rotate right.
                if (kb.wKey.isPressed) discreteActionsOut[0] = 1; // forward
                if (kb.sKey.isPressed) discreteActionsOut[0] = 2; // back
                if (kb.aKey.isPressed) discreteActionsOut[2] = 1; // rotate CCW
                if (kb.dKey.isPressed) discreteActionsOut[2] = 2; // rotate CW
                break;

            case HumanSlot.P2:
                // P2: ↑=forward, ↓=back, ←=rotate left, →=rotate right.
                if (kb.upArrowKey.isPressed)    discreteActionsOut[0] = 1; // forward
                if (kb.downArrowKey.isPressed)  discreteActionsOut[0] = 2; // back
                if (kb.leftArrowKey.isPressed)  discreteActionsOut[2] = 1; // rotate CCW
                if (kb.rightArrowKey.isPressed) discreteActionsOut[2] = 2; // rotate CW
                break;
        }
#else
        // Fallback when the new Input System package isn't available.
        // W=forward, S=back, A=rotate left, D=rotate right.
        if (Input.GetKey(KeyCode.W)) discreteActionsOut[0] = 1;
        if (Input.GetKey(KeyCode.S)) discreteActionsOut[0] = 2;
        if (Input.GetKey(KeyCode.A)) discreteActionsOut[2] = 1;
        if (Input.GetKey(KeyCode.D)) discreteActionsOut[2] = 2;
#endif
    }
    /// <summary>
    /// Used to provide a "kick" to the ball.
    /// </summary>
    void OnCollisionEnter(Collision c)
    {
        var force = k_Power * m_KickPower;
        if (position == Position.Goalie)
        {
            force = k_Power;
        }
        if (c.gameObject.CompareTag("ball"))
        {
            AddReward(.2f * m_BallTouch);
            var dir = c.contacts[0].point - transform.position;
            dir = dir.normalized;
            c.gameObject.GetComponent<Rigidbody>().AddForce(dir * force);
        }
    }

    public override void OnEpisodeBegin()
    {
        m_BallTouch = m_ResetParams.GetWithDefault("ball_touch", 0);
    }

}
