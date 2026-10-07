using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PutObjectsOnGround
{
    static readonly string[] Groups = { "_HuntFoliage", "_GeneratedIslandLayout" };

    static readonly HashSet<string> Skip = new HashSet<string>
    {
        "_RealisticTerrain", "Player", "Canvas", "EventSystem", "Directional Light",
        "MiniMapCamerra", "Ground_1", "Wall"
    };

    [MenuItem("Tools/Hunting Game/Put Objects On Ground")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Ground", "Press Stop first, then run this again.", "OK");
            return;
        }

        List<Terrain> terrains = new List<Terrain>();
        foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude))
        {
            if (t.enabled && t.terrainData != null)
                terrains.Add(t);
        }

        if (terrains.Count == 0)
        {
            EditorUtility.DisplayDialog("Ground", "No terrain found in the scene.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        List<Transform> targets = new List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (System.Array.IndexOf(Groups, root.name) >= 0)
            {
                foreach (Transform child in root.transform)
                    targets.Add(child);
                continue;
            }

            if (Skip.Contains(root.name) ||
                root.GetComponent<Terrain>() != null ||
                root.GetComponent<Camera>() != null ||
                root.GetComponent<Light>() != null ||
                root.GetComponent<Canvas>() != null)
                continue;

            targets.Add(root.transform);
        }

        Undo.SetCurrentGroupName("Put Objects On Ground");
        int moved = 0;
        int outside = 0;

        foreach (Transform target in targets)
        {
            if (!target.gameObject.activeInHierarchy)
                continue;

            if (!TryGroundHeight(terrains, target, out float groundY))
            {
                outside++;
                continue;
            }

            Vector3 pos = target.position;
            if (Mathf.Abs(pos.y - groundY) < 0.01f)
                continue;

            Undo.RecordObject(target, "Put Objects On Ground");
            target.position = new Vector3(pos.x, groundY, pos.z);
            moved++;
        }

        EditorSceneManager.MarkSceneDirty(scene);

        string message = "Put " + moved + " objects on the ground.";
        if (outside > 0)
            message += "\n\n" + outside + " objects are not above any terrain, so they were left where they are.";
        message += "\n\nPress Ctrl+S to save. Ctrl+Z undoes it.";
        EditorUtility.DisplayDialog("Ground", message, "OK");
    }

    // Uses the lowest ground point under the object's base, so no side floats on a slope.
    static bool TryGroundHeight(List<Terrain> terrains, Transform target, out float groundY)
    {
        Vector3 center = target.position;
        float radius = BaseRadius(target);
        Vector3[] samples =
        {
            center,
            center + new Vector3(radius, 0f, 0f),
            center - new Vector3(radius, 0f, 0f),
            center + new Vector3(0f, 0f, radius),
            center - new Vector3(0f, 0f, radius)
        };

        groundY = float.MaxValue;
        bool found = false;
        foreach (Vector3 point in samples)
        {
            Terrain terrain = TerrainUnder(terrains, point);
            if (terrain == null)
                continue;

            float y = terrain.SampleHeight(point) + terrain.transform.position.y;
            groundY = Mathf.Min(groundY, y);
            found = true;
        }

        return found && TerrainUnder(terrains, center) != null;
    }

    static float BaseRadius(Transform target)
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds();
        foreach (Renderer r in target.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled)
                continue;
            if (hasBounds)
                bounds.Encapsulate(r.bounds);
            else
                bounds = r.bounds;
            hasBounds = true;
        }

        if (!hasBounds)
            return 0f;

        float smallest = Mathf.Min(bounds.extents.x, bounds.extents.z);
        return Mathf.Clamp(smallest * 0.5f, 0f, 1f);
    }

    static Terrain TerrainUnder(List<Terrain> terrains, Vector3 point)
    {
        foreach (Terrain terrain in terrains)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (point.x >= origin.x && point.x <= origin.x + size.x &&
                point.z >= origin.z && point.z <= origin.z + size.z)
                return terrain;
        }

        return null;
    }
}
