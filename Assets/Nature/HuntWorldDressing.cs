using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class HuntWorldDressing : MonoBehaviour
{
    const string FoliageRootName = "_HuntFoliage";
    const string TreePrefab = "Assets/RealisticTree/RealisticTree_LOD.prefab";
    const string UnwantedRockName = "FCP_RockCluster_Type1_Color1";

    const float QualityDensityMultiplier = 1.35f;
    const float FoliageRadiusMultiplier = 1.25f;
    const float AtmosphereFogDensity = 0.0019f;
    const float AtmosphereFogBlue = 0.74f;

    static readonly Color[] UnsRockPalette =
    {
        new Color(0.27f, 0.30f, 0.32f, 1f),
        new Color(0.33f, 0.35f, 0.34f, 1f),
        new Color(0.30f, 0.33f, 0.29f, 1f),
        new Color(0.39f, 0.37f, 0.34f, 1f)
    };

    static readonly string[] PrefabFolders =
    {
        "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs",
        "Assets/URP GanzSe Free Camping Props/Prefabs",
        "Assets/RealisticTree",
        "Assets/Nature"
    };

    static readonly string[] TreeHints = { "tree", "pine", "oak", "fir", "spruce" };
    static readonly string[] GrassHints = { "grass", "fern", "weed", "plant", "tuft" };
    static readonly string[] BushHints = { "bush", "shrub", "hedge" };
    static readonly string[] RockHints = { "rock", "stone", "boulder" };
    static readonly string[] DecorHints = { "stump", "log", "mushroom", "flower", "branch" };
    static readonly string[] ExcludeHints =
    {
        "demo", "preview", "fx", "particle", "weapon", "shot", "axe", "animal", "deer", "bird", "player", "ui", "icon"
    };

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

            // Never tint RPG Poly Pack rocks: the whole pack shares one atlas material,
            // so tinting it turns every house, tree and prop dark grey.
            if (filled[m] != null && IsRockRenderer(renderer) && !UsesRpgppMaterial(filled[m]) &&
                MaterialTooBright(filled[m]))
            {
                ApplyUnsRockTint(filled[m], renderer, m);
                changed = true;
            }
        }

        if (changed)
            renderer.sharedMaterials = filled;
    }

    static bool IsRockRenderer(Renderer renderer)
    {
        Transform t = renderer.transform;
        while (t != null)
        {
            if (t.name.IndexOf("rock", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.name.IndexOf("stone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                t.name.IndexOf("boulder", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            t = t.parent;
        }

        return false;
    }

    static bool UsesRpgppMaterial(Material mat)
    {
        return mat.name.StartsWith("rpgpp_", System.StringComparison.OrdinalIgnoreCase);
    }

    static bool MaterialTooBright(Material mat)
    {
        Color color = Color.white;
        if (mat.HasProperty("_BaseColor"))
            color = mat.GetColor("_BaseColor");
        else if (mat.HasProperty("_Color"))
            color = mat.GetColor("_Color");

        float luminance = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        bool neonGreen = color.g > 0.7f && color.r > 0.6f;
        return luminance > 0.62f || neonGreen;
    }

    static void ApplyUnsRockTint(Material mat, Renderer renderer, int slot)
    {
        int idx = Mathf.Abs((renderer.transform.GetSiblingIndex() * 17 + slot * 31) % UnsRockPalette.Length);
        Color tint = UnsRockPalette[idx];

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", tint);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", tint);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.22f);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.02f);

        if (mat.IsKeywordEnabled("_EMISSION"))
            mat.DisableKeyword("_EMISSION");
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

    public static void Dress(bool rebuildFoliage = false)
    {
        FixTerrainShaders();
        GameCulling.ApplyForActiveScene();
        ReplaceBrokenTreeMeshes();
        // The RPG Poly Pack rebuild lays out the camp on a levelled pad; don't re-arrange it.
        if (!RpgppScenery.Present)
            ArrangeCamp();
        SnapCastlesToGround();
        CastleMarket.Place();
        RemoveUnwantedRocks();

        // The RPG Poly Pack scenery replaces this foliage; don't regenerate it.
        if (RpgppScenery.Present)
            return;

        GameObject existingRoot = GameObject.Find(FoliageRootName);
        if (existingRoot != null && !rebuildFoliage)
            return;

        if (existingRoot != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(existingRoot);
            else
                Object.Destroy(existingRoot);
#else
            Object.Destroy(existingRoot);
#endif
        }

        Terrain terrain = Object.FindAnyObjectByType<Terrain>();
        if (terrain == null)
            return;

        SnapCampAndWildlife();
        ApplyFog();

        GameObject root = new GameObject(FoliageRootName);
        Vector3 camp = FindCamp();

        int trees = ScaledCount(120);
        int grasses = ScaledCount(480);
        int bushes = ScaledCount(92);
        int rocks = ScaledCount(54);
        int decor = ScaledCount(74);

        string[] treePrefabs = CategoryPrefabs(TreeHints, new[] { TreePrefab });
        string[] grassPrefabs = CategoryPrefabs(
            GrassHints,
            new[]
            {
                "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Grass_11.prefab",
                "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Grass_15.prefab",
                "Assets/URP GanzSe Free Camping Props/Prefabs/FCP_GrassCluster_Type1_Color1.prefab"
            });
        string[] bushPrefabs = CategoryPrefabs(
            BushHints,
            new[]
            {
                "Assets/URP GanzSe Free Camping Props/Prefabs/FCP__Bush_Type1_Color1.prefab"
            });
        string[] rockPrefabs = CategoryPrefabs(
            RockHints,
            new[]
            {
                "Assets/Pure Poly/Free Low Poly Nature Pack/Prefabs/PP_Rock_Moss_Grown_09.prefab"
            });
        string[] decorPrefabs = CategoryPrefabs(DecorHints, new string[0]);

        ScatterPrefabs(root.transform, camp, treePrefabs, trees, 22f, 520f * FoliageRadiusMultiplier, 0.95f, 1.6f, 40f, 5.4f, true);
        ScatterPrefabs(root.transform, camp, grassPrefabs, grasses, 8f, 260f, 0.75f, 1.8f, 45f, 1.05f, false);
        ScatterPrefabs(root.transform, camp, bushPrefabs, bushes, 16f, 300f, 0.85f, 1.55f, 40f, 2.0f, false);
        ScatterPrefabs(root.transform, camp, rockPrefabs, rocks, 22f, 360f, 0.75f, 1.6f, 52f, 3.2f, false);
        ScatterPrefabs(root.transform, camp, decorPrefabs, decor, 14f, 260f, 0.75f, 1.35f, 42f, 1.6f, false);

        RemoveUnwantedRocks();
    }

    static void RemoveUnwantedRocks()
    {
        GameObject[] objects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
        for (int i = objects.Length - 1; i >= 0; i--)
        {
            GameObject go = objects[i];
            if (go == null)
                continue;
            if (go.name.IndexOf(UnwantedRockName, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(go);
            else
                Object.Destroy(go);
#else
            Object.Destroy(go);
#endif
        }
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
            if (animals[i] == null || animals[i].kind == AnimalKind.Bird)
                continue;
            animals[i].transform.position = WorldGround.Snap(animals[i].transform.position);
        }
    }

    static void SnapCastlesToGround()
    {
        CastleBuilder[] builders = Object.FindObjectsByType<CastleBuilder>(FindObjectsInactive.Exclude);
        for (int i = 0; i < builders.Length; i++)
        {
            if (builders[i] != null)
                SitBuildingOnGround(builders[i].transform, 0.2f);
        }

        string[] names = { "newCastle", "Castle", "Castleie" };
        for (int i = 0; i < names.Length; i++)
        {
            GameObject go = GameObject.Find(names[i]);
            if (go == null)
                continue;
            if (go.GetComponent<CastleBuilder>() != null)
                continue;
            SitBuildingOnGround(go.transform, 0.2f);
        }
    }

    static void SitBuildingOnGround(Transform root, float sink)
    {
        if (root == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            if (root.childCount == 0)
                return;
            root.position = WorldGround.Sit(root.position, -sink);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
                bounds.Encapsulate(renderers[i].bounds);
        }

        // Use the terrain itself (a physics ray can hit the building's own floors and
        // lift it into the air), and the lowest point under the footprint so no edge floats.
        float lowest = float.MaxValue;
        for (int ix = -1; ix <= 1; ix++)
        {
            for (int iz = -1; iz <= 1; iz++)
            {
                Vector3 probe = new Vector3(
                    bounds.center.x + ix * bounds.extents.x * 0.9f,
                    bounds.min.y,
                    bounds.center.z + iz * bounds.extents.z * 0.9f);
                if (WorldGround.TryGetGround(probe, out Vector3 point, out _))
                    lowest = Mathf.Min(lowest, point.y);
            }
        }

        if (lowest == float.MaxValue)
            return;

        float delta = lowest - bounds.min.y - sink;
        if (Mathf.Abs(delta) < 0.02f)
            return;

        root.position += Vector3.up * delta;
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
        RenderSettings.fogDensity = AtmosphereFogDensity;
        RenderSettings.fogColor = new Color(0.58f, 0.66f, AtmosphereFogBlue);
    }

    static int ScaledCount(int baseCount)
    {
        Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Exclude);
        float area = 0f;
        for (int i = 0; i < terrains.Length; i++)
        {
            if (terrains[i] == null || terrains[i].terrainData == null)
                continue;
            Vector3 size = terrains[i].terrainData.size;
            area += size.x * size.z;
        }

        float baseline = 512f * 512f;
        float factor = area > 1f ? Mathf.Clamp(area / baseline, 1.0f, 5.2f) : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(baseCount * factor * QualityDensityMultiplier));
    }

    static string[] CategoryPrefabs(string[] hints, string[] fallback)
    {
#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:prefab", PrefabFolders);
        List<string> found = new List<string>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path))
                continue;
            if (path.IndexOf(UnwantedRockName, System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            string lower = path.ToLowerInvariant();
            if (!ContainsAny(lower, hints) || ContainsAny(lower, ExcludeHints))
                continue;

            if (!found.Contains(path))
                found.Add(path);
        }

        if (fallback != null)
        {
            for (int i = 0; i < fallback.Length; i++)
            {
                if (!string.IsNullOrEmpty(fallback[i]) && !found.Contains(fallback[i]))
                    found.Add(fallback[i]);
            }
        }

        return found.ToArray();
#else
        return fallback ?? new string[0];
#endif
    }

    static bool ContainsAny(string text, string[] hints)
    {
        if (string.IsNullOrEmpty(text) || hints == null)
            return false;

        for (int i = 0; i < hints.Length; i++)
        {
            if (!string.IsNullOrEmpty(hints[i]) && text.Contains(hints[i]))
                return true;
        }

        return false;
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
        float maxSlope,
        float minSpacing,
        bool harvestable)
    {
        GameObject[] prefabs = LoadPrefabs(paths);
        if (prefabs.Length == 0)
            return;

        int attempts = Mathf.Max(count * 10, 64);
        int placed = 0;
        for (int i = 0; i < attempts && placed < count; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float radius = Mathf.Lerp(minRadius, maxRadius, Mathf.Sqrt(Random.value));
            Vector3 pos = camp + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            if (!WorldGround.TryGetGround(pos, out Vector3 ground, out Vector3 normal))
                continue;
            if (WorldGround.InsideWater(ground))
                continue;

            float slope = Vector3.Angle(normal, Vector3.up);
            if (slope > maxSlope)
                continue;
            if (minSpacing > 0.01f && TooCloseToExisting(parent, ground, minSpacing))
                continue;

            GameObject prefab = prefabs[placed % prefabs.Length];
            Quaternion rot = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up);
            GameObject instance = Object.Instantiate(prefab, ground, rot, parent);
            float scale = Random.Range(minScale, maxScale);
            instance.transform.localScale *= scale;

            if (harvestable && instance.GetComponent<HarvestableTree>() == null)
                instance.AddComponent<HarvestableTree>();

            placed++;
        }
    }

    static bool TooCloseToExisting(Transform parent, Vector3 position, float spacing)
    {
        float sqr = spacing * spacing;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
                continue;
            Vector3 delta = child.position - position;
            delta.y = 0f;
            if (delta.sqrMagnitude < sqr)
                return true;
        }

        return false;
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
