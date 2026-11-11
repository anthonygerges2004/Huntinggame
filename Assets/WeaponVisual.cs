using UnityEngine;

public enum HuntWeapon
{
    None = 0,
    Shotgun = 1,
    Rifle = 2,
    Sniper = 3,
    Pistol = 4,
    SMG = 5
}

public struct WeaponModel
{
    public GameObject root;
    public Vector3 stock;
    public Vector3 muzzle;
    public Vector3 rightGrip;
    public Vector3 leftGrip;
    public float scale;
}

public static class WeaponVisual
{
    public const int Count = 6;

    // Gun pack models (Resources/LowPolyGuns), one per weapon type, at real-world lengths.
    static readonly string[] PackNames = { null, "shotgun1", "assault1", "sniper1", "pistol1", "smg1" };
    static readonly float[] PackLengths = { 0f, 1.02f, 0.94f, 1.2f, 0.27f, 0.66f };
    // Where the firing hand sits, as a fraction of the length from the back of the gun.
    static readonly float[] GripAt = { 0f, 0.38f, 0.36f, 0.4f, 0.22f, 0.36f };

    public static string DisplayName(HuntWeapon weapon)
    {
        switch (weapon)
        {
            case HuntWeapon.Pistol: return "Pistol";
            case HuntWeapon.Shotgun: return "Combat shotgun";
            case HuntWeapon.SMG: return "SMG";
            case HuntWeapon.Rifle: return "Assault rifle";
            case HuntWeapon.Sniper: return "Sniper rifle";
            default: return "";
        }
    }

    public static WeaponModel Build(HuntWeapon weapon, Transform parent)
    {
        GameObject pack = BuildPack(weapon, out Vector3 packStock, out Vector3 packMuzzle);
        if (pack != null)
        {
            if (parent != null)
                pack.transform.SetParent(parent, false);
            WeaponModel model = Finish(pack, packStock, packMuzzle, 1f);
            if (weapon == HuntWeapon.Pistol)
            {
                // Both hands wrap the grip.
                model.rightGrip = new Vector3(0f, -0.03f, -0.01f);
                model.leftGrip = new Vector3(-0.025f, -0.04f, 0.01f);
            }
            return model;
        }

        if (weapon == HuntWeapon.Shotgun)
            return Finish(ShotgunVisual.Build(parent), ShotgunVisual.StockLocal, ShotgunVisual.MuzzleLocal, ShotgunVisual.FittedScale);

        // Real-proportioned models: contoured stock, turned barrel, action, sights (and a scope on the sniper).
        Vector3 stock, muzzle;
        GameObject root = weapon == HuntWeapon.Sniper
            ? GunForge.Sniper(out stock, out muzzle)
            : GunForge.Rifle(out stock, out muzzle);
        if (parent != null)
            root.transform.SetParent(parent, false);
        return Finish(root, stock, muzzle, 1f);
    }

    /// <summary>
    /// Loads the pack model and lines it up with the game's gun convention: barrel along +Z, top up,
    /// origin at the firing grip, bore 4.5 cm above it, real length. The pack's axes are not assumed:
    /// the longest extent is the barrel, the thin end is the muzzle, the barrel runs along the top.
    /// </summary>
    static GameObject BuildPack(HuntWeapon weapon, out Vector3 stock, out Vector3 muzzle)
    {
        stock = muzzle = Vector3.zero;
        int i = (int)weapon;
        if (i <= 0 || i >= PackNames.Length)
            return null;
        string name = PackNames[i];
        GameObject prefab = Resources.Load<GameObject>("LowPolyGuns/" + name + "/" + name);
        if (prefab == null)
            return null;

        GameObject model = Object.Instantiate(prefab);
        model.name = "Model";
        model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        model.transform.localScale = Vector3.one;
        foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
            Object.Destroy(c);
        foreach (Animator a in model.GetComponentsInChildren<Animator>(true))
            Object.Destroy(a);

        Texture2D diffuse = Resources.Load<Texture2D>("LowPolyGuns/" + name + "/" + name + "_diffuse");
        Material mat = MeshForge.Lit("pack_" + name, Color.white, 0.42f, 0.35f, diffuse);

        var points = new System.Collections.Generic.List<Vector3>(4096);
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
                mats[m] = mat;
            r.sharedMaterials = mats;

            Mesh mesh = null;
            if (r is SkinnedMeshRenderer sk)
                mesh = sk.sharedMesh;
            else
            {
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null)
                    mesh = mf.sharedMesh;
            }
            if (mesh == null)
                continue;
            Matrix4x4 toRoot = r.transform.localToWorldMatrix;
            if (mesh.isReadable)
            {
                Vector3[] v = mesh.vertices;
                for (int k = 0; k < v.Length; k++)
                    points.Add(toRoot.MultiplyPoint3x4(v[k]));
            }
            else
            {
                Bounds b = mesh.bounds;
                for (int k = 0; k < 8; k++)
                    points.Add(toRoot.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                        new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1))));
            }
        }
        if (points.Count == 0)
        {
            Object.Destroy(model);
            return null;
        }

        Bounds all = new Bounds(points[0], Vector3.zero);
        for (int k = 1; k < points.Count; k++)
            all.Encapsulate(points[k]);

        // Axes by extent: length > height > width.
        Vector3 size = all.size;
        // The pack models stand upright (Y up); the barrel runs along the longer of X and Z.
        // (A revolver's tall grip made height look like the length and stood the gun on end.)
        int L = size.x >= size.z ? 0 : 2, U = 1;
        Vector3 lengthAxis = Vector3.zero; lengthAxis[L] = 1f;
        Vector3 upAxis = Vector3.zero; upAxis[U] = 1f;
        float length = size[L];
        float lo = all.min[L], hi = all.max[L];
        float slice = length * 0.12f;

        // The thin end is the muzzle.
        Bounds front = default, back = default;
        bool hasFront = false, hasBack = false;
        for (int k = 0; k < points.Count; k++)
        {
            Vector3 q = points[k];
            if (q[L] > hi - slice)
            {
                if (!hasFront) { front = new Bounds(q, Vector3.zero); hasFront = true; }
                else front.Encapsulate(q);
            }
            if (q[L] < lo + slice)
            {
                if (!hasBack) { back = new Bounds(q, Vector3.zero); hasBack = true; }
                else back.Encapsulate(q);
            }
        }
        bool muzzleHigh = !hasBack || (hasFront && front.size[U] <= back.size[U]);
        Bounds muzzleEnd = muzzleHigh ? front : back;
        Vector3 forward = muzzleHigh ? lengthAxis : -lengthAxis;

        // The barrel runs along the top: a muzzle below the middle means the model is upside down.


        Quaternion align = Quaternion.Inverse(Quaternion.LookRotation(forward, upAxis));
        float scale = PackLengths[i] / Mathf.Max(0.001f, length);
        float gripZ = PackLengths[i] * GripAt[i];

        Vector3 rear = all.center - forward * (length * 0.5f);
        Vector3 muzzleCentre = muzzleEnd.center;
        muzzleCentre[L] = all.center[L] + (muzzleHigh ? 0.5f : -0.5f) * length;
        float bore = (align * (muzzleCentre - rear)).y * scale;

        GameObject root = new GameObject(weapon.ToString());
        model.transform.SetParent(root.transform, false);
        model.transform.localRotation = align;
        model.transform.localScale = Vector3.one * scale;
        // Rear of the gun at z = -gripZ, bore at y = 0.045.
        model.transform.localPosition = -(align * rear) * scale + new Vector3(0f, 0.045f - bore, -gripZ);

        stock = new Vector3(0f, 0.025f, -gripZ);
        muzzle = new Vector3(0f, 0.045f, PackLengths[i] - gripZ);
        return root;
    }

    static WeaponModel Finish(GameObject root, Vector3 stock, Vector3 muzzle, float scale)
    {
        return new WeaponModel
        {
            root = root,
            stock = stock,
            muzzle = muzzle,
            rightGrip = Vector3.Lerp(stock, muzzle, 0.28f),
            leftGrip = Vector3.Lerp(stock, muzzle, 0.62f),
            scale = scale
        };
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
