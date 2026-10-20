
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class DroneAgentLanding : Agent
{
    [Header("Landing")]
    public Transform landingPoint;
    public Rigidbody truckRigidbody;
    public Terrain terrain;

    [Header("Training")]
    public float startRange = 5f;
    public float startHeight = 5f;
    public float landingDistance = 1.2f;
    public float maximumLandingSpeed = 1.5f;

    private Rigidbody body;
    private DroneController controller;
    private float previousDistance;
    private bool landed;

    public override void Initialize()
    {
        body = GetComponent<Rigidbody>();
        controller = GetComponent<DroneController>();

        MaxStep = 5000;
        Debug.Log("LANDING AGENT INITIALIZED. MaxStep = " + MaxStep);
    }

    public override void OnEpisodeBegin()
{
    landed = false;

    // Make sure the drone is not kinematic before resetting velocity.
    //transform.SetParent(null, true);
    body.isKinematic = false;

    // Reset the drone near the truck.
    Vector3 start = landingPoint.position;

    start.x += Random.Range(-startRange, startRange);
    start.z += Random.Range(-startRange, startRange);
    start.y += startHeight;

    transform.SetPositionAndRotation(
        start, Quaternion.identity);

    body.linearVelocity = Vector3.zero;
    body.angularVelocity = Vector3.zero;

    controller.ClearAgentControls();

    previousDistance = Vector3.Distance(
        transform.position, landingPoint.position);
}

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 relativeTarget =
            transform.InverseTransformDirection(
                landingPoint.position - transform.position);

        Vector3 localVelocity =
            transform.InverseTransformDirection(body.linearVelocity);

        sensor.AddObservation(relativeTarget);
        sensor.AddObservation(localVelocity);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (landed)
            return;

        controller.SetAgentControls(
            actions.ContinuousActions[0],
            actions.ContinuousActions[1],
            actions.ContinuousActions[2]);

        float distance = Vector3.Distance(
            transform.position, landingPoint.position);

        float speed = body.linearVelocity.magnitude;

        AddReward(-0.0002f);

        // Reward approaching the truck.
        AddReward((previousDistance - distance) * 0.05f);
        previousDistance = distance;

        // Encourage slow movement near the landing point.
        if (distance < 4f && speed > maximumLandingSpeed)
        {
            AddReward(-0.002f * (speed - maximumLandingSpeed));
        }

        // Penalize flying too far away.
        if (distance > 30f)
        {
            AddReward(-5f);
            EndEpisode();
            return;
        }

        // Penalize falling below terrain.
        if (terrain != null)
        {
            float ground =
                terrain.SampleHeight(transform.position) +
                terrain.transform.position.y;

            if (transform.position.y < ground - 2f)
            {
                AddReward(-5f);
                EndEpisode();
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (landed)
            return;

        // Only a gentle collision with the truck counts.
        bool touchedTruck =
            truckRigidbody != null && collision.rigidbody == truckRigidbody;

        if (touchedTruck)
        {
            float distance = Vector3.Distance(
                transform.position, landingPoint.position);

            float relativeSpeed =
                (body.linearVelocity -
                 truckRigidbody.linearVelocity).magnitude;

            if (distance < landingDistance &&
                relativeSpeed < maximumLandingSpeed)
            {
                landed = true;
                AddReward(20f);

                Debug.Log("LANDING SUCCESS - Drone touched truck!!!!");

                EndEpisode();
                return;
            }
        }

        // Other collisions or hard landings are failures.
        AddReward(-5f);
        EndEpisode();
    }
}