
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

public class DroneAgentLevel4Training : Agent
{
    [Header("Targets and Terrain")]
    [SerializeField] private Transform droneTarget;
    [SerializeField] private Transform meetingPoint;
    [SerializeField] private Terrain terrain;

    [Header("Random Start")]
    [SerializeField] private float randomRangeX = 15f;
    [SerializeField] private float randomRangeZ = 15f;

    [Header("Random Meeting Point")]
    [SerializeField] private float meetingPointRangeX = 15f;
    [SerializeField] private float meetingPointRangeZ = 15f;

    [Header("Navigation")]
    [SerializeField] private float droneHeightAboveTerrain = 8f;
    [SerializeField] private float successDistance = 3f;
    [SerializeField] private float distanceRewardScale = 0.05f;

    [Header("Obstacle Detection")]
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private float detectionDistance = 25f;
    [SerializeField] private float detectionRadius = 2f;
    [SerializeField] private float clearanceHeight = 5f;

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
            meetingPointStartingPosition = meetingPoint.position;

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

        float x = meetingPointStartingPosition.x +
            Random.Range(-meetingPointRangeX, meetingPointRangeX);

        float z = meetingPointStartingPosition.z +
            Random.Range(-meetingPointRangeZ, meetingPointRangeZ);

        float ground = terrain.SampleHeight(
            new Vector3(x, 0f, z)
        ) + terrain.transform.position.y;

        float originalGround =
            terrain.SampleHeight(meetingPointStartingPosition) +
            terrain.transform.position.y;

        float offset = meetingPointStartingPosition.y - originalGround;

        meetingPoint.position = new Vector3(x, ground + offset, z);
    }

    private void ResetDrone()
    {
        float x = startingPosition.x +
            Random.Range(-randomRangeX, randomRangeX);

        float z = startingPosition.z +
            Random.Range(-randomRangeZ, randomRangeZ);

        float ground = terrain != null
            ? terrain.SampleHeight(new Vector3(x, 0f, z)) +
              terrain.transform.position.y
            : startingPosition.y;

        transform.SetPositionAndRotation(
            new Vector3(x, ground + droneHeightAboveTerrain, z),
            startingRotation
        );

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        if (droneController != null)
            droneController.ClearAgentControls();
    }

    // Exactly six vector observations, compatible with Level 3.
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
        var controls = actionsOut.ContinuousActions;

        controls[0] = 0f;
        controls[1] = 0f;
        controls[2] = 0f;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.wKey.isPressed) controls[0] = 1f;
        if (Keyboard.current.sKey.isPressed) controls[0] = -1f;

        if (Keyboard.current.aKey.isPressed) controls[1] = -1f;
        if (Keyboard.current.dKey.isPressed) controls[1] = 1f;

        if (Keyboard.current.rKey.isPressed) controls[2] = 1f;
        if (Keyboard.current.fKey.isPressed) controls[2] = -1f;
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

        AddReward(-0.0002f);

        float currentGoalDistance = GoalDistance();

        AddReward(
            (previousGoalDistance - currentGoalDistance) *
            distanceRewardScale
        );

        previousGoalDistance = currentGoalDistance;
        //Level 4 test slow speed near target
        // Encourage the drone to slow down near the target.
        if (currentGoalDistance < 15f)
        {
            float speed = body.linearVelocity.magnitude;

            if (speed > 3f)
            {
                AddReward(-0.0005f * (speed - 3f));
            }
        }       

        //Level 4 terrain 
        // End the episode if the drone flies outside the terrain.
        if (terrain != null)
        {
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;

            Vector3 dronePosition = transform.position;

            if (dronePosition.x < terrainPosition.x ||
                dronePosition.x > terrainPosition.x + terrainSize.x ||
                dronePosition.z < terrainPosition.z ||
                dronePosition.z > terrainPosition.z + terrainSize.z)
            {
                AddReward(-5f);

                Debug.Log("LEVEL 4 FAILED - Drone left the terrain.");

                EndEpisode();
                return;
            }
        }



        if (currentGoalDistance <= successDistance)
        {
            AddReward(20f);

            Debug.Log(
                "LEVEL 4 SUCCESS - Drone reached DroneTarget. Distance: " +
                currentGoalDistance
            );

            EndEpisode();
            return;
        }

        if (terrain != null)
        {
            float groundHeight =
                terrain.SampleHeight(transform.position) +
                terrain.transform.position.y;

            if (transform.position.y < groundHeight - 2f)
            {
                AddReward(-1f);
                EndEpisode();
                return;
            }
        }

        if (transform.position.y > 150f)
        {
            AddReward(-1f);
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("barrier"))
        {
            AddReward(-5f);

            Debug.Log("LEVEL 4 COLLISION - Drone hit a barrier.");

            EndEpisode();
        }
    }

    private float GoalDistance()
    {
        if (droneTarget == null)
            return 0f;

        return Vector3.Distance(
            transform.position,
            droneTarget.position
        );
    }
}
