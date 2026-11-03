using UnityEngine;

/// <summary>
/// Shared lookup for the RPG Poly Pack Lite scenery built by
/// Tools > Hunting Game > Rebuild Scenery (RPG Poly Pack).
/// The older foliage generators check <see cref="Present"/> so they don't
/// regenerate the previous forest on top of the new scenery.
/// </summary>
public static class RpgppScenery
{
    public const string RootName = "_RPGPP_Scenery";

    public static bool Present
    {
        get { return GameObject.Find(RootName) != null; }
    }
}
