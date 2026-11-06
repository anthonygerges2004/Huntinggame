using UnityEngine;

public enum HuntWeapon
{
    None = 0,
    Shotgun = 1,
    Rifle = 2,
    Sniper = 3
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
    public static WeaponModel Build(HuntWeapon weapon, Transform parent)
    {
        if (weapon == HuntWeapon.Shotgun)
            return Finish(ShotgunVisual.Build(parent), ShotgunVisual.StockLocal, ShotgunVisual.MuzzleLocal, ShotgunVisual.FittedScale);

        GameObject root = new GameObject(weapon == HuntWeapon.Sniper ? "Sniper" : "Rifle");
        if (parent != null)
            root.transform.SetParent(parent, false);

        bool sniper = weapon == HuntWeapon.Sniper;
        Material wood = MakeMat(sniper ? new Color(0.16f, 0.13f, 0.10f) : new Color(0.40f, 0.24f, 0.12f), 0.22f);
        Material steel = MakeMat(sniper ? new Color(0.12f, 0.13f, 0.14f) : new Color(0.22f, 0.23f, 0.25f), 0.72f);
        Material glass = MakeMat(new Color(0.05f, 0.12f, 0.16f), 0.92f);

        Part(root.transform, PrimitiveType.Cube, wood, "Stock",
            new Vector3(0f, 0.01f, -0.28f), Quaternion.Euler(sniper ? 6f : 10f, 0f, 0f), new Vector3(0.04f, 0.07f, 0.34f));
        Part(root.transform, PrimitiveType.Cube, steel, "Receiver",
            new Vector3(0f, 0.035f, 0.02f), Quaternion.identity, new Vector3(0.038f, 0.046f, 0.22f));
        Part(root.transform, PrimitiveType.Cube, steel, "Barrel",
            new Vector3(0f, 0.045f, sniper ? 0.52f : 0.42f), Quaternion.identity,
            new Vector3(sniper ? 0.018f : 0.022f, sniper ? 0.018f : 0.022f, sniper ? 0.78f : 0.58f));
        Part(root.transform, PrimitiveType.Cube, steel, "Bolt",
            new Vector3(0.03f, 0.05f, -0.02f), Quaternion.Euler(0f, 0f, 70f), new Vector3(0.012f, 0.04f, 0.012f));

        Vector3 muzzle = new Vector3(0f, 0.045f, sniper ? 0.91f : 0.71f);
        if (sniper)
        {
            Part(root.transform, PrimitiveType.Cylinder, steel, "ScopeTube",
                new Vector3(0f, 0.09f, 0.08f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.034f, 0.16f, 0.034f));
            Part(root.transform, PrimitiveType.Cylinder, steel, "ScopeFront",
                new Vector3(0f, 0.09f, 0.25f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.046f, 0.03f, 0.046f));
            Part(root.transform, PrimitiveType.Cylinder, glass, "ScopeGlass",
                new Vector3(0f, 0.09f, 0.275f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.032f, 0.008f, 0.032f));
            Part(root.transform, PrimitiveType.Cylinder, steel, "EyePiece",
                new Vector3(0f, 0.09f, -0.09f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.04f, 0.025f, 0.04f));
        }
        else
        {
            Part(root.transform, PrimitiveType.Cube, steel, "Sight",
                new Vector3(0f, 0.07f, 0.62f), Quaternion.identity, new Vector3(0.006f, 0.03f, 0.006f));
        }

        Vector3 stock = new Vector3(0f, 0.02f, -0.44f);
        return Finish(root, stock, muzzle, 1f);
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
