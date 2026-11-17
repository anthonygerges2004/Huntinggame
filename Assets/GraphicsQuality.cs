using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Graphics presets for the pause menu: Very Low, Low, Medium, High, Very High.
/// One choice sets render resolution, anti-aliasing, shadows, tree/grass/terrain detail,
/// texture detail, view distance and the heavier camera effects. Saved between sessions and
/// applied instantly. The URP asset is only changed while playing and restored on exit, so
/// project settings on disk never change.
/// </summary>
public static class GraphicsQuality
{
    public static readonly string[] Names = { "VERY LOW", "LOW", "MEDIUM", "HIGH", "VERY HIGH" };
    public const int Count = 5;
    const string Key = "HG.Graphics.v2"; // new key: everyone starts on the default once
    const int DefaultLevel = 1; // LOW: the best frame rate

    //                                               VLow   Low    Med    High   VHigh
    static readonly float[] RenderScale =          { 0.6f,  0.75f, 0.9f,  1f,    1f };
    static readonly int[] Msaa =                   { 1,     1,     2,     4,     4 };
    static readonly float[] ShadowDistance =       { 25f,   45f,   80f,   150f,  200f };
    static readonly int[] ShadowCascades =         { 1,     2,     2,     4,     4 };
    static readonly int[] ShadowResolution =       { 512,   1024,  2048,  4096,  4096 };
    static readonly float[] TreeDistanceT =        { 450f,  650f,  900f,  1500f, 1800f };
    static readonly float[] TreeBillboardT =       { 50f,   90f,   150f,  220f,  400f };
    static readonly int[] TreeFullLod =            { 25,    50,    90,    150,   300 };
    static readonly float[] DetailDistanceT =      { 35f,   55f,   80f,   110f,  150f };
    static readonly float[] DetailDensityT =       { 0.3f,  0.45f, 0.65f, 0.8f,  1f };
    static readonly float[] PixelErrorT =          { 8f,    6f,    4f,    2f,    1.5f };
    static readonly float[] FarClipT =             { 600f,  900f,  1300f, 2000f, 2500f };
    static readonly float[] LodBias =              { 0.5f,  0.7f,  1f,    1.5f,  2f };
    static readonly int[] TextureMipLimit =        { 2,     1,     0,     0,     0 };

    static int level = -1;

    public static int Level
    {
        get
        {
            if (level < 0)
                level = Mathf.Clamp(PlayerPrefs.GetInt(Key, DefaultLevel), 0, Count - 1);
            return level;
        }
        set
        {
            level = Mathf.Clamp(value, 0, Count - 1);
            PlayerPrefs.SetInt(Key, level);
            PlayerPrefs.Save();
            Apply();
        }
    }

    // Values the rest of the game reads (terrain and camera culling).
    public static float TreeDistance => TreeDistanceT[Level];
    public static float TreeBillboard => TreeBillboardT[Level];
    public static int TreeMaximumFullLod => TreeFullLod[Level];
    public static float DetailDistance => DetailDistanceT[Level];
    public static float DetailDensity => DetailDensityT[Level];
    public static float PixelError => PixelErrorT[Level];
    public static float FarClip => FarClipT[Level];
    /// <summary>Bloom, grain and colour fringing.</summary>
    public static bool RichPost => Level >= 2;
    /// <summary>Depth-of-field haze.</summary>
    public static bool DepthOfField => Level >= 3;

    // Original URP asset values, restored when play stops.
    static UniversalRenderPipelineAsset urp;
    static bool saved;
    static float origScale, origShadowDistance;
    static int origMsaa, origCascades, origShadowRes;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        level = -1; // re-read (domain reload may be off)
        Apply();
    }

    public static void Apply()
    {
        int l = Level;

        urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp != null)
        {
            if (!saved)
            {
                origScale = urp.renderScale;
                origMsaa = urp.msaaSampleCount;
                origShadowDistance = urp.shadowDistance;
                origCascades = urp.shadowCascadeCount;
                origShadowRes = urp.mainLightShadowmapResolution;
                saved = true;
                Application.quitting += Restore;
            }
            urp.renderScale = RenderScale[l];
            urp.msaaSampleCount = Msaa[l];
            urp.shadowDistance = ShadowDistance[l];
            urp.shadowCascadeCount = ShadowCascades[l];
            urp.mainLightShadowmapResolution = ShadowResolution[l];
        }

        QualitySettings.lodBias = LodBias[l];
        QualitySettings.globalTextureMipmapLimit = TextureMipLimit[l];
        QualitySettings.anisotropicFiltering = l >= 3 ? AnisotropicFiltering.ForceEnable
            : l >= 1 ? AnisotropicFiltering.Enable : AnisotropicFiltering.Disable;

        GameCulling.ApplyForActiveScene();
    }

    static void Restore()
    {
        Application.quitting -= Restore;
        if (urp == null || !saved)
            return;
        urp.renderScale = origScale;
        urp.msaaSampleCount = origMsaa;
        urp.shadowDistance = origShadowDistance;
        urp.shadowCascadeCount = origCascades;
        urp.mainLightShadowmapResolution = origShadowRes;
        QualitySettings.globalTextureMipmapLimit = 0;
        saved = false;
    }
}
