using System.Collections;
using UnityEngine;

public class BuildPiece : MonoBehaviour
{
    public int refund = 2;
    public int hitsToBreak = 3;

    int hits;
    bool broken;

    public void Hit()
    {
        if (broken)
            return;

        hits++;
        if (hits < hitsToBreak)
        {
            if (Inventory.Instance != null)
                Inventory.Instance.ShowNotice("Wood " + hits + " / " + hitsToBreak);
            StartCoroutine(Shake());
            return;
        }

        Break();
    }

    public void Break()
    {
        if (broken)
            return;

        broken = true;
        foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        if (Inventory.Instance != null)
        {
            int added = Inventory.Instance.CollectItem("Wood", refund);
            if (added < refund) InventoryWorldDrop.Spawn(transform.position, "Wood", refund - added);
            Inventory.Instance.ShowNotice("Wood broken. +" + added + " wood" + (added < refund ? "; remaining wood dropped." : ""));
        }
        else InventoryWorldDrop.Spawn(transform.position, "Wood", refund);
        Destroy(gameObject);
    }

    IEnumerator Shake()
    {
        Vector3 start = transform.position;
        for (float t = 0f; t < 0.15f; t += Time.deltaTime)
        {
            transform.position = start + Random.insideUnitSphere * 0.04f;
            yield return null;
        }
        transform.position = start;
    }
}
