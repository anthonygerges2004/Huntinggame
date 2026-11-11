using UnityEngine;

public static class FirstPersonHands
{
    const string RootName = "FP_Hands";

    static Material sleeveMat;
    static Material cuffMat;
    static Material skinMat;

    public static void AttachTo(Transform gun)
    {
        if (gun == null)
            return;

        Transform existing = gun.Find(RootName);
        if (existing != null)
            Object.Destroy(existing.gameObject);

        EnsureMaterials();

        GameObject root = new GameObject(RootName);
        root.transform.SetParent(gun, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        // Discover grip and barrel guard anchors if available
        Vector3 gripPos = FindAnchor(gun, "Grip", new Vector3(0f, -0.012f, -0.045f));
        Vector3 guardPos = FindAnchor(gun, "BarrelGuard", new Vector3(0f, 0.010f, 0.345f));

        BuildRightArm(root.transform, gripPos);
        BuildLeftArm(root.transform, guardPos);
    }

    static Vector3 FindAnchor(Transform gun, string keyword, Vector3 fallback)
    {
        Transform found = null;
        Transform[] all = gun.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                found = all[i];
                break;
            }
        }

        if (found != null && found != gun)
            return gun.InverseTransformPoint(found.position);

        return fallback;
    }

    static void BuildRightArm(Transform root, Vector3 grip)
    {
        Transform armRoot = new GameObject("RightArm").transform;
        armRoot.SetParent(root, false);

        // Forearm extends from off-screen bottom-right up to the wrist
        Vector3 armBase = grip + new Vector3(0.20f, -0.32f, -0.38f);
        Vector3 wrist = grip + new Vector3(0.040f, -0.065f, -0.09f);

        // Forearm sleeve, tapering toward the wrist
        MakeSleeve(armRoot, "ForearmSleeve", armBase, wrist, 0.094f, 0.078f, sleeveMat);

        // Leather gauntlet cuff of the glove
        Vector3 cuffPos = Vector3.Lerp(armBase, wrist, 0.86f);
        MakeSleeve(armRoot, "WristCuff", cuffPos, wrist, 0.088f, 0.074f, cuffMat);

        // Wrist joint
        MakeSphere(armRoot, "WristJoint", wrist, 0.066f, skinMat);

        // Palm holding the right side of the pistol grip
        Vector3 palmPos = grip + new Vector3(0.024f, -0.035f, -0.035f);
        MakeBox(armRoot, "Palm", palmPos, Quaternion.Euler(-10f, 15f, 25f), new Vector3(0.038f, 0.075f, 0.052f), skinMat);

        // Thumb curving over the top left of the grip
        Vector3 thumbRoot = grip + new Vector3(0.012f, 0.012f, -0.035f);
        Vector3 thumbTip = grip + new Vector3(-0.016f, 0.022f, -0.028f);
        MakeCylinder(armRoot, "Thumb", thumbRoot, thumbTip, 0.020f, skinMat);

        // Trigger finger (index) reaching towards the trigger
        Vector3 indexRoot = grip + new Vector3(0.018f, -0.005f, -0.010f);
        Vector3 indexTip = grip + new Vector3(0.006f, 0.004f, 0.022f);
        MakeCylinder(armRoot, "IndexTrigger", indexRoot, indexTip, 0.018f, skinMat);

        // Wrapped fingers (middle, ring, pinky) curling around the front of the handle
        Vector3 midRoot = grip + new Vector3(0.018f, -0.032f, -0.018f);
        Vector3 midTip = grip + new Vector3(-0.008f, -0.036f, -0.008f);
        MakeCylinder(armRoot, "MiddleFinger", midRoot, midTip, 0.018f, skinMat);

        Vector3 ringRoot = grip + new Vector3(0.016f, -0.058f, -0.038f);
        Vector3 ringTip = grip + new Vector3(-0.008f, -0.062f, -0.028f);
        MakeCylinder(armRoot, "RingFinger", ringRoot, ringTip, 0.017f, skinMat);

        Vector3 pinkyRoot = grip + new Vector3(0.014f, -0.082f, -0.058f);
        Vector3 pinkyTip = grip + new Vector3(-0.006f, -0.086f, -0.048f);
        MakeCylinder(armRoot, "PinkyFinger", pinkyRoot, pinkyTip, 0.016f, skinMat);
    }

    static void BuildLeftArm(Transform root, Vector3 guard)
    {
        Transform armRoot = new GameObject("LeftArm").transform;
        armRoot.SetParent(root, false);

        // Forearm reaches from bottom-left screen across to cradle the barrel guard
        Vector3 armBase = guard + new Vector3(-0.28f, -0.38f, -0.44f);
        Vector3 wrist = guard + new Vector3(-0.052f, -0.075f, -0.09f);

        // Forearm sleeve, tapering toward the wrist
        MakeSleeve(armRoot, "ForearmSleeve", armBase, wrist, 0.094f, 0.078f, sleeveMat);

        // Leather gauntlet cuff of the glove
        Vector3 cuffPos = Vector3.Lerp(armBase, wrist, 0.86f);
        MakeSleeve(armRoot, "WristCuff", cuffPos, wrist, 0.088f, 0.074f, cuffMat);

        // Wrist joint
        MakeSphere(armRoot, "WristJoint", wrist, 0.066f, skinMat);

        // Palm cradling the bottom of the wooden barrel guard
        Vector3 palmPos = guard + new Vector3(-0.018f, -0.040f, 0.015f);
        MakeBox(armRoot, "Palm", palmPos, Quaternion.Euler(15f, -10f, -25f), new Vector3(0.065f, 0.038f, 0.068f), skinMat);

        // Thumb resting on the top-left side of the handguard
        Vector3 thumbRoot = guard + new Vector3(-0.032f, -0.010f, -0.010f);
        Vector3 thumbTip = guard + new Vector3(-0.030f, 0.018f, 0.030f);
        MakeCylinder(armRoot, "Thumb", thumbRoot, thumbTip, 0.020f, skinMat);

        // Fingers curling under and wrapping up the right side of the fore-end
        Vector3 fIndexRoot = guard + new Vector3(-0.005f, -0.042f, 0.045f);
        Vector3 fIndexTip = guard + new Vector3(0.026f, 0.010f, 0.048f);
        MakeCylinder(armRoot, "IndexFinger", fIndexRoot, fIndexTip, 0.018f, skinMat);

        Vector3 fMidRoot = guard + new Vector3(-0.005f, -0.042f, 0.020f);
        Vector3 fMidTip = guard + new Vector3(0.026f, 0.006f, 0.022f);
        MakeCylinder(armRoot, "MiddleFinger", fMidRoot, fMidTip, 0.018f, skinMat);

        Vector3 fRingRoot = guard + new Vector3(-0.005f, -0.042f, -0.005f);
        Vector3 fRingTip = guard + new Vector3(0.024f, -0.002f, -0.004f);
        MakeCylinder(armRoot, "RingFinger", fRingRoot, fRingTip, 0.017f, skinMat);

        Vector3 fPinkyRoot = guard + new Vector3(-0.005f, -0.042f, -0.030f);
        Vector3 fPinkyTip = guard + new Vector3(0.022f, -0.010f, -0.028f);
        MakeCylinder(armRoot, "PinkyFinger", fPinkyRoot, fPinkyTip, 0.016f, skinMat);
    }

    /// <summary>A finger: a rounded capsule from start to end (thickness is the diameter).</summary>
    static GameObject MakeCylinder(Transform parent, string name, Vector3 start, Vector3 end, float thickness, Material mat)
    {
        return MakeSleeve(parent, name, start, end, thickness, thickness * 0.9f, mat);
    }

    /// <summary>A tapered tube with rounded ends, for sleeves, cuffs and fingers.</summary>
    static GameObject MakeSleeve(Transform parent, string name, Vector3 start, Vector3 end, float startDiameter, float endDiameter, Material mat)
    {
        Vector3 dir = end - start;
        float length = dir.magnitude;
        if (length < 0.001f)
            return null;

        float r0 = startDiameter * 0.5f, r1 = endDiameter * 0.5f;
        float cap0 = Mathf.Min(r0 * 0.8f, length * 0.3f), cap1 = Mathf.Min(r1 * 0.8f, length * 0.3f);
        var profile = new System.Collections.Generic.List<Vector2>();
        for (int i = 0; i <= 4; i++)
        {
            float a = i / 4f * Mathf.PI * 0.5f;
            profile.Add(new Vector2(Mathf.Sin(a) * r0, cap0 - Mathf.Cos(a) * cap0));
        }
        for (int i = 4; i >= 0; i--)
        {
            float a = i / 4f * Mathf.PI * 0.5f;
            profile.Add(new Vector2(Mathf.Sin(a) * r1, length - cap1 + Mathf.Cos(a) * cap1));
        }
        profile[0] = new Vector2(0.0001f, profile[0].y);
        profile[profile.Count - 1] = new Vector2(0.0001f, profile[profile.Count - 1].y);

        GameObject obj = MeshForge.Part(parent, name, MeshForge.Lathe(profile.ToArray(), 16, false, false), mat,
                                        start, Quaternion.LookRotation(dir / length));
        return obj;
    }

    static GameObject MakeSphere(Transform parent, string name, Vector3 pos, float diameter, Material mat)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = name;
        Object.Destroy(obj.GetComponent<Collider>());
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = pos;
        obj.transform.localScale = Vector3.one * diameter;
        if (mat != null)
            obj.GetComponent<Renderer>().sharedMaterial = mat;
        return obj;
    }

    /// <summary>A palm: a block with well-rounded edges instead of a hard cube.</summary>
    static GameObject MakeBox(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 scale, Material mat)
    {
        float radius = Mathf.Min(scale.y, scale.z) * 0.35f;
        Vector2[] outline = MeshForge.RoundedRect(new Vector2(-scale.z * 0.5f, -scale.y * 0.5f), new Vector2(scale.z * 0.5f, scale.y * 0.5f), radius);
        return MeshForge.Part(parent, name, MeshForge.Extrude(outline, scale.x * 0.5f, Mathf.Min(scale.x * 0.3f, radius)), mat, pos, rot);
    }

    static void EnsureMaterials()
    {
        if (sleeveMat != null && cuffMat != null && skinMat != null)
            return;

        Shader shader = FindSafeShader();
        // Same waxed-canvas jacket and leather gloves as the third-person hunter.
        sleeveMat = HunterOutfit.JacketMaterial();
        cuffMat = MeshForge.Lit("FPCuff", new Color(0.26f, 0.17f, 0.10f), 0.4f);
        skinMat = MeshForge.Lit("HunterGloves", new Color(0.46f, 0.32f, 0.19f), 0.42f);
    }

    static Shader FindSafeShader()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        if (s != null) return s;
        s = Shader.Find("Universal Render Pipeline/Unlit");
        if (s != null) return s;
        s = Shader.Find("Standard");
        if (s != null) return s;
        s = Shader.Find("Mobile/Diffuse");
        if (s != null) return s;
        s = Shader.Find("Sprites/Default");
        if (s != null) return s;
        return Shader.Find("Hidden/InternalErrorShader");
    }

    static Material CreateMaterial(Shader shader, Color color, float smoothness)
    {
        if (shader == null)
            return null;

        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0f);
        return mat;
    }
}
