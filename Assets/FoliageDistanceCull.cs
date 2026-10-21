using UnityEngine;

/// <summary>Turns off distant chop trees so LOD and frustum culling actually save work.</summary>
public class FoliageDistanceCull : MonoBehaviour
{
    public float cullDistance = 220f;
    public float hysteresis = 35f;
    public float updateInterval = 0.35f;

    Transform player;
    float nextUpdate;
    Entry[] entries;

    struct Entry
    {
        public Transform root;
        public bool culled;
    }

    void Update()
    {
        if (entries == null || entries.Length == 0)
        {
            TryBuildEntries();
            if (entries == null || entries.Length == 0)
                return;
        }

        if (Time.time < nextUpdate)
            return;

        nextUpdate = Time.time + updateInterval;

        if (player == null)
        {
            PlayerMove move = Object.FindAnyObjectByType<PlayerMove>();
            if (move == null)
                return;
            player = move.transform;
        }

        Vector3 origin = player.position;
        float cullSq = cullDistance * cullDistance;
        float restoreSq = (cullDistance + hysteresis) * (cullDistance + hysteresis);

        for (int i = 0; i < entries.Length; i++)
        {
            Transform root = entries[i].root;
            if (root == null)
                continue;

            Vector3 delta = root.position - origin;
            delta.y = 0f;
            float distSq = delta.sqrMagnitude;

            if (!entries[i].culled && distSq > restoreSq)
            {
                SetVisible(root, false);
                entries[i].culled = true;
            }
            else if (entries[i].culled && distSq < cullSq)
            {
                SetVisible(root, true);
                entries[i].culled = false;
            }
        }
    }

    static void SetVisible(Transform root, bool visible)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = visible;
        }
    }

    void TryBuildEntries()
    {
        HarvestableTree[] trees = Object.FindObjectsByType<HarvestableTree>(FindObjectsInactive.Exclude);
        if (trees.Length == 0)
            return;

        entries = new Entry[trees.Length];
        for (int i = 0; i < trees.Length; i++)
        {
            if (trees[i] == null)
                continue;

            Transform root = trees[i].transform;
            LODGroup lod = root.GetComponent<LODGroup>();
            if (lod != null)
                root = lod.transform;

            entries[i] = new Entry { root = root, culled = false };
        }
    }
}
