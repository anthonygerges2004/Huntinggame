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
            Debug.Log("Hit " + hits + " of " + hitsToBreak);
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
        if (Inventory.Instance != null)
            Inventory.Instance.AddWood(refund);
        Debug.Log("Broke " + name + ". +" + refund + " wood");
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
