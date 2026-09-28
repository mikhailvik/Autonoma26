using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class DroneAgent : Agent
{
    [Header("Target")]
    public Transform meetingPoint;

    [Header("References")]
    public DroneController droneController;

    private Rigidbody rb;

    private Vector3 startPosition;
    private Quaternion startRotation;

    private float previousDistance;

    // reward for approaching the meeting point
    [Header("Rewards")]
    public float distanceRewardScale = 0.01f;
    public float successReward = 1.0f;
    public float successDistance = 2.0f;
    public float stepPenalty = -0.0001f;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();

        if (droneController == null)
        {
            droneController = GetComponent<DroneController>();
        }

        startPosition = transform.position;
        startRotation = transform.rotation;
    }
 
      public override void OnEpisodeBegin()
    {
        // Reset drone to its original starting position.
        transform.position = startPosition;
        transform.rotation = startRotation;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        droneController.ResetAgentControl();

        float distance = Vector3.Distance(
            transform.position,
            meetingPoint.position);

        previousDistance = distance;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1–3: MeetingPoint relative to Drone, in local coordinates
        Vector3 localTarget =
            transform.InverseTransformPoint(meetingPoint.position);

        sensor.AddObservation(localTarget);

        // 4–6: Drone local velocity
        Vector3 localVelocity =
            transform.InverseTransformDirection(rb.linearVelocity);

        sensor.AddObservation(localVelocity);

        // 7: Drone height
        sensor.AddObservation(transform.position.y);
    }
    /*
    public override void OnActionReceived(ActionBuffers actions)
    {
        float pitch = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float roll = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        float thrust = Mathf.Clamp(actions.ContinuousActions[2], -1f, 1f);

        droneController.SetAgentInput(pitch, roll, thrust);

        // Reward movement toward MeetingPoint
        float distance =
            Vector3.Distance(transform.position, meetingPoint.position);

        AddReward(-distance * 0.001f);
    }
    */

        public override void OnActionReceived(ActionBuffers actions)
    {
        float pitch = Mathf.Clamp(
            actions.ContinuousActions[0], -1f, 1f);

        float roll = Mathf.Clamp(
            actions.ContinuousActions[1], -1f, 1f);

        float thrust = Mathf.Clamp(
            actions.ContinuousActions[2], -1f, 1f);

        droneController.SetAgentInput(
            pitch,
            roll,
            thrust);

        // Calculate current distance to MeetingPoint.
        float currentDistance = Vector3.Distance(
            transform.position,
            meetingPoint.position);
        // My test
        Debug.Log(
            "Drone = " + transform.position +
            " | MeetingPoint = " + meetingPoint.position +
            " | Distance = " + currentDistance);

        // Reward the agent when it gets closer.
        float distanceChange =
            previousDistance - currentDistance;

        AddReward(distanceChange * distanceRewardScale);

        // Small penalty for spending time.
        AddReward(stepPenalty);

        previousDistance = currentDistance;

        // Success condition.
        if (currentDistance <= successDistance)
        {
            Debug.Log("SUCCESS! Drone reached MeetingPoint. Distance = " + currentDistance);

            AddReward(successReward);
            EndEpisode();
        }
}

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var actions = actionsOut.ContinuousActions;

        float pitch = 0f;
        float roll = 0f;
        float thrust = 0f;

        if (Input.GetKey(KeyCode.W))
            pitch = 1f;

        if (Input.GetKey(KeyCode.S))
            pitch = -1f;

        if (Input.GetKey(KeyCode.A))
            roll = 1f;

        if (Input.GetKey(KeyCode.D))
            roll = -1f;

        if (Input.GetKey(KeyCode.R))
            thrust = 1f;

        if (Input.GetKey(KeyCode.F))
            thrust = -1f;

        actions[0] = pitch;
        actions[1] = roll;
        actions[2] = thrust;
    }
}
