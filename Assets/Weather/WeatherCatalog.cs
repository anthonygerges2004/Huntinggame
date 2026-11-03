using UnityEngine;

namespace HuntingGame.Weather
{
    public static class WeatherCatalog
    {
        public static WeatherPreset[] BuiltIn()
        {
            return new WeatherPreset[]
            {
                Make(0, "Clear", 0.0011f, 1f, 0.16f, new Color(0.62f, 0.7f, 0.76f), false, 0f, "The sky clears over the valley."),
                Make(1, "Overcast", 0.0026f, 0.62f, 0.35f, new Color(0.55f, 0.58f, 0.62f), false, 0f, "Clouds cover the valley."),
                Make(2, "Rain", 0.0048f, 0.42f, 0.85f, new Color(0.42f, 0.46f, 0.5f), true, 520f, "Rain moves across the land."),
                Make(3, "Fog", 0.012f, 0.7f, 0.12f, new Color(0.74f, 0.76f, 0.78f), false, 0f, "Fog settles over the woods.")
            };
        }

        static WeatherPreset Make(int kindId, string label, float density, float light, float wind, Color fog, bool rain, float rainRate, string notice)
        {
            WeatherPreset preset = ScriptableObject.CreateInstance<WeatherPreset>();
            preset.kindId = kindId;
            preset.label = label;
            preset.fogDensity = density;
            preset.lightScale = light;
            preset.wind = wind;
            preset.fogColor = fog;
            preset.rain = rain;
            preset.rainRate = rainRate;
            preset.holdSeconds = 55f;
            preset.notice = notice;
            return preset;
        }
    }
}
