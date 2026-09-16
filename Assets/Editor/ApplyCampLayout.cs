using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ApplyCampLayout
{
    const float CampX = 16f;
    const float CampZ = -14f;

    [MenuItem("Tools/Hunting Game/Apply Camp Layout")]
    public static void Apply()
    {
        var campfire = GameObject.Find("Campfire");
        if (campfire == null)
        {
            Debug.LogError("Campfire not found in scene.");
            return;
        }

        var pos = campfire.transform.position;
        if (pos.x < 8f || pos.z > -8f)
        {
            Debug.LogWarning("Camp already moved. Skipping camp shift.");
        }
        else
        {
            var delta = new Vector3(CampX - pos.x, 0f, CampZ - pos.z);
            MoveRootsByName(
                delta,
                "Campfire",
                "Tree",
                "Tree_1",
                "Tree_2",
                "Tree_3",
                "Tree_4",
                "Tree_5");

            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name.StartsWith("FCP_"))
                    root.transform.position += delta;
            }
        }

        SetPosition("Player", new Vector3(0f, 1f, 22f));
        SetPosition("Deer", new Vector3(-14f, 1f, 14f));
        SetPosition("Deer_1", new Vector3(10f, 1f, 16f));
        SetPosition("Bird", new Vector3(-10f, 1f, 20f));
        SetPosition("BigAnimal", new Vector3(18f, 1f, 8f));
        SetPosition("Legendary", new Vector3(-20f, 1f, 6f));

        EnsureDuplicateAnimal("Deer", "Deer_2", new Vector3(-5f, 1f, 8f));
        EnsureDuplicateAnimal("Bird", "Bird_2", new Vector3(6f, 1f, 12f));
        EnsureDuplicateTree("Tree_1", "Tree_6", new Vector3(-12f, 0f, 18f));
        EnsureDuplicateTree("Tree_1", "Tree_7", new Vector3(8f, 0f, 20f));
        EnsureDuplicateTree("Tree_1", "Tree_8", new Vector3(-18f, 0f, 12f));

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("Camp layout applied. Save the scene (Ctrl+S).");
    }

    static void MoveRootsByName(Vector3 delta, params string[] names)
    {
        foreach (var name in names)
        {
            var go = GameObject.Find(name);
            if (go != null && go.transform.parent == null)
                go.transform.position += delta;
        }
    }

    static void SetPosition(string name, Vector3 worldPos)
    {
        var go = GameObject.Find(name);
        if (go != null)
            go.transform.position = worldPos;
    }

    static void EnsureDuplicateAnimal(string sourceName, string newName, Vector3 pos)
    {
        if (GameObject.Find(newName) != null)
            return;
        var src = GameObject.Find(sourceName);
        if (src == null)
            return;
        var copy = Object.Instantiate(src);
        copy.name = newName;
        copy.transform.position = pos;
    }

    static void EnsureDuplicateTree(string sourceName, string newName, Vector3 pos)
    {
        if (GameObject.Find(newName) != null)
            return;
        var src = GameObject.Find(sourceName);
        if (src == null)
            return;
        var copy = Object.Instantiate(src);
        copy.name = newName;
        copy.transform.position = pos;
    }
}
