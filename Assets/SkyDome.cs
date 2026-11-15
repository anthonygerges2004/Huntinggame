using UnityEngine;

/// <summary>
/// Drives the live sky (Resources/Shaders/HuntSky.shader) from the sun: blue day, golden
/// sunrise and sunset, a starry moonlit night, cloud cover that follows the weather. Takes
/// over from the three static skybox pictures, which swapped abruptly and never moved.
/// Set Enabled = false to go back to the pictures.
/// </summary>
public class SkyDome : MonoBehaviour
{
    public const bool Enabled = true;

    Material sky;
    float cover = 0.4f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!Enabled || Object.FindAnyObjectByType<SkyDome>() != null)
            return;
        Shader shader = Shader.Find("HuntingGame/Sky");
        if (shader == null)
            return;
        SkyDome dome = new GameObject("_SkyDome").AddComponent<SkyDome>();
        dome.sky = new Material(shader) { name = "HuntSky (live)" };
    }

    static readonly Color DayZenith = new Color(0.16f, 0.36f, 0.78f);
    static readonly Color DayHorizon = new Color(0.62f, 0.76f, 0.92f);
    static readonly Color DuskZenith = new Color(0.20f, 0.26f, 0.52f);
    static readonly Color DuskHorizon = new Color(1.00f, 0.56f, 0.32f);
    static readonly Color NightZenith = new Color(0.008f, 0.014f, 0.04f);
    static readonly Color NightHorizon = new Color(0.04f, 0.06f, 0.12f);

    void LateUpdate()
    {
        if (sky == null)
            return;
        if (RenderSettings.skybox != sky)
            RenderSettings.skybox = sky; // replaces DayNightCycle's static pictures

        Light sun = DayNightCycle.Instance != null ? DayNightCycle.Instance.sun : RenderSettings.sun;
        Vector3 toSun = sun != null ? -sun.transform.forward : new Vector3(0.3f, 0.6f, 0.7f).normalized;
        float elev = toSun.y;

        float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.32f, elev));
        float night = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, -0.16f, elev));
        if (DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight)
            night = Mathf.Max(night, 0.85f);
        float dusk = Mathf.Clamp01(1f - day - night);

        Color zenith = DayZenith * day + DuskZenith * dusk + NightZenith * night;
        Color horizon = DayHorizon * day + DuskHorizon * dusk + NightHorizon * night;
        Color sunColor = sun != null ? sun.color : Color.white;
        sunColor = Color.Lerp(sunColor, new Color(1f, 0.62f, 0.36f), dusk * 0.6f);

        // Weather: thicker fog means heavier cloud.
        float target = Mathf.Clamp01(0.3f + (RenderSettings.fogDensity - 0.0009f) / 0.005f);
        cover = Mathf.MoveTowards(cover, target, Time.deltaTime * 0.05f);

        sky.SetColor("_ZenithColor", zenith);
        sky.SetColor("_HorizonColor", horizon);
        sky.SetColor("_GroundColor", Color.Lerp(horizon * 0.55f, new Color(0.02f, 0.025f, 0.03f), night));
        sky.SetColor("_SunColor", sunColor);
        sky.SetVector("_SunDir", toSun);
        sky.SetFloat("_GlowAmount", dusk * 1.4f);
        sky.SetFloat("_Night", night);
        sky.SetFloat("_CloudCover", cover);
        sky.SetColor("_CloudShade", Color.Lerp(new Color(0.55f, 0.6f, 0.68f), new Color(0.6f, 0.42f, 0.4f), dusk));
        sky.SetFloat("_FogBlend", Mathf.Lerp(0.75f, 0.95f, cover));
        sky.SetFloat("_Exposure", Mathf.Lerp(1.15f, 1f, night));
    }

    void OnDestroy()
    {
        if (sky != null)
            Destroy(sky);
    }
}
