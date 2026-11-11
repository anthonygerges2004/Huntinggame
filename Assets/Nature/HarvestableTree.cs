using System.Collections;
using UnityEngine;

public class HarvestableTree : MonoBehaviour
{
    public int woodAmount = 3;
    public int hitsToFall = 3;
    public float chopRange = 2.2f;

    int hits;
    bool chopped;
    Quaternion restingRotation;
    Coroutine recoil;

    void Start()
    {
        if (name.IndexOf("Legendary", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
            GetComponent<Animal>() == null)
        {
            gameObject.SetActive(false);
            return;
        }

        chopRange = Mathf.Min(chopRange, 2.2f);
        restingRotation = transform.localRotation;
    }

    public static HarvestableTree AimedTree()
    {
        return FindLookedAtTree();
    }

    public bool InChopRange()
    {
        if (chopped)
            return false;

        Vector3 point = transform.position + Vector3.up * 1.5f;
        return Vector3.Distance(PlayerView.AimOrigin(), point) <= Mathf.Min(chopRange, 2.2f);
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

        if (Inventory.Instance == null || !AxeSwing.HasAxe || !Inventory.AxeInHand)
        {
            if (Inventory.Instance != null)
                Inventory.Instance.ShowNotice("Equip your axe to chop.");
            return;
        }

        hits++;
        if (recoil != null) StopCoroutine(recoil);
        transform.localRotation = restingRotation;
        recoil = StartCoroutine(ImpactRecoil());

        if (hits < hitsToFall)
        {
            Inventory.Instance.ShowNotice("Chop " + hits + " / " + hitsToFall);
            return;
        }

        chopped = true;
        if (recoil != null) StopCoroutine(recoil);
        transform.localRotation = restingRotation;
        Inventory.Instance.AddWood(woodAmount);
        Inventory.Instance.ShowNotice("Tree down. +" + woodAmount + " wood");
        StartCoroutine(FallOver());
    }

    IEnumerator ImpactRecoil()
    {
        for (float elapsed = 0f; elapsed < 0.3f; elapsed += Time.deltaTime)
        {
            float t = elapsed / 0.3f;
            float bend = Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * 1.2f;
            transform.localRotation = restingRotation * Quaternion.Euler(bend, 0f, bend * 0.4f);
            yield return null;
        }
        transform.localRotation = restingRotation;
        recoil = null;
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

    static HarvestableTree FindLookedAtTree()
    {
        // Use the first solid surface at the crosshair: walls and nearby props block a chop.
        Ray ray = PlayerView.AimRay();
        Transform player = PlayerView.Body();
        RaycastHit[] contacts = Physics.RaycastAll(ray, 2.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        HarvestableTree best = null;
        foreach (RaycastHit contact in contacts)
        {
            if (player != null && (contact.transform == player || contact.transform.IsChildOf(player))) continue;
            if (contact.distance >= nearest) continue;
            nearest = contact.distance;
            best = contact.collider.GetComponentInParent<HarvestableTree>();
        }
        return best != null && !best.chopped ? best : null;
    }
}