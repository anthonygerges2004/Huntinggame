using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class HuntWorldDressing : MonoBehaviour
{
    const string FoliageRootName = "_HuntFoliage";
    const string TreePrefab = "Assets/RealisticTree/RealisticTree_LOD.prefab";

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

    static Material fallbackMaterial;

    static Material FallbackMaterial()
    {
        if (fallbackMaterial != null)
            return fallbackMaterial;

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null)
            unlit = Shader.Find("Universal Render Pipeline/Lit");
        if (unlit == null)
            return null;

        fallbackMaterial = new Material(unlit);
        fallbackMaterial.name = "Hunt_Fallback";
        fallbackMaterial.color = new Color(0.32f, 0.42f, 0.18f, 1f);
        if (fallbackMaterial.HasProperty("_BaseColor"))
            fallbackMaterial.SetColor("_BaseColor", fallbackMaterial.color);
        return fallbackMaterial;
    }

    static bool ShaderIsBroken(Material material)
    {
        if (material == null || material.shader == null)
            return true;

        string name = material.shader.name;
        return name.Contains("Error") ||
               name.StartsWith("Hidden/") ||
               name == "Standard" ||
               name.StartsWith("Standard ") ||
               name.StartsWith("Legacy Shaders") ||
               name.StartsWith("Nature/") ||
               name.StartsWith("Particles/") ||
               name.StartsWith("Mobile/");
    }

    static void RepairRenderer(Renderer renderer)
    {
        if (renderer == null)
            return;

        if (renderer.GetComponentInParent<LODGroup>() == null)
            renderer.forceMeshLod = -1;

        if (renderer.GetComponent<Animal>() != null && renderer is MeshRenderer meshRenderer)
        {
            meshRenderer.enabled = false;
            return;
        }

        Material[] materials = renderer.sharedMaterials;
        int slotCount = materials == null ? 0 : materials.Length;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
            slotCount = Mathf.Max(slotCount, filter.sharedMesh.subMeshCount);
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        if (skinned != null && skinned.sharedMesh != null)
            slotCount = Mathf.Max(slotCount, skinned.sharedMesh.subMeshCount);
        if (slotCount == 0)
            return;

        Material fallback = FallbackMaterial();
        Material[] filled = new Material[slotCount];
        bool changed = materials == null || materials.Length != slotCount;
        for (int m = 0; m < slotCount; m++)
        {
            Material current = materials != null && m < materials.Length ? materials[m] : null;
            if (ShaderIsBroken(current))
            {
                if (current != null && current.shader != null && fallback != null)
                {
                    Texture tex = current.HasProperty("_BaseMap") ? current.GetTexture("_BaseMap") : null;
                    if (tex == null && current.HasProperty("_MainTex"))
                        tex = current.GetTexture("_MainTex");
                    current.shader = fallback.shader;
                    if (tex != null)
                    {
                        current.SetTexture("_BaseMap", tex);
                        current.SetTexture("_MainTex", tex);
                    }
                    filled[m] = current;
                }
                else
                {
                    filled[m] = fallback;
                }
                changed = true;
            }
            else
            {
                filled[m] = current;
            }
        }

        if (changed)
            renderer.sharedMaterials = filled;
    }

    static void FixPinkTreesNow()
    {
        Animal[] animals = Object.FindObjectsByType<Animal>(FindObjectsInactive.Include);
        for (int i = 0; i < animals.Length; i++)
        {
            if (animals[i] == null)
                continue;
            MeshRenderer capsule = animals[i].GetComponent<MeshRenderer>();
            if (capsule != null)
                capsule.enabled = false;
        }

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        for (int i = 0; i < renderers.Length; i++)
            RepairRenderer(renderers[i]);

        GameCulling.ApplyForActiveScene();
    }

    public static void Dress()
    {
        FixTerrainShaders();
        GameCulling.ApplyForActiveScene();
        ReplaceBrokenTreeMeshes();
        ArrangeCamp();

        if (GameObject.Find(FoliageRootName) != null)
            return;

        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null)
            return;

        SnapCampAndWildlife();
        ApplyFog();

        GameObject root = new GameObject(FoliageRootName);
        Vector3 camp = FindCamp();

        ScatterPrefabs(root.transform, camp, new[] { TreePrefab }, 36, 18f, 260f, 0.95f, 1.35f, true);

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

    static void ArrangeCamp()
    {
        GameObject fire = GameObject.Find("Campfire");
        if (fire == null)
            fire = GameObject.Find("FCP_Campfire_Type1_Color1");
        if (fire == null)
            return;

        WorldGround.TryGetGround(fire.transform.position, out Vector3 center, out Vector3 slope);
        fire.transform.position = center;
        fire.transform.rotation = Quaternion.identity;

        Vector3 uphill = new Vector3(slope.x, 0f, slope.z);
        if (uphill.sqrMagnitude < 0.0001f)
            uphill = Vector3.forward;
        uphill.Normalize();
        Vector3 downhill = -uphill;
        Vector3 across = Vector3.Cross(Vector3.up, uphill).normalized;

        PlaceCamp("FCP_Campfire_Type1_Color1", center, Vector3.zero, false);
        PlaceCamp("FCP_Tent_Type1_Color1", center + uphill * 5.4f, center, true);
        PlaceCamp("FCP_Bench_Type1_Color1", center + across * -2.8f + downhill * 0.8f, center, true);
        PlaceCamp("FCP_Bench_Type1_Color1_1", center + across * 2.8f + downhill * 0.8f, center, true);
        PlaceCamp("FCP_FlagBunting_Type1_Color1", center + downhill * 4.6f, center + downhill * 8f, false);
        PlaceCamp("FCP_FlagBunting_Type1_Color1_1", center + downhill * 4.6f, center + downhill * 8f, false);
    }

    static void PlaceCamp(string objectName, Vector3 worldPos, Vector3 lookAt, bool sitOnSlope)
    {
        GameObject go = GameObject.Find(objectName);
        if (go == null)
            return;

        WorldGround.TryGetGround(worldPos, out Vector3 point, out Vector3 normal);
        go.transform.position = point;

        Vector3 forward = lookAt - point;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.05f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 up = sitOnSlope ? Vector3.Slerp(Vector3.up, normal, 0.65f) : Vector3.up;
        forward = Vector3.ProjectOnPlane(forward, up);
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;
        go.transform.rotation = Quaternion.LookRotation(forward.normalized, up);
    }

    static void SnapCampAndWildlife()
    {
        ArrangeCamp();

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
            if (shader != null && (terrain.materialTemplate == null || terrain.materialTemplate.shader != shader))
                terrain.materialTemplate = new Material(shader);
        }
    }

    static void ReplaceBrokenTreeMeshes()
    {
        MeshRenderer[] renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null)
                continue;

            if (renderer.GetComponentInParent<LODGroup>() == null)
                renderer.forceMeshLod = -1;
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
