using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Renders each gun's real in-game 3D model once into a small picture for the inventory slots,
/// so the icons always match the gun the player holds. Call Warm() from Update, never from OnGUI.
/// </summary>
public static class WeaponIcons
{
    const int Size = 256;
    const int IconLayer = 30;
    static readonly Color ClearColor = new Color(0.12f, 0.12f, 0.12f, 0f);
    static readonly HuntWeapon[] All = { HuntWeapon.Shotgun, HuntWeapon.Rifle, HuntWeapon.Sniper };
    static readonly Dictionary<HuntWeapon, Texture2D> cache = new Dictionary<HuntWeapon, Texture2D>();
    static bool gaveUp;
    static int attempts;
    static float nextTry;
    const int MaxAttempts = 6;

    public static bool Ready => gaveUp || cache.Count >= All.Length;

    /// <summary>The photo of the real model if it rendered, otherwise a drawn picture, so a slot is never blank.</summary>
    public static Texture2D Get(HuntWeapon weapon)
    {
        if (weapon == HuntWeapon.None)
            return null;

        if (cache.TryGetValue(weapon, out Texture2D icon) && icon != null)
            return icon;
        return ItemIcons.Get(weapon.ToString());
    }

    public static void Warm()
    {
        if (Ready)
            return;

        // Models and shaders can still be loading in the first frames; try again a few times.
        if (Time.unscaledTime < nextTry)
            return;
        nextTry = Time.unscaledTime + 0.75f;
        if (++attempts > MaxAttempts)
        {
            gaveUp = true;
            return;
        }

        for (int i = 0; i < All.Length; i++)
        {
            HuntWeapon weapon = All[i];
            if (cache.ContainsKey(weapon))
                continue;

            try
            {
                Texture2D icon = Render(weapon);
                if (icon != null)
                    cache[weapon] = icon;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Could not render the " + weapon + " icon: " + e.Message);
                gaveUp = true;
                return;
            }
        }
    }

    static Texture2D Render(HuntWeapon weapon)
    {
        GameObject stage = new GameObject("_WeaponIconStage");
        stage.transform.position = new Vector3(0f, 10000f, 0f);
        Camera cam = null;
        Light lamp = null;
        RenderTexture rt = null;
        RenderTexture previousActive = RenderTexture.active;
        bool fog = RenderSettings.fog;

        try
        {
            WeaponModel model = WeaponVisual.Build(weapon, stage.transform);
            if (model.root == null)
                return null;

            // Barrel to the right, tilted up like a classic inventory picture.
            model.root.transform.localPosition = Vector3.zero;
            model.root.transform.rotation = Quaternion.Euler(0f, 0f, 32f) * Quaternion.Euler(0f, 90f, 0f);
            PrepareForPhotograph(stage.transform);

            if (!TryBounds(stage, out Bounds bounds))
                return null;

            cam = new GameObject("_WeaponIconCamera").AddComponent<Camera>();
            cam.enabled = false;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.08f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ClearColor;
            cam.cullingMask = 1 << IconLayer;
            cam.allowHDR = false; // the HDR buffer has no alpha channel
            cam.allowMSAA = true;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 50f;
            cam.transform.SetPositionAndRotation(bounds.center - Vector3.forward * 10f, Quaternion.identity);

            lamp = new GameObject("_WeaponIconLight").AddComponent<Light>();
            lamp.type = LightType.Directional;
            lamp.intensity = 1.3f;
            lamp.color = new Color(1f, 0.97f, 0.92f);
            lamp.shadows = LightShadows.None;
            lamp.cullingMask = 1 << IconLayer;
            lamp.transform.rotation = Quaternion.Euler(35f, 25f, 0f);

            rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default, 4);
            cam.targetTexture = rt;

            RenderSettings.fog = false;
            // Unity 6's pipeline renders single cameras through a render request; Camera.Render
            // can come back empty there. Fall back to it only if requests are not supported.
            RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest { destination = rt };
            if (GraphicsSettings.currentRenderPipeline != null && RenderPipeline.SupportsRenderRequest(cam, request))
            {
                cam.targetTexture = null;
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            else
                cam.Render();
            RenderSettings.fog = fog;

            RenderTexture.active = rt;
            Texture2D icon = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            icon.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            KeyOutBackground(icon);
            if (!HasPicture(icon))
            {
                Object.DestroyImmediate(icon);
                return null;
            }

            icon.wrapMode = TextureWrapMode.Clamp;
            icon.filterMode = FilterMode.Bilinear;
            icon.name = weapon + "Icon";
            icon.Apply(false, true);
            return icon;
        }
        finally
        {
            RenderSettings.fog = fog;
            RenderTexture.active = previousActive;
            if (cam != null)
            {
                cam.targetTexture = null;
                Object.DestroyImmediate(cam.gameObject);
            }

            if (rt != null)
                RenderTexture.ReleaseTemporary(rt);
            if (lamp != null)
                Object.DestroyImmediate(lamp.gameObject);
            Object.DestroyImmediate(stage);
        }
    }

    static void PrepareForPhotograph(Transform root)
    {
        // The rifle and sniper are built from primitives, which come with colliders.
        // Strip them so the photo stage never touches physics.
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            Object.DestroyImmediate(colliders[i]);

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
            all[i].gameObject.layer = IconLayer;
    }

    static bool TryBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool any = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || !renderers[i].enabled)
                continue;
            if (!any)
            {
                bounds = renderers[i].bounds;
                any = true;
            }
            else
                bounds.Encapsulate(renderers[i].bounds);
        }

        return any && bounds.size.sqrMagnitude > 0.000001f;
    }

    /// <summary>
    /// If the pipeline kept the transparent clear, nothing to do. If it flattened alpha,
    /// cut out the flat background color so the slot color shows through.
    /// </summary>
    /// <summary>A blank render (nothing drawn) is rejected so the drawn picture is used instead.</summary>
    static bool HasPicture(Texture2D icon)
    {
        Color32[] pixels = icon.GetPixels32();
        int solid = 0;
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a > 128)
                solid++;
        }

        return solid > pixels.Length / 100;
    }

    static void KeyOutBackground(Texture2D icon)
    {
        Color32[] pixels = icon.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a < 250)
                return;
        }

        Color32 key = (Color)ClearColor;
        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 p = pixels[i];
            int d = Mathf.Abs(p.r - key.r) + Mathf.Abs(p.g - key.g) + Mathf.Abs(p.b - key.b);
            if (d <= 6)
                pixels[i].a = 0;
        }

        icon.SetPixels32(pixels);
    }
}
