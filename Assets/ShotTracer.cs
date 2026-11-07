using UnityEngine;

/// <summary>
/// A short bright streak that runs from the gun barrel to the point the shot hit.
/// </summary>
public class ShotTracer : MonoBehaviour
{
    Vector3 from;
    Vector3 to;
    float age;
    Transform streak;
    Transform tip;
    Light flash;
    static Material shared;

    public static void Play(Vector3 barrel, Vector3 hit)
    {
        if ((hit - barrel).sqrMagnitude < 0.25f)
            return;

        GameObject root = new GameObject("ShotTracer");
        int ignore = LayerMask.NameToLayer("Ignore Raycast");
        if (ignore >= 0)
            root.layer = ignore;

        ShotTracer tracer = root.AddComponent<ShotTracer>();
        tracer.from = barrel;
        tracer.to = hit;
        tracer.Build();
    }

    void Build()
    {
        streak = MakeBox("Streak");
        tip = MakeBox("Tip");
        flash = new GameObject("TipLight").AddComponent<Light>();
        flash.transform.SetParent(transform, false);
        flash.type = LightType.Point;
        flash.color = new Color(1f, 0.92f, 0.65f);
        flash.intensity = 12f;
        flash.range = 6f;
        Place(0f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float travel = 0.16f;
        Place(Mathf.Clamp01(age / travel));
        if (flash != null)
            flash.intensity = Mathf.Lerp(12f, 0f, Mathf.Clamp01((age - travel) / 0.18f));
        if (age > travel + 0.22f)
            Destroy(gameObject);
    }

    void Place(float along)
    {
        float tailAlong = Mathf.Max(0f, along - 0.07f);
        Vector3 head = Vector3.Lerp(from, to, along);
        Vector3 tail = Vector3.Lerp(from, to, tailAlong);
        Vector3 span = head - tail;
        float length = span.magnitude;
        if (length < 0.05f)
            length = 0.05f;

        streak.position = (head + tail) * 0.5f;
        streak.rotation = Quaternion.LookRotation(span.sqrMagnitude > 0.0001f ? span : (to - from));
        streak.localScale = new Vector3(0.035f, 0.035f, length);

        tip.position = head;
        tip.rotation = streak.rotation;
        tip.localScale = Vector3.one * 0.09f;
        if (flash != null)
            flash.transform.position = head;
    }

    Transform MakeBox(string boxName)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Collider solid = box.GetComponent<Collider>();
        if (solid != null)
            Destroy(solid);

        box.name = boxName;
        int ignore = LayerMask.NameToLayer("Ignore Raycast");
        if (ignore >= 0)
            box.layer = ignore;

        box.transform.SetParent(transform, false);
        Renderer renderer = box.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = StreakMaterial();
        return box.transform;
    }

    static Material StreakMaterial()
    {
        if (shared != null)
            return shared;

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shared = new Material(temp.GetComponent<Renderer>().sharedMaterial);
        Destroy(temp);

        Color hot = new Color(1f, 0.86f, 0.45f, 1f);
        shared.color = hot;
        if (shared.HasProperty("_BaseColor"))
            shared.SetColor("_BaseColor", hot);
        if (shared.HasProperty("_Color"))
            shared.SetColor("_Color", hot);
        if (shared.HasProperty("_EmissionColor"))
        {
            shared.EnableKeyword("_EMISSION");
            shared.SetColor("_EmissionColor", new Color(1.4f, 0.85f, 0.3f));
        }

        return shared;
    }
}
