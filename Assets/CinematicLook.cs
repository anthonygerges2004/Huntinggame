using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The game's camera look: filmic tone mapping, a warm natural grade, soft bloom on the sun and
/// fires, a gentle vignette and a touch of film grain. Night pulls the grade cool and dark;
/// low health drains the colour. Built at runtime as a global post-processing volume, and the
/// player camera is switched to render it (it was off).
/// </summary>
public class CinematicLook : MonoBehaviour
{
    Volume volume;
    VolumeProfile profile;
    ColorAdjustments color;
    WhiteBalance balance;
    Vignette vignette;
    Bloom bloom;
    Camera tuned;

    public static void Ensure(GameObject host)
    {
        if (host.GetComponent<CinematicLook>() == null)
            host.AddComponent<CinematicLook>();
    }

    void Start()
    {
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "HuntingGameLook";

        Tonemapping tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.35f);
        color.contrast.Override(14f);
        color.saturation.Override(10f);

        balance = profile.Add<WhiteBalance>(true);
        balance.temperature.Override(6f);
        balance.tint.Override(2f);

        ShadowsMidtonesHighlights smh = profile.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.Override(new Vector4(0.96f, 0.98f, 1.05f, 0f));   // slightly cool shadows
        smh.highlights.Override(new Vector4(1.04f, 1.0f, 0.95f, 0f)); // warm highlights

        bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.05f);
        bloom.intensity.Override(0.55f);
        bloom.scatter.Override(0.68f);

        vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.22f);
        vignette.smoothness.Override(0.45f);

        FilmGrain grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.12f);

        GameObject go = new GameObject("CinematicVolume");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
    }

    void OnDestroy()
    {
        if (profile != null)
            Destroy(profile);
    }

    void LateUpdate()
    {
        if (profile == null)
            return;

        // The player camera can be replaced (view switch); keep post-processing on for it.
        Camera cam = PlayerView.GetCamera();
        if (cam != null && cam != tuned)
        {
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.None; // MSAA already smooths edges
            }
            tuned = cam;
        }

        // Night: cooler, darker, more vignette. Low health: colour drains away.
        float night = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight ? 1f : 0f;
        float daylight = DayNightCycle.Instance != null ? Mathf.Clamp01(DayNightCycle.Instance.Daylight / 1.35f) : 1f;
        float health = PlayerHealth.Instance != null ? Mathf.Clamp01(PlayerHealth.Instance.health / Mathf.Max(1f, PlayerHealth.Instance.maxHealth)) : 1f;
        float hurt = Mathf.InverseLerp(0.4f, 0.1f, health);

        balance.temperature.value = Mathf.Lerp(6f, -14f, night);
        color.saturation.value = Mathf.Lerp(10f, -15f, night) - 55f * hurt;
        color.postExposure.value = Mathf.Lerp(0.35f, 0.15f, night) + (1f - daylight) * 0.1f;
        vignette.intensity.value = Mathf.Lerp(0.22f, 0.34f, night) + 0.15f * hurt;
        bloom.intensity.value = Mathf.Lerp(0.55f, 0.8f, night); // fires and the moon glow more at night
    }
}
