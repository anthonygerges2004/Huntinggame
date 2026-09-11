using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance;

    public Light sun;
    public float dayLengthSeconds = 90f;
    public bool IsNight { get; private set; }

    float timeOfDay;

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

        IsNight = timeOfDay > 0.5f;

        if (sun != null)
        {
            sun.intensity = IsNight ? 0.15f : 1f;
            sun.color = IsNight ? new Color(0.35f, 0.4f, 0.7f) : Color.white;
        }
    }

    public string TimeLabel()
    {
        return IsNight ? "Night" : "Morning";
    }
}
