using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Splits large ground into chunks and keeps only the nearby ones drawn and solid.
/// Chunk data stays in memory, so a place you leave is the same place when you return.
/// </summary>
public class TerrainStream : MonoBehaviour
{
    const float DrawRadius = 1450f;
    const float SolidRadius = 900f;
    const float SplitAbove = 900f;

    class Chunk
    {
        public Terrain terrain;
        public TerrainCollider solid;
        public Vector3 origin;
        public Vector3 size;
    }

    readonly List<Chunk> chunks = new List<Chunk>();
    Transform root;

    public int ChunkCount => chunks.Count;

    public void Setup(List<Terrain> terrains)
    {
        chunks.Clear();
        if (terrains == null || terrains.Count == 0)
            return;

        if (root == null)
        {
            GameObject holder = new GameObject("_TerrainStream");
            root = holder.transform;
        }

        for (int i = 0; i < terrains.Count; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
                continue;

            Vector3 size = terrain.terrainData.size;
            bool wide = size.x > SplitAbove || size.z > SplitAbove;
            if (wide && Split(terrain))
                Mute(terrain);
            else
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

    bool Split(Terrain source)
    {
        TerrainData data = source.terrainData;
        int spanX = data.heightmapResolution - 1;
        int spanZ = data.heightmapResolution - 1;
        if (spanX < 2 || spanZ < 2)
            return false;

        int piecesX = BestPieces(data.size.x, spanX);
        int piecesZ = BestPieces(data.size.z, spanZ);
        while (piecesX * piecesZ > 8 && piecesX >= piecesZ && piecesX > 1)
        {
            piecesX--;
            while (piecesX > 1 && spanX % piecesX != 0)
                piecesX--;
        }

        while (piecesX * piecesZ > 8 && piecesZ > 1)
        {
            piecesZ--;
            while (piecesZ > 1 && spanZ % piecesZ != 0)
                piecesZ--;
        }

        if (piecesX <= 1 && piecesZ <= 1)
            return false;

        int stepX = spanX / piecesX;
        int stepZ = spanZ / piecesZ;
        int resolution = ChunkResolution(stepX, stepZ);
        Vector3 origin = source.transform.position;
        int before = chunks.Count;

        for (int iz = 0; iz < piecesZ; iz++)
        {
            for (int ix = 0; ix < piecesX; ix++)
            {
                float[,] rawHeights = data.GetHeights(ix * stepX, iz * stepZ, stepX + 1, stepZ + 1);
                TerrainData piece = new TerrainData();
                piece.heightmapResolution = resolution;
                piece.size = new Vector3(data.size.x / piecesX, data.size.y, data.size.z / piecesZ);
                piece.SetHeights(0, 0, SquareHeights(rawHeights, resolution));
                Paint(piece, data, ix, piecesX, iz, piecesZ);
                CopyHoles(piece, data, ix * stepX, iz * stepZ, stepX, stepZ, resolution);
                CopyTrees(piece, data, ix, piecesX, iz, piecesZ);
                CopyDetails(piece, data, ix, piecesX, iz, piecesZ);
                piece.baseMapResolution = 256;

                GameObject ground = Terrain.CreateTerrainGameObject(piece);
                ground.name = "Stream_" + ix + "_" + iz;
                ground.transform.SetParent(root, true);
                ground.transform.position = origin + new Vector3(
                    data.size.x * ix / piecesX,
                    0f,
                    data.size.z * iz / piecesZ);

                Terrain terrain = ground.GetComponent<Terrain>();
                terrain.materialTemplate = source.materialTemplate;
                terrain.drawInstanced = false;
                terrain.groupingID = source.groupingID;
                terrain.allowAutoConnect = true;
                terrain.Flush();

                Chunk chunk = new Chunk
                {
                    terrain = terrain,
                    solid = ground.GetComponent<TerrainCollider>(),
                    origin = ground.transform.position,
                    size = piece.size
                };
                chunks.Add(chunk);
            }
        }

        return chunks.Count > before;
    }

    static int ChunkResolution(int stepX, int stepZ)
    {
        int needed = Mathf.Max(stepX, stepZ);
        int power = Mathf.NextPowerOfTwo(Mathf.Max(32, needed));
        if (power > 512)
            power = 512;
        return power + 1;
    }

    static float[,] SquareHeights(float[,] raw, int resolution)
    {
        float[,] square = new float[resolution, resolution];
        int rawHeight = raw.GetLength(0);
        int rawWidth = raw.GetLength(1);
        int xMax = rawWidth - 1;
        int zMax = rawHeight - 1;
        for (int z = 0; z < resolution; z++)
        {
            float v = z / (float)(resolution - 1) * zMax;
            int z0 = Mathf.Clamp(Mathf.FloorToInt(v), 0, zMax);
            int z1 = Mathf.Min(z0 + 1, zMax);
            float tz = v - z0;
            for (int x = 0; x < resolution; x++)
            {
                float u = x / (float)(resolution - 1) * xMax;
                int x0 = Mathf.Clamp(Mathf.FloorToInt(u), 0, xMax);
                int x1 = Mathf.Min(x0 + 1, xMax);
                float tx = u - x0;
                float a = Mathf.Lerp(raw[z0, x0], raw[z0, x1], tx);
                float b = Mathf.Lerp(raw[z1, x0], raw[z1, x1], tx);
                square[z, x] = Mathf.Lerp(a, b, tz);
            }
        }

        return square;
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

    static int BestPieces(float worldSize, int span)
    {
        int target = Mathf.Clamp(Mathf.RoundToInt(worldSize / 850f), 1, 4);
        int best = 1;
        for (int pieces = 1; pieces <= target; pieces++)
        {
            if (span % pieces == 0)
                best = pieces;
        }

        return best;
    }

    static void Paint(TerrainData piece, TerrainData source, int ix, int piecesX, int iz, int piecesZ)
    {
        if (source.alphamapLayers <= 0 || source.alphamapWidth < 2 || source.terrainLayers == null)
            return;

        int width = source.alphamapWidth;
        int height = source.alphamapHeight;
        int x0 = ix * width / piecesX;
        int z0 = iz * height / piecesZ;
        int x1 = Mathf.Min(width, Mathf.Max(x0 + 1, (ix + 1) * width / piecesX));
        int z1 = Mathf.Min(height, Mathf.Max(z0 + 1, (iz + 1) * height / piecesZ));
        float[,,] raw = source.GetAlphamaps(x0, z0, x1 - x0, z1 - z0);
        int layers = raw.GetLength(2);
        if (layers <= 0 || source.terrainLayers.Length != layers)
            return;

        const int dst = 64;
        float[,,] map = new float[dst, dst, layers];
        int rawHeight = raw.GetLength(0);
        int rawWidth = raw.GetLength(1);
        for (int z = 0; z < dst; z++)
        {
            int sz = Mathf.Clamp(z * rawHeight / dst, 0, rawHeight - 1);
            for (int x = 0; x < dst; x++)
            {
                int sx = Mathf.Clamp(x * rawWidth / dst, 0, rawWidth - 1);
                for (int layer = 0; layer < layers; layer++)
                    map[z, x, layer] = raw[sz, sx, layer];
            }
        }

        piece.terrainLayers = source.terrainLayers;
        piece.alphamapResolution = dst;
        piece.SetAlphamaps(0, 0, map);
    }

    static void CopyHoles(TerrainData piece, TerrainData source, int x, int z, int width, int height, int resolution)
    {
        bool[,] raw = source.GetHoles(x, z, width, height);
        if (raw == null)
            return;

        int holeRes = resolution - 1;
        bool[,] holes = new bool[holeRes, holeRes];
        int rawHeight = raw.GetLength(0);
        int rawWidth = raw.GetLength(1);
        for (int hz = 0; hz < holeRes; hz++)
        {
            int sz = Mathf.Clamp(hz * rawHeight / holeRes, 0, rawHeight - 1);
            for (int hx = 0; hx < holeRes; hx++)
            {
                int sx = Mathf.Clamp(hx * rawWidth / holeRes, 0, rawWidth - 1);
                holes[hz, hx] = raw[sz, sx];
            }
        }

        piece.SetHoles(0, 0, holes);
    }

    static void CopyTrees(TerrainData piece, TerrainData source, int ix, int piecesX, int iz, int piecesZ)
    {
        TreePrototype[] prototypes = source.treePrototypes;
        if (prototypes == null || prototypes.Length == 0)
            return;

        piece.treePrototypes = prototypes;
        float x0 = ix / (float)piecesX;
        float x1 = (ix + 1) / (float)piecesX;
        float z0 = iz / (float)piecesZ;
        float z1 = (iz + 1) / (float)piecesZ;
        float spanX = Mathf.Max(0.0001f, x1 - x0);
        float spanZ = Mathf.Max(0.0001f, z1 - z0);

        TreeInstance[] all = source.treeInstances;
        List<TreeInstance> local = new List<TreeInstance>();
        for (int i = 0; i < all.Length; i++)
        {
            Vector3 position = all[i].position;
            if (position.x < x0 || position.x >= x1 || position.z < z0 || position.z >= z1)
                continue;

            TreeInstance copy = all[i];
            copy.position = new Vector3((position.x - x0) / spanX, position.y, (position.z - z0) / spanZ);
            local.Add(copy);
        }

        piece.treeInstances = local.ToArray();
    }

    static void CopyDetails(TerrainData piece, TerrainData source, int ix, int piecesX, int iz, int piecesZ)
    {
        DetailPrototype[] prototypes = source.detailPrototypes;
        if (prototypes == null || prototypes.Length == 0 || source.detailWidth < 2)
            return;

        piece.detailPrototypes = prototypes;
        piece.SetDetailResolution(64, 8);
        piece.wavingGrassStrength = source.wavingGrassStrength;
        piece.wavingGrassAmount = source.wavingGrassAmount;
        piece.wavingGrassSpeed = source.wavingGrassSpeed;
        piece.wavingGrassTint = source.wavingGrassTint;

        int x0 = ix * source.detailWidth / piecesX;
        int z0 = iz * source.detailHeight / piecesZ;
        int x1 = Mathf.Min(source.detailWidth, Mathf.Max(x0 + 1, (ix + 1) * source.detailWidth / piecesX));
        int z1 = Mathf.Min(source.detailHeight, Mathf.Max(z0 + 1, (iz + 1) * source.detailHeight / piecesZ));
        int rawWidth = x1 - x0;
        int rawHeight = z1 - z0;

        for (int layer = 0; layer < prototypes.Length; layer++)
        {
            int[,] raw = source.GetDetailLayer(x0, z0, rawWidth, rawHeight, layer);
            int[,] map = new int[64, 64];
            for (int z = 0; z < 64; z++)
            {
                int sz = Mathf.Clamp(z * rawHeight / 64, 0, rawHeight - 1);
                for (int x = 0; x < 64; x++)
                {
                    int sx = Mathf.Clamp(x * rawWidth / 64, 0, rawWidth - 1);
                    map[z, x] = raw[sz, sx];
                }
            }

            piece.SetDetailLayer(0, 0, layer, map);
        }
    }

    static void Mute(Terrain terrain)
    {
        terrain.drawHeightmap = false;
        terrain.drawTreesAndFoliage = false;
        terrain.SetNeighbors(null, null, null, null);
        TerrainCollider solid = terrain.GetComponent<TerrainCollider>();
        if (solid != null)
            solid.enabled = false;
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
