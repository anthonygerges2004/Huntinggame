#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Menu: Tools > Realistic Tree > Setup Materials + LOD Prefab
// Works with the Built-in pipeline (Standard shader) and URP (Lit shader). HDRP is not supported.
public static class RealisticTreeSetup
{
    const string Folder = "Assets/RealisticTree";
    public const string PrefabPath = Folder + "/RealisticTree_LOD.prefab";
    static bool settingUp;

    [InitializeOnLoadMethod]
    static void AutoSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (settingUp || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!System.IO.File.Exists(Folder + "/RealisticTree.obj"))
                return;
            if (System.IO.File.Exists(PrefabPath))
                return;
            Setup();
        };
    }

    [MenuItem("Tools/Realistic Tree/Setup Materials + LOD Prefab")]
    public static void Setup()
    {
        if (settingUp)
            return;
        settingUp = true;
        try
        {
            SetupNow();
        }
        finally
        {
            settingUp = false;
        }
    }

    static void SetupNow()
    {
        bool urp = GraphicsSettings.currentRenderPipeline != null;
        Shader shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
        if (shader == null) { Debug.LogError("Lit/Standard shader not found (HDRP is not supported)."); return; }

        SetTex("Bark_normal.png", true, false);
        SetTex("Bark_albedo.png", false, false);
        SetTex("Leaf_normal.png", true, false);
        SetTex("Leaf_albedo.png", false, true);

        var bark   = MakeMat("Bark", shader, urp, "Bark_albedo.png", "Bark_normal.png", false, Color.white, 0.10f);
        var leaves = MakeMat("Leaves", shader, urp, "Leaf_albedo.png", "Leaf_normal.png", true, new Color(1f, 1f, 1f), 0.25f);
        var mid    = MakeMat("LeavesMid", shader, urp, "Leaf_albedo.png", "Leaf_normal.png", true, new Color(0.86f, 0.90f, 0.82f), 0.22f);
        var inner  = MakeMat("LeavesInner", shader, urp, "Leaf_albedo.png", "Leaf_normal.png", true, new Color(0.66f, 0.74f, 0.60f), 0.18f);

        foreach (var model in new[] { "RealisticTree.obj", "RealisticTree_LOD1.obj" })
        {
            var mi = AssetImporter.GetAtPath(Folder + "/" + model) as ModelImporter;
            if (mi == null) { Debug.LogError("Missing " + Folder + "/" + model); continue; }
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            Remap(mi, "Bark", bark); Remap(mi, "Leaves", leaves);
            Remap(mi, "LeavesMid", mid); Remap(mi, "LeavesInner", inner);
            mi.importNormals = ModelImporterNormals.Import;
            mi.SaveAndReimport();
        }
        BuildLodPrefab();
        Debug.Log("Realistic Tree ready: drag Assets/RealisticTree/RealisticTree_LOD.prefab into your scene.");
    }

    static void Remap(ModelImporter mi, string srcName, Material m)
    {
        mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), srcName), m);
    }

    static void BuildLodPrefab()
    {
        var m0 = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/RealisticTree.obj");
        var m1 = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/RealisticTree_LOD1.obj");
        if (m0 == null || m1 == null) return;
        var root = new GameObject("RealisticTree_LOD");
        var l0 = (GameObject)PrefabUtility.InstantiatePrefab(m0); l0.name = "LOD0"; l0.transform.SetParent(root.transform, false);
        var l1 = (GameObject)PrefabUtility.InstantiatePrefab(m1); l1.name = "LOD1"; l1.transform.SetParent(root.transform, false);
        var lg = root.AddComponent<LODGroup>();
        lg.SetLODs(new[] {
            new LOD(0.30f, l0.GetComponentsInChildren<Renderer>()),
            new LOD(0.06f, l1.GetComponentsInChildren<Renderer>())
        });
        lg.RecalculateBounds();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }

    static void SetTex(string file, bool normal, bool alpha)
    {
        var ti = AssetImporter.GetAtPath(Folder + "/" + file) as TextureImporter;
        if (ti == null) return;
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = !normal;
        ti.alphaIsTransparency = alpha;
        ti.mipmapEnabled = true;
        ti.anisoLevel = 4;
        if (alpha) { ti.mipMapsPreserveCoverage = true; ti.alphaTestReferenceValue = 0.5f; }
        ti.SaveAndReimport();
    }

    static Material MakeMat(string name, Shader sh, bool urp, string albedo, string normal, bool cutout, Color tint, float smooth)
    {
        string path = Folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        m.SetTexture(urp ? "_BaseMap" : "_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + albedo));
        m.SetColor(urp ? "_BaseColor" : "_Color", tint);
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + normal));
        m.SetFloat("_BumpScale", cutout ? 0.8f : 1.0f);
        m.EnableKeyword("_NORMALMAP");
        m.SetFloat(urp ? "_Smoothness" : "_Glossiness", smooth);
        if (cutout)
        {
            m.SetFloat("_Cutoff", 0.5f);
            if (urp) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cull", 0f); }
            else m.SetFloat("_Mode", 1f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            m.SetOverrideTag("RenderType", "TransparentCutout");
        }
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }
}
#endif
