using UnityEngine;

/// <summary>Cosmetic finishes only. Property blocks preserve the source materials and textures.</summary>
public static class GunSkins
{
    public const int Count = 4;
    public static string Name(int skin) => skin == 1 ? "Forest" : skin == 2 ? "Desert" : skin == 3 ? "Gold" : "Original";
    public static int Price(int skin) => skin == 1 ? 30 : skin == 2 ? 45 : skin == 3 ? 90 : 0;
    public static Color Tint(int skin) => skin == 1 ? new Color(0.30f, 0.55f, 0.26f)
        : skin == 2 ? new Color(0.85f, 0.67f, 0.40f) : new Color(1f, 0.77f, 0.25f);
}

