using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Keeps only the nearby terrains drawn and solid, with distance-based detail.
/// </summary>
public class TerrainStream : MonoBehaviour
{
    const float DrawRadius = 1450f;
    const float SolidRadius = 900f;

    class Chunk
    {
        public Terrain terrain;
        public TerrainCollider solid;
        public Vector3 origin;
        public Vector3 size;
    }

    readonly List<Chunk> chunks = new List<Chunk>();

    public int ChunkCount => chunks.Count;

    public void Setup(List<Terrain> terrains)
    {
        chunks.Clear();
        if (terrains == null || terrains.Count == 0)
            return;

        for (int i = 0; i < terrains.Count; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
                continue;

            // Stream the real terrains instead of splitting them into rebuilt copies.
            // The copies resampled heights to at most 513 samples and paint to 64x64,
            // which made placed objects (castles, buildings, trees) float or sink and
            // smeared the ground textures. Each original terrain is still shown/hidden
            // and made solid by distance, so streaming keeps working.
            Adopt(terrain);
        }

        Link();
        Vector3 player = PlayerPosition();
        for (int i = 0; i < chunks.Count; i++)
            Apply(chunks[i], player);

        Debug.Log(
            "Terrain streaming on. " + chunks.Count +
            " chunks. Distant ground is not drawn until you walk toward it.");
    }

    public void Tick(Vector3 player)
    {
        for (int i = 0; i < chunks.Count; i++)
            Apply(chunks[i], player);
    }

    void Adopt(Terrain terrain)
    {
        Chunk chunk = new Chunk
        {
            terrain = terrain,
            solid = terrain.GetComponent<TerrainCollider>(),
            origin = terrain.transform.position,
            size = terrain.terrainData.size
        };
        terrain.allowAutoConnect = true;
        terrain.drawInstanced = false;
        chunks.Add(chunk);
    }

    void Link()
    {
        for (int i = 0; i < chunks.Count; i++)
        {
            Terrain left = null;
            Terrain right = null;
            Terrain top = null;
            Terrain bottom = null;
            for (int j = 0; j < chunks.Count; j++)
            {
                if (i == j)
                    continue;

                float rightGap = chunks[j].origin.x - (chunks[i].origin.x + chunks[i].size.x);
                float leftGap = chunks[i].origin.x - (chunks[j].origin.x + chunks[j].size.x);
                float topGap = chunks[j].origin.z - (chunks[i].origin.z + chunks[i].size.z);
                float bottomGap = chunks[i].origin.z - (chunks[j].origin.z + chunks[j].size.z);
                bool sameRow = Overlap(chunks[i].origin.z, chunks[i].size.z, chunks[j].origin.z, chunks[j].size.z);
                bool sameColumn = Overlap(chunks[i].origin.x, chunks[i].size.x, chunks[j].origin.x, chunks[j].size.x);

                bool sameResolution = chunks[i].terrain.terrainData.heightmapResolution ==
                                      chunks[j].terrain.terrainData.heightmapResolution;
                if (!sameResolution)
                    continue;

                if (Mathf.Abs(rightGap) < 3f && sameRow)
                    right = chunks[j].terrain;
                if (Mathf.Abs(leftGap) < 3f && sameRow)
                    left = chunks[j].terrain;
                if (Mathf.Abs(topGap) < 3f && sameColumn)
                    top = chunks[j].terrain;
                if (Mathf.Abs(bottomGap) < 3f && sameColumn)
                    bottom = chunks[j].terrain;
            }

            chunks[i].terrain.SetNeighbors(left, top, right, bottom);
        }

        Terrain.SetConnectivityDirty();
    }

    static bool Overlap(float originA, float sizeA, float originB, float sizeB)
    {
        float overlap = Mathf.Min(originA + sizeA, originB + sizeB) - Mathf.Max(originA, originB);
        return overlap > Mathf.Min(sizeA, sizeB) * 0.35f;
    }

    void Apply(Chunk chunk, Vector3 player)
    {
        if (chunk.terrain == null)
            return;

        float distance = DistanceTo(chunk, player);
        bool draw = distance <= DrawRadius;
        bool solid = distance <= SolidRadius;
        chunk.terrain.drawHeightmap = draw;
        chunk.terrain.drawTreesAndFoliage = draw;
        if (chunk.solid != null)
            chunk.solid.enabled = solid;

        if (!draw)
            return;

        if (distance < 380f)
        {
            chunk.terrain.heightmapPixelError = 5f;
            chunk.terrain.basemapDistance = 450f;
            chunk.terrain.treeDistance = 520f;
            chunk.terrain.treeBillboardDistance = 160f;
            chunk.terrain.detailObjectDistance = 95f;
            chunk.terrain.detailObjectDensity = 0.7f;
            chunk.terrain.shadowCastingMode = ShadowCastingMode.On;
        }
        else if (distance < 850f)
        {
            chunk.terrain.heightmapPixelError = 14f;
            chunk.terrain.basemapDistance = 220f;
            chunk.terrain.treeDistance = 260f;
            chunk.terrain.treeBillboardDistance = 70f;
            chunk.terrain.detailObjectDistance = 28f;
            chunk.terrain.detailObjectDensity = 0.35f;
            chunk.terrain.shadowCastingMode = ShadowCastingMode.Off;
        }
        else
        {
            chunk.terrain.heightmapPixelError = 32f;
            chunk.terrain.basemapDistance = 60f;
            chunk.terrain.treeDistance = 90f;
            chunk.terrain.treeBillboardDistance = 30f;
            chunk.terrain.detailObjectDistance = 0f;
            chunk.terrain.detailObjectDensity = 0.15f;
            chunk.terrain.shadowCastingMode = ShadowCastingMode.Off;
        }

        chunk.terrain.enableHeightmapLODFrustumCulling = true;
    }

    static float DistanceTo(Chunk chunk, Vector3 player)
    {
        float dx = 0f;
        if (player.x < chunk.origin.x)
            dx = chunk.origin.x - player.x;
        else if (player.x > chunk.origin.x + chunk.size.x)
            dx = player.x - (chunk.origin.x + chunk.size.x);

        float dz = 0f;
        if (player.z < chunk.origin.z)
            dz = chunk.origin.z - player.z;
        else if (player.z > chunk.origin.z + chunk.size.z)
            dz = player.z - (chunk.origin.z + chunk.size.z);

        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    static Vector3 PlayerPosition()
    {
        GameObject player = GameObject.Find("Player");
        if (player != null)
            return player.transform.position;
        return Vector3.zero;
    }
}
