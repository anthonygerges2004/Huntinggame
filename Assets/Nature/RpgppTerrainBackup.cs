using UnityEngine;

/// <summary>
/// Remembers a terrain's original state before the RPG Poly Pack scenery builder
/// repainted and levelled it, so Restore Old Scenery can put it back.
/// </summary>
[DisallowMultipleComponent]
public class RpgppTerrainBackup : MonoBehaviour
{
    /// <summary>The texture layers the terrain had before it was first stylised.</summary>
    public TerrainLayer[] originalLayers;

    /// <summary>A copy of the TerrainData (heights and paint) taken before any levelling or repainting.</summary>
    public TerrainData backupData;
}
