using UnityEngine;

/// <summary>
/// A short walk cycle on LegFL, LegFR, LegHL and LegHR. Rabbits hop; the others trot.
/// </summary>
public class CritterGait : MonoBehaviour
{
    public float amplitude = 22f;
    public bool hop;

    Transform[] legs;
    Quaternion[] rest;
    Transform[] knees;
    Animal animal;
    Vector3 last;
    float phase;
    bool ready;

    void Start()
    {
        animal = GetComponent<Animal>();
        string[] names = { "LegFL", "LegFR", "LegHL", "LegHR" };
        legs = new Transform[4];
        rest = new Quaternion[4];
        knees = new Transform[4];
        for (int i = 0; i < names.Length; i++)
        {
            Transform leg = transform.Find(names[i]);
            legs[i] = leg;
            if (leg == null)
                continue;
            rest[i] = leg.localRotation;
            knees[i] = leg.Find("Knee");
        }

        last = transform.position;
        phase = Random.Range(0f, Mathf.PI * 2f);
        ready = true;
    }

    void LateUpdate()
    {
        if (!ready || (animal != null && animal.Dead))
            return;

        Vector3 now = transform.position;
        float moved = Vector3.Distance(new Vector3(now.x, 0f, now.z), new Vector3(last.x, 0f, last.z));
        last = now;
        float stride = Mathf.Clamp01(moved / Mathf.Max(0.0008f, Time.deltaTime) / 1.4f);
        phase += moved * (hop ? 9f : 14f);

        float lead = Mathf.Sin(phase);
        float opposite = Mathf.Sin(phase + Mathf.PI);
        Swing(0, hop ? opposite : lead, stride);
        Swing(1, hop ? opposite : opposite, stride);
        Swing(2, hop ? lead : opposite, stride);
        Swing(3, hop ? lead : lead, stride);
    }

    void Swing(int index, float wave, float stride)
    {
        Transform leg = legs[index];
        if (leg == null)
            return;

        float swing = wave * amplitude * stride;
        if (hop && index >= 2)
            swing *= 1.35f;
        leg.localRotation = rest[index] * Quaternion.Euler(swing, 0f, 0f);
        if (knees[index] != null)
            knees[index].localRotation = Quaternion.Euler(-Mathf.Abs(swing) * 0.85f, 0f, 0f);
    }
}
