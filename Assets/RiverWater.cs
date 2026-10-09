using UnityEngine;

public class RiverWater : MonoBehaviour
{
    // World positions along the middle of the river, from its beginning to its end; y is the water surface.
    public Vector3[] points;
    public float halfWidth = 10f;
    public float flowSpeed = 0.8f;

    Bounds bounds;
    bool hasBounds;

    void OnEnable()
    {
        WaterBodies.Rivers.Add(this);
        hasBounds = false;
    }

    void OnDisable()
    {
        WaterBodies.Rivers.Remove(this);
    }

    public bool Sample(Vector3 position, float margin, out float surfaceY, out Vector3 current)
    {
        surfaceY = 0f;
        current = Vector3.zero;
        if (points == null || points.Length < 2)
            return false;

        if (!hasBounds)
        {
            bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 p in points)
                bounds.Encapsulate(p);
            bounds.Expand(new Vector3(halfWidth * 2f, 10000f, halfWidth * 2f));
            hasBounds = true;
        }

        if (!bounds.Contains(new Vector3(position.x, bounds.center.y, position.z)))
            return false;

        float best = float.MaxValue;
        int bestIndex = 0;
        float bestT = 0f;
        Vector2 p2 = new Vector2(position.x, position.z);

        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 a = new Vector2(points[i].x, points[i].z);
            Vector2 b = new Vector2(points[i + 1].x, points[i + 1].z);
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p2 - a, ab) / ab.sqrMagnitude) : 0f;
            float d = (a + ab * t - p2).sqrMagnitude;
            if (d < best)
            {
                best = d;
                bestIndex = i;
                bestT = t;
            }
        }

        if (Mathf.Sqrt(best) > halfWidth - margin)
            return false;

        Vector3 from = points[bestIndex];
        Vector3 to = points[bestIndex + 1];
        surfaceY = Mathf.Lerp(from.y, to.y, bestT);
        Vector3 flow = to - from;
        flow.y = 0f;
        current = flow.normalized * flowSpeed;
        return true;
    }
}
