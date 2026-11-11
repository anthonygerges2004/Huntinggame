using UnityEngine;

public enum FlyerKind
{
    Sparrow,
    Crow,
    Duck,
    Hawk,
    Thrush,
    Robin,
    Jay,
    Woodpecker,
    Pigeon,
    Pheasant,
    Goose,
    Owl
}

public static class BirdVisual
{
    public const string BodyName = "BirdBody";

    public static void Build(Transform parent, FlyerKind kind, out Transform wingLeft, out Transform wingRight)
    {
        FlyerProfile profile = FlyerProfile.Of(kind);
        float s = profile.scale;
        Material body = CreatureForm.Fur(profile.body);
        Material wing = CreatureForm.FeatherMat(profile.wing);
        Material beak = CreatureForm.Keratin(profile.beak);
        Material accent = CreatureForm.Fur(profile.accent);
        Material dark = CreatureForm.Fur(profile.accent * 0.45f + Color.black * 0.55f);

        if (profile.shape == BirdShape.Water)
            BuildWater(parent, profile, s, body, accent, beak);
        else if (profile.shape == BirdShape.Owl)
            BuildOwl(parent, profile, s, body, accent, beak);
        else
            BuildSong(parent, kind, profile, s, body, accent, beak, dark);

        Material bar = kind == FlyerKind.Pigeon ? CreatureForm.FeatherMat(new Color(0.08f, 0.08f, 0.09f))
            : kind == FlyerKind.Jay ? CreatureForm.FeatherMat(new Color(0.06f, 0.06f, 0.08f))
            : null;
        Material speculum = kind == FlyerKind.Duck
            ? CreatureForm.FeatherMat(new Color(0.05f, 0.28f, 0.55f))
            : null;
        wingLeft = Wing(parent, wing, bar, speculum, -1f, s, profile.wingSpan);
        wingRight = Wing(parent, wing, bar, speculum, 1f, s, profile.wingSpan);
        Tail(parent, profile, s, wing, accent);
        Feet(parent, s, dark);
        if (parent.GetComponent<BirdFeet>() == null)
            parent.gameObject.AddComponent<BirdFeet>();
    }

    public static float ColliderRadius(FlyerKind kind)
    {
        return FlyerProfile.Of(kind).collider;
    }

    static void BuildSong(Transform parent, FlyerKind kind, FlyerProfile profile, float s, Material body, Material accent, Material beak, Material dark)
    {
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, body, BodyName, new Vector3(0f, 0.16f * s, -0.02f * s), new Vector3(0.22f, 0.18f, 0.36f) * s);
        Material headMat = kind == FlyerKind.Pheasant ? accent : body;
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, headMat, "Head", new Vector3(0f, 0.28f * s, 0.16f * s), new Vector3(0.16f, 0.15f, 0.16f) * s);
        Beak(parent, beak, new Vector3(0f, 0.27f * s, 0.25f * s), profile.beakLength * s, 0.03f * s, kind == FlyerKind.Hawk);
        Eyes(parent, new Vector3(0f, 0.31f * s, 0.24f * s), 0.04f * s, 0.018f * s);
        Breast(parent, kind, profile, s, accent, dark);
        Marks(parent, kind, s, accent);
    }

    static void BuildWater(Transform parent, FlyerProfile profile, float s, Material body, Material accent, Material beak)
    {
        bool goose = profile.objectName == "WildGoose";
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, body, BodyName, new Vector3(0f, 0.16f * s, -0.04f * s), new Vector3(0.28f, 0.2f, 0.46f) * s);
        if (!goose)
        {
            Material chest = CreatureForm.Fur(new Color(0.55f, 0.28f, 0.12f));
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, chest, "Chest", new Vector3(0f, 0.16f * s, 0.08f * s), new Vector3(0.2f, 0.14f, 0.16f) * s);
        }

        float neck = goose ? 0.22f : 0.12f;
        Material neckMat = goose ? body : accent;
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, neckMat, "Neck", new Vector3(0f, (0.24f + neck * 0.35f) * s, 0.16f * s), new Vector3(0.09f, neck, 0.09f) * s);
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, neckMat, "Head", new Vector3(0f, (0.3f + neck) * s, 0.22f * s), new Vector3(0.16f, 0.14f, 0.2f) * s);
        float beakY = (0.28f + neck) * s;
        float beakZ = 0.32f * s;
        CreatureForm.Add(parent, CreatureForm.Shape.Beak, beak, "Beak", new Vector3(0f, beakY, beakZ), new Vector3(goose ? 0.09f : 0.11f, 0.035f, profile.beakLength) * s);
        if (!goose)
        {
            Material ring = CreatureForm.Fur(new Color(0.92f, 0.92f, 0.88f));
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, ring, "Ring", new Vector3(0f, (0.2f + neck * 0.2f) * s, 0.14f * s), new Vector3(0.12f, 0.035f, 0.08f) * s);
        }

        Eyes(parent, new Vector3(0f, (0.33f + neck) * s, 0.33f * s), 0.045f * s, 0.02f * s);
    }

    static void BuildOwl(Transform parent, FlyerProfile profile, float s, Material body, Material accent, Material beak)
    {
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, body, BodyName, new Vector3(0f, 0.2f * s, 0f), new Vector3(0.32f, 0.34f, 0.3f) * s);
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, body, "Head", new Vector3(0f, 0.4f * s, 0.06f * s), new Vector3(0.28f, 0.26f, 0.26f) * s);
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, accent, "Disc", new Vector3(0f, 0.38f * s, 0.14f * s), new Vector3(0.22f, 0.2f, 0.08f) * s);
        CreatureForm.Add(parent, CreatureForm.Shape.Beak, beak, "Beak", new Vector3(0f, 0.34f * s, 0.2f * s), Quaternion.Euler(20f, 0f, 0f), new Vector3(0.04f, 0.035f, profile.beakLength) * s);
        Material eye = CreatureForm.Eye(new Color(0.05f, 0.04f, 0.03f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, eye, "Eye", new Vector3(-0.06f * s, 0.4f * s, 0.2f * s), Vector3.one * (0.07f * s));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, eye, "Eye", new Vector3(0.06f * s, 0.4f * s, 0.2f * s), Vector3.one * (0.07f * s));
        Catchlight(parent, new Vector3(-0.05f * s, 0.42f * s, 0.23f * s), 0.012f * s);
        Catchlight(parent, new Vector3(0.07f * s, 0.42f * s, 0.23f * s), 0.012f * s);
    }

    static void Breast(Transform parent, FlyerKind kind, FlyerProfile profile, float s, Material accent, Material dark)
    {
        if (kind == FlyerKind.Robin)
        {
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, accent, "Breast", new Vector3(0f, 0.14f * s, 0.08f * s), new Vector3(0.12f, 0.1f, 0.1f) * s);
            return;
        }

        if (kind == FlyerKind.Sparrow)
        {
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, accent, "Bib", new Vector3(0f, 0.18f * s, 0.12f * s), new Vector3(0.06f, 0.07f, 0.04f) * s);
            return;
        }

        if (kind == FlyerKind.Hawk)
        {
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, accent, "Chest", new Vector3(0f, 0.14f * s, 0.06f * s), new Vector3(0.16f, 0.12f, 0.14f) * s);
            return;
        }

        if (profile.breastSpots <= 1)
            return;

        Material buff = CreatureForm.Fur(Color.Lerp(profile.body, new Color(0.9f, 0.84f, 0.7f), 0.72f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, buff, "Breast", new Vector3(0f, 0.14f * s, 0.08f * s), new Vector3(0.14f, 0.11f, 0.12f) * s);
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, dark, "Spot", new Vector3(0f, 0.15f * s, 0.13f * s), Vector3.one * (0.028f * s));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, dark, "Spot", new Vector3(-0.035f * s, 0.12f * s, 0.11f * s), Vector3.one * (0.022f * s));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, dark, "Spot", new Vector3(0.035f * s, 0.12f * s, 0.11f * s), Vector3.one * (0.022f * s));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, dark, "Spot", new Vector3(0f, 0.1f * s, 0.12f * s), Vector3.one * (0.02f * s));
    }

    static void Marks(Transform parent, FlyerKind kind, float s, Material accent)
    {
        if (kind == FlyerKind.Jay)
        {
            Material black = CreatureForm.Fur(new Color(0.06f, 0.05f, 0.05f));
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, accent, "Rump", new Vector3(0f, 0.16f * s, -0.16f * s), new Vector3(0.1f, 0.08f, 0.08f) * s);
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, black, "Moustache", new Vector3(-0.04f * s, 0.24f * s, 0.2f * s), new Vector3(0.02f, 0.045f, 0.02f) * s);
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, black, "Moustache", new Vector3(0.04f * s, 0.24f * s, 0.2f * s), new Vector3(0.02f, 0.045f, 0.02f) * s);
        }
        else if (kind == FlyerKind.Pheasant)
        {
            Material white = CreatureForm.Fur(new Color(0.92f, 0.9f, 0.84f));
            Material red = CreatureForm.Fur(new Color(0.7f, 0.12f, 0.08f));
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, white, "Ring", new Vector3(0f, 0.24f * s, 0.12f * s), new Vector3(0.14f, 0.03f, 0.08f) * s);
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, red, "Wattle", new Vector3(0f, 0.24f * s, 0.22f * s), new Vector3(0.04f, 0.035f, 0.03f) * s);
        }
        else if (kind == FlyerKind.Woodpecker)
        {
            Material white = CreatureForm.Fur(new Color(0.9f, 0.9f, 0.86f));
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, accent, "Cap", new Vector3(0f, 0.36f * s, 0.16f * s), new Vector3(0.08f, 0.05f, 0.08f) * s);
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, white, "Cheek", new Vector3(0f, 0.26f * s, 0.2f * s), new Vector3(0.1f, 0.06f, 0.04f) * s);
        }
        else if (kind == FlyerKind.Pigeon)
        {
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, accent, "Neck", new Vector3(0f, 0.22f * s, 0.1f * s), new Vector3(0.1f, 0.06f, 0.08f) * s);
        }
    }

    static void Beak(Transform parent, Material beak, Vector3 pos, float length, float thick, bool hook)
    {
        Quaternion rot = hook ? Quaternion.Euler(28f, 0f, 0f) : Quaternion.identity;
        CreatureForm.Add(parent, CreatureForm.Shape.Beak, beak, "Beak", pos, rot, new Vector3(thick, thick * (hook ? 0.7f : 0.85f), length));
    }

    static void Eyes(Transform parent, Vector3 mid, float spread, float size)
    {
        Material eye = CreatureForm.Eye(new Color(0.04f, 0.035f, 0.03f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, eye, "Eye", mid + new Vector3(-spread, 0f, 0f), Vector3.one * size);
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, eye, "Eye", mid + new Vector3(spread, 0f, 0f), Vector3.one * size);
        Catchlight(parent, mid + new Vector3(-spread * 0.7f, size * 0.35f, size * 0.45f), size * 0.28f);
        Catchlight(parent, mid + new Vector3(spread * 1.15f, size * 0.35f, size * 0.45f), size * 0.28f);
    }

    static void Catchlight(Transform parent, Vector3 pos, float size)
    {
        Material shine = CreatureForm.Eye(new Color(0.95f, 0.95f, 0.92f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, shine, "Catch", pos, Vector3.one * size);
    }

    static void Tail(Transform parent, FlyerProfile profile, float s, Material wing, Material accent)
    {
        bool longTail = profile.shape == BirdShape.LongTail;
        int count = longTail ? 3 : profile.shape == BirdShape.Owl ? 2 : 3;
        float length = (longTail ? 0.72f : profile.shape == BirdShape.Owl ? 0.12f : 0.2f) * s;
        float width = (longTail ? 0.045f : 0.07f) * s;
        Material mat = longTail ? CreatureForm.FeatherMat(Color.Lerp(profile.body, profile.wing, 0.4f)) : wing;
        for (int i = 0; i < count; i++)
        {
            float side = count == 1 ? 0f : (i / (float)(count - 1) - 0.5f);
            float yaw = 90f + side * (longTail ? 10f : 28f);
            Material feather = longTail && i == 1 ? accent : mat;
            CreatureForm.Add(parent, CreatureForm.Shape.Feather, feather, "TailFeather",
                new Vector3(side * width * 0.4f, 0.15f * s, -0.14f * s),
                Quaternion.Euler(longTail ? 6f : 12f, yaw, 0f),
                new Vector3(length, length * 0.12f, width));
        }
    }

    static Transform Wing(Transform parent, Material mat, Material bar, Material speculum, float side, float scale, float span)
    {
        bool left = side < 0f;
        Transform pivot = new GameObject(left ? "WingLeft" : "WingRight").transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = new Vector3(0.08f * side * scale, 0.2f * scale, 0.02f * scale);

        const int count = 5;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            float length = Mathf.Lerp(0.16f, 0.42f, t) * scale * span;
            float width = Mathf.Lerp(0.11f, 0.045f, t) * scale;
            float root = Mathf.Lerp(0.01f, 0.08f, t) * scale;
            float sweep = Mathf.Lerp(6f, 34f, t);
            Material featherMat = mat;
            if (speculum != null && i == 2)
                featherMat = speculum;
            else if (bar != null && (i == 1 || i == 3))
                featherMat = bar;
            Feather(pivot, featherMat, left, root, length, width, sweep);
        }

        return pivot;
    }

    static void Feather(Transform pivot, Material mat, bool left, float root, float length, float width, float sweep)
    {
        float yaw = left ? 180f + sweep : -sweep;
        CreatureForm.Add(pivot, CreatureForm.Shape.Feather, mat, "Feather",
            new Vector3((left ? -1f : 1f) * root, 0.005f, 0f),
            Quaternion.Euler(-3f, yaw, 0f),
            new Vector3(length, length * 0.14f, width));
    }

    static void Feet(Transform parent, float s, Material dark)
    {
        Foot(parent, "FootLeft", new Vector3(-0.035f * s, 0.08f * s, 0.02f * s), s, dark);
        Foot(parent, "FootRight", new Vector3(0.035f * s, 0.08f * s, 0.02f * s), s, dark);
    }

    static void Foot(Transform parent, string name, Vector3 hip, float s, Material dark)
    {
        Transform pivot = new GameObject(name).transform;
        pivot.SetParent(parent, false);
        pivot.localPosition = hip;
        CreatureForm.Add(pivot, CreatureForm.Shape.Limb, dark, "Shin", Vector3.zero, new Vector3(0.012f * s, 0.07f * s, 0.012f * s));
        CreatureForm.Add(pivot, CreatureForm.Shape.Beak, dark, "Toes", new Vector3(0f, -0.07f * s, 0.01f * s), Quaternion.Euler(80f, 0f, 0f), new Vector3(0.02f, 0.008f, 0.04f) * s);
    }
}
