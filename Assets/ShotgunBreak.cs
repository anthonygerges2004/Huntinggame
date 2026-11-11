using UnityEngine;

/// <summary>
/// Break-action reload: the barrels hinge open at the breech and two shells are seated.
/// </summary>
public class ShotgunBreak : MonoBehaviour
{
    const float OpenDegrees = 52f;

    Transform hinge;
    Transform spentLeft;
    Transform spentRight;
    Transform freshLeft;
    Transform freshRight;

    static Material hullMat;
    static Material brassMat;

    public static void Rig(Transform root)
    {
        if (root == null || root.GetComponent<ShotgunBreak>() != null)
            return;

        ShotgunBreak action = root.gameObject.AddComponent<ShotgunBreak>();
        action.Build();
    }

    public void GrabForeend(Transform leftGrip)
    {
        if (hinge == null)
            return;

        Transform hands = transform.Find("FP_Hands");
        if (hands != null)
        {
            Transform leftArm = hands.Find("LeftArm");
            if (leftArm != null)
                leftArm.SetParent(hinge, true);
        }

        if (leftGrip != null && leftGrip.parent != hinge)
            leftGrip.SetParent(hinge, true);
    }

    public void SetOpen(float open, float load)
    {
        open = Mathf.Clamp01(open);
        load = Mathf.Clamp01(load);
        if (hinge != null)
            hinge.localRotation = Quaternion.Euler(OpenDegrees * open, 0f, 0f);

        float eject = Mathf.InverseLerp(0.08f, 0.78f, open);
        bool showSpent = open > 0.08f && open < 0.86f && load < 0.08f;
        PlaceShell(spentLeft, -1f, eject, showSpent, true);
        PlaceShell(spentRight, 1f, eject, showSpent, true);

        float rightLoad = Mathf.Clamp01((load - 0.22f) / 0.78f);
        bool showFresh = open > 0.25f && load > 0.02f;
        PlaceShell(freshLeft, -1f, load, showFresh, false);
        PlaceShell(freshRight, 1f, rightLoad, showFresh && load > 0.18f, false);
    }

    void Build()
    {
        Transform barrel = FindPart("ShotGun_C_Barrel");
        if (barrel == null)
            barrel = FindPart("Barrels");

        hinge = new GameObject("BarrelHinge").transform;
        hinge.SetParent(transform, false);
        hinge.localRotation = Quaternion.identity;
        hinge.localScale = Vector3.one;
        if (barrel != null)
        {
            hinge.position = BreechPoint(barrel);
            hinge.rotation = transform.rotation;
        }
        else
        {
            hinge.localPosition = new Vector3(0f, 0.03f, 0.16f);
        }

        Reparent("ShotGun_C_Barrel");
        Reparent("ShotGun_C_BarrelGuard");
        Reparent("ShotGun_C_Sight_Forward");
        Reparent("ShotGun_C_Mag");
        Reparent("Barrels");

        spentLeft = MakeShell("SpentLeft");
        spentRight = MakeShell("SpentRight");
        freshLeft = MakeShell("FreshLeft");
        freshRight = MakeShell("FreshRight");
        SetOpen(0f, 0f);
    }

    void Reparent(string partName)
    {
        Transform part = FindPart(partName);
        if (part == null || hinge == null || part == hinge || part.IsChildOf(hinge))
            return;

        part.SetParent(hinge, true);
    }

    Transform FindPart(string partName)
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].name == partName)
                return all[i];
        }

        return null;
    }

    static Vector3 BreechPoint(Transform barrel)
    {
        MeshFilter filter = barrel.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
        {
            Bounds mesh = filter.sharedMesh.bounds;
            return barrel.TransformPoint(new Vector3(mesh.center.x, mesh.center.y, mesh.min.z));
        }

        return barrel.position;
    }

    Transform MakeShell(string shellName)
    {
        EnsureMaterials();
        GameObject hull = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hull.name = shellName;
        Collider solid = hull.GetComponent<Collider>();
        if (solid != null)
            Destroy(solid);

        hull.transform.SetParent(hinge, false);
        hull.transform.localScale = new Vector3(0.02f, 0.034f, 0.02f);
        Renderer hullRenderer = hull.GetComponent<Renderer>();
        hullRenderer.sharedMaterial = hullMat;
        hullRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        GameObject brass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        brass.name = "Brass";
        Collider brassSolid = brass.GetComponent<Collider>();
        if (brassSolid != null)
            Destroy(brassSolid);
        brass.transform.SetParent(hull.transform, false);
        brass.transform.localPosition = new Vector3(0f, -0.82f, 0f);
        brass.transform.localRotation = Quaternion.identity;
        brass.transform.localScale = new Vector3(1.08f, 0.28f, 1.08f);
        Renderer brassRenderer = brass.GetComponent<Renderer>();
        brassRenderer.sharedMaterial = brassMat;
        brassRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        hull.SetActive(false);
        return hull.transform;
    }

    static void PlaceShell(Transform shell, float side, float t, bool visible, bool spent)
    {
        if (shell == null)
            return;

        shell.gameObject.SetActive(visible);
        if (!visible)
            return;

        t = Mathf.Clamp01(t);
        if (spent)
        {
            shell.localPosition = new Vector3(
                0.016f * side,
                Mathf.Lerp(0.01f, 0.11f, t),
                Mathf.Lerp(0.04f, -0.05f, t));
            shell.localRotation = Quaternion.Euler(Mathf.Lerp(90f, 18f, t), 24f * side * t, 0f);
            return;
        }

        shell.localPosition = new Vector3(
            0.016f * side * Mathf.Lerp(1.6f, 1f, t),
            Mathf.Lerp(0.1f, 0.012f, t),
            Mathf.Lerp(-0.02f, 0.055f, t));
        shell.localRotation = Quaternion.Euler(Mathf.Lerp(28f, 90f, t), 0f, 0f);
    }

    static void EnsureMaterials()
    {
        if (hullMat != null && brassMat != null)
            return;

        hullMat = MakeMat(new Color(0.62f, 0.14f, 0.1f), 0.35f);
        brassMat = MakeMat(new Color(0.78f, 0.58f, 0.22f), 0.72f);
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
