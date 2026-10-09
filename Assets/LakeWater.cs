using System.Collections.Generic;
using UnityEngine;

public class LakeWater : MonoBehaviour
{
    public int fishCount = 40;
    public float respawnSeconds = 20f;

    readonly List<LakeFish> fish = new List<LakeFish>();

    public float SurfaceY => transform.position.y + transform.lossyScale.y;
    public float Radius => transform.lossyScale.x * 0.5f;
    public Vector3 Center => transform.position;

    void OnEnable()
    {
        WaterBodies.Lakes.Add(this);
    }

    void OnDisable()
    {
        WaterBodies.Lakes.Remove(this);
    }

    void Start()
    {
        int count = Mathf.Clamp(Mathf.RoundToInt(Radius * 0.35f), 6, fishCount);
        for (int i = 0; i < count; i++)
            SpawnFish();
    }

    public bool Inside(Vector3 position, float margin)
    {
        Vector3 offset = position - Center;
        offset.y = 0f;
        return offset.magnitude < Radius - margin;
    }

    public float GroundY(Vector3 position)
    {
        float ground = WaterBodies.GroundY(position);
        return ground == float.MinValue ? SurfaceY - 10f : ground;
    }

    public bool RandomUnderwaterPoint(out Vector3 point)
    {
        for (int i = 0; i < 25; i++)
        {
            Vector2 r = Random.insideUnitCircle * Radius * 0.85f;
            Vector3 p = Center + new Vector3(r.x, 0f, r.y);
            float ground = GroundY(p);
            if (ground > SurfaceY - 1f)
                continue;

            float low = Mathf.Max(ground + 0.4f, SurfaceY - 1.8f);
            p.y = Random.Range(low, SurfaceY - 0.35f);
            point = p;
            return true;
        }

        point = new Vector3(Center.x, SurfaceY - 0.6f, Center.z);
        return false;
    }

    public LakeFish ClosestFreeFish(Vector3 point)
    {
        LakeFish best = null;
        float bestDistance = float.MaxValue;
        foreach (LakeFish f in fish)
        {
            if (f == null || f.Busy)
                continue;
            float d = (f.transform.position - point).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = f;
            }
        }
        return best;
    }

    public void Catch(LakeFish caught)
    {
        if (caught == null)
            return;

        fish.Remove(caught);
        Destroy(caught.gameObject);
        Invoke(nameof(SpawnFish), respawnSeconds);
    }

    void SpawnFish()
    {
        if (!RandomUnderwaterPoint(out Vector3 point))
            return;
        fish.Add(LakeFish.Spawn(this, point));
    }
}
