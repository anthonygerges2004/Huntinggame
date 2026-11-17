using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The game's camera look, built at runtime as a global post-processing volume:
/// filmic ACES tone mapping; a graded image with cool teal shadows and warm highlights;
/// golden-hour warmth at sunrise and sunset, a blue moonlit night; soft bloom on the sun,
/// fires and the moon; atmospheric depth that softens the far hills; a gentle focus pull
/// toward what you aim at; fine film grain and vignette. Low health drains the colour and
/// closes the vignette. The player camera is switched to render post-processing.
/// </summary>
public class CinematicLook : MonoBehaviour
{
    VolumeProfile profile;
    ColorAdjustments color;
    WhiteBalance balance;
    SplitToning split;
    LiftGammaGain lgg;
    Vignette vignette;
    Bloom bloom;
    DepthOfField focus;
    ChromaticAberration fringe;
    Camera tuned;
    bool sunTuned;
    float aimBlend;

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
        color.postExposure.Override(0.3f);
        color.contrast.Override(18f);
        color.saturation.Override(14f);

        balance = profile.Add<WhiteBalance>(true);
        balance.temperature.Override(6f);
        balance.tint.Override(3f);

        // Teal shadows, warm highlights: the classic outdoor film grade.
        split = profile.Add<SplitToning>(true);
        split.shadows.Override(new Color(0.36f, 0.52f, 0.60f));
        split.highlights.Override(new Color(0.96f, 0.80f, 0.60f));
        split.balance.Override(-12f);

        lgg = profile.Add<LiftGammaGain>(true);
        lgg.lift.Override(new Vector4(0.98f, 1.0f, 1.03f, -0.02f));   // deeper, slightly cool blacks
        lgg.gamma.Override(new Vector4(1.0f, 1.0f, 1.0f, 0.0f));
        lgg.gain.Override(new Vector4(1.03f, 1.0f, 0.96f, 0.03f));    // warm, bright top end

        ShadowsMidtonesHighlights smh = profile.Add<ShadowsMidtonesHighlights>(true);
        smh.shadows.Override(new Vector4(0.96f, 0.99f, 1.05f, 0f));
        smh.midtones.Override(new Vector4(1.0f, 1.0f, 0.99f, 0f));
        smh.highlights.Override(new Vector4(1.04f, 1.0f, 0.95f, 0f));

        bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.95f);
        bloom.intensity.Override(0.6f);
        bloom.scatter.Override(0.72f);
        bloom.tint.Override(new Color(1f, 0.93f, 0.84f));
        bloom.highQualityFiltering.Override(true);

        vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.24f);
        vignette.smoothness.Override(0.5f);
        vignette.rounded.Override(false);

        // Distant hills soften, like air in a real valley; the near world stays crisp.
        focus = profile.Add<DepthOfField>(true);
        focus.mode.Override(DepthOfFieldMode.Gaussian);
        focus.gaussianStart.Override(180f);
        focus.gaussianEnd.Override(900f);
        focus.gaussianMaxRadius.Override(0.65f);
        focus.highQualitySampling.Override(true);

        fringe = profile.Add<ChromaticAberration>(true);
        fringe.intensity.Override(0.05f);

        FilmGrain grain = profile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin1);
        grain.intensity.Override(0.1f);
        grain.response.Override(0.85f);

        GameObject go = new GameObject("CinematicVolume");
        go.transform.SetParent(transform, false);
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
    }

    void OnDestroy()
    {
        if (profile != null)
            Destroy(profile);
    }

    void TuneSun()
    {
        if (sunTuned)
            return;
        Light sun = DayNightCycle.Instance != null ? DayNightCycle.Instance.sun : RenderSettings.sun;
        if (sun == null)
            return;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.88f;
        sun.shadowBias = 0.04f;
        sun.shadowNormalBias = 0.35f;
        sunTuned = true;
    }

    void LateUpdate()
    {
        if (profile == null)
            return;

        // The heavier effects follow the Graphics setting in the pause menu.
        bool rich = GraphicsQuality.RichPost;
        bloom.active = rich;
        fringe.active = rich;
        focus.active = GraphicsQuality.DepthOfField;

        TuneSun();

        // The player camera can be replaced (view switch); keep post-processing on for it.
        Camera cam = PlayerView.GetCamera();
        if (cam != null && cam != tuned)
        {
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
                data.stopNaN = true;
                data.dithering = true; // no banding in the sky gradient
            }
            tuned = cam;
        }

        DayNightCycle day = DayNightCycle.Instance;
        float night = day != null && day.IsNight ? 1f : 0f;
        float daylight = day != null ? Mathf.Clamp01(day.Daylight / 1.35f) : 1f;
        // Golden hour: the sun low in the sky (just after sunrise, just before sunset).
        float t = day != null ? day.TimeOfDay01 : 0.25f;
        float golden = night > 0.5f ? 0f
            : Mathf.Max(Mathf.InverseLerp(0.36f, 0.5f, t) * (t < 0.5f ? 1f : 0f),
                        Mathf.InverseLerp(0.82f, 0.72f, t) * (t > 0.72f ? 1f : 0f));

        float health = PlayerHealth.Instance != null
            ? Mathf.Clamp01(PlayerHealth.Instance.health / Mathf.Max(1f, PlayerHealth.Instance.maxHealth))
            : 1f;
        float hurt = Mathf.InverseLerp(0.4f, 0.1f, health);

        balance.temperature.value = Mathf.Lerp(Mathf.Lerp(6f, 22f, golden), -18f, night);
        balance.tint.value = Mathf.Lerp(3f, 6f, golden);
        color.saturation.value = Mathf.Lerp(Mathf.Lerp(14f, 20f, golden), -22f, night) - 60f * hurt;
        color.contrast.value = Mathf.Lerp(18f, 24f, night);
        color.postExposure.value = Mathf.Lerp(0.3f, 0.05f, night) + (1f - daylight) * 0.15f;
        color.colorFilter.value = Color.Lerp(Color.white, new Color(0.80f, 0.88f, 1.0f), night);
        split.balance.value = Mathf.Lerp(-12f, -45f, night);
        bloom.intensity.value = Mathf.Lerp(Mathf.Lerp(0.6f, 0.85f, golden), 0.95f, night);

        // Aiming pulls focus onto the target: the far haze relaxes so distant game stays sharp,
        // and the frame edges darken slightly to draw the eye in.
        float aimTarget = PlayerShotgun.IsAiming ? 1f : 0f;
        aimBlend = Mathf.MoveTowards(aimBlend, aimTarget, Time.deltaTime * 4f);
        focus.gaussianStart.value = Mathf.Lerp(180f, 650f, aimBlend);
        focus.gaussianMaxRadius.value = Mathf.Lerp(0.65f, 0.25f, aimBlend);

        vignette.intensity.value = Mathf.Lerp(0.24f, 0.36f, night) + 0.08f * aimBlend + 0.2f * hurt;
        vignette.color.value = Color.Lerp(Color.black, new Color(0.35f, 0f, 0f), hurt);
        fringe.intensity.value = 0.05f + 0.25f * hurt;
    }
}
