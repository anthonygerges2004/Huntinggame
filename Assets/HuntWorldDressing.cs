using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class HuntWorldDressing : MonoBehaviour
{
    const string FoliageRootName = "_HuntFoliage";

    void OnEnable()
    {
        FixPinkTreesNow();
        if (Application.isPlaying)
            StartCoroutine(DressNextFrame());
    }

    IEnumerator DressNextFrame()
    {
        yield return null;
        Dress();
    }

    static void FixPinkTreesNow()
    {
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.forceMeshLod = 0;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
                continue;

            bool changed = false;
            Material[] filled = materials;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && filter.sharedMesh.subMeshCount > materials.Length)
            {
                filled = new Material[filter.sharedMesh.subMeshCount];
                for (int m = 0; m < filled.Length; m++)
                    filled[m] = m < materials.Length && materials[m] != null ? materials[m] : materials[0];
                changed = true;
            }

            if (unlit != null)
            {
                for (int m = 0; m < filled.Length; m++)
                {
                    if (filled[m] != null && filled[m].shader != null &&
                        (filled[m].shader.name.Contains("Error") || filled[m].shader.name.StartsWith("Nature/")))
                    {
                        filled[m].shader = unlit;
                        changed = true;
                    }
                }
            }

            if (changed)
                renderer.sharedMaterials = filled;
        }

        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
        for (int i = 0; i < terrains.Length; i++)
        {
            if (terrains[i] == null)
                continue;
            terrains[i].treeDistance = 0f;
            terrains[i].detailObjectDistance = 0f;
        }
    }

    public static void Dress()
    {
        FixTerrainShaders();
        ReplaceBrokenTreeMeshes();

        if (GameObject.Find(FoliageRootName) != null)
            return;

        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null)
            return;

        SnapCampAndWildlife();
        StretchView();
        ApplyFog();

        GameObject root = new GameObject(FoliageRootName);
        Vector3 camp = FindCamp();

        string[] treePrefabs = TreePrefabs();
        bool conifers = false;
        for (int i = 0; i < treePrefabs.Length; i++)
        {
            if (treePrefabs[i].IndexOf("RealisticTree", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                treePrefabs[i].IndexOf("Conifer", System.StringComparison.OrdinalIgnoreCase) >= 0)
                conifers = true;
        }

        if (conifers)
        {
            ScatterPrefabs(root.transform, camp, treePrefabs, 36, 18f, 260f, 0.95f, 1.35f, true);
        }
        else
        {
            ScatterPrefabs(root.transform, camp, treePrefabs, 220, 16f, 380f, 0.85f, 1.5f, true);
        }

        ScatterPrefabs(
            root.transform,
            camp,
            new[]
            {
                "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Grass_11.prefab",
                "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Grass_15.prefab",
                "Assets/URP GanzSe Free Camping Props/Prefabs/FCP_GrassCluster_Type1_Color1.prefab"
            },
            180,
            4f,
            90f,
            0.8f,
            1.5f,
            false);

        ScatterPrefabs(
            root.transform,
            camp,
            new[]
            {
                "Assets/URP GanzSe Free Camping Props/Prefabs/FCP__Bush_Type1_Color1.prefab"
            },
            18,
            12f,
            50f,
            0.9f,
            1.3f,
            false);

        ScatterPrefabs(
            root.transform,
            camp,
            new[]
            {
                "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Rock_Moss_Grown_09.prefab",
                "Assets/URP GanzSe Free Camping Props/Prefabs/FCP_RockCluster_Type1_Color1.prefab"
            },
            12,
            16f,
            85f,
            0.7f,
            1.4f,
            false);
    }

    static Vector3 FindCamp()
    {
        GameObject tent = GameObject.Find("FCP_Tent_Type1_Color1");
        if (tent != null)
            return WorldGround.Snap(tent.transform.position);

        GameObject player = GameObject.Find("Player");
        if (player != null)
            return WorldGround.Snap(player.transform.position);

        return WorldGround.Snap(new Vector3(48f, 0f, 57f));
    }

    static void SnapCampAndWildlife()
    {
        string[] campNames =
        {
            "Campfire", "FCP_Campfire_Type1_Color1", "FCP_Tent_Type1_Color1",
            "FCP_Bench_Type1_Color1", "FCP_Bench_Type1_Color1_1",
            "FCP_FlagBunting_Type1_Color1", "FCP_FlagBunting_Type1_Color1_1"
        };

        for (int i = 0; i < campNames.Length; i++)
        {
            GameObject go = GameObject.Find(campNames[i]);
            if (go == null)
                continue;
            go.transform.position = WorldGround.Snap(go.transform.position);
        }

        Animal[] animals = Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        for (int i = 0; i < animals.Length; i++)
        {
            if (animals[i] == null)
                continue;
            animals[i].transform.position = WorldGround.Snap(animals[i].transform.position);
        }
    }

    static void StretchView()
    {
        GameObject player = GameObject.Find("Player");
        if (player == null)
            return;

        Camera cam = player.GetComponentInChildren<Camera>();
        if (cam != null)
            cam.farClipPlane = 8000f;
    }

    static void FixTerrainShaders()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null)
                continue;

            terrain.drawInstanced = false;
            terrain.treeDistance = 0f;
            terrain.detailObjectDistance = 0f;
            if (shader != null && (terrain.materialTemplate == null || terrain.materialTemplate.shader != shader))
                terrain.materialTemplate = new Material(shader);
        }
    }

    static string[] TreePrefabs()
    {
        const string realistic = "Assets/RealisticTree/RealisticTree_LOD.prefab";
        if (System.IO.File.Exists(realistic))
            return new[] { realistic };

#if UNITY_EDITOR
        var matches = new System.Collections.Generic.List<string>();
        if (System.IO.Directory.Exists("Assets"))
        {
            string[] folders = System.IO.Directory.GetDirectories("Assets", "*", System.IO.SearchOption.AllDirectories);
            for (int i = 0; i < folders.Length; i++)
            {
                string folder = folders[i].Replace('\\', '/');
                if (folder.IndexOf("Conifer", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
                for (int g = 0; g < guids.Length; g++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[g]);
                    string file = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (file.IndexOf("Billboard", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (file.IndexOf("Mobile", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    if (!matches.Contains(path))
                        matches.Add(path);
                }
            }
        }

        if (matches.Count > 0)
            return matches.ToArray();
#endif
        return new[]
        {
            "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Tree_02.prefab",
            "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Tree_10.prefab",
            "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Birch_Tree_05.prefab",
            "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Birch_Tree_06.prefab"
        };
    }

    static void ReplaceBrokenTreeMeshes()
    {
        MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.forceMeshLod = 0;
            Material[] current = renderer.sharedMaterials;
            if (current == null || current.Length == 0 || current[0] == null)
                continue;

            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                continue;
            if (filter.sharedMesh.subMeshCount <= current.Length)
                continue;

            Material[] filled = new Material[filter.sharedMesh.subMeshCount];
            for (int m = 0; m < filled.Length; m++)
                filled[m] = m < current.Length && current[m] != null ? current[m] : current[0];
            renderer.sharedMaterials = filled;
        }
    }

    static void ApplyFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.0028f;
        RenderSettings.fogColor = new Color(0.55f, 0.62f, 0.68f);
    }

    static void ScatterPrefabs(
        Transform parent,
        Vector3 camp,
        string[] paths,
        int count,
        float minRadius,
        float maxRadius,
        float minScale,
        float maxScale,
        bool harvestable)
    {
        GameObject[] prefabs = LoadPrefabs(paths);
        if (prefabs.Length == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            float angle = i * 137.508f * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(minRadius, maxRadius, (i % 9) / 8f);
            Vector3 pos = camp + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            pos = WorldGround.Snap(pos);

            GameObject prefab = prefabs[i % prefabs.Length];
            GameObject instance = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, i * 47f, 0f), parent);
            float scale = Mathf.Lerp(minScale, maxScale, (i % 5) / 4f);
            instance.transform.localScale *= scale;

            if (harvestable && instance.GetComponent<HarvestableTree>() == null)
                instance.AddComponent<HarvestableTree>();
        }
    }

    static GameObject[] LoadPrefabs(string[] paths)
    {
        var list = new System.Collections.Generic.List<GameObject>();
        for (int i = 0; i < paths.Length; i++)
        {
            GameObject prefab = Load(paths[i]);
            if (prefab != null)
                list.Add(prefab);
        }

        return list.ToArray();
    }

    static GameObject Load(string path)
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
        return null;
#endif
    }
}
