using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Fills the empty space between terrain tiles with swimable water.
// Does not move terrains, people, or scenery, and does not carve the ground.
public static class FillTerrainGapsWithWater
{
    public const string RootName = "_GapRivers";
    const string MeshFolder = MakeFishingLake.Folder + "/GapRivers";
    const string UndoName = "Fill Terrain Gaps With Water";
    const float MinGap = 10f;
    const float Overlap = 8f;
    const float BelowBank = 2f;
    const float SampleStep = 20f;
    const float PointSpacing = 16f;
    const float Inland = 6f;
    const string PrefsKey = "HuntingGame.GapRiversFilled";

    [InitializeOnLoadMethod]
    static void AutoFillOnce()
    {
        EditorApplication.delayCall += () =>
        {
            EditorApplication.delayCall += TryAutoFill;
        };
    }

    static void TryAutoFill()
    {
        if (EditorPrefs.GetBool(PrefsKey, false))
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "GetStarted_Scene")
            return;
        if (GameObject.Find(RootName) != null)
        {
            EditorPrefs.SetBool(PrefsKey, true);
            return;
        }

        List<Terrain> tiles = FindTiles();
        if (tiles.Count < 2)
            return;

        int filled = Build(tiles);
        if (filled > 0)
        {
            EditorPrefs.SetBool(PrefsKey, true);
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    [MenuItem("Tools/Hunting Game/Fill Terrain Gaps With Water")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Water", "Press Stop first, then run this again.", "OK");
            return;
        }

        List<Terrain> tiles = FindTiles();
        if (tiles.Count < 2)
        {
            EditorUtility.DisplayDialog("Water", "Need at least 2 terrains to find a gap.", "OK");
            return;
        }

        int filled = Build(tiles);
        if (filled == 0)
        {
            EditorUtility.DisplayDialog("Water", "The terrains already touch. There is no gap to fill.", "OK");
            return;
        }

        EditorPrefs.SetBool(PrefsKey, true);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog(
            "Water",
            "Put water in " + filled + " gap" + (filled == 1 ? "" : "s") +
            " between the terrains.\n\nPeople and the original ground were not moved.",
            "OK");
    }

    static int Build(List<Terrain> tiles)
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        if (!AssetDatabase.IsValidFolder(MeshFolder))
        {
            Directory.CreateDirectory(MeshFolder);
            AssetDatabase.Refresh();
        }

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, UndoName);

        int filled = 0;
        for (int i = 1; i < tiles.Count; i++)
        {
            Terrain left = tiles[i - 1];
            Terrain right = tiles[i];
            if (left.terrainData == null || right.terrainData == null)
                continue;

            Vector3 leftPos = left.transform.position;
            Vector3 rightPos = right.transform.position;
            Vector3 leftSize = left.terrainData.size;
            Vector3 rightSize = right.terrainData.size;

            float leftEdge = leftPos.x + leftSize.x;
            float rightEdge = rightPos.x;
            float gap = rightEdge - leftEdge;
            if (gap < MinGap)
                continue;

            float zMin = Mathf.Min(leftPos.z, rightPos.z);
            float zMax = Mathf.Max(leftPos.z + leftSize.z, rightPos.z + rightSize.z);
            if (zMax - zMin < 10f)
                continue;

            Vector3[] path = BuildPath(left, right, leftEdge, rightEdge, zMin, zMax);
            if (path == null || path.Length < 2)
                continue;

            float halfWidth = gap * 0.5f + Overlap;
            Mesh mesh = BuildMesh(path, halfWidth);
            string meshPath = MeshFolder + "/GapRiver_" + i + ".asset";
            Mesh old = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (old != null)
                AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            GameObject river = new GameObject("GapRiver_" + i);
            river.transform.SetParent(root.transform, true);
            river.AddComponent<MeshFilter>().sharedMesh = mesh;

            MeshRenderer renderer = river.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = MakeFishingLake.WaterMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            RiverWater water = river.AddComponent<RiverWater>();
            water.points = path;
            water.halfWidth = halfWidth;
            water.flowSpeed = 0.35f;

            if (river.GetComponent<FishingSpot>() == null)
                river.AddComponent<FishingSpot>();

            Undo.RegisterCreatedObjectUndo(river, UndoName);
            filled++;
        }

        AssetDatabase.SaveAssets();
        return filled;
    }

    static Vector3[] BuildPath(Terrain left, Terrain right, float leftEdge, float rightEdge, float zMin, float zMax)
    {
        float xCenter = (leftEdge + rightEdge) * 0.5f;
        float leftSampleX = leftEdge - Inland;
        float rightSampleX = rightEdge + Inland;

        List<Vector3> samples = new List<Vector3>();
        for (float z = zMin; z <= zMax + 0.01f; z += SampleStep)
        {
            float y = Mathf.Min(
                BankHeight(left, leftSampleX, z),
                BankHeight(right, rightSampleX, z));
            samples.Add(new Vector3(xCenter, y - BelowBank, z));
        }

        if (samples.Count < 2)
            return null;

        SmoothY(samples, 4);
        return Resample(samples, PointSpacing);
    }

    static float BankHeight(Terrain terrain, float x, float z)
    {
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        float sx = Mathf.Clamp(x, origin.x + 0.5f, origin.x + size.x - 0.5f);
        float sz = Mathf.Clamp(z, origin.z + 0.5f, origin.z + size.z - 0.5f);
        return terrain.SampleHeight(new Vector3(sx, 0f, sz)) + origin.y;
    }

    static void SmoothY(List<Vector3> points, int radius)
    {
        float[] y = new float[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            float sum = 0f;
            int n = 0;
            for (int k = Mathf.Max(0, i - radius); k <= Mathf.Min(points.Count - 1, i + radius); k++)
            {
                sum += points[k].y;
                n++;
            }
            y[i] = sum / n;
        }

        for (int i = 0; i < points.Count; i++)
        {
            Vector3 p = points[i];
            p.y = y[i];
            points[i] = p;
        }
    }

    static Vector3[] Resample(List<Vector3> points, float spacing)
    {
        List<Vector3> result = new List<Vector3> { points[0] };
        float carried = 0f;
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];
            float length = Vector3.Distance(a, b);
            float d = spacing - carried;
            while (d < length)
            {
                result.Add(Vector3.Lerp(a, b, d / length));
                d += spacing;
            }
            carried = length - (d - spacing);
        }

        Vector3 last = points[points.Count - 1];
        if (Vector3.Distance(result[result.Count - 1], last) > spacing * 0.3f)
            result.Add(last);
        else
            result[result.Count - 1] = last;
        return result.ToArray();
    }

    static Mesh BuildMesh(Vector3[] path, float halfWidth)
    {
        Vector3[] vertices = new Vector3[path.Length * 2];
        Vector2[] uvs = new Vector2[path.Length * 2];
        int[] triangles = new int[(path.Length - 1) * 6];
        float along = 0f;

        for (int i = 0; i < path.Length; i++)
        {
            Vector3 prev = path[Mathf.Max(0, i - 1)];
            Vector3 next = path[Mathf.Min(path.Length - 1, i + 1)];
            Vector3 tangent = next - prev;
            tangent.y = 0f;
            if (tangent.sqrMagnitude < 0.0001f)
                tangent = Vector3.forward;
            tangent.Normalize();
            Vector3 side = new Vector3(-tangent.z, 0f, tangent.x) * halfWidth;

            if (i > 0)
                along += Vector3.Distance(Flat(path[i]), Flat(path[i - 1]));

            float v = along / Mathf.Max(1f, halfWidth * 2f);
            vertices[i * 2] = path[i] + side;
            vertices[i * 2 + 1] = path[i] - side;
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);
        }

        for (int i = 0; i < path.Length - 1; i++)
        {
            int vi = i * 2;
            int ti = i * 6;
            triangles[ti] = vi;
            triangles[ti + 1] = vi + 2;
            triangles[ti + 2] = vi + 1;
            triangles[ti + 3] = vi + 1;
            triangles[ti + 4] = vi + 2;
            triangles[ti + 5] = vi + 3;
        }

        Mesh mesh = new Mesh { name = "GapRiver" };
        if (vertices.Length > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Vector3 Flat(Vector3 p)
    {
        return new Vector3(p.x, 0f, p.z);
    }

    static List<Terrain> FindTiles()
    {
        List<Terrain> tiles = new List<Terrain>();
        GameObject root = GameObject.Find(ApplyRealisticTerrain.RootName);
        if (root != null)
        {
            foreach (Terrain t in root.GetComponentsInChildren<Terrain>(true))
            {
                if (t.enabled && t.terrainData != null && t.gameObject.activeInHierarchy)
                    tiles.Add(t);
            }
        }

        tiles.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        return tiles;
    }
}
