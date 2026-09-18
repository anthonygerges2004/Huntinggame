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

    float timeOfDay;
    Material currentSkybox;
    Material nextSkybox;
    float skyboxBlend = 0f;

    void Awake()
    {
        Instance = this;
        if (sun == null)
            sun = FindAnyObjectByType<Light>();
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

        Material targetSkybox = null;

        // Much longer transition periods for smooth visual transition
        if (timeOfDay < 0.2f)
            targetSkybox = skyboxDay;
        else if (timeOfDay < 0.5f)
            targetSkybox = skyboxSunset; // Long sunset period (0.2 to 0.5)
        else if (timeOfDay < 0.7f)
            targetSkybox = skyboxNight; // Night (0.5 to 0.7)
        else
            targetSkybox = skyboxSunset; // Long sunrise period (0.7 onwards)

        if (targetSkybox != currentSkybox)
        {
            currentSkybox = targetSkybox;
            RenderSettings.skybox = currentSkybox;
            DynamicGI.UpdateEnvironment();
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
            sun.intensity = Mathf.Max(0.1f, sunHeight * 1.3f);

            // Smooth color transitions across entire day
            Color targetColor = Color.white;

            if (timeOfDay < 0.2f)
            {
                // Day: white
                targetColor = Color.white;
            }
            else if (timeOfDay < 0.5f)
            {
                // Long sunset transition (0.2 to 0.5 = 30 seconds out of 90)
                float sunsetProgress = (timeOfDay - 0.2f) / 0.3f;
                targetColor = Color.Lerp(Color.white, new Color(1f, 0.4f, 0.2f), sunsetProgress);
            }
            else if (timeOfDay < 0.7f)
            {
                // Night: deep blue
                targetColor = new Color(0.6f, 0.6f, 0.9f);
            }
            else
            {
                // Long sunrise transition (0.7 to 1.0)
                float sunriseProgress = (timeOfDay - 0.7f) / 0.3f;
                targetColor = Color.Lerp(new Color(1f, 0.4f, 0.2f), Color.white, sunriseProgress);
            }

            sun.color = targetColor;
        }

        // Smooth ambient light transition
        float targetIntensity = IsNight ? 0.3f : 1f;
        RenderSettings.ambientIntensity = Mathf.Lerp(RenderSettings.ambientIntensity, targetIntensity, Time.deltaTime * 0.3f);
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
