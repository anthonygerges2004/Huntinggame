#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Menu: Tools > Realistic Axe > Setup Materials + Prefabs
// Built-in (Standard) and URP (Lit) supported. HDRP is not supported by this script.
public static class RealisticAxeSetup
{
    const string Folder = "Assets/RealisticAxe";

    [MenuItem("Tools/Realistic Axe/Setup Materials + Prefabs")]
    public static void Setup()
    {
        bool urp = GraphicsSettings.currentRenderPipeline != null;
        Shader sh = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
        if (sh == null) { Debug.LogError("Lit/Standard shader not found."); return; }

        Tex("Wood_albedo.png", false, true);  Tex("Wood_normal.png", true, true);
        Tex("Steel_albedo.png", false, true); Tex("Steel_normal.png", true, true);
        Tex("Steel_MetalSmooth.png", false, false);   // linear data texture (R = metallic, A = smoothness)

        var wood  = Mat("Wood", sh, urp, "Wood_albedo.png", "Wood_normal.png", null, 0.30f, 0f);
        var steel = Mat("Steel", sh, urp, "Steel_albedo.png", "Steel_normal.png", "Steel_MetalSmooth.png", 1f, 1f);

        foreach (var n in new[] { "FellingAxe", "Pickaxe" })
        {
            var mi = AssetImporter.GetAtPath(Folder + "/" + n + ".obj") as ModelImporter;
            if (mi == null) { Debug.LogError("Missing " + Folder + "/" + n + ".obj"); continue; }
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Wood"), wood);
            mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Steel"), steel);
            mi.importNormals = ModelImporterNormals.Import;
            mi.SaveAndReimport();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + n + ".obj");
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = n;
            PrefabUtility.SaveAsPrefabAsset(inst, Folder + "/" + n + ".prefab");
            Object.DestroyImmediate(inst);
        }
        Debug.Log("Realistic Axe ready: FellingAxe.prefab and Pickaxe.prefab created in " + Folder);
    }

    static void Tex(string file, bool normal, bool srgb)
    {
        var ti = AssetImporter.GetAtPath(Folder + "/" + file) as TextureImporter;
        if (ti == null) return;
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = srgb && !normal;
        ti.mipmapEnabled = true; ti.anisoLevel = 8;
        ti.SaveAndReimport();
    }

    static Material Mat(string name, Shader sh, bool urp, string albedo, string normal, string metalSmooth, float smooth, float metal)
    {
        string path = Folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        m.SetTexture(urp ? "_BaseMap" : "_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + albedo));
        m.SetColor(urp ? "_BaseColor" : "_Color", Color.white);
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + normal));
        m.EnableKeyword("_NORMALMAP");
        if (metalSmooth != null)
        {
            m.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + metalSmooth));
            m.EnableKeyword(urp ? "_METALLICSPECGLOSSMAP" : "_METALLICGLOSSMAP");
            m.SetFloat("_Metallic", 1f);
            m.SetFloat(urp ? "_Smoothness" : "_GlossMapScale", 1f);
        }
        else
        {
            m.SetFloat("_Metallic", metal);
            m.SetFloat(urp ? "_Smoothness" : "_Glossiness", smooth);
        }
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }
}
#endif
