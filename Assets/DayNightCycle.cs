using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance;

    public Light sun;
    public Material skyboxDay;
    public Material skyboxSunset;
    public Material skyboxNight;
    public float dayLengthSeconds = 90f;
    public bool IsNight { get; private set; }

    public Color ambientSky = new Color(0.68f, 0.73f, 0.80f);
    public Color ambientHorizon = new Color(0.58f, 0.58f, 0.54f);
    public Color ambientGround = new Color(0.38f, 0.35f, 0.30f);

    float timeOfDay;
    float ambientLevel = 1f;
    Material currentSkybox;

    void Awake()
    {
        Instance = this;
        if (sun == null)
            sun = FindAnyObjectByType<Light>();

        // The sky images have a teal sea in their lower half, which tints shaded surfaces blue.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.reflectionIntensity = 0.28f;
        RenderSettings.ambientIntensity = 1f;
    }

    void Update()
    {
        timeOfDay += Time.deltaTime / dayLengthSeconds;
        if (timeOfDay > 1f)
            timeOfDay -= 1f;

        IsNight = timeOfDay >= 0.5f && timeOfDay < 0.7f;

        UpdateSkybox();
        UpdateLighting();
    }

    void UpdateSkybox()
    {
        if (skyboxDay == null || skyboxSunset == null || skyboxNight == null)
            return;

        Material targetSkybox;
        if (timeOfDay < 0.2f)
            targetSkybox = skyboxDay;
        else if (timeOfDay < 0.5f)
            targetSkybox = skyboxSunset;
        else if (timeOfDay < 0.7f)
            targetSkybox = skyboxNight;
        else
            targetSkybox = skyboxSunset;

        if (targetSkybox != currentSkybox)
        {
            currentSkybox = targetSkybox;
            RenderSettings.skybox = currentSkybox;
        }
    }

    void UpdateLighting()
    {
        if (sun != null)
        {
            // 0 = dawn, 0.25 = noon, 0.5 = dusk, 0.75 = midnight
            sun.transform.rotation = Quaternion.Euler(timeOfDay * 360f, 170f, 0f);

            // Dynamic intensity based on sun position with smooth curve
            float sunHeight = Mathf.Sin(timeOfDay * Mathf.PI);
            float shaped = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(sunHeight));
            sun.intensity = Mathf.Lerp(0.16f, 1.35f, shaped);

            // Smooth color transitions across entire day
            Color targetColor = Color.white;

            if (timeOfDay < 0.18f)
            {
                targetColor = new Color(1f, 0.98f, 0.93f);
            }
            else if (timeOfDay < 0.52f)
            {
                float sunsetProgress = (timeOfDay - 0.18f) / 0.34f;
                targetColor = Color.Lerp(new Color(1f, 0.96f, 0.90f), new Color(1f, 0.48f, 0.26f), sunsetProgress);
            }
            else if (timeOfDay < 0.72f)
            {
                targetColor = new Color(0.58f, 0.62f, 0.90f);
            }
            else
            {
                float sunriseProgress = (timeOfDay - 0.72f) / 0.28f;
                targetColor = Color.Lerp(new Color(1f, 0.50f, 0.28f), new Color(1f, 0.97f, 0.92f), sunriseProgress);
            }

            sun.color = targetColor;
        }

        float targetLevel = IsNight ? 0.42f : 1f;
        ambientLevel = Mathf.Lerp(ambientLevel, targetLevel, Time.deltaTime * 0.45f);
        RenderSettings.ambientSkyColor = ambientSky * ambientLevel;
        RenderSettings.ambientEquatorColor = ambientHorizon * ambientLevel;
        RenderSettings.ambientGroundColor = ambientGround * ambientLevel;
    }

    public string TimeLabel()
    {
        if (timeOfDay < 0.2f)
            return "Morning";
        else if (timeOfDay < 0.5f)
            return "Sunset";
        else if (timeOfDay < 0.7f)
            return "Night";
        else
            return "Sunrise";
    }
}
