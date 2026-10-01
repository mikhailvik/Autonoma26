using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;


public class DroneAgentLevel1 : Agent
{
    // Target used for Level 1 training.
    [SerializeField] private Transform droneTarget;

    // Distance from DroneTarget that counts as success.
    [SerializeField] private float successDistance = 3.0f;

    // Reward for making progress toward the target.
    [SerializeField] private float distanceRewardScale = 0.05f;


    // Fixed starting position for Level 1.
    private Vector3 startingPosition;
    private Quaternion startingRotation;

    private Rigidbody body;
    private DroneController droneController;

    private float previousGoalDistance;


    private void Awake()
    {
        // Save the fixed Level 1 starting position.
        startingPosition = transform.position;
        startingRotation = transform.rotation;

        body = GetComponent<Rigidbody>();
        droneController = GetComponent<DroneController>();

        // Maximum length of one training episode.
        MaxStep = 5000;
    }


    public override void OnEpisodeBegin()
    {
        // Reset the drone when a new training episode begins.
        ResetDrone();

        previousGoalDistance = GoalDistance();
    }


    private void ResetDrone()
    {
        // Return the drone to its fixed Level 1 starting position.
        transform.SetPositionAndRotation(
            startingPosition,
            startingRotation);

        // Remove velocity from the previous episode.
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        // Remove previous movement commands.
        if (droneController != null)
        {
            droneController.ClearAgentControls();
        }
    }


    public override void CollectObservations(VectorSensor sensor)
    {
        if (droneTarget == null)
        {
            // Keep the observation size constant even if
            // DroneTarget has not been assigned.
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(Vector3.zero);
            return;
        }

        // Relative direction from the drone to DroneTarget.
        // Local coordinates are used so the agent does not
        // depend on absolute world coordinates.
        Vector3 relativeTarget =
            transform.InverseTransformDirection(
                droneTarget.position - transform.position);

        sensor.AddObservation(relativeTarget);


        // Drone velocity in local coordinates.
        Vector3 localVelocity =
            transform.InverseTransformDirection(
                body.linearVelocity);

        sensor.AddObservation(localVelocity);
    }


    public override void Heuristic(
        in ActionBuffers actionsOut)
    {
        // Manual controls for testing the agent before training.
        var continuousActions =
            actionsOut.ContinuousActions;

        continuousActions[0] = 0.0f;
        continuousActions[1] = 0.0f;
        continuousActions[2] = 0.0f;

        if (Keyboard.current == null)
        {
            return;
        }


        // Action 0:
        // W = forward
        // S = backward
        if (Keyboard.current.wKey.isPressed)
        {
            continuousActions[0] = 1.0f;
        }
        else if (Keyboard.current.sKey.isPressed)
        {
            continuousActions[0] = -1.0f;
        }


        // Action 1:
        // A = left
        // D = right
        if (Keyboard.current.aKey.isPressed)
        {
            continuousActions[1] = -1.0f;
        }
        else if (Keyboard.current.dKey.isPressed)
        {
            continuousActions[1] = 1.0f;
        }


        // Action 2:
        // R = up
        // F = down
        if (Keyboard.current.rKey.isPressed)
        {
            continuousActions[2] = 1.0f;
        }
        else if (Keyboard.current.fKey.isPressed)
        {
            continuousActions[2] = -1.0f;
        }
    }


    public override void OnActionReceived(
        ActionBuffers actions)
    {
        float forward =
            actions.ContinuousActions[0];

        float sideways =
            actions.ContinuousActions[1];

        float vertical =
            actions.ContinuousActions[2];


        // Send the ML-Agent actions to the original
        // DroneController physics system.
        droneController.SetAgentControls(
            forward,
            sideways,
            vertical);


        // Small time penalty encourages the agent to reach
        // the target without dominating the progress reward.
        AddReward(-0.0001f);


        float currentGoalDistance =
            GoalDistance();


        // Reward the drone for moving closer to DroneTarget.
        // Moving away produces a negative reward.
        float distanceImprovement =
            previousGoalDistance -
            currentGoalDistance;

        AddReward(
            distanceImprovement *
            distanceRewardScale);

        previousGoalDistance =
            currentGoalDistance;


        // Level 1 success:
        // reach the fixed DroneTarget.
        if (currentGoalDistance <= successDistance)
        {
            AddReward(1.0f);

            Debug.Log(
                "SUCCESS - Drone reached DroneTarget. Distance: "
                + currentGoalDistance);

            EndEpisode();
        }


        // Safety condition:
        // stop an episode if the drone flies too far away vertically.
        if (transform.position.y < -5.0f ||
            transform.position.y > 150.0f)
        {
            AddReward(-1.0f);

            EndEpisode();
        }
    }


    private float GoalDistance()
    {
        if (droneTarget == null)
        {
            return 0.0f;
        }

        return Vector3.Distance(
            transform.position,
            droneTarget.position);
    }
}