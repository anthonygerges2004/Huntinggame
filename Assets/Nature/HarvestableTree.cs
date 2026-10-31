using System.Collections;
using UnityEngine;

public class HarvestableTree : MonoBehaviour
{
    public int woodAmount = 3;
    public int hitsToFall = 3;
    public float chopRange = 6f;

    int hits;
    bool chopped;

    void Start()
    {
        if (name.IndexOf("Legendary", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
            GetComponent<Animal>() == null)
        {
            gameObject.SetActive(false);
            return;
        }

        if (chopRange < 6f)
            chopRange = 6f;
    }

    public static bool IsLookingAtTree()
    {
        Camera cam = Camera.main;
        return cam != null && FindLookedAtTree(cam) != null;
    }

    public static HarvestableTree AimedTree()
    {
        Camera cam = Camera.main;
        return cam == null ? null : FindLookedAtTree(cam);
    }

    public bool InChopRange()
    {
        Camera cam = Camera.main;
        if (cam == null || chopped)
            return false;

        Vector3 point = transform.position + Vector3.up * 1.5f;
        return Vector3.Distance(cam.transform.position, point) <= chopRange + 1.5f;
    }

    public static void ChopLookedAtTree()
    {
        HarvestableTree tree = AimedTree();
        if (tree != null)
            tree.Chop();
    }

    public void Chop()
    {
        if (chopped)
            return;

        if (Inventory.Instance == null || !Inventory.Instance.hasPickaxe)
        {
            if (Inventory.Instance != null)
                Inventory.Instance.ShowNotice("Buy a pickaxe at the trader, then chop.");
            return;
        }

        hits++;
        transform.localRotation *= Quaternion.Euler(0f, 0f, hits % 2 == 0 ? 8f : -8f);

        if (hits < hitsToFall)
        {
            Inventory.Instance.ShowNotice("Chop " + hits + " / " + hitsToFall);
            return;
        }

        chopped = true;
        Inventory.Instance.AddWood(woodAmount);
        Inventory.Instance.ShowNotice("Tree down. +" + woodAmount + " wood");
        StartCoroutine(FallOver());
    }

    IEnumerator FallOver()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Quaternion start = transform.rotation;
        Quaternion end = start * Quaternion.Euler(-80f, 0f, 0f);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.7f;
            transform.rotation = Quaternion.Slerp(start, end, t);
            yield return null;
        }

        Destroy(gameObject);
    }

    static HarvestableTree FindLookedAtTree(Camera cam)
    {
        HarvestableTree[] trees = Object.FindObjectsByType<HarvestableTree>(FindObjectsInactive.Exclude);
        HarvestableTree best = null;
        float bestDistance = 999f;
        Vector3 origin = cam.transform.position;
        Vector3 forward = cam.transform.forward;

        for (int i = 0; i < trees.Length; i++)
        {
            HarvestableTree tree = trees[i];
            if (tree == null || tree.chopped)
                continue;

            Vector3 point = tree.transform.position + Vector3.up * 1.5f;
            Vector3 toTree = point - origin;
            float distance = toTree.magnitude;
            if (distance > tree.chopRange || distance < 0.2f)
                continue;

            if (Vector3.Dot(forward, toTree.normalized) < 0.65f)
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = tree;
            }
        }

        return best;
    }
}
