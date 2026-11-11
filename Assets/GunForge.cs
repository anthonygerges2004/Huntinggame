using UnityEngine;

/// <summary>
/// Builds the three hunting guns as real-proportioned models: turned barrels and scopes,
/// contoured walnut or synthetic stocks with softened edges, actions, triggers, sights.
/// Barrel along +Z, butt toward -Z, bore at y = 0.045, sizes in metres.
/// Named anchors: "Grip" (right hand), "BarrelGuard" (left hand), and on the shotgun a
/// "Barrels" part that the break-open reload hinges.
/// </summary>
public static class GunForge
{
    const float Bore = 0.045f;

    // ---------------------------------------------------------------- hunting rifle

    public static GameObject Rifle(out Vector3 stock, out Vector3 muzzle)
    {
        GameObject root = new GameObject("Rifle");
        Transform t = root.transform;
        Material wood = MeshForge.Walnut;
        Material steel = MeshForge.BluedSteel;

        Vector2[] stockOutline =
        {
            new Vector2(-0.46f, 0.050f), new Vector2(-0.30f, 0.049f), new Vector2(-0.19f, 0.042f),
            new Vector2(-0.13f, 0.026f), new Vector2(-0.075f, 0.030f), new Vector2(0.12f, 0.034f),
            new Vector2(0.33f, 0.034f), new Vector2(0.36f, 0.029f), new Vector2(0.366f, 0.018f),
            new Vector2(0.345f, 0.008f), new Vector2(0.12f, 0.004f), new Vector2(0.04f, 0.0f),
            new Vector2(0.0f, -0.012f), new Vector2(-0.04f, -0.014f), new Vector2(-0.058f, -0.022f),
            new Vector2(-0.072f, -0.072f), new Vector2(-0.088f, -0.084f), new Vector2(-0.108f, -0.083f),
            new Vector2(-0.118f, -0.066f), new Vector2(-0.132f, -0.036f), new Vector2(-0.25f, -0.052f),
            new Vector2(-0.40f, -0.071f), new Vector2(-0.46f, -0.079f), new Vector2(-0.466f, -0.02f),
        };
        MeshForge.Part(t, "Stock", MeshForge.Extrude(stockOutline, 0.021f, 0.007f), wood, Vector3.zero, Quaternion.identity);
        ButtPad(t, -0.466f, -0.079f, 0.050f, 0.022f);

        Turned(t, "Receiver", steel, Bore, new[] { V(0.008f, -0.078f), V(0.0145f, -0.07f), V(0.0145f, 0.115f), V(0.012f, 0.12f) });
        Turned(t, "Barrel", steel, Bore, new[] { V(0.0115f, 0.118f), V(0.0105f, 0.25f), V(0.0085f, 0.70f), V(0.0095f, 0.704f), V(0.0095f, 0.712f) });
        BoltHandle(t, steel, 0.016f);
        TriggerGroup(t, steel, 0f);
        Sight(t, steel, 0.69f, 0.0105f);
        Sight(t, steel, 0.30f, 0.013f);

        Anchor(t, "Grip", new Vector3(0f, -0.045f, -0.092f));
        Anchor(t, "BarrelGuard", new Vector3(0f, 0.012f, 0.24f));
        stock = new Vector3(0f, 0.02f, -0.46f);
        muzzle = new Vector3(0f, Bore, 0.712f);
        return root;
    }

    // ---------------------------------------------------------------- scoped sniper

    public static GameObject Sniper(out Vector3 stock, out Vector3 muzzle)
    {
        GameObject root = new GameObject("Sniper");
        Transform t = root.transform;
        Material body = MeshForge.Polymer;
        Material steel = MeshForge.BluedSteel;

        Vector2[] stockOutline =
        {
            new Vector2(-0.47f, 0.058f), new Vector2(-0.30f, 0.058f), new Vector2(-0.20f, 0.052f),
            new Vector2(-0.175f, 0.032f), new Vector2(-0.08f, 0.030f), new Vector2(0.40f, 0.030f),
            new Vector2(0.42f, 0.024f), new Vector2(0.42f, 0.004f), new Vector2(0.40f, -0.005f),
            new Vector2(0.10f, -0.007f), new Vector2(0.0f, -0.014f), new Vector2(-0.048f, -0.018f),
            new Vector2(-0.058f, -0.084f), new Vector2(-0.094f, -0.088f), new Vector2(-0.10f, -0.042f),
            new Vector2(-0.13f, -0.032f), new Vector2(-0.30f, -0.046f), new Vector2(-0.42f, -0.064f),
            new Vector2(-0.47f, -0.070f), new Vector2(-0.476f, -0.01f),
        };
        MeshForge.Part(t, "Stock", MeshForge.Extrude(stockOutline, 0.024f, 0.006f), body, Vector3.zero, Quaternion.identity);
        ButtPad(t, -0.476f, -0.070f, 0.058f, 0.025f);

        Turned(t, "Receiver", steel, Bore, new[] { V(0.009f, -0.086f), V(0.016f, -0.078f), V(0.016f, 0.125f), V(0.0135f, 0.13f) });
        Turned(t, "Barrel", steel, Bore, new[] { V(0.0135f, 0.13f), V(0.0135f, 0.30f), V(0.012f, 0.90f) });
        Turned(t, "MuzzleBrake", steel, Bore, new[] { V(0.012f, 0.898f), V(0.0165f, 0.902f), V(0.0165f, 0.925f), V(0.0145f, 0.928f),
                                                       V(0.0145f, 0.935f), V(0.0165f, 0.938f), V(0.0165f, 0.962f), V(0.014f, 0.966f) });
        BoltHandle(t, steel, 0.02f);
        TriggerGroup(t, steel, 0f);

        // Scope, rings and turrets.
        const float scopeY = Bore + 0.058f;
        Turned(t, "Scope", steel, scopeY, new[] { V(0.0175f, -0.165f), V(0.020f, -0.15f), V(0.020f, -0.11f), V(0.0127f, -0.088f),
                                                  V(0.0127f, 0.15f), V(0.024f, 0.20f), V(0.0255f, 0.27f), V(0.0235f, 0.276f) });
        Turned(t, "EyeLens", MeshForge.Glass, scopeY, new[] { V(0.0165f, -0.167f), V(0.0165f, -0.164f) });
        Turned(t, "ObjectiveLens", MeshForge.Glass, scopeY, new[] { V(0.0222f, 0.274f), V(0.0222f, 0.277f) });
        Turret(t, steel, new Vector3(0f, scopeY + 0.0127f, 0.03f), Quaternion.Euler(-90f, 0f, 0f));
        Turret(t, steel, new Vector3(0.0127f, scopeY, 0.03f), Quaternion.Euler(0f, 90f, 0f));
        for (int i = 0; i < 2; i++)
        {
            float z = i == 0 ? -0.045f : 0.105f;
            Turned(t, "ScopeRing", steel, scopeY, new[] { V(0.0155f, z), V(0.0155f, z + 0.014f) });
            MeshForge.Part(t, "RingBase", MeshForge.Extrude(MeshForge.RoundedRect(new Vector2(z - 0.002f, Bore + 0.012f),
                                                                                    new Vector2(z + 0.016f, scopeY - 0.012f), 0.003f), 0.008f, 0.002f),
                           steel, Vector3.zero, Quaternion.identity);
        }

        // Folded bipod under the forend.
        MeshForge.Part(t, "BipodMount", MeshForge.Extrude(MeshForge.RoundedRect(new Vector2(0.33f, -0.018f), new Vector2(0.37f, -0.004f), 0.004f), 0.014f, 0.003f),
                       steel, Vector3.zero, Quaternion.identity);
        for (int side = -1; side <= 1; side += 2)
            MeshForge.Rod(t, "BipodLeg", new Vector3(side * 0.011f, -0.014f, 0.35f), new Vector3(side * 0.011f, -0.012f, 0.13f), 0.0045f, steel);

        Anchor(t, "Grip", new Vector3(0f, -0.05f, -0.075f));
        Anchor(t, "BarrelGuard", new Vector3(0f, 0.01f, 0.26f));
        stock = new Vector3(0f, 0.025f, -0.476f);
        muzzle = new Vector3(0f, Bore, 0.966f);
        return root;
    }

    // ---------------------------------------------------------------- side-by-side shotgun

    public static GameObject Shotgun()
    {
        GameObject root = new GameObject("Shotgun");
        Transform t = root.transform;
        Material wood = MeshForge.Walnut;
        Material action = MeshForge.BrightSteel;
        Material steel = MeshForge.BluedSteel;

        Vector2[] stockOutline =
        {
            new Vector2(-0.42f, 0.044f), new Vector2(-0.25f, 0.042f), new Vector2(-0.125f, 0.031f),
            new Vector2(-0.07f, 0.034f), new Vector2(-0.056f, 0.030f), new Vector2(-0.056f, 0.004f),
            new Vector2(-0.072f, -0.022f), new Vector2(-0.108f, -0.046f), new Vector2(-0.16f, -0.042f),
            new Vector2(-0.30f, -0.058f), new Vector2(-0.42f, -0.076f), new Vector2(-0.426f, -0.015f),
        };
        MeshForge.Part(t, "Stock", MeshForge.Extrude(stockOutline, 0.021f, 0.007f), wood, Vector3.zero, Quaternion.identity);
        ButtPad(t, -0.426f, -0.076f, 0.044f, 0.022f);

        MeshForge.Part(t, "Action", MeshForge.Extrude(MeshForge.RoundedRect(new Vector2(-0.062f, 0.012f), new Vector2(0.066f, 0.062f), 0.012f), 0.026f, 0.004f),
                       action, Vector3.zero, Quaternion.identity);
        MeshForge.Rod(t, "TopLever", new Vector3(0f, 0.064f, -0.045f), new Vector3(0.012f, 0.066f, -0.085f), 0.004f, action);
        TriggerGroup(t, steel, 0.012f);
        MeshForge.Part(t, "SecondTrigger", MeshForge.Extrude(new[] { V(0.012f, -0.0f), V(0.015f, 0.0f), V(0.011f, -0.017f), V(0.007f, -0.016f) }, 0.0022f, 0.0008f),
                       steel, new Vector3(0f, 0.012f, 0f), Quaternion.identity);

        // The barrels are one part named "Barrels" so the break-open reload can hinge them.
        Mesh barrels = CombineBarrels();
        GameObject barrelPart = MeshForge.Part(t, "Barrels", barrels, steel, Vector3.zero, Quaternion.identity);
        for (int side = -1; side <= 1; side += 2)
        {
            Turned(barrelPart.transform, "Bore", MeshForge.Rubber, Bore, new[] { V(0.0095f, 0.7005f), V(0.0095f, 0.7015f) },
                   new Vector3(side * 0.0122f, 0f, 0f));
        }

        MeshForge.Ball(barrelPart.transform, "Bead", new Vector3(0f, 0.0625f, 0.69f), Vector3.one * 0.005f, MeshForge.Brass);
        GameObject forend = MeshForge.Part(barrelPart.transform, "BarrelGuard",
            MeshForge.Extrude(new[] { V(0.075f, 0.036f), V(0.30f, 0.036f), V(0.31f, 0.030f), V(0.30f, 0.020f), V(0.09f, 0.018f), V(0.075f, 0.026f) }, 0.022f, 0.006f),
            wood, Vector3.zero, Quaternion.identity);
        forend.name = "BarrelGuard";

        Anchor(t, "Grip", new Vector3(0f, -0.028f, -0.092f));
        return root;
    }

    static Mesh CombineBarrels()
    {
        Mesh tube = MeshForge.Lathe(new[] { V(0.0128f, 0.066f), V(0.0128f, 0.20f), V(0.0118f, 0.70f) }, 18);
        Mesh rib = MeshForge.Extrude(MeshForge.RoundedRect(new Vector2(0.066f, -0.002f), new Vector2(0.70f, 0.002f), 0.0015f), 0.004f, 0.0012f);
        var parts = new CombineInstance[4];
        parts[0] = new CombineInstance { mesh = tube, transform = Matrix4x4.Translate(new Vector3(-0.0122f, Bore, 0f)) };
        parts[1] = new CombineInstance { mesh = tube, transform = Matrix4x4.Translate(new Vector3(0.0122f, Bore, 0f)) };
        parts[2] = new CombineInstance { mesh = rib, transform = Matrix4x4.Translate(new Vector3(0f, Bore + 0.0128f, 0f)) };
        parts[3] = new CombineInstance { mesh = rib, transform = Matrix4x4.Translate(new Vector3(0f, Bore - 0.0122f, 0f)) };
        Mesh combined = new Mesh { name = "Barrels" };
        combined.CombineMeshes(parts, true, true);
        combined.RecalculateBounds();
        return combined;
    }

    // ---------------------------------------------------------------- shared parts

    static Vector2 V(float a, float b) => new Vector2(a, b);

    static GameObject Turned(Transform parent, string name, Material mat, float y, Vector2[] profile, Vector3 offset = default)
    {
        return MeshForge.Part(parent, name, MeshForge.Lathe(profile, 20), mat, new Vector3(offset.x, y, offset.z), Quaternion.identity);
    }

    static void ButtPad(Transform t, float z, float bottom, float top, float halfWidth)
    {
        MeshForge.Part(t, "ButtPad", MeshForge.Extrude(MeshForge.RoundedRect(new Vector2(z - 0.016f, bottom - 0.002f), new Vector2(z + 0.002f, top + 0.002f), 0.006f),
                                                     halfWidth + 0.001f, 0.004f),
                       MeshForge.Rubber, Vector3.zero, Quaternion.identity);
    }

    static void BoltHandle(Transform t, Material steel, float knob)
    {
        MeshForge.Rod(t, "BoltHandle", new Vector3(0.012f, Bore + 0.004f, -0.035f), new Vector3(0.046f, Bore - 0.016f, -0.042f), 0.0035f, steel);
        MeshForge.Ball(t, "BoltKnob", new Vector3(0.048f, Bore - 0.018f, -0.043f), Vector3.one * knob, steel);
    }

    static void TriggerGroup(Transform t, Material steel, float y)
    {
        Vector2[] guard =
        {
            V(0.022f, -0.011f), V(0.018f, -0.028f), V(0.005f, -0.036f), V(-0.02f, -0.037f), V(-0.038f, -0.030f), V(-0.046f, -0.016f),
            V(-0.041f, -0.015f), V(-0.034f, -0.026f), V(-0.02f, -0.032f), V(0.004f, -0.031f), V(0.014f, -0.025f), V(0.017f, -0.011f),
        };
        MeshForge.Part(t, "TriggerGuard", MeshForge.Extrude(guard, 0.0035f, 0.001f), steel, new Vector3(0f, y, 0f), Quaternion.identity);
        MeshForge.Part(t, "Trigger", MeshForge.Extrude(new[] { V(-0.004f, -0.011f), V(-0.001f, -0.011f), V(-0.005f, -0.028f), V(-0.009f, -0.027f) }, 0.0022f, 0.0008f),
                       steel, new Vector3(0f, y, 0f), Quaternion.identity);
    }

    static void Sight(Transform t, Material steel, float z, float height)
    {
        MeshForge.Part(t, "Sight", MeshForge.Extrude(new[] { V(z - 0.012f, Bore + 0.006f), V(z + 0.008f, Bore + 0.006f), V(z + 0.006f, Bore + height),
                                                            V(z, Bore + height + 0.003f), V(z - 0.006f, Bore + height) }, 0.0025f, 0.0008f),
                       steel, Vector3.zero, Quaternion.identity);
    }

    static void Turret(Transform t, Material steel, Vector3 at, Quaternion rot)
    {
        MeshForge.Part(t, "Turret", MeshForge.Lathe(new[] { V(0.0085f, 0f), V(0.0085f, 0.016f), V(0.0075f, 0.019f), V(0.004f, 0.02f) }, 16),
                       steel, at, rot);
    }

    static void Anchor(Transform t, string name, Vector3 local)
    {
        GameObject a = new GameObject(name);
        a.transform.SetParent(t, false);
        a.transform.localPosition = local;
    }
}
