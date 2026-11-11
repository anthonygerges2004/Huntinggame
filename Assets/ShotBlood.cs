using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Directional droplets and surface splashes. All dimensions are in metres.</summary>
public class ShotBlood : MonoBehaviour
{
    const float Lifetime = 12f;
    const int SprayCount = 32;
    const int DripCount = 6;

    class Drop
    {
        public Transform body;
        public Vector3 velocity;
        public float size;
        public float born;
        public float landedAt = -1f;
        public Vector3 splashScale;
    }

    readonly RaycastHit[] hits = new RaycastHit[32];
    Drop[] drops;
    Transform wound;
    Transform animal;
    float age;
    float nextDrip = 0.18f;
    int dripIndex = SprayCount;
    Vector3 woundScale;
    Material material;
    Mesh splashMesh;

    public static void Play(Vector3 point, Vector3 normal, Transform stickTo)
    {
        GameObject root = new GameObject("BloodSpray");
        root.transform.position = point;
        root.layer = 2; // Ignore Raycast, including colliders pending destruction.
        root.AddComponent<ShotBlood>().Begin(point, normal, stickTo);
    }

    void Begin(Vector3 point, Vector3 normal, Transform follow)
    {
        normal = normal.sqrMagnitude < 0.001f ? Vector3.up : normal.normalized;
        animal = follow;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
        {
            Destroy(gameObject);
            return;
        }

        material = new Material(shader);
        material.color = new Color(0.32f, 0.009f, 0.014f, 1f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.62f);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.62f);
        splashMesh = MakeSplashMesh();

        wound = MakeSplash("Wound", point + normal * 0.008f, normal);
        woundScale = new Vector3(Random.Range(0.065f, 0.095f), Random.Range(0.09f, 0.13f), 1f);
        wound.localScale = woundScale;
        if (animal != null)
        {
            wound.SetParent(animal, true);
            woundScale = wound.localScale;
        }

        drops = new Drop[SprayCount + DripCount];
        Quaternion direction = Quaternion.LookRotation(normal);
        for (int i = 0; i < SprayCount; i++)
        {
            Vector2 spread = Random.insideUnitCircle * 0.65f;
            Vector3 velocity = direction * new Vector3(spread.x, spread.y, 1f).normalized;
            float size = Random.Range(0.012f, 0.038f);
            drops[i] = MakeDrop(point + normal * 0.025f, velocity * Random.Range(1.2f, 4.2f), size);
        }
    }

    Drop MakeDrop(Vector3 point, Vector3 velocity, float size)
    {
        GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        blob.name = "BloodDrop";
        blob.layer = 2;
        Collider collider = blob.GetComponent<Collider>();
        collider.enabled = false;
        Destroy(collider);
        blob.transform.SetParent(transform, true);
        blob.transform.position = point;
        blob.transform.localScale = Vector3.one * size;
        ConfigureRenderer(blob.GetComponent<Renderer>());
        return new Drop { body = blob.transform, velocity = velocity, size = size, born = age };
    }

    void Update()
    {
        if (drops == null)
            return;
        float dt = Time.deltaTime;
        age += dt;
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Lifetime - 2f, Lifetime, age));
        if (wound != null)
            wound.localScale = Vector3.Scale(woundScale, new Vector3(fade, fade, 1f));

        for (int i = 0; i < drops.Length; i++)
        {
            Drop drop = drops[i];
            if (drop == null || drop.body == null)
                continue;
            if (drop.landedAt >= 0f)
            {
                float spread = Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0f, 1f, (age - drop.landedAt) / 0.12f));
                drop.body.localScale = Vector3.Scale(drop.splashScale, new Vector3(spread * fade, spread * fade, 1f));
                continue;
            }

            // Sweep each step, so low frame rates cannot push droplets through terrain.
            Vector3 start = drop.body.position;
            Vector3 step = drop.velocity * dt + Physics.gravity * (0.5f * dt * dt);
            drop.velocity += Physics.gravity * dt;
            drop.velocity *= Mathf.Exp(-0.45f * dt);
            if (TrySurface(start, step, out RaycastHit hit))
            {
                Destroy(drop.body.gameObject);
                drop.body = MakeSplash("BloodSplash", hit.point + hit.normal * 0.004f, hit.normal);
                float width = drop.size * Random.Range(2f, 3.4f);
                drop.splashScale = new Vector3(width, width * Random.Range(0.65f, 1.25f), 1f);
                drop.body.localScale = drop.splashScale * 0.3f;
                drop.landedAt = age;
            }
            else
            {
                drop.body.position += step;
                if (drop.velocity.sqrMagnitude > 0.001f)
                    drop.body.rotation = Quaternion.LookRotation(drop.velocity);
                drop.body.localScale = new Vector3(drop.size, drop.size, drop.size * Mathf.Clamp(1f + drop.velocity.magnitude * 0.14f, 1f, 2.4f));
                // No surface below a cliff: retire the drop rather than inventing ground.
                if (age - drop.born > 2.5f)
                    Destroy(drop.body.gameObject);
            }
        }

        if (animal != null && wound != null && dripIndex < drops.Length && age >= nextDrip)
        {
            drops[dripIndex++] = MakeDrop(wound.position, Vector3.down * 0.3f, Random.Range(0.012f, 0.023f));
            nextDrip = age + Random.Range(0.15f, 0.28f);
        }
        if (age >= Lifetime)
            Destroy(gameObject);
    }

    bool TrySurface(Vector3 start, Vector3 step, out RaycastHit closest)
    {
        closest = default;
        float distance = step.magnitude;
        if (distance < 0.00001f)
            return false;
        int count = Physics.RaycastNonAlloc(start, step / distance, hits, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (animal != null && (hit.transform == animal || hit.transform.IsChildOf(animal)))
                continue;
            if (hit.distance >= nearest)
                continue;
            nearest = hit.distance;
            closest = hit;
            found = true;
        }
        return found;
    }

    Transform MakeSplash(string objectName, Vector3 position, Vector3 normal)
    {
        GameObject splash = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
        splash.layer = 2;
        splash.transform.SetParent(transform, false);
        splash.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
        splash.GetComponent<MeshFilter>().sharedMesh = splashMesh;
        ConfigureRenderer(splash.GetComponent<Renderer>());
        return splash.transform;
    }

    void ConfigureRenderer(Renderer renderer)
    {
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
    }

    internal static Mesh MakeSplashMesh()
    {
        const int count = 28;
        Vector3[] vertices = new Vector3[count + 1];
        Vector3[] normals = new Vector3[count + 1];
        for (int i = 0; i < normals.Length; i++)
            normals[i] = Vector3.forward;
        int[] triangles = new int[count * 6];
        for (int i = 0; i < count; i++)
        {
            float angle = i * Mathf.PI * 2f / count;
            float radius = Random.Range(0.32f, 0.5f);
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            int next = (i + 1) % count + 1;
            int t = i * 6;
            triangles[t] = 0;
            triangles[t + 1] = i + 1;
            triangles[t + 2] = next;
            // Both faces render on steep terrain and on the animal's hide.
            triangles[t + 3] = 0;
            triangles[t + 4] = next;
            triangles[t + 5] = i + 1;
        }
        Mesh mesh = new Mesh { name = "IrregularBloodSplash", vertices = vertices, triangles = triangles, normals = normals };
        mesh.RecalculateBounds();
        return mesh;
    }

    void OnDestroy()
    {
        if (wound != null)
            Destroy(wound.gameObject);
        if (material != null)
            Destroy(material);
        if (splashMesh != null)
            Destroy(splashMesh);
    }
}
