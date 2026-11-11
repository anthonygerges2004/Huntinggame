using UnityEngine;

/// <summary>
/// Feet stay tucked in flight and drop when a bird is perched or walking.
/// A walking bird also steps.
/// </summary>
public class BirdFeet : MonoBehaviour
{
    Transform left;
    Transform right;
    Vector3 leftHome;
    Vector3 rightHome;
    BirdFlight flight;
    Animal animal;
    Vector3 last;
    float phase;

    void Start()
    {
        left = transform.Find("FootLeft");
        right = transform.Find("FootRight");
        if (left != null)
            leftHome = left.localPosition;
        if (right != null)
            rightHome = right.localPosition;
        flight = GetComponent<BirdFlight>();
        animal = GetComponent<Animal>();
        last = transform.position;
    }

    void LateUpdate()
    {
        if (left == null && right == null)
            return;
        if (animal != null && animal.Dead)
            return;

        bool down = flight == null || flight.Landed;
        Vector3 now = transform.position;
        float moved = Vector3.Distance(new Vector3(now.x, 0f, now.z), new Vector3(last.x, 0f, last.z));
        last = now;
        float stride = down ? Mathf.Clamp01(moved / Mathf.Max(0.0008f, Time.deltaTime) / 1.1f) : 0f;
        phase += moved * 18f;

        Place(left, leftHome, down, Mathf.Sin(phase) * 22f * stride);
        Place(right, rightHome, down, Mathf.Sin(phase + Mathf.PI) * 22f * stride);
    }

    static void Place(Transform foot, Vector3 home, bool down, float swing)
    {
        if (foot == null)
            return;
        foot.localPosition = down ? home : home + new Vector3(0f, 0.07f, -0.02f);
        foot.localRotation = Quaternion.Euler(down ? swing : 40f, 0f, 0f);
        foot.localScale = Vector3.one * (down ? 1f : 0.65f);
    }
}
