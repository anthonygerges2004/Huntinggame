using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class ShotgunVisual
{
    public const string RootName = "Shotgun";
    const string PrefabPath = "Assets/Low Poly ShotGun Weapon Pack 1/Prefabs/Weapons/ShotGun_C.prefab";
    const string ResourcesPath = "Weapons/ShotGun_C";

    public static Vector3 StockLocal = new Vector3(0f, 0.02f, -0.26f);
    public static Vector3 MuzzleLocal = new Vector3(0f, 0.04f, 0.72f);
    public static float FittedScale = 1f;

    public static GameObject Build(Transform parent)
    {
        // The side-by-side model built in code; the imported low-poly asset is only a fallback.
        GameObject root = GunForge.Shotgun();
        root.name = RootName;
        GameObject prefab = root == null ? LoadPrefab() : null;
        if (prefab != null)
        {
            root = Object.Instantiate(prefab);
            root.name = RootName;
            StripExtras(root);
            FixShading(root);
        }
        else if (root == null)
        {
            root = FallbackBuild(null);
        }

        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        root.transform.localScale = Vector3.one;
        Measure(root.transform);
        FitLength(root.transform, 0.95f);
        Measure(root.transform);

        if (parent != null)
            root.transform.SetParent(parent, false);

        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        SetWorldScale(root.transform, FittedScale);
        ShotgunBreak.Rig(root.transform);
        return root;
    }

    public static void SetWorldScale(Transform target, float worldSize)
    {
        if (target == null)
            return;

        target.localScale = Vector3.one;
        Vector3 lossy = target.lossyScale;
        target.localScale = new Vector3(
            worldSize / Mathf.Max(0.0001f, lossy.x),
            worldSize / Mathf.Max(0.0001f, lossy.y),
            worldSize / Mathf.Max(0.0001f, lossy.z));
    }

    static GameObject LoadPrefab()
    {
        GameObject prefab = Resources.Load<GameObject>(ResourcesPath);
#if UNITY_EDITOR
        if (prefab == null)
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
#endif
        return prefab;
    }

    static void StripExtras(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            Object.Destroy(colliders[i]);

        Camera[] cameras = root.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
            cameras[i].enabled = false;

        AudioSource[] audio = root.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audio.Length; i++)
            audio[i].enabled = false;
    }

    static void FixShading(GameObject root)
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] mats = renderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null || mat.shader == null)
                    continue;
                if (mat.shader.name.Contains("Error") || mat.shader.name == "Standard")
                {
                    Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    if (tex == null && mat.HasProperty("_BaseMap"))
                        tex = mat.GetTexture("_BaseMap");
                    mat.shader = lit;
                    if (tex != null)
                    {
                        mat.SetTexture("_BaseMap", tex);
                        mat.SetTexture("_MainTex", tex);
                    }
                }
            }

            renderers[i].materials = mats;
        }
    }

    static void FitLength(Transform root, float targetLength)
    {
        float length = MuzzleLocal.z - StockLocal.z;
        if (length < 0.15f)
            return;

        FittedScale = targetLength / length;
        root.localScale = Vector3.one * FittedScale;
    }

    static void Measure(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds world = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled)
                world.Encapsulate(renderers[i].bounds);
        }

        Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        Vector3 e = world.extents;
        Vector3 c = world.center;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);
            Vector3 local = root.InverseTransformPoint(corner);
            min = Vector3.Min(min, local);
            max = Vector3.Max(max, local);
        }

        StockLocal = new Vector3(0f, Mathf.Lerp(min.y, max.y, 0.45f), min.z + 0.02f);
        MuzzleLocal = new Vector3(0f, Mathf.Lerp(min.y, max.y, 0.72f), max.z);
    }

    static GameObject FallbackBuild(Transform parent)
    {
        GameObject root = new GameObject(RootName);
        if (parent != null)
            root.transform.SetParent(parent, false);
        Material wood = MakeMat(new Color(0.34f, 0.20f, 0.10f), 0.16f);
        Material steel = MakeMat(new Color(0.20f, 0.21f, 0.23f), 0.68f);
        Part(root.transform, PrimitiveType.Cube, wood, "Stock",
            new Vector3(0f, 0.02f, -0.26f), Quaternion.Euler(12f, 0f, 0f), new Vector3(0.045f, 0.08f, 0.30f));
        Part(root.transform, PrimitiveType.Cube, steel, "Barrels",
            new Vector3(0f, 0.03f, 0.38f), Quaternion.identity, new Vector3(0.055f, 0.04f, 0.62f));
        return root;
    }

    static void Part(Transform parent, PrimitiveType type, Material mat, string name, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        Object.Destroy(part.GetComponent<Collider>());
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        part.transform.localRotation = rot;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Material MakeMat(Color color, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }
}
