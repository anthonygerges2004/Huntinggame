using UnityEngine;

/// <summary>
/// Lives on the _RPGPP_Scenery root. Tells OpenWorld which RPG Poly Pack prefabs to
/// use for the wilderness props it spawns around the player far from the settlements,
/// and which areas the hand-built scenery already covers so the two don't double up.
/// Filled in by Tools > Hunting Game > Rebuild Scenery (RPG Poly Pack).
/// </summary>
public class RpgppWildSet : MonoBehaviour
{
    public Vector3 center;
    public float coverageRadius = 650f;

    /// <summary>Hand-built areas: x, z = centre, w = radius (y unused).</summary>
    public Vector4[] zones = new Vector4[0];

    public GameObject[] trees;
    public Vector2 treeHeight = new Vector2(6.5f, 11f);

    public GameObject[] bushes;
    public Vector2 bushHeight = new Vector2(1.1f, 1.9f);

    public GameObject[] rocks;
    public Vector2 rockHeight = new Vector2(0.8f, 2.6f);

    public GameObject[] grass;
    public Vector2 grassScale = new Vector2(1f, 1.6f);

    /// <summary>True if <paramref name="p"/> is inside a hand-built area, shrunk by <paramref name="margin"/>.</summary>
    public bool Covers(Vector3 p, float margin)
    {
        if (zones == null || zones.Length == 0)
        {
            Vector3 d = p - center;
            d.y = 0f;
            return d.magnitude < coverageRadius - margin;
        }

        foreach (Vector4 z in zones)
        {
            float dx = p.x - z.x;
            float dz = p.z - z.z;
            float r = z.w - margin;
            if (r > 0f && dx * dx + dz * dz < r * r)
                return true;
        }

        return false;
    }

    public static RpgppWildSet Find()
    {
        GameObject root = GameObject.Find(RpgppScenery.RootName);
        return root != null ? root.GetComponent<RpgppWildSet>() : null;
    }
}
