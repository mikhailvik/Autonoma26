using UnityEngine;

public class TerrainGeneratorLevel3 : MonoBehaviour
{
    [Header("Terrain Shape")]
    [SerializeField] private float hillHeight = 8.0f;
    [SerializeField] private float hillScale = 0.025f;

    [Header("Random Terrain")]
    [SerializeField] private bool randomizeOnStart = true;

    private Terrain terrain;
    private TerrainData terrainData;

    private float[,] originalHeights;

    private void Awake()
    {
        terrain = GetComponent<Terrain>();

        if (terrain == null)
        {
            Debug.LogError(
                "TerrainGeneratorLevel3 must be attached to a Terrain object."
            );
            return;
        }

        terrainData = terrain.terrainData;

        int resolution = terrainData.heightmapResolution;

        // Save the original terrain so generation always starts
        // from the same base terrain.
        originalHeights = terrainData.GetHeights(
            0,
            0,
            resolution,
            resolution
        );

        GenerateTerrain();
    }

    public void GenerateTerrain()
    {
        if (terrainData == null)
            return;

        int resolution = terrainData.heightmapResolution;

        float[,] heights = new float[resolution, resolution];

        float offsetX = randomizeOnStart
            ? Random.Range(0.0f, 10000.0f)
            : 0.0f;

        float offsetZ = randomizeOnStart
            ? Random.Range(0.0f, 10000.0f)
            : 0.0f;

        // Convert the desired hill height in world units
        // to Unity Terrain normalized height.
        float normalizedHillHeight =
            hillHeight / terrainData.size.y;

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float worldX =
                    ((float)x / (resolution - 1))
                    * terrainData.size.x;

                float worldZ =
                    ((float)z / (resolution - 1))
                    * terrainData.size.z;

                float noise = Mathf.PerlinNoise(
                    worldX * hillScale + offsetX,
                    worldZ * hillScale + offsetZ
                );

                // Keep the hills smooth and relatively low.
                float hill = noise * normalizedHillHeight;

                heights[z, x] =
                    Mathf.Clamp01(originalHeights[z, x] + hill);
            }
        }

        terrainData.SetHeights(0, 0, heights);

        Debug.Log("LEVEL 3 - Procedural terrain generated.");
    }

    private void OnDestroy()
    {
        // Restore the original terrain when leaving Play Mode,
        // so the source Terrain asset is not permanently changed.
        if (terrainData != null && originalHeights != null)
        {
            terrainData.SetHeights(0, 0, originalHeights);
        }
    }
}