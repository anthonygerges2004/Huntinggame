using UnityEngine;

public enum FlyerKind
{
    Sparrow,
    Crow,
    Duck,
    Hawk
}

public static class BirdVisual
{
    public const string BodyName = "BirdBody";

    public static void Build(Transform parent, FlyerKind kind, out Transform wingLeft, out Transform wingRight)
    {
        Colors(kind, out Color body, out Color wing, out Color beak, out Color accent);
        float s = Scale(kind);
        Material bodyMat = Make(body);
        Material wingMat = Make(wing);
        Material beakMat = Make(beak);
        Material accentMat = Make(accent);

        Part(parent, PrimitiveType.Sphere, bodyMat, BodyName, new Vector3(0f, 0.15f * s, 0f), new Vector3(0.32f, 0.28f, 0.52f) * s);

        if (kind == FlyerKind.Duck)
        {
            Part(parent, PrimitiveType.Sphere, accentMat, "Head", new Vector3(0f, 0.34f * s, 0.22f * s), new Vector3(0.24f, 0.24f, 0.24f) * s);
            Part(parent, PrimitiveType.Cube, beakMat, "Beak", new Vector3(0f, 0.32f * s, 0.4f * s), new Vector3(0.1f, 0.05f, 0.18f) * s);
        }
        else
        {
            Part(parent, PrimitiveType.Sphere, bodyMat, "Head", new Vector3(0f, 0.33f * s, 0.2f * s), new Vector3(0.22f, 0.22f, 0.22f) * s);
            Part(parent, PrimitiveType.Cube, beakMat, "Beak", new Vector3(0f, 0.32f * s, 0.35f * s), new Vector3(0.06f, 0.06f, 0.14f) * s);
        }

        float tail = kind == FlyerKind.Hawk ? 0.42f : 0.3f;
        Part(parent, PrimitiveType.Cube, wingMat, "Tail", new Vector3(0f, 0.17f * s, -tail * s), new Vector3(0.16f, 0.03f, 0.24f) * s);

        float wingSpan = kind == FlyerKind.Hawk ? 1.55f : kind == FlyerKind.Duck ? 1.2f : 1f;
        wingLeft = Wing(parent, wingMat, -1f, s, wingSpan);
        wingRight = Wing(parent, wingMat, 1f, s, wingSpan);
    }

    public static float ColliderRadius(FlyerKind kind)
    {
        switch (kind)
        {
            case FlyerKind.Hawk:
                return 0.55f;
            case FlyerKind.Duck:
                return 0.42f;
            case FlyerKind.Crow:
                return 0.36f;
            default:
                return 0.3f;
        }
    }

    static Transform Wing(Transform parent, Material mat, float side, float scale, float span)
    {
        Transform pivot = new GameObject(side < 0f ? "WingLeft" : "WingRight").transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = new Vector3(0.12f * side * scale, 0.22f * scale, 0f);
        Part(pivot, PrimitiveType.Cube, mat, "Feather", new Vector3(0.22f * side * scale * span, 0f, 0f), new Vector3(0.4f * scale * span, 0.03f * scale, 0.26f * scale));
        return pivot;
    }

    static void Part(Transform parent, PrimitiveType type, Material mat, string name, Vector3 pos, Vector3 scale)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        Object.Destroy(part.GetComponent<Collider>());
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static float Scale(FlyerKind kind)
    {
        switch (kind)
        {
            case FlyerKind.Crow:
                return 1.2f;
            case FlyerKind.Duck:
                return 1.35f;
            case FlyerKind.Hawk:
                return 1.7f;
            default:
                return 1f;
        }
    }

    static void Colors(FlyerKind kind, out Color body, out Color wing, out Color beak, out Color accent)
    {
        switch (kind)
        {
            case FlyerKind.Crow:
                body = new Color(0.12f, 0.12f, 0.14f);
                wing = new Color(0.08f, 0.08f, 0.1f);
                beak = new Color(0.2f, 0.2f, 0.22f);
                accent = body;
                return;
            case FlyerKind.Duck:
                body = new Color(0.45f, 0.32f, 0.18f);
                wing = new Color(0.28f, 0.22f, 0.14f);
                beak = new Color(0.92f, 0.55f, 0.12f);
                accent = new Color(0.12f, 0.38f, 0.2f);
                return;
            case FlyerKind.Hawk:
                body = new Color(0.62f, 0.48f, 0.32f);
                wing = new Color(0.42f, 0.34f, 0.24f);
                beak = new Color(0.85f, 0.55f, 0.2f);
                accent = new Color(0.86f, 0.82f, 0.72f);
                return;
            default:
                body = new Color(0.45f, 0.3f, 0.18f);
                wing = new Color(0.28f, 0.19f, 0.11f);
                beak = new Color(0.95f, 0.62f, 0.15f);
                accent = body;
                return;
        }
    }

    static Material Make(Color color)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        return mat;
    }
}
