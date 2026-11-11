using UnityEngine;

/// <summary>
/// Wild pig, boar, fox, rabbit and wolf. Proportions follow the real animals:
/// a pig's barrel and disc snout, a fox's brush, a rabbit's ears, a wolf's long leg.
/// </summary>
public static class GroundCritterVisual
{
    public const string BodyName = "CritterBody";

    public static void Build(Transform parent, QuarryKind kind)
    {
        if (kind == QuarryKind.None)
            kind = QuarryKind.Pig;

        switch (kind)
        {
            case QuarryKind.Boar:
                BuildPig(parent, 1.12f, true);
                break;
            case QuarryKind.Fox:
                BuildFox(parent);
                break;
            case QuarryKind.Rabbit:
                BuildRabbit(parent);
                break;
            case QuarryKind.Wolf:
                BuildWolf(parent);
                break;
            default:
                BuildPig(parent, 1f, false);
                break;
        }

        CritterGait gait = parent.GetComponent<CritterGait>();
        if (gait == null)
            gait = parent.gameObject.AddComponent<CritterGait>();
        gait.hop = kind == QuarryKind.Rabbit;
        gait.amplitude = kind == QuarryKind.Rabbit ? 34f : kind == QuarryKind.Fox ? 26f : kind == QuarryKind.Pig ? 16f : 22f;
    }

    static void BuildPig(Transform parent, float s, bool boar)
    {
        Color hide = boar ? new Color(0.2f, 0.14f, 0.1f) : new Color(0.4f, 0.31f, 0.22f);
        Color dark = boar ? new Color(0.08f, 0.06f, 0.05f) : new Color(0.26f, 0.18f, 0.13f);
        Color belly = boar ? new Color(0.32f, 0.24f, 0.16f) : new Color(0.55f, 0.44f, 0.34f);
        Material body = CreatureForm.Fur(hide);
        Material shade = CreatureForm.Fur(dark);
        Material pale = CreatureForm.Fur(belly);
        Material tusk = CreatureForm.Keratin(new Color(0.86f, 0.82f, 0.68f));

        CreatureForm.Add(parent, CreatureForm.Shape.Ball, body, BodyName, new Vector3(0f, 0.4f * s, 0f), new Vector3(0.42f, 0.34f, 0.72f) * s);
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, pale, "Belly", new Vector3(0f, 0.3f * s, 0.02f * s), new Vector3(0.3f, 0.16f, 0.48f) * s);
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, body, "Head", new Vector3(0f, 0.46f * s, 0.36f * s), new Vector3(0.26f, 0.24f, 0.28f) * s);
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, shade, "Snout", new Vector3(0f, 0.4f * s, 0.52f * s), new Vector3(0.16f, 0.12f, 0.14f) * s);
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, shade, "Nostril", new Vector3(-0.03f * s, 0.4f * s, 0.58f * s), Vector3.one * (0.025f * s));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, shade, "Nostril", new Vector3(0.03f * s, 0.4f * s, 0.58f * s), Vector3.one * (0.025f * s));
        Ear(parent, shade, new Vector3(-0.08f * s, 0.58f * s, 0.32f * s), s, 50f);
        Ear(parent, shade, new Vector3(0.08f * s, 0.58f * s, 0.32f * s), s, -50f);
        Eye(parent, new Vector3(-0.09f * s, 0.5f * s, 0.5f * s), 0.022f * s);
        Eye(parent, new Vector3(0.09f * s, 0.5f * s, 0.5f * s), 0.022f * s);

        float reach = 0.24f * s;
        Leg(parent, "LegFL", new Vector3(-0.14f * s, reach, 0.2f * s), reach, 0.07f * s, shade, shade, false);
        Leg(parent, "LegFR", new Vector3(0.14f * s, reach, 0.2f * s), reach, 0.07f * s, shade, shade, false);
        Leg(parent, "LegHL", new Vector3(-0.14f * s, reach, -0.22f * s), reach, 0.075f * s, shade, shade, true);
        Leg(parent, "LegHR", new Vector3(0.14f * s, reach, -0.22f * s), reach, 0.075f * s, shade, shade, true);

        if (boar)
        {
            for (int i = 0; i < 5; i++)
            {
                float z = Mathf.Lerp(0.12f, -0.22f, i / 4f) * s;
                CreatureForm.Add(parent, CreatureForm.Shape.Feather, CreatureForm.FeatherMat(dark), "Bristle",
                    new Vector3(0f, 0.56f * s, z), Quaternion.Euler(-70f, 90f, 0f), new Vector3(0.1f * s, 0.02f * s, 0.035f * s));
            }

            CreatureForm.Add(parent, CreatureForm.Shape.Beak, tusk, "Tusk", new Vector3(-0.04f * s, 0.34f * s, 0.56f * s), Quaternion.Euler(70f, 8f, 0f), new Vector3(0.02f, 0.018f, 0.09f) * s);
            CreatureForm.Add(parent, CreatureForm.Shape.Beak, tusk, "Tusk", new Vector3(0.04f * s, 0.34f * s, 0.56f * s), Quaternion.Euler(70f, -8f, 0f), new Vector3(0.02f, 0.018f, 0.09f) * s);
        }
        else
        {
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, shade, "Tail", new Vector3(0f, 0.42f * s, -0.36f * s), Vector3.one * (0.06f * s));
            CreatureForm.Add(parent, CreatureForm.Shape.Ball, shade, "Tail", new Vector3(0.02f * s, 0.46f * s, -0.34f * s), Vector3.one * (0.045f * s));
        }
    }

    static void BuildFox(Transform parent)
    {
        Color red = new Color(0.78f, 0.38f, 0.14f);
        Material coat = CreatureForm.Fur(red);
        Material dark = CreatureForm.Fur(new Color(0.1f, 0.07f, 0.06f));
        Material white = CreatureForm.Fur(new Color(0.93f, 0.9f, 0.84f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, coat, BodyName, new Vector3(0f, 0.36f, 0f), new Vector3(0.2f, 0.2f, 0.52f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, white, "Chest", new Vector3(0f, 0.28f, 0.12f), new Vector3(0.12f, 0.1f, 0.16f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, coat, "Head", new Vector3(0f, 0.42f, 0.3f), new Vector3(0.14f, 0.13f, 0.18f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, white, "Muzzle", new Vector3(0f, 0.38f, 0.4f), new Vector3(0.07f, 0.06f, 0.1f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, dark, "Nose", new Vector3(0f, 0.38f, 0.46f), Vector3.one * 0.03f);
        Ear(parent, dark, new Vector3(-0.045f, 0.52f, 0.28f), 0.85f, 8f);
        Ear(parent, dark, new Vector3(0.045f, 0.52f, 0.28f), 0.85f, -8f);
        Eye(parent, new Vector3(-0.05f, 0.44f, 0.4f), 0.018f);
        Eye(parent, new Vector3(0.05f, 0.44f, 0.4f), 0.018f);

        float reach = 0.32f;
        Leg(parent, "LegFL", new Vector3(-0.08f, reach, 0.14f), reach, 0.04f, coat, dark, false);
        Leg(parent, "LegFR", new Vector3(0.08f, reach, 0.14f), reach, 0.04f, coat, dark, false);
        Leg(parent, "LegHL", new Vector3(-0.08f, reach, -0.16f), reach, 0.045f, coat, dark, true);
        Leg(parent, "LegHR", new Vector3(0.08f, reach, -0.16f), reach, 0.045f, coat, dark, true);

        CreatureForm.Add(parent, CreatureForm.Shape.Feather, CreatureForm.FeatherMat(red), "Brush",
            new Vector3(0f, 0.4f, -0.28f), Quaternion.Euler(18f, 90f, 0f), new Vector3(0.42f, 0.08f, 0.14f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, white, "Tip", new Vector3(0f, 0.46f, -0.62f), Vector3.one * 0.07f);
    }

    static void BuildRabbit(Transform parent)
    {
        Material coat = CreatureForm.Fur(new Color(0.48f, 0.36f, 0.24f));
        Material dark = CreatureForm.Fur(new Color(0.28f, 0.18f, 0.12f));
        Material white = CreatureForm.Fur(new Color(0.94f, 0.92f, 0.86f));
        Material pink = CreatureForm.Fur(new Color(0.78f, 0.48f, 0.46f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, coat, BodyName, new Vector3(0f, 0.22f, 0f), new Vector3(0.2f, 0.18f, 0.3f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, white, "Belly", new Vector3(0f, 0.16f, 0.02f), new Vector3(0.14f, 0.08f, 0.18f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, coat, "Head", new Vector3(0f, 0.28f, 0.14f), new Vector3(0.12f, 0.12f, 0.13f));
        LongEar(parent, coat, pink, new Vector3(-0.03f, 0.36f, 0.12f));
        LongEar(parent, coat, pink, new Vector3(0.03f, 0.36f, 0.12f));
        Eye(parent, new Vector3(-0.045f, 0.3f, 0.21f), 0.016f);
        Eye(parent, new Vector3(0.045f, 0.3f, 0.21f), 0.016f);
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, white, "Tail", new Vector3(0f, 0.24f, -0.15f), Vector3.one * 0.06f);

        Leg(parent, "LegFL", new Vector3(-0.06f, 0.16f, 0.08f), 0.16f, 0.028f, coat, dark, false);
        Leg(parent, "LegFR", new Vector3(0.06f, 0.16f, 0.08f), 0.16f, 0.028f, coat, dark, false);
        Leg(parent, "LegHL", new Vector3(-0.07f, 0.2f, -0.08f), 0.2f, 0.04f, coat, dark, true);
        Leg(parent, "LegHR", new Vector3(0.07f, 0.2f, -0.08f), 0.2f, 0.04f, coat, dark, true);
    }

    static void BuildWolf(Transform parent)
    {
        Color grey = new Color(0.46f, 0.46f, 0.48f);
        Color darkGrey = new Color(0.2f, 0.2f, 0.22f);
        Material coat = CreatureForm.Fur(grey);
        Material saddle = CreatureForm.Fur(darkGrey);
        Material pale = CreatureForm.Fur(new Color(0.78f, 0.74f, 0.66f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, coat, BodyName, new Vector3(0f, 0.5f, 0f), new Vector3(0.26f, 0.28f, 0.78f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, saddle, "Saddle", new Vector3(0f, 0.58f, -0.04f), new Vector3(0.16f, 0.1f, 0.5f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, pale, "Chest", new Vector3(0f, 0.4f, 0.18f), new Vector3(0.16f, 0.12f, 0.18f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, coat, "Head", new Vector3(0f, 0.58f, 0.4f), new Vector3(0.16f, 0.16f, 0.22f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, pale, "Muzzle", new Vector3(0f, 0.52f, 0.52f), new Vector3(0.08f, 0.07f, 0.14f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, saddle, "Nose", new Vector3(0f, 0.53f, 0.6f), Vector3.one * 0.035f);
        Ear(parent, coat, new Vector3(-0.05f, 0.7f, 0.36f), 0.9f, 6f);
        Ear(parent, coat, new Vector3(0.05f, 0.7f, 0.36f), 0.9f, -6f);
        Material amber = CreatureForm.Eye(new Color(0.72f, 0.52f, 0.16f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, amber, "Eye", new Vector3(-0.055f, 0.6f, 0.52f), Vector3.one * 0.026f);
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, amber, "Eye", new Vector3(0.055f, 0.6f, 0.52f), Vector3.one * 0.026f);

        float reach = 0.46f;
        Leg(parent, "LegFL", new Vector3(-0.1f, reach, 0.2f), reach, 0.05f, coat, saddle, false);
        Leg(parent, "LegFR", new Vector3(0.1f, reach, 0.2f), reach, 0.05f, coat, saddle, false);
        Leg(parent, "LegHL", new Vector3(-0.1f, reach, -0.22f), reach, 0.055f, coat, saddle, true);
        Leg(parent, "LegHR", new Vector3(0.1f, reach, -0.22f), reach, 0.055f, coat, saddle, true);

        CreatureForm.Add(parent, CreatureForm.Shape.Feather, CreatureForm.FeatherMat(darkGrey), "Tail",
            new Vector3(0f, 0.48f, -0.4f), Quaternion.Euler(24f, 90f, 0f), new Vector3(0.4f, 0.07f, 0.1f));
    }

    static void Ear(Transform parent, Material mat, Vector3 pos, float s, float roll)
    {
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, mat, "Ear", pos, Quaternion.Euler(0f, 0f, roll), new Vector3(0.04f, 0.1f, 0.025f) * s);
    }

    static void LongEar(Transform parent, Material outer, Material inner, Vector3 pos)
    {
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, outer, "Ear", pos, Quaternion.Euler(-8f, 0f, pos.x < 0f ? 8f : -8f), new Vector3(0.03f, 0.16f, 0.02f));
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, inner, "EarInner", pos + new Vector3(0f, 0.01f, 0.008f), Quaternion.Euler(-8f, 0f, pos.x < 0f ? 8f : -8f), new Vector3(0.016f, 0.12f, 0.01f));
    }

    static void Eye(Transform parent, Vector3 pos, float size)
    {
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, CreatureForm.Eye(new Color(0.05f, 0.04f, 0.03f)), "Eye", pos, Vector3.one * size);
        CreatureForm.Add(parent, CreatureForm.Shape.Ball, CreatureForm.Eye(new Color(0.95f, 0.95f, 0.9f)), "Catch", pos + new Vector3(0f, size * 0.25f, size * 0.4f), Vector3.one * (size * 0.28f));
    }

    static void Leg(Transform parent, string name, Vector3 hip, float length, float thick, Material upper, Material lower, bool hind)
    {
        Transform hipBone = new GameObject(name).transform;
        hipBone.SetParent(parent, false);
        hipBone.localPosition = hip;
        if (hind)
            hipBone.localRotation = Quaternion.Euler(-16f, 0f, 0f);

        float upperLen = length * 0.52f;
        CreatureForm.Add(hipBone, CreatureForm.Shape.Limb, upper, "Upper", Vector3.zero, new Vector3(thick, upperLen, thick));
        Transform knee = new GameObject("Knee").transform;
        knee.SetParent(hipBone, false);
        knee.localPosition = new Vector3(0f, -upperLen, hind ? 0.02f : 0f);
        float lowerLen = length - upperLen;
        CreatureForm.Add(knee, CreatureForm.Shape.Limb, lower, "Lower", Vector3.zero, new Vector3(thick * 0.78f, lowerLen, thick * 0.78f));
        CreatureForm.Add(knee, CreatureForm.Shape.Ball, lower, "Paw", new Vector3(0f, -lowerLen, thick * 0.55f), new Vector3(thick * 1.1f, thick * 0.45f, thick * 1.7f));
    }
}
