using UnityEngine;

/// <summary>Player options saved between sessions. Changed from the pause menu.</summary>
public static class GameSettings
{
    const string SensKey = "HG.Sensitivity";
    const string FovKey = "HG.Fov";
    const string VolumeKey = "HG.Volume";
    const string InvertKey = "HG.InvertY";

    public const float MinSensitivity = 0.2f;
    public const float MaxSensitivity = 3f;
    public const float MinFov = 55f;
    public const float MaxFov = 95f;

    static bool loaded;
    static float sensitivity = 1f;
    static float fov = -1f;
    static float volume = 1f;
    static bool invertY;

    /// <summary>Multiplier on the base mouse look speed.</summary>
    public static float Sensitivity
    {
        get { Load(); return sensitivity; }
        set { Load(); sensitivity = Mathf.Clamp(value, MinSensitivity, MaxSensitivity); PlayerPrefs.SetFloat(SensKey, sensitivity); }
    }

    /// <summary>Field of view in degrees, or -1 to keep the camera's own.</summary>
    public static float Fov
    {
        get { Load(); return fov; }
        set { Load(); fov = Mathf.Clamp(value, MinFov, MaxFov); PlayerPrefs.SetFloat(FovKey, fov); }
    }

    public static float Volume
    {
        get { Load(); return volume; }
        set { Load(); volume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(VolumeKey, volume); AudioListener.volume = volume; }
    }

    public static bool InvertY
    {
        get { Load(); return invertY; }
        set { Load(); invertY = value; PlayerPrefs.SetInt(InvertKey, value ? 1 : 0); }
    }

    public static void Save()
    {
        PlayerPrefs.Save();
    }

    static void Load()
    {
        if (loaded)
            return;

        loaded = true;
        sensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(SensKey, 1f), MinSensitivity, MaxSensitivity);
        fov = PlayerPrefs.GetFloat(FovKey, -1f);
        volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        invertY = PlayerPrefs.GetInt(InvertKey, 0) == 1;
        AudioListener.volume = volume;
    }
}
