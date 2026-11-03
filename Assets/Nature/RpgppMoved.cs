using UnityEngine;

/// <summary>
/// Remembers where an existing scene object was before the RPG Poly Pack scenery builder
/// moved it (camp props, the campfire, the player), so Restore Old Scenery can put it back.
/// </summary>
[DisallowMultipleComponent]
public class RpgppMoved : MonoBehaviour
{
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;

    public static void Remember(GameObject go)
    {
        if (go == null || go.GetComponent<RpgppMoved>() != null)
            return;
        RpgppMoved moved = go.AddComponent<RpgppMoved>();
        moved.position = go.transform.position;
        moved.rotation = go.transform.rotation;
    }
}
