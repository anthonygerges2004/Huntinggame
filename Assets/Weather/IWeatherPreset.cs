using UnityEngine;

namespace HuntingGame.Weather
{
    public interface IWeatherPreset
    {
        string Label { get; }
        int KindId { get; }
        float FogDensity { get; }
        float LightScale { get; }
        float Wind { get; }
        Color FogColor { get; }
        bool Rain { get; }
        float RainRate { get; }
        float HoldSeconds { get; }
        string Notice { get; }
    }
}
