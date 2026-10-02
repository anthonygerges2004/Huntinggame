using UnityEngine;
using UnityEngine.InputSystem;

public class HarvestableTree : MonoBehaviour
{
    public int woodAmount = 3;
    public float chopRange = 6f;

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
        if (chopped || Keyboard.current == null)
            return;

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (!Keyboard.current.eKey.wasPressedThisFrame)
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

        HarvestableTree tree = FindLookedAtTree(cam);
        if (tree == null || tree.chopped)
            return;

        if (Inventory.Instance == null || !Inventory.Instance.hasPickaxe)
        {
            Debug.Log("You need a pickaxe to chop wood.");
            return;
        }

        Inventory.Instance.AddWood(tree.woodAmount);
        tree.chopped = true;
        Destroy(tree.gameObject);
        Debug.Log("Chopped wood. +" + tree.woodAmount);
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
