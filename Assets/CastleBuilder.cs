using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Builds a complete castle (4 towers, walls, gate, hall buildings) out of the
/// pieces in the imported "Castles_and_Forts" model.
///
/// How to use:
///   1. Put this script on an empty GameObject (for example one named "Castle").
///   2. Drag "Castles_and_Forts" (from the Project window) into the "Kit" slot.
///   3. Right-click the component title in the Inspector and choose "Build Castle".
///   Move that empty object to place the whole castle. Run "Build Castle" again to rebuild.
/// </summary>
public class CastleBuilder : MonoBehaviour
{
    [Header("Drag the imported Castles_and_Forts model here")]
    public GameObject kit;

    [Header("Settings")]
    [Tooltip("1 = original size (walls 2 m high). 2 is good for a normal-sized player.")]
    public float scale = 2f;
    public bool addColliders = true;
    [Tooltip("Drop the castle onto the terrain below it when building.")]
    public bool snapToGround = true;
    [Tooltip("Negative = sink the castle slightly into the ground.")]
    public float groundOffset = -0.3f;

    struct Item
    {
        public string name; public float x, z, yaw, y;
        public Item(string name, float x, float z, float yaw, float y)
        { this.name = name; this.x = x; this.z = z; this.yaw = yaw; this.y = y; }
    }

    // name, x, z, yaw, y  (in "piece units": 4 units = one wall piece)
    static readonly Item[] items = new Item[]
    {
        new Item("Tower_Mid", -11f, -11f, 0f, 0f),
        new Item("Tower_Mid", -11f, -11f, 0f, 2f),
        new Item("Tower_top_1", -11f, -11f, 0f, 4f),
        new Item("Tower_Mid", 11f, -11f, 0f, 0f),
        new Item("Tower_mid_hollow_ruined", 11f, -11f, 0f, 2f),
        new Item("Tower_Mid", -11f, 3f, 0f, 0f),
        new Item("Tower_Mid", -11f, 3f, 0f, 2f),
        new Item("Tower_top_1", -11f, 3f, 0f, 4f),
        new Item("Tower_Mid", 11f, 3f, 0f, 0f),
        new Item("Tower_Mid", 11f, 3f, 0f, 2f),
        new Item("Roof_Cone", 11f, 3f, 0f, 4f),
        new Item("Tower_Mid", -11f, 17f, 0f, 0f),
        new Item("Tower_Mid", -11f, 17f, 0f, 2f),
        new Item("Tower_Mid", -11f, 17f, 0f, 4f),
        new Item("Roof_Cone", -11f, 17f, 0f, 6f),
        new Item("Tower_Mid", 11f, 17f, 0f, 0f),
        new Item("Tower_Mid", 11f, 17f, 0f, 2f),
        new Item("Roof_Cone", 11f, 17f, 0f, 4f),
        new Item("Tower_Mid", -1f, 14.6f, 0f, 0f),
        new Item("Tower_Mid", -1f, 14.6f, 0f, 2f),
        new Item("Roof_Cone", -1f, 14.6f, 0f, 4f),
        new Item("Tower_Mid", 2f, 15f, 0f, 0f),
        new Item("Tower_Mid", 2f, 15f, 0f, 2f),
        new Item("Tower_Mid", 2f, 15f, 0f, 4f),
        new Item("Tower_Mid", 2f, 15f, 0f, 6f),
        new Item("Tower_mid_hollow_ruined", 2f, 15f, 0f, 8f),
        new Item("Gate_2x4", -8f, -11f, 90f, 0f),
        new Item("Wall_2x4_walkway", -4f, -11f, 90f, 0f),
        new Item("Wall_2x4_walkway", 0f, -11f, 90f, 0f),
        new Item("Wall_2x4_ruined", 4f, -11f, 90f, 0f),
        new Item("Wall_2x4_ruined", 8f, -11f, 90f, 0f),
        new Item("Wall_2x4_walkway", -8f, 17f, -90f, 0f),
        new Item("Wall_2x4_walkway", -4f, 17f, -90f, 0f),
        new Item("Wall_2x4_walkway", 0f, 17f, -90f, 0f),
        new Item("Wall_2x4_walkway", 4f, 17f, -90f, 0f),
        new Item("Wall_2x4_walkway", 8f, 17f, -90f, 0f),
        new Item("Wall_2x4_walkway", 11f, -8f, 0f, 0f),
        new Item("Wall_2x4_ruined", 11f, -4f, 0f, 0f),
        new Item("Wall_2x4_ruined", 11f, 0f, 0f, 0f),
        new Item("Wall_2x4_walkway", 11f, 6f, 0f, 0f),
        new Item("Wall_2x4_walkway", 11f, 10f, 0f, 0f),
        new Item("Wall_2x4_walkway", 11f, 14f, 0f, 0f),
        new Item("Wall_2x4_walkway", -11f, -8f, 180f, 0f),
        new Item("Wall_2x4_walkway", -11f, -4f, 180f, 0f),
        new Item("Wall_2x4_walkway", -11f, 0f, 180f, 0f),
        new Item("Wall_2x4_walkway", -11f, 6f, 180f, 0f),
        new Item("Wall_2x4_walkway", -11f, 10f, 180f, 0f),
        new Item("Wall_2x4_walkway", -11f, 14f, 180f, 0f),
        new Item("Wall_2x4_walkway", -8f, 3f, 90f, 0f),
        new Item("Wall_2x4_walkway", -4f, 3f, 90f, 0f),
        new Item("Gate_2x4_doorway", 0f, 3f, 90f, 0f),
        new Item("Wall_2x4_walkway", 4f, 3f, 90f, 0f),
        new Item("Wall_2x4_walkway", 8f, 3f, 90f, 0f),
        new Item("Building_Shape_Beveled_rectangle", -8f, 14.5f, 90f, 0f),
        new Item("Building_Shape_Beveled_rectangle", -8f, 14.5f, 90f, 2f),
        new Item("Roof_rectangle", -8f, 14.5f, 90f, 4f),
        new Item("Building_Shape_Beveled_rectangle", -4f, 14.5f, 90f, 0f),
        new Item("Building_Shape_Beveled_rectangle", -4f, 14.5f, 90f, 2f),
        new Item("Roof_rectangle", -4f, 14.5f, 90f, 4f),
        new Item("Building_Shape_Beveled_cube", -7f, 6.5f, 0f, 0f),
        new Item("Roof_Cube", -7f, 6.5f, 0f, 2f),
        new Item("Building_Shape_Beveled_cube", -3.5f, 9.5f, 0f, 0f),
        new Item("Roof_Cube", -3.5f, 9.5f, 0f, 2f),
        new Item("Building_Shape_Beveled_cube", 4f, 6.5f, 0f, 0f),
        new Item("Roof_Cube", 4f, 6.5f, 0f, 2f),
        new Item("Building_Shape_Beveled_cube", 7.5f, 9.5f, 0f, 0f),
        new Item("Roof_Cube", 7.5f, 9.5f, 0f, 2f),
        new Item("Building_Shape_Beveled_cube", 5f, 12.5f, 0f, 0f),
        new Item("Roof_Cube", 5f, 12.5f, 0f, 2f),
        new Item("Building_Shape_Beveled_rectangle", -7.5f, 10.5f, 0f, 0f),
        new Item("Roof_rectangle", -7.5f, 10.5f, 0f, 2f),
        new Item("Scaffle", 5f, 4.9f, 90f, 0f),
        new Item("Scaffle_Ramp", 2.4f, 4.9f, 90f, 0f)
    };

    [ContextMenu("Build Castle")]
    public void Build()
    {
        if (kit == null) { Debug.LogError("CastleBuilder: drag Castles_and_Forts into the Kit slot first."); return; }

        // missing pieces?
        var missing = new HashSet<string>();
        foreach (var it in items) if (FindDeep(kit.transform, it.name) == null) missing.Add(it.name);
        if (missing.Count > 0)
        {
            Debug.LogError("CastleBuilder: could not find these pieces inside the kit: " + string.Join(", ", missing));
            return;
        }

        // remove an older build
        for (int i = transform.childCount - 1; i >= 0; i--) DestroyImmediate(transform.GetChild(i).gameObject);

        // put the castle on the terrain
        if (snapToGround)
        {
            if (Physics.Raycast(transform.position + Vector3.up * 1000f, Vector3.down, out RaycastHit hit, 3000f))
                transform.position = new Vector3(transform.position.x, hit.point.y + groundOffset, transform.position.z);
            else
                Debug.LogWarning("CastleBuilder: no ground found below, so the castle was not moved down. Set its Y position by hand.");
        }

        // work out the size of one piece and which way the pieces face
        Calibrate(out float unit, out float baseYaw);

        foreach (var it in items)
        {
            Transform src = FindDeep(kit.transform, it.name);

            var holder = new GameObject(it.name);
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = new Vector3(it.x, it.y, it.z) * unit * scale;
            holder.transform.localRotation = Quaternion.Euler(0f, it.yaw + baseYaw, 0f);
            holder.transform.localScale = Vector3.one * scale;

            GameObject copy = Instantiate(src.gameObject);
            copy.name = it.name + "_mesh";
            copy.transform.SetParent(holder.transform, false);
            copy.transform.localPosition = Vector3.zero;   // keeps the piece's own rotation and size

            if (addColliders)
            {
                foreach (var mf in copy.GetComponentsInChildren<MeshFilter>())
                    if (mf.GetComponent<Collider>() == null) mf.gameObject.AddComponent<MeshCollider>();
            }
        }

        Debug.Log("CastleBuilder: castle built with " + items.Length + " pieces.");
    }

    // Measures a wall piece so the layout still fits however Unity imported the model.
    void Calibrate(out float unit, out float baseYaw)
    {
        unit = 1f; baseYaw = 0f;

        var holder = new GameObject("calibration");
        holder.transform.SetParent(transform, false);
        GameObject copy = Instantiate(FindDeep(kit.transform, "Wall_2x4_walkway").gameObject);
        copy.transform.SetParent(holder.transform, false);
        copy.transform.localPosition = Vector3.zero;

        float best = -1000f;
        for (int k = 0; k < 4; k++)
        {
            holder.transform.localRotation = Quaternion.Euler(0f, 90f * k, 0f);
            Bounds b = GetBounds(copy);
            Vector3 o = holder.transform.position;
            bool lengthAlongZ = b.size.z > b.size.x;
            // the low walkway side must point inwards (towards -x) so the battlements face out
            float inward = -(b.center.x - o.x);
            float score = (lengthAlongZ ? 100f : 0f) + inward;
            if (score > best) { best = score; baseYaw = 90f * k; }
        }
        holder.transform.localRotation = Quaternion.identity;
        Bounds nb = GetBounds(copy);
        unit = Mathf.Max(nb.size.x, nb.size.z) / 4f;   // a wall piece is 4 units long
        DestroyImmediate(holder);
    }

    static Bounds GetBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var r = FindDeep(c, name);
            if (r != null) return r;
        }
        return null;
    }
}