using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

public class DroneAgentLevel3 : Agent
{
    [SerializeField] private Transform droneTarget;
     //Added for Level 2
    [SerializeField] private Transform meetingPoint;
    //Add terrain for Level 3
    [SerializeField] private Terrain terrain;

    [Header("Level 3 Random Start")]
    [SerializeField] private float randomRangeX = 15.0f;
    [SerializeField] private float randomRangeZ = 15.0f;

    [Header("Level 3 Random Meeting Point")]
    [SerializeField] private float meetingPointRangeX = 15.0f;
    [SerializeField] private float meetingPointRangeZ = 15.0f;

// for level 3, we need to ensure the drone maintains a certain height above the terrain, so we add a new serialized field for that.
    [Header("Height Above Terrain")]
    [SerializeField] private float droneHeightAboveTerrain = 8.0f;

    [SerializeField] private float successDistance = 3.0f;
    [SerializeField] private float distanceRewardScale = 0.05f;

    private Vector3 startingPosition;
    private Quaternion startingRotation;
    private Vector3 meetingPointStartingPosition;

    private Rigidbody body;
    private DroneController droneController;

    private float previousGoalDistance;

    private void Awake()
    {
        startingPosition = transform.position;
        startingRotation = transform.rotation;

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
        if (meetingPoint == null || terrain == null)
            return;

        float randomX = Random.Range(
            -meetingPointRangeX,
            meetingPointRangeX
        );

        float randomZ = Random.Range(
            -meetingPointRangeZ,
            meetingPointRangeZ
        );

        float worldX = meetingPointStartingPosition.x + randomX;
        float worldZ = meetingPointStartingPosition.z + randomZ;

        // For level 3, find the generated terrain height at this position.
        float terrainHeight = terrain.SampleHeight(
            new Vector3(worldX, 0.0f, worldZ)
        ) + terrain.transform.position.y;

        // Keep the MeetingPoint base aligned with the terrain and the original Y offset is preserved because DroneTarget is already positioned correctly inside MeetingPoint.
        float originalGroundOffset =
            meetingPointStartingPosition.y -
            terrain.SampleHeight(meetingPointStartingPosition) -
            terrain.transform.position.y;

        meetingPoint.position = new Vector3(
            worldX,
            terrainHeight + originalGroundOffset,
            worldZ
        );
    }

    private void ResetDrone()
    {
        float randomX = Random.Range(-randomRangeX, randomRangeX);
        float randomZ = Random.Range(-randomRangeZ, randomRangeZ);

        float worldX = startingPosition.x + randomX;
        float worldZ = startingPosition.z + randomZ;

        float terrainHeight = startingPosition.y;

        if (terrain != null)
        {
            terrainHeight = terrain.SampleHeight(
                new Vector3(worldX, 0.0f, worldZ)
            ) + terrain.transform.position.y;
        }

        Vector3 randomStartPosition = new Vector3(
            worldX,
            terrainHeight + droneHeightAboveTerrain,
            worldZ
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

        Vector3 relativeTarget =
            transform.InverseTransformDirection(
                droneTarget.position - transform.position
            );

        sensor.AddObservation(relativeTarget);

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

        if (Keyboard.current.wKey.isPressed)
            continuousActions[0] = 1.0f;
        else if (Keyboard.current.sKey.isPressed)
            continuousActions[0] = -1.0f;

        if (Keyboard.current.aKey.isPressed)
            continuousActions[1] = -1.0f;
        else if (Keyboard.current.dKey.isPressed)
            continuousActions[1] = 1.0f;

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

        AddReward(-0.0001f);

        float currentGoalDistance = GoalDistance();

        float distanceImprovement =
            previousGoalDistance - currentGoalDistance;

        AddReward(
            distanceImprovement * distanceRewardScale
        );

        previousGoalDistance = currentGoalDistance;

        if (currentGoalDistance <= successDistance)
        {
            AddReward(1.0f);

            Debug.Log(
                "LEVEL 3 SUCCESS - Drone reached DroneTarget. Distance: "
                + currentGoalDistance
            );

            EndEpisode();
        }

        // Fail if our drone goes below the terrain!!!
        if (terrain != null)
        {
            float groundHeight =
                terrain.SampleHeight(transform.position) +
                terrain.transform.position.y;

            if (transform.position.y < groundHeight - 2.0f)
            {
                AddReward(-1.0f);
                EndEpisode();
            }
        }

        if (transform.position.y > 150.0f)
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