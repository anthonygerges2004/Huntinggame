using UnityEngine;

namespace HuntingGame.Weather
{
    [CreateAssetMenu(fileName = "WeatherPreset", menuName = "Hunting Game/Weather Preset")]
    public class WeatherPreset : ScriptableObject, IWeatherPreset
    {
        public string label = "Clear";
        public int kindId;
        public float fogDensity = 0.0011f;
        public float lightScale = 1f;
        public float wind = 0.16f;
        public Color fogColor = new Color(0.62f, 0.7f, 0.76f);
        public bool rain;
        public float rainRate;
        public float holdSeconds = 55f;
        public string notice;

        public string Label => label;
        public int KindId => kindId;
        public float FogDensity => fogDensity;
        public float LightScale => lightScale;
        public float Wind => wind;
        public Color FogColor => fogColor;
        public bool Rain => rain;
        public float RainRate => rainRate;
        public float HoldSeconds => holdSeconds;
        public string Notice => notice;
    }
}
