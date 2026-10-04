using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

public class DroneAgentLevel2 : Agent
{
    [SerializeField] private Transform droneTarget;
    //Added for Level 2
    [SerializeField] private Transform meetingPoint;


    [Header("Level 2 Random Start")]
    [SerializeField] private float randomRangeX = 15.0f;
    [SerializeField] private float randomRangeZ = 15.0f;

    [Header("Level 2 Random Meeting Point")]
    [SerializeField] private float meetingPointRangeX = 15.0f;
    [SerializeField] private float meetingPointRangeZ = 15.0f;


    [SerializeField] private float successDistance = 3.0f;
    [SerializeField] private float distanceRewardScale = 0.05f;

    private Vector3 startingPosition;
    private Quaternion startingRotation;
    //Added for Level 2
    private Vector3 meetingPointStartingPosition;

    private Rigidbody body;
    private DroneController droneController;

    private float previousGoalDistance;

    private void Awake()
    {
        startingPosition = transform.position;
        startingRotation = transform.rotation;
        //Added for Level 2
        if (meetingPoint != null)
        {
            meetingPointStartingPosition = meetingPoint.position;
        }

        body = GetComponent<Rigidbody>();

        droneController = GetComponent<DroneController>();

        MaxStep = 5000;
    }

    public override void OnEpisodeBegin()
    {
        RandomizeMeetingPoint();
        ResetDrone();

        previousGoalDistance = GoalDistance();
    }

    private void RandomizeMeetingPoint()
    {
        if (meetingPoint == null)
            return;

        float randomX =
            Random.Range(-meetingPointRangeX, meetingPointRangeX);

        float randomZ =
            Random.Range(-meetingPointRangeZ, meetingPointRangeZ);

        meetingPoint.position = new Vector3(
            meetingPointStartingPosition.x + randomX,
            meetingPointStartingPosition.y,
            meetingPointStartingPosition.z + randomZ
        );
    }

    private void ResetDrone()
    {
        // Level 2:
        // Start every episode from a random position
        // around the original starting point.
        float randomX = Random.Range(-randomRangeX, randomRangeX);
        float randomZ = Random.Range(-randomRangeZ, randomRangeZ);

        Vector3 randomStartPosition = new Vector3(
            startingPosition.x + randomX,
            startingPosition.y,
            startingPosition.z + randomZ
        );

        transform.SetPositionAndRotation(
            randomStartPosition,
            startingRotation
        );

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        if (droneController != null)
        {
            droneController.ClearAgentControls();
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (droneTarget == null)
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(Vector3.zero);
            return;
        }

        // Relative target position.
        // This is important for Level 2 because
        // the starting position can now change.
        Vector3 relativeTarget =
            transform.InverseTransformDirection(
                droneTarget.position - transform.position
            );

        sensor.AddObservation(relativeTarget);

        // Local drone velocity.
        Vector3 localVelocity =
            transform.InverseTransformDirection(body.linearVelocity);

        sensor.AddObservation(localVelocity);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;

        continuousActions[0] = 0.0f;
        continuousActions[1] = 0.0f;
        continuousActions[2] = 0.0f;

        if (Keyboard.current == null)
            return;

        // Forward / backward.
        if (Keyboard.current.wKey.isPressed)
            continuousActions[0] = 1.0f;
        else if (Keyboard.current.sKey.isPressed)
            continuousActions[0] = -1.0f;

        // Left / right.
        if (Keyboard.current.aKey.isPressed)
            continuousActions[1] = -1.0f;
        else if (Keyboard.current.dKey.isPressed)
            continuousActions[1] = 1.0f;

        // Up / down.
        if (Keyboard.current.rKey.isPressed)
            continuousActions[2] = 1.0f;
        else if (Keyboard.current.fKey.isPressed)
            continuousActions[2] = -1.0f;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float forward = actions.ContinuousActions[0];
        float sideways = actions.ContinuousActions[1];
        float vertical = actions.ContinuousActions[2];

        droneController.SetAgentControls(
            forward,
            sideways,
            vertical
        );

        // Small time penalty.
        AddReward(-0.0001f);

        // Reward the drone for getting closer to the target.
        float currentGoalDistance = GoalDistance();

        float distanceImprovement =
            previousGoalDistance - currentGoalDistance;

        AddReward(
            distanceImprovement * distanceRewardScale
        );

        previousGoalDistance = currentGoalDistance;

        // Success.
        if (currentGoalDistance <= successDistance)
        {
            AddReward(1.0f);

            Debug.Log(
                "LEVEL 2 SUCCESS - Drone reached DroneTarget. Distance: "
                + currentGoalDistance
            );

            EndEpisode();
        }

        // End the episode if the drone flies too low or too high.
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
            return 0.0f;

        return Vector3.Distance(
            transform.position,
            droneTarget.position
        );
    }
}