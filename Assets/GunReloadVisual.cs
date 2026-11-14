using UnityEngine;

/// <summary>Moves a separate magazine mesh, or a temporary replacement for a fused imported model.</summary>
public sealed class GunReloadVisual : MonoBehaviour
{
    Transform magazine;
    Vector3 seated;
    Quaternion rotation;
    bool replacement;

    public void Build(HuntWeapon weapon)
    {
        if (weapon == HuntWeapon.Shotgun) return;
        foreach (Transform part in GetComponentsInChildren<Transform>(true))
        {
            string name = part.name.ToLowerInvariant();
            if (part != transform && (name.Contains("magazine") || name.EndsWith("_mag"))
                && part.GetComponent<Renderer>() != null)
            { magazine = part; break; }
        }
        if (magazine == null)
        {
            // Some pack guns are a single mesh. Show the fresh magazine only during the reload.
            GameObject model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.name = "Reload magazine";
            Collider collider = model.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            magazine = model.transform;
            magazine.SetParent(transform, false);
            magazine.localPosition = weapon == HuntWeapon.Pistol
                ? new Vector3(0f, -0.065f, -0.025f) : new Vector3(0f, -0.065f, 0.12f);
            magazine.localScale = weapon == HuntWeapon.Pistol
                ? new Vector3(0.024f, 0.085f, 0.035f) : new Vector3(0.035f, 0.13f, 0.065f);
            Renderer renderer = model.GetComponent<Renderer>();
            renderer.sharedMaterial = MeshForge.BluedSteel;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            replacement = true;
            model.SetActive(false);
        }
        seated = magazine.localPosition;
        rotation = magazine.localRotation;
    }

    public void Pose(bool reloading, float progress)
    {
        if (magazine == null) return;
        float outAmount = Removal01(progress);
        magazine.gameObject.SetActive(!replacement || (reloading && progress > 0.18f && progress < 0.82f));
        magazine.localPosition = seated + Vector3.down * (reloading ? 0.22f * outAmount : 0f);
        magazine.localRotation = rotation * Quaternion.Euler(0f, 0f, reloading ? -15f * outAmount : 0f);
    }

    public static float Removal01(float progress)
    {
        float outwards = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.38f, progress));
        float inwards = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.52f, 0.78f, progress));
        return outwards * (1f - inwards);
    }
}
