using HuntingGame.Weather;
using HuntingGame.Wind;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// Weather moves over the whole land. It does not reload a scene and it does not reset when you walk away.
/// </summary>
[DefaultExecutionOrder(50)]
public class WorldWeather : MonoBehaviour
{
    public static WorldWeather Instance;

    public enum Kind
    {
        Clear,
        Overcast,
        Rain,
        Fog
    }

    public Kind Current { get; private set; }
    public string Label { get; private set; } = "Clear";

    public void SetKind(Kind kind)
    {
        Current = kind;
        if (kind == Kind.Clear)
            Label = "Clear";
        else if (kind == Kind.Overcast)
            Label = "Overcast";
        else if (kind == Kind.Rain)
            Label = "Rain";
        else
            Label = "Fog";
    }

    static readonly ProfilerMarker ApplyMarker = new ProfilerMarker("HuntingGame.Weather");

    [SerializeField] WeatherPreset[] presets;

    Light sun;
    WindZone wind;
    ParticleSystem rain;
    WeatherPreset active;
    int index;
    float nextChange;
    float flashUntil;

    void Awake()
    {
        Instance = this;
        sun = FindAnyObjectByType<Light>();
        BuildWind();
        BuildRain();
        if (presets == null || presets.Length == 0)
            presets = WeatherCatalog.BuiltIn();

        index = 0;
        active = presets[0];
        if (active != null)
        {
            int kind = active.KindId;
            Current = kind >= 0 && kind <= 3 ? (Kind)kind : Kind.Clear;
            Label = active.Label;
        }
        else
        {
            Current = Kind.Clear;
            Label = "Clear";
        }

        float hold = active != null && active.HoldSeconds >= 5f ? active.HoldSeconds : 55f;
        nextChange = Time.time + hold;
        Apply(1f);
    }

    void Update()
    {
        if (Time.time >= nextChange)
            Advance();

        if (active != null && active.Rain && DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight)
        {
            if (flashUntil <= 0f && Random.value < Time.deltaTime * 0.08f)
                flashUntil = 0.12f;
        }

        if (flashUntil > 0f)
            flashUntil -= Time.deltaTime;
    }

    void LateUpdate()
    {
        FollowPlayer();
        float blend = 1f - Mathf.Exp(-1.2f * Time.deltaTime);
        Apply(blend);
    }

    void Advance()
    {
        if (presets == null || presets.Length == 0)
            return;

        index++;
        if (index >= presets.Length)
            index = 0;

        active = presets[index];
        if (active == null)
        {
            nextChange = Time.time + 55f;
            return;
        }

        int kind = active.KindId;
        Current = kind >= 0 && kind <= 3 ? (Kind)kind : Kind.Clear;
        Label = active.Label;
        float hold = active.HoldSeconds < 5f ? 5f : active.HoldSeconds;
        nextChange = Time.time + hold;
        if (!string.IsNullOrEmpty(active.Notice))
            Notice(active.Notice);
    }

    void Apply(float blend)
    {
        if (active == null)
            return;

        ApplyMarker.Begin();
        float light = active.LightScale;
        if (flashUntil > 0f)
            light = 1.4f;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, active.FogDensity, blend);
        RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, active.FogColor, blend);

        if (sun != null)
            sun.intensity = Mathf.Max(0.02f, sun.intensity * light);

        RenderSettings.ambientSkyColor *= light;
        RenderSettings.ambientEquatorColor *= light;
        RenderSettings.ambientGroundColor *= light;

        if (wind != null)
        {
            float gust = WindSystem.Current != null ? WindSystem.Current.Strength : 1f;
            wind.windMain = Mathf.Lerp(wind.windMain, active.Wind * gust, blend);
            if (WindSystem.Current != null && WindSystem.Current.Direction.sqrMagnitude > 0.0001f)
                wind.transform.rotation = Quaternion.LookRotation(WindSystem.Current.Direction, Vector3.up);
        }

        if (rain != null)
        {
            ParticleSystem.EmissionModule emission = rain.emission;
            emission.enabled = active.Rain;
            emission.rateOverTime = active.Rain ? active.RainRate : 0f;
        }

        ApplyMarker.End();
    }

    void FollowPlayer()
    {
        if (rain == null)
            return;

        Transform player = null;
        if (PlayerHealth.Instance != null)
            player = PlayerHealth.Instance.transform;
        if (player == null)
        {
            PlayerMove move = FindAnyObjectByType<PlayerMove>();
            if (move != null)
                player = move.transform;
        }

        if (player == null)
            return;

        rain.transform.position = player.position + Vector3.up * 18f;
    }

    void BuildWind()
    {
        GameObject windObject = new GameObject("ValleyWind");
        windObject.transform.SetParent(transform, false);
        wind = windObject.AddComponent<WindZone>();
        wind.mode = WindZoneMode.Directional;
        wind.radius = 0f;
        wind.windMain = 0.16f;
        wind.windTurbulence = 0.25f;
        wind.windPulseMagnitude = 0.15f;
        wind.windPulseFrequency = 0.2f;
    }

    void BuildRain()
    {
        GameObject rainObject = new GameObject("ValleyRain");
        rain = rainObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = rain.main;
        main.playOnAwake = true;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 1.15f;
        main.startSpeed = 16f;
        main.startSize = 0.045f;
        main.maxParticles = 900;
        main.startColor = new Color(0.75f, 0.82f, 0.9f, 0.45f);
        main.gravityModifier = 0.35f;

        ParticleSystem.EmissionModule emission = rain.emission;
        emission.rateOverTime = 0f;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = rain.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(42f, 1f, 42f);

        ParticleSystem.CollisionModule collision = rain.collision;
        collision.enabled = false;

        ParticleSystemRenderer renderer = rain.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Unlit");
        if (shader != null)
        {
            Material material = new Material(shader);
            Color drop = new Color(0.78f, 0.86f, 0.95f, 0.55f);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", drop);
            material.color = drop;
            renderer.material = material;
        }
    }

    static void Notice(string text)
    {
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(text);
    }
}
