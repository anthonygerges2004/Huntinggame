using UnityEngine;

/// <summary>
/// Puts back anything an earlier cull pass hid, then leaves objects alone.
/// The camera frustum and the fixed terrain distances do the culling.
/// Flipping renderers off made them blink and left the scene dirty, which
/// made the next play-mode reload worse.
/// </summary>
public class HuntVisibility : MonoBehaviour
{
    const float FrustumPad = 6f;
    const float CoverInset = 0.012f;
    const float DepthGap = 2f;

    bool restored;

    void LateUpdate()
    {
        if (!Application.isPlaying || restored)
            return;

        restored = true;
        Restore();
        GameCulling.ApplyForActiveScene();
        enabled = false;
    }

    static void Restore()
    {
        int offLayer = LayerMask.NameToLayer("OffScreen");
        int offBit = offLayer >= 0 ? 1 << offLayer : 0;

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.forceRenderingOff = false;
            if (offLayer >= 0 && renderer.gameObject.layer == offLayer)
                renderer.gameObject.layer = 0;
        }

        if (offBit == 0)
            return;

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i] != null)
                cameras[i].cullingMask |= offBit;
        }
    }

    public static bool Outside(Plane[] planes, Bounds bounds)
    {
        bounds.Expand(FrustumPad);
        return !GeometryUtility.TestPlanesAABB(planes, bounds);
    }

    public static bool TryViewport(Camera cam, Bounds bounds, out Rect rect, out float nearZ, out float farZ)
    {
        rect = default;
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        nearZ = float.PositiveInfinity;
        farZ = float.NegativeInfinity;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = center + new Vector3(
                (i & 1) == 0 ? -extents.x : extents.x,
                (i & 2) == 0 ? -extents.y : extents.y,
                (i & 4) == 0 ? -extents.z : extents.z);
            Vector3 vp = cam.WorldToViewportPoint(corner);
            if (vp.z <= 0.05f)
                return false;

            if (vp.x < minX) minX = vp.x;
            if (vp.y < minY) minY = vp.y;
            if (vp.x > maxX) maxX = vp.x;
            if (vp.y > maxY) maxY = vp.y;
            if (vp.z < nearZ) nearZ = vp.z;
            if (vp.z > farZ) farZ = vp.z;
        }

        rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    public static bool Covers(Rect face, float faceNear, Rect cover, float coverFar, float inset)
    {
        if (faceNear < coverFar + DepthGap)
            return false;

        return face.xMin >= cover.xMin + inset
            && face.xMax <= cover.xMax - inset
            && face.yMin >= cover.yMin + inset
            && face.yMax <= cover.yMax - inset;
    }

    public static bool Occluded(Camera cam, Bounds subject, Bounds occluder)
    {
        occluder.extents *= 0.82f;
        if (!TryViewport(cam, occluder, out Rect cover, out _, out float coverFar))
            return false;
        if (cover.width < 0.04f || cover.height < 0.04f)
            return false;
        if (!TryViewport(cam, subject, out Rect face, out float faceNear, out _))
            return false;
        return Covers(face, faceNear, cover, coverFar, CoverInset);
    }

    public static bool Solid(Transform root, Bounds bounds)
    {
        if (root == null || !SolidSize(bounds))
            return false;
        if (root.GetComponentInParent<Animal>() != null)
            return false;
        if (root.GetComponentInParent<HarvestableTree>() != null)
            return false;
        return !NamedFoliage(root);
    }

    public static bool SolidSize(Bounds bounds)
    {
        float x = bounds.extents.x;
        float y = bounds.extents.y;
        float z = bounds.extents.z;
        float small = x;
        float mid = y;
        float large = z;
        if (small > mid) Swap(ref small, ref mid);
        if (mid > large) Swap(ref mid, ref large);
        if (small > mid) Swap(ref small, ref mid);
        return large > 1.4f && large < 45f && mid > 0.8f && small > 0.08f;
    }

    static bool NamedFoliage(Transform root)
    {
        Transform t = root;
        for (int depth = 0; depth < 5 && t != null; depth++)
        {
            string name = t.name;
            if (Has(name, "tree") || Has(name, "bush") || Has(name, "grass")
                || Has(name, "plant") || Has(name, "fern") || Has(name, "leaf")
                || Has(name, "flower") || Has(name, "shrub") || Has(name, "pine")
                || Has(name, "oak") || Has(name, "birch") || Has(name, "weed"))
                return true;
            t = t.parent;
        }

        return false;
    }

    static bool Has(string name, string token)
    {
        return name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void Swap(ref float a, ref float b)
    {
        float hold = a;
        a = b;
        b = hold;
    }
}
