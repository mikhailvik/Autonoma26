using System.Collections.Generic;
using UnityEngine;

public class Level5ObstacleRandomizer : MonoBehaviour
{
    [Header("Scene References")]
    public Terrain terrain;
    public Transform meetingPoint;
    public Transform drone;
    public Transform truck;

    [Header("Obstacles")]
    public List<Transform> buildings = new List<Transform>();
    public List<Transform> rocks = new List<Transform>();

    [Header("Randomization Settings")]
    public float edgeMargin = 40f;
    public float meetingPointClearance = 65f;
    public float droneClearance = 80f;
    public float truckClearance = 25f;
    public float obstacleSpacing = 80f;
    public int minRocks = 2;
    public int maxPlacementAttempts = 100;

    private readonly List<Vector3> placedPositions = new List<Vector3>();

    public void RandomizeObstacles()
    {
        if (terrain == null || meetingPoint == null)
        {
            Debug.LogWarning("Level 5: Terrain or MeetingPoint is missing.");
            return;
        }

        placedPositions.Clear();

        foreach (Transform building in buildings)
        {
            PlaceObstacle(building);
        }

        int rockCount = Random.Range(
            Mathf.Clamp(minRocks, 0, rocks.Count),
            rocks.Count + 1
        );

        for (int i = 0; i < rocks.Count; i++)
        {
            if (rocks[i] == null) continue;

            bool enabledRock = i < rockCount;
            rocks[i].gameObject.SetActive(enabledRock);

            if (enabledRock)
                PlaceObstacle(rocks[i]);
        }

        Debug.Log("LEVEL 5: Obstacles randomized.");
    }

    private void PlaceObstacle(Transform obstacle)
    {
        if (obstacle == null) return;

        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            float x = Random.Range(
                terrainPosition.x + edgeMargin,
                terrainPosition.x + terrainSize.x - edgeMargin
            );

            float z = Random.Range(
                terrainPosition.z + edgeMargin,
                terrainPosition.z + terrainSize.z - edgeMargin
            );

            Vector3 candidate = new Vector3(x, 0f, z);

            if (TooClose(candidate, meetingPoint.position, meetingPointClearance))
                continue;

            if (drone != null && TooClose(candidate, drone.position, droneClearance))
                continue;

            if (truck != null && TooClose(candidate, truck.position, truckClearance))
                continue;

            bool overlaps = false;

            foreach (Vector3 placed in placedPositions)
            {
                if (TooClose(candidate, placed, obstacleSpacing))
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps) continue;

            float groundY = terrain.SampleHeight(candidate)
                            + terrainPosition.y;

            // Preserve the obstacle's existing vertical offset.
            float offsetY = obstacle.position.y
                            - (terrain.SampleHeight(obstacle.position)
                               + terrainPosition.y);

            obstacle.position = new Vector3(x, groundY + offsetY, z);

            placedPositions.Add(candidate);
            return;
        }

        Debug.LogWarning(
            "LEVEL 5: Could not find a safe position for " + obstacle.name
        );
    }

    private bool TooClose(Vector3 a, Vector3 b, float distance)
    {
        Vector2 a2 = new Vector2(a.x, a.z);
        Vector2 b2 = new Vector2(b.x, b.z);

        return Vector2.Distance(a2, b2) < distance;
    }
}