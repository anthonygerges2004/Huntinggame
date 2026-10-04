using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class HarvestableTree : MonoBehaviour
{
    public int woodAmount = 3;
    public int hitsToFall = 3;
    public float chopRange = 6f;

    int hits;
    bool chopped;
    static int chopFrame = -1;

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

    void Update()
    {
        if (chopped || Mouse.current == null)
            return;

        if (WoodBuilder.IsBuilding || !AxeSwing.HasAxe)
            return;

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (Time.frameCount == chopFrame)
            return;

        chopFrame = Time.frameCount;
        TryChopLookedAtTree();
    }

    static void TryChopLookedAtTree()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        if (WoodBuilder.LookedAtPiece(4f) != null)
            return;

        HarvestableTree tree = FindLookedAtTree(cam);
        if (tree == null || tree.chopped)
            return;

        if (Inventory.Instance == null || !Inventory.Instance.hasPickaxe)
        {
            Debug.Log("You need a pickaxe to chop wood.");
            return;
        }

        tree.Hit();
    }

    void Hit()
    {
        hits++;
        transform.localRotation *= Quaternion.Euler(0f, 0f, hits % 2 == 0 ? 3f : -3f);

        if (hits < hitsToFall)
        {
            Debug.Log("Chop " + hits + " of " + hitsToFall);
            return;
        }

        chopped = true;
        Inventory.Instance.AddWood(woodAmount);
        Debug.Log("Tree fell. +" + woodAmount);
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
