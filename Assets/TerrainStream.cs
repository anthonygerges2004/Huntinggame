using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps every terrain tile drawn and solid. Detail drops with distance,
/// without hiding the ground or rebuilding its mesh every frame.
/// </summary>
public class TerrainStream : MonoBehaviour
{
    class Chunk
    {
        public Terrain terrain;
        public TerrainCollider solid;
        public Vector3 origin;
        public Vector3 size;
        public bool tuned;
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
        for (int i = 0; i < chunks.Count; i++)
            Apply(chunks[i]);

        Debug.Log("Terrain streaming on. " + chunks.Count + " chunks. Ground stays drawn.");
    }

    public void Tick()
    {
        for (int i = 0; i < chunks.Count; i++)
            Apply(chunks[i]);
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
        // This flag holes the ground at the edge of the view. Leave it off.
        terrain.enableHeightmapLODFrustumCulling = false;
        terrain.drawHeightmap = true;
        terrain.drawTreesAndFoliage = true;
        if (chunk.solid != null)
            chunk.solid.enabled = true;
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

    void Apply(Chunk chunk)
    {
        if (chunk.terrain == null)
            return;

        // Ground, trees, and colliders stay on. Quality is set once. Switching it
        // by distance rebuilt the mesh and made the forest pop back in.
        if (!chunk.terrain.drawHeightmap)
            chunk.terrain.drawHeightmap = true;
        if (!chunk.terrain.drawTreesAndFoliage)
            chunk.terrain.drawTreesAndFoliage = true;
        if (chunk.solid != null && !chunk.solid.enabled)
            chunk.solid.enabled = true;

        if (chunk.tuned)
            return;

        chunk.tuned = true;
        GameCulling.ApplyToTerrain(chunk.terrain);
    }
}
