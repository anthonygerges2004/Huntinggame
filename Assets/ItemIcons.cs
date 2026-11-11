using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inventory pictures for non-weapon items, painted once in code with anti-aliased shapes:
/// a raw steak with fat and bone, a grilled steak, a spoiled cut with flies, and a pair of logs.
/// Guns use WeaponIcons, which photographs the real models.
/// </summary>
public static class ItemIcons
{
    const int Size = 128;
    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

    public static Texture2D Get(string kind)
    {
        if (string.IsNullOrEmpty(kind))
            return null;
        if (cache.TryGetValue(kind, out Texture2D tex))
            return tex;

        switch (kind)
        {
            case "RawMeat": tex = Paint(Steak(SteakStyle.Raw)); break;
            case "CookedMeat": tex = Paint(Steak(SteakStyle.Cooked)); break;
            case "SpoiledMeat": tex = Paint(Steak(SteakStyle.Spoiled)); break;
            case "Wood": tex = Paint(Logs); break;
            case "Axe": tex = Paint(Parts(35f, AxeParts())); break;
            case "Shotgun": tex = Paint(Parts(28f, GunParts(0))); break;
            case "Rifle": tex = Paint(Parts(28f, GunParts(1))); break;
            case "Sniper": tex = Paint(Parts(28f, GunParts(2))); break;
            default: tex = null; break;
        }

        if (tex != null)
        {
            tex.name = kind + "Icon";
            cache[kind] = tex;
        }

        return tex;
    }

    /// <summary>A pelt or carcass picture in the animal's own fur colour (tiger stripes for the legendary).</summary>
    public static Texture2D Goods(HuntGoods goods, bool pelt)
    {
        if (goods == null)
            return null;

        Color32 c = goods.fur;
        string key = (pelt ? "Pelt_" : "Carcass_") + c.r + "_" + c.g + "_" + c.b + (goods.stripes ? "_s" : "");
        if (cache.TryGetValue(key, out Texture2D tex))
            return tex;

        Color fur = goods.fur;
        bool stripes = goods.stripes;
        tex = pelt ? Paint(PeltShader(fur, stripes)) : Paint(CarcassShader(fur, stripes));
        tex.name = key;
        cache[key] = tex;
        return tex;
    }

    static Color Fur(Color fur, Vector2 p, float depth, bool stripes)
    {
        // Darker at the edges, lighter down the middle, with a little grain.
        Color c = Color.Lerp(fur * 0.62f, Color.Lerp(fur, Color.white, 0.12f), Mathf.Clamp01(depth / 0.09f));
        c *= 0.92f + 0.16f * Mathf.PerlinNoise(p.x * 30f, p.y * 30f);
        if (stripes && Mathf.Sin(p.y * 38f + Mathf.PerlinNoise(p.x * 6f, p.y * 3f) * 5f) > 0.55f)
            c = Color.Lerp(c, new Color(0.08f, 0.06f, 0.05f), 0.85f);
        c.a = 1f;
        return c;
    }

    // A stretched hide: body, four leg flaps, head and tail.
    static Shader PeltShader(Color fur, bool stripes)
    {
        return (Vector2 p, out float sdf) =>
        {
            float body = Ellipse(p - new Vector2(0.5f, 0.48f), new Vector2(0.2f, 0.29f));
            float legs = Mathf.Min(
                Mathf.Min(Capsule(p, new Vector2(0.42f, 0.66f), new Vector2(0.2f, 0.82f), 0.05f),
                          Capsule(p, new Vector2(0.58f, 0.66f), new Vector2(0.8f, 0.82f), 0.05f)),
                Mathf.Min(Capsule(p, new Vector2(0.42f, 0.3f), new Vector2(0.18f, 0.14f), 0.055f),
                          Capsule(p, new Vector2(0.58f, 0.3f), new Vector2(0.82f, 0.14f), 0.055f)));
            float head = Ellipse(p - new Vector2(0.5f, 0.82f), new Vector2(0.08f, 0.07f));
            float tail = Capsule(p, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.06f), 0.025f);
            float shape = SmoothMin(SmoothMin(body, legs, 0.05f), SmoothMin(head, tail, 0.03f), 0.04f);
            sdf = shape;

            Color c = Fur(fur, p, -shape, stripes);
            // A pale skin-side rim where the hide was cut and stretched.
            if (-shape < 0.012f)
                c = Color.Lerp(new Color(0.93f, 0.86f, 0.74f), c, 0.35f);
            c.a = 1f;
            return c;
        };
    }

    // A downed animal lying on its side.
    static Shader CarcassShader(Color fur, bool stripes)
    {
        return (Vector2 p, out float sdf) =>
        {
            float body = Ellipse(p - new Vector2(0.48f, 0.46f), new Vector2(0.3f, 0.15f));
            float neck = Capsule(p, new Vector2(0.7f, 0.5f), new Vector2(0.8f, 0.56f), 0.06f);
            float head = Ellipse(Rotate(p - new Vector2(0.85f, 0.55f), 20f), new Vector2(0.08f, 0.05f));
            float legs = Mathf.Min(
                Mathf.Min(Capsule(p, new Vector2(0.3f, 0.36f), new Vector2(0.2f, 0.18f), 0.025f),
                          Capsule(p, new Vector2(0.38f, 0.35f), new Vector2(0.34f, 0.16f), 0.025f)),
                Mathf.Min(Capsule(p, new Vector2(0.6f, 0.36f), new Vector2(0.62f, 0.17f), 0.025f),
                          Capsule(p, new Vector2(0.67f, 0.37f), new Vector2(0.78f, 0.2f), 0.025f)));
            float tail = Capsule(p, new Vector2(0.19f, 0.5f), new Vector2(0.12f, 0.55f), 0.02f);
            float shape = SmoothMin(SmoothMin(body, neck, 0.04f), Mathf.Min(head, Mathf.Min(legs, tail)), 0.02f);
            sdf = shape;

            Color c = Fur(fur, p, -shape, stripes);
            // A dark wound and a little blood under the chest.
            float wound = Circle(p - new Vector2(0.58f, 0.45f), 0.025f);
            if (wound < 0f)
                c = new Color(0.35f, 0.04f, 0.04f);
            c.a = 1f;
            return c;
        };
    }

    // ---------------------------------------------------------------- painter

    delegate Color Shader(Vector2 p, out float sdf);

    static Texture2D Paint(Shader shader)
    {
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Trilinear;
        tex.hideFlags = HideFlags.DontSave;
        Color[] px = new Color[Size * Size];
        Vector2 shadowShift = new Vector2(0.018f, -0.03f);
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size);
                Color c = shader(p, out float sdf);
                float a = Mathf.Clamp01(-sdf * Size + 0.5f);

                // Soft shadow under the shape.
                shader(p - shadowShift, out float shadowSdf);
                float shadow = Mathf.Clamp01(1f - Mathf.Clamp01((shadowSdf + 0.02f) / 0.05f)) * 0.45f;

                Color under = new Color(0f, 0f, 0f, shadow);
                float outA = a + under.a * (1f - a);
                Color rgb = outA > 0.0001f ? (c * a + under * under.a * (1f - a)) / outA : Color.clear;
                rgb.a = outA;
                px[y * Size + x] = rgb;
            }
        }

        tex.SetPixels(px);
        tex.Apply(true, true);
        return tex;
    }

    // ---------------------------------------------------------------- shapes

    enum SteakStyle { Raw, Cooked, Spoiled }

    static Shader Steak(SteakStyle style)
    {
        return (Vector2 p, out float sdf) =>
        {
            // Two blended ellipses make the classic T-bone outline.
            float main = Ellipse(Rotate(p - new Vector2(0.55f, 0.47f), -18f), new Vector2(0.34f, 0.25f));
            float lobe = Ellipse(Rotate(p - new Vector2(0.30f, 0.55f), 25f), new Vector2(0.17f, 0.15f));
            float shape = SmoothMin(main, lobe, 0.06f);
            if (style == SteakStyle.Spoiled)
                shape += (Mathf.PerlinNoise(p.x * 9f, p.y * 9f) - 0.5f) * 0.025f;

            float bone = Circle(p - new Vector2(0.58f, 0.52f), 0.075f);
            float marrow = Circle(p - new Vector2(0.58f, 0.52f), 0.04f);
            float depth = -shape; // how far inside the outline

            Color c;
            switch (style)
            {
                case SteakStyle.Cooked:
                {
                    c = Color.Lerp(new Color(0.33f, 0.15f, 0.06f), new Color(0.62f, 0.34f, 0.15f), Mathf.Clamp01(depth / 0.1f));
                    float stripe = Mathf.Repeat((p.x + p.y) * 7f, 1f);
                    if (depth > 0.035f && stripe < 0.13f)
                        c = Color.Lerp(c, new Color(0.16f, 0.07f, 0.03f), 0.85f);
                    c = Color.Lerp(c, new Color(1f, 0.85f, 0.6f), Mathf.Clamp01(1f - depth / 0.015f) * 0.25f);
                    break;
                }
                case SteakStyle.Spoiled:
                {
                    c = Color.Lerp(new Color(0.30f, 0.32f, 0.15f), new Color(0.50f, 0.50f, 0.28f), Mathf.Clamp01(depth / 0.1f));
                    float rot = Mathf.PerlinNoise(p.x * 14f + 3f, p.y * 14f);
                    if (rot > 0.62f)
                        c = Color.Lerp(c, new Color(0.18f, 0.20f, 0.08f), 0.8f);
                    break;
                }
                default:
                {
                    c = Color.Lerp(new Color(0.50f, 0.07f, 0.08f), new Color(0.80f, 0.18f, 0.18f), Mathf.Clamp01(depth / 0.1f));
                    float marble = Mathf.PerlinNoise(p.x * 18f, p.y * 18f);
                    if (marble > 0.7f && depth > 0.03f)
                        c = Color.Lerp(c, new Color(0.95f, 0.75f, 0.72f), 0.45f);
                    // Rim of fat around the edge.
                    if (depth < 0.028f)
                        c = Color.Lerp(new Color(0.97f, 0.90f, 0.82f), c, Mathf.Clamp01(depth / 0.028f) * 0.35f);
                    break;
                }
            }

            if (bone < 0f)
            {
                Color boneColor = style == SteakStyle.Cooked ? new Color(0.86f, 0.78f, 0.62f) : new Color(0.95f, 0.92f, 0.85f);
                if (style == SteakStyle.Spoiled)
                    boneColor = new Color(0.75f, 0.74f, 0.58f);
                c = marrow < 0f ? boneColor * 0.72f : boneColor;
                c.a = 1f;
            }

            sdf = shape;

            if (style == SteakStyle.Spoiled)
            {
                // Three flies circling above the meat.
                Vector2[] flies = { new Vector2(0.30f, 0.84f), new Vector2(0.52f, 0.90f), new Vector2(0.74f, 0.80f) };
                for (int i = 0; i < flies.Length; i++)
                {
                    float body = Circle(p - flies[i], 0.022f);
                    float wing = Ellipse(p - flies[i] - new Vector2(0.018f, 0.022f), new Vector2(0.022f, 0.013f));
                    if (body < sdf)
                    {
                        sdf = body;
                        c = new Color(0.06f, 0.06f, 0.06f);
                    }
                    else if (wing < sdf)
                    {
                        sdf = wing;
                        c = new Color(0.85f, 0.9f, 0.95f);
                    }
                }
            }

            c.a = 1f;
            return c;
        };
    }

    static Color Logs(Vector2 p, out float sdf)
    {
        // Two stacked logs, cut ends facing right.
        float top = Capsule(p, new Vector2(0.20f, 0.62f), new Vector2(0.74f, 0.62f), 0.12f);
        float bottom = Capsule(p, new Vector2(0.26f, 0.37f), new Vector2(0.80f, 0.37f), 0.12f);
        bool onTop = top < 0f || (top < bottom && bottom >= 0f);
        sdf = Mathf.Min(top, bottom);

        Vector2 axisPoint = onTop ? new Vector2(0.74f, 0.62f) : new Vector2(0.80f, 0.37f);
        float across = (p.y - axisPoint.y) / 0.12f;
        Color bark = Color.Lerp(new Color(0.26f, 0.16f, 0.08f), new Color(0.50f, 0.33f, 0.17f), 1f - Mathf.Abs(across));
        float grain = Mathf.Repeat(p.y * 46f + Mathf.PerlinNoise(p.x * 6f, p.y * 30f) * 2f, 1f);
        if (grain < 0.18f)
            bark *= 0.78f;
        Color c = bark;

        // End grain: a lighter disc with growth rings at the cut end.
        float cap = Ellipse(p - axisPoint, new Vector2(0.06f, 0.115f));
        if (cap < 0f)
        {
            float r = (p - axisPoint).magnitude;
            c = Color.Lerp(new Color(0.88f, 0.70f, 0.45f), new Color(0.70f, 0.52f, 0.30f), Mathf.Clamp01(r / 0.12f));
            if (Mathf.Repeat(r * 55f, 1f) < 0.2f)
                c *= 0.85f;
            if (cap > -0.012f)
                c = new Color(0.30f, 0.19f, 0.09f);
        }

        c.a = 1f;
        return c;
    }

    // ---------------------------------------------------------------- part-built pictures (guns, axe)

    struct Part
    {
        public System.Func<Vector2, float> sdf;
        public Color top;
        public Color bottom;
        public float height; // vertical span used for the top-to-bottom shading
    }

    static Part Box(Vector2 centre, Vector2 half, float tilt, Color top, Color bottom)
    {
        return new Part
        {
            sdf = q => BoxSdf(Rotate(q - centre, -tilt), half),
            top = top,
            bottom = bottom,
            height = half.y,
        };
    }

    static Part Round(Vector2 a, Vector2 b, float r, Color top, Color bottom)
    {
        return new Part { sdf = q => Capsule(q, a, b, r), top = top, bottom = bottom, height = r };
    }

    /// <summary>Draws parts in order in a frame turned by tilt degrees; later parts sit on top.</summary>
    static Shader Parts(float tilt, Part[] parts)
    {
        return (Vector2 p, out float sdf) =>
        {
            Vector2 q = Rotate(p - new Vector2(0.5f, 0.5f), -tilt);
            sdf = float.MaxValue;
            Color c = Color.clear;
            float nearest = float.MaxValue;
            for (int i = 0; i < parts.Length; i++)
            {
                float d = parts[i].sdf(q);
                if (d < sdf)
                    sdf = d;
                if (d < 0f || (c.a <= 0f && d < nearest))
                {
                    nearest = d;
                    // Lighter along the top edge of each part, like light from above.
                    float shade = Mathf.Clamp01(0.5f - d / Mathf.Max(0.004f, parts[i].height * 2f));
                    c = Color.Lerp(parts[i].bottom, parts[i].top, shade);
                    if (d < 0f)
                        c.a = 1f;
                }
            }

            c.a = 1f;
            return c;
        };
    }

    static Part[] GunParts(int type)
    {
        Color woodTop = new Color(0.62f, 0.38f, 0.19f), woodLow = new Color(0.33f, 0.18f, 0.08f);
        Color darkTop = new Color(0.30f, 0.32f, 0.30f), darkLow = new Color(0.12f, 0.13f, 0.12f);
        Color steelTop = new Color(0.62f, 0.65f, 0.68f), steelLow = new Color(0.22f, 0.24f, 0.26f);
        Color blackTop = new Color(0.22f, 0.23f, 0.25f), blackLow = new Color(0.06f, 0.06f, 0.07f);

        switch (type)
        {
            case 0: // double barrel shotgun
                return new[]
                {
                    Box(new Vector2(-0.30f, -0.03f), new Vector2(0.13f, 0.045f), -9f, woodTop, woodLow),
                    Box(new Vector2(-0.10f, 0.0f), new Vector2(0.08f, 0.04f), 0f, steelTop, steelLow),
                    Box(new Vector2(0.18f, 0.022f), new Vector2(0.24f, 0.014f), 0f, steelTop, steelLow),
                    Box(new Vector2(0.18f, -0.008f), new Vector2(0.24f, 0.014f), 0f, steelTop, steelLow),
                    Box(new Vector2(0.06f, -0.03f), new Vector2(0.09f, 0.02f), 0f, woodTop, woodLow),
                };
            case 1: // hunting rifle
                return new[]
                {
                    Box(new Vector2(-0.31f, -0.035f), new Vector2(0.13f, 0.045f), -8f, woodTop, woodLow),
                    Box(new Vector2(0.02f, -0.012f), new Vector2(0.20f, 0.025f), 0f, woodTop, woodLow),
                    Box(new Vector2(-0.08f, 0.018f), new Vector2(0.09f, 0.022f), 0f, steelTop, steelLow),
                    Box(new Vector2(0.25f, 0.016f), new Vector2(0.21f, 0.010f), 0f, steelTop, steelLow),
                    Round(new Vector2(-0.06f, 0.04f), new Vector2(-0.02f, 0.07f), 0.01f, steelTop, steelLow),
                    Box(new Vector2(0.43f, 0.034f), new Vector2(0.006f, 0.012f), 0f, steelTop, steelLow),
                };
            default: // scoped sniper
                return new[]
                {
                    Box(new Vector2(-0.31f, -0.035f), new Vector2(0.13f, 0.045f), -6f, darkTop, darkLow),
                    Box(new Vector2(0.00f, -0.012f), new Vector2(0.20f, 0.024f), 0f, darkTop, darkLow),
                    Box(new Vector2(-0.08f, 0.016f), new Vector2(0.09f, 0.02f), 0f, blackTop, blackLow),
                    Box(new Vector2(0.27f, 0.014f), new Vector2(0.23f, 0.008f), 0f, blackTop, blackLow),
                    Box(new Vector2(-0.06f, 0.072f), new Vector2(0.12f, 0.02f), 0f, blackTop, blackLow),
                    Box(new Vector2(0.075f, 0.072f), new Vector2(0.02f, 0.03f), 0f, blackTop, blackLow),
                    Box(new Vector2(-0.19f, 0.072f), new Vector2(0.018f, 0.026f), 0f, blackTop, blackLow),
                    Box(new Vector2(-0.06f, 0.044f), new Vector2(0.015f, 0.012f), 0f, blackTop, blackLow),
                };
        }
    }

    static Part[] AxeParts()
    {
        Color woodTop = new Color(0.70f, 0.47f, 0.25f), woodLow = new Color(0.38f, 0.22f, 0.10f);
        Color steelTop = new Color(0.82f, 0.85f, 0.88f), steelLow = new Color(0.30f, 0.32f, 0.35f);
        return new[]
        {
            Round(new Vector2(-0.34f, 0f), new Vector2(0.26f, 0f), 0.032f, woodTop, woodLow),
            Box(new Vector2(0.22f, 0.045f), new Vector2(0.05f, 0.07f), 0f, steelTop, steelLow),
            new Part
            {
                // The blade flares wider than the cheek of the head.
                sdf = q => Ellipse(q - new Vector2(0.22f, 0.13f), new Vector2(0.10f, 0.045f)),
                top = new Color(0.95f, 0.96f, 0.98f),
                bottom = steelLow,
                height = 0.045f,
            },
            Box(new Vector2(0.22f, -0.02f), new Vector2(0.03f, 0.025f), 0f, steelTop, steelLow),
        };
    }

    static float BoxSdf(Vector2 p, Vector2 b)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x) - b.x, Mathf.Abs(p.y) - b.y);
        return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
    }

    // ---------------------------------------------------------------- sdf helpers

    static float Circle(Vector2 p, float r) => p.magnitude - r;

    static float Ellipse(Vector2 p, Vector2 r)
    {
        // Cheap approximation, good enough at icon size.
        float k = new Vector2(p.x / r.x, p.y / r.y).magnitude;
        return (k - 1f) * Mathf.Min(r.x, r.y);
    }

    static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude - r;
    }

    static Vector2 Rotate(Vector2 p, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(p.x * c - p.y * s, p.x * s + p.y * c);
    }

    static float SmoothMin(float a, float b, float k)
    {
        float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
        return Mathf.Lerp(b, a, h) - k * h * (1f - h);
    }
}
