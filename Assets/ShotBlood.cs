using UnityEngine;

/// <summary>
/// Visible blood when a shot connects: a wound stuck on the hide, a spray of drops, then puddles.
/// Uses the same material Unity gives a primitive, so it still draws under URP.
/// </summary>
public class ShotBlood : MonoBehaviour
{
    class Drop
    {
        public Transform body;
        public Vector3 velocity;
        public bool landed;
        public float landTime;
        public float splatSize;
        public float flySize;
    }

    Drop[] drops;
    Transform wound;
    Transform stickTo;
    Vector3 origin;
    float age;
    float nextDrip;
    int dripsLeft;
    float woundSize;
    static Material shared;

    public static void Play(Vector3 point, Vector3 normal, Transform stickTo)
    {
        if (normal.sqrMagnitude < 0.001f)
            normal = Vector3.up;

        GameObject root = new GameObject("BloodSpray");
        root.transform.position = point;
        int ignore = LayerMask.NameToLayer("Ignore Raycast");
        if (ignore >= 0)
            root.layer = ignore;

        ShotBlood spray = root.AddComponent<ShotBlood>();
        spray.Begin(point, normal.normalized, stickTo);
    }

    void Begin(Vector3 point, Vector3 normal, Transform follow)
    {
        origin = point;
        stickTo = follow;
        woundSize = 0.62f;
        wound = MakeBlob("Wound", point, 0.2f);
        if (stickTo != null)
        {
            wound.SetParent(stickTo, true);
            wound.rotation = Quaternion.LookRotation(normal);
            FitScale(wound, 0.2f);
        }

        Light flash = new GameObject("BloodLight").AddComponent<Light>();
        flash.transform.SetParent(transform, false);
        flash.transform.position = point;
        flash.type = LightType.Point;
        flash.color = new Color(1f, 0.15f, 0.08f);
        flash.intensity = 18f;
        flash.range = 9f;
        Destroy(flash.gameObject, 0.16f);

        drops = new Drop[22];
        for (int i = 0; i < drops.Length; i++)
        {
            Vector3 spray = (normal + Random.insideUnitSphere * 0.9f).normalized;
            spray.y = Mathf.Abs(spray.y) + 0.45f;
            float size = Random.Range(0.18f, 0.34f);
            Transform blob = MakeBlob("Drop", point, size);
            drops[i] = new Drop
            {
                body = blob,
                velocity = spray * Random.Range(3.2f, 7.5f),
                flySize = size
            };
        }

        dripsLeft = 8;
        nextDrip = 0.12f;
    }

    void Update()
    {
        age += Time.deltaTime;
        if (wound != null)
        {
            float punch = 1f + Mathf.Sin(Mathf.Clamp01(age / 0.28f) * Mathf.PI) * 0.45f;
            float settle = Mathf.Lerp(0.35f, woundSize, Mathf.Clamp01(age / 0.2f));
            FitScale(wound, settle * punch);
        }

        for (int i = 0; i < drops.Length; i++)
        {
            Drop drop = drops[i];
            if (drop.body == null)
                continue;

            if (!drop.landed)
            {
                drop.velocity += Vector3.down * (14f * Time.deltaTime);
                drop.body.position += drop.velocity * Time.deltaTime;
                if (TryLand(drop))
                {
                    drop.landed = true;
                    drop.landTime = age;
                    drop.splatSize = Random.Range(0.38f, 0.85f);
                }
            }
            else
            {
                float grow = Mathf.Clamp01((age - drop.landTime) / 0.18f);
                float width = Mathf.Lerp(drop.flySize, drop.splatSize, grow);
                drop.body.localScale = new Vector3(width, 0.05f, width);
            }
        }

        if (stickTo != null && dripsLeft > 0 && wound != null && age >= nextDrip && age < 1.8f)
        {
            dripsLeft--;
            nextDrip = age + 0.2f;
            LaunchDrip();
        }

        if (age > 6.5f)
            Destroy(gameObject);
    }

    void LaunchDrip()
    {
        Transform drip = MakeBlob("Drip", wound.position, 0.12f);
        for (int i = 0; i < drops.Length; i++)
        {
            if (!drops[i].landed)
                continue;

            if (drops[i].body != null)
                Destroy(drops[i].body.gameObject);

            drops[i].body = drip;
            drops[i].velocity = Vector3.down * 2.2f + stickTo.forward * 0.35f;
            drops[i].landed = false;
            drops[i].flySize = 0.12f;
            return;
        }

        Destroy(drip.gameObject);
    }

    bool TryLand(Drop drop)
    {
        if (drop.velocity.y > 0f && age < 0.7f)
            return false;

        Vector3 from = drop.body.position + Vector3.up * 0.15f;
        if (!Physics.Raycast(from, Vector3.down, out RaycastHit ground, 2.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return age > 0.7f && SnapFallback(drop);

        if (stickTo != null && (ground.transform == stickTo || ground.transform.IsChildOf(stickTo)))
            return age > 0.7f && SnapFallback(drop);

        if (drop.body.position.y > ground.point.y + 0.08f && age < 0.7f)
            return false;

        PlaceFlat(drop, ground.point + Vector3.up * 0.05f);
        return true;
    }

    bool SnapFallback(Drop drop)
    {
        Vector3 spot = drop.body.position;
        spot.y = origin.y - 1.1f;
        if (Physics.Raycast(drop.body.position + Vector3.up, Vector3.down, out RaycastHit ground, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (stickTo == null || (ground.transform != stickTo && !ground.transform.IsChildOf(stickTo)))
                spot = ground.point + Vector3.up * 0.05f;
        }

        PlaceFlat(drop, spot);
        return true;
    }

    static void PlaceFlat(Drop drop, Vector3 spot)
    {
        drop.body.position = spot;
        drop.body.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    void OnDestroy()
    {
        if (wound != null)
            Destroy(wound.gameObject);
    }

    Transform MakeBlob(string blobName, Vector3 position, float size)
    {
        GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Collider solid = blob.GetComponent<Collider>();
        if (solid != null)
            Destroy(solid);

        blob.name = blobName;
        int ignore = LayerMask.NameToLayer("Ignore Raycast");
        if (ignore >= 0)
            blob.layer = ignore;

        blob.transform.SetParent(transform, true);
        blob.transform.position = position;
        blob.transform.localScale = Vector3.one * size;

        Renderer renderer = blob.GetComponent<Renderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = BloodMaterial();
        return blob.transform;
    }

    void FitScale(Transform blob, float worldSize)
    {
        Vector3 lossy = blob.parent != null ? blob.parent.lossyScale : Vector3.one;
        float sx = Mathf.Abs(lossy.x) < 0.01f ? 1f : Mathf.Abs(lossy.x);
        float sy = Mathf.Abs(lossy.y) < 0.01f ? 1f : Mathf.Abs(lossy.y);
        float sz = Mathf.Abs(lossy.z) < 0.01f ? 1f : Mathf.Abs(lossy.z);
        blob.localScale = new Vector3(worldSize / sx, worldSize / sy, worldSize / sz);
    }

    static Material BloodMaterial()
    {
        if (shared != null)
            return shared;

        GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Renderer source = temp.GetComponent<Renderer>();
        shared = new Material(source.sharedMaterial);
        Destroy(temp);

        Color blood = new Color(0.72f, 0.02f, 0.02f, 1f);
        shared.color = blood;
        if (shared.HasProperty("_BaseColor"))
            shared.SetColor("_BaseColor", blood);
        if (shared.HasProperty("_Color"))
            shared.SetColor("_Color", blood);
        if (shared.HasProperty("_EmissionColor"))
        {
            shared.EnableKeyword("_EMISSION");
            shared.SetColor("_EmissionColor", new Color(0.35f, 0f, 0f));
        }

        return shared;
    }
}
