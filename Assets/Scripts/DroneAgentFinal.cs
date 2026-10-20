using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;
using Unity.InferenceEngine;
using Random = UnityEngine.Random;
using UnityEngine;
using UnityEngine.InputSystem;

public class DroneAgentFinal : Agent
{
    [Header("Final Scenario")]
    [SerializeField] private Transform landingPoint;
    [SerializeField] private Rigidbody truckRigidbody;
    [SerializeField] private ModelAsset landingModel;

    [SerializeField] private float truckArrivalDistance = 8f;
    [SerializeField] private float truckStopSpeed = 0.5f;

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
    [SerializeField] private float successDistance = 6f;
    [SerializeField] private float distanceRewardScale = 0.02f;

    [Header("Obstacle Detection")]
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private float detectionRadius = 2f;

    [Header("Level 5 Random Obstacles")]
    [SerializeField] private Level5ObstacleRandomizer obstacleRandomizer;

    private enum DronePhase
    {
        FlyingToMeetingPoint,
        WaitingForTruck,
        Landing,
        Landed
    }

    private DronePhase currentPhase = DronePhase.FlyingToMeetingPoint;

    private Rigidbody body;
    private DroneController droneController;

    private Vector3 startingPosition;
    private Quaternion startingRotation;
    private Vector3 meetingPointStartingPosition;

    private float previousGoalDistance;

    private void Awake()
    {
        startingPosition = transform.position;
        startingRotation = transform.rotation;

        if (meetingPoint != null)
            meetingPointStartingPosition = meetingPoint.position;

        body = GetComponent<Rigidbody>();
        droneController = GetComponent<DroneController>();

        // Final demonstration should not restart automatically.
        MaxStep = 0;
    }

    public override void OnEpisodeBegin()
    {
        currentPhase = DronePhase.FlyingToMeetingPoint;

        transform.SetParent(null, true);

        body.isKinematic = false;

        RandomizeMeetingPoint();
        ResetDrone();

        if (obstacleRandomizer != null)
            obstacleRandomizer.RandomizeObstacles();

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

        float offset =
            meetingPointStartingPosition.y - originalGround;

        meetingPoint.position =
            new Vector3(x, ground + offset, z);
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
            new Vector3(
                x,
                ground + droneHeightAboveTerrain,
                z
            ),
            startingRotation
        );

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        droneController.ClearAgentControls();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Transform target =
            currentPhase == DronePhase.Landing
                ? landingPoint
                : droneTarget;

        if (target == null)
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(Vector3.zero);
            return;
        }

        Vector3 relativeTarget =
            transform.InverseTransformDirection(
                target.position - transform.position
            );

        Vector3 localVelocity =
            transform.InverseTransformDirection(
                body.linearVelocity
            );

        // Both trained models use six vector observations.
        sensor.AddObservation(relativeTarget);
        sensor.AddObservation(localVelocity);
    }

    public override void Heuristic(
        in ActionBuffers actionsOut
    )
    {
        var controls = actionsOut.ContinuousActions;

        controls[0] = 0f;
        controls[1] = 0f;
        controls[2] = 0f;

        if (Keyboard.current == null)
            return;

        if (Keyboard.current.wKey.isPressed)
            controls[0] = 1f;

        if (Keyboard.current.sKey.isPressed)
            controls[0] = -1f;

        if (Keyboard.current.aKey.isPressed)
            controls[1] = -1f;

        if (Keyboard.current.dKey.isPressed)
            controls[1] = 1f;

        if (Keyboard.current.rKey.isPressed)
            controls[2] = 1f;

        if (Keyboard.current.fKey.isPressed)
            controls[2] = -1f;
    }

    private void FixedUpdate()
    {
        if (currentPhase != DronePhase.WaitingForTruck)
            return;

        if (truckRigidbody == null ||
            landingPoint == null ||
            meetingPoint == null)
            return;

        // Check horizontal distance between truck and meeting point.
        Vector3 truckPosition = truckRigidbody.position;
        Vector3 meetingPosition = meetingPoint.position;

        truckPosition.y = 0f;
        meetingPosition.y = 0f;

        float distance = Vector3.Distance(
            truckPosition,
            meetingPosition
        );

        float truckSpeed =
            truckRigidbody.linearVelocity.magnitude;

        if (distance <= truckArrivalDistance &&
            truckSpeed <= truckStopSpeed)
        {
            StartLanding();
        }
    }

    private void StartLanding()
    {
        if (landingModel == null)
        {
            Debug.LogError(
                "FINAL - Landing model is not assigned!"
            );
            return;
        }

        currentPhase = DronePhase.Landing;

        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        droneController.ClearAgentControls();

        // Switch from navigation policy to landing policy.
        SetModel("DroneLandingTest", landingModel);

        Debug.Log(
            "FINAL - Truck arrived and stopped. " +
            "Drone starts automatic landing."
        );
    }

    public override void OnActionReceived(
        ActionBuffers actions
    )
    {
        if (currentPhase == DronePhase.WaitingForTruck ||
            currentPhase == DronePhase.Landed)
        {
            droneController.ClearAgentControls();
            return;
        }

        float forward = actions.ContinuousActions[0];
        float sideways = actions.ContinuousActions[1];
        float vertical = actions.ContinuousActions[2];

        droneController.SetAgentControls(
            forward,
            sideways,
            vertical
        );

        if (currentPhase == DronePhase.Landing)
        {
            // The landing model controls the drone.
            // Do not end or reset the episode here.
            return;
        }

        // Navigation phase.
        float currentGoalDistance = GoalDistance();

        AddReward(
            (previousGoalDistance - currentGoalDistance) *
            distanceRewardScale
        );

        previousGoalDistance = currentGoalDistance;

        if (Physics.CheckSphere(
            transform.position,
            detectionRadius + 8f,
            obstacleLayers))
        {
            AddReward(-0.002f);
        }

        if (currentGoalDistance <= successDistance)
        {
            currentPhase = DronePhase.WaitingForTruck;

            droneController.ClearAgentControls();

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;

            Debug.Log(
                "FINAL - Drone reached MeetingPoint. " +
                "Waiting for Truck."
            );

            return;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (currentPhase == DronePhase.Landing)
        {
            bool touchedTruck =
                truckRigidbody != null &&
                collision.rigidbody == truckRigidbody;

            if (touchedTruck)
            {
                float distance = Vector3.Distance(
                    transform.position,
                    landingPoint.position
                );

                float relativeSpeed =
                    (body.linearVelocity -
                     truckRigidbody.linearVelocity).magnitude;

                if (distance < 2f && relativeSpeed < 3f)
                {
                    CompleteLanding();
                }
            }

            return;
        }

        if (currentPhase == DronePhase.FlyingToMeetingPoint &&
            collision.gameObject.CompareTag("barrier"))
        {
            Debug.LogWarning(
                "FINAL - Drone touched an obstacle."
            );
        }
    }

    




    private void CompleteLanding()
    {
        currentPhase = DronePhase.Landed;

        // Stop drone controls and physics.
        droneController.ClearAgentControls();
        droneController.enabled = false;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;
        body.detectCollisions = false;

        // Place the drone directly on the landing point.
        transform.SetPositionAndRotation(
            landingPoint.position,
            landingPoint.rotation
        );

        // Attach drone to the truck.
        transform.SetParent(truckRigidbody.transform, true);

        Debug.Log("FINAL SUCCESS - Drone landed and attached to Truck!");
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
