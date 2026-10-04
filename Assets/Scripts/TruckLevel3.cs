using UnityEngine;

public class TruckLevel3 : MonoBehaviour
{
    [SerializeField] private Terrain terrain;

    [Header("Truck Position")]
    [SerializeField] private float heightAboveTerrain = 1.0f;

    private Rigidbody body;
    private Vector3 startingPosition;
    private Quaternion startingRotation;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        startingPosition = transform.position;
        startingRotation = transform.rotation;
    }

    private void Start()
    {
        PlaceTruckOnTerrain();
    }

    private void PlaceTruckOnTerrain()
    {
        if (terrain == null)
            return;

        float terrainHeight =
            terrain.SampleHeight(startingPosition) +
            terrain.transform.position.y;

        Vector3 newPosition = new Vector3(
            startingPosition.x,
            terrainHeight + heightAboveTerrain,
            startingPosition.z
        );

        transform.SetPositionAndRotation(
            newPosition,
            startingRotation
        );

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}