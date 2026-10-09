using UnityEngine;

public class LakeFish : MonoBehaviour
{
    static Material bodyMat;
    static Material finMat;

    LakeWater lake;
    Transform tail;
    Vector3 target;
    Vector3? lure;
    float speed;
    float cruiseSpeed;
    float age;
    float nextJump;
    float jumpT = -1f;
    Vector3 jumpFrom;
    Vector3 jumpTo;

    public bool Busy => lure.HasValue;
    public bool AtLure => lure.HasValue && Vector3.Distance(transform.position, lure.Value) < 0.5f;

    public static LakeFish Spawn(LakeWater lake, Vector3 position)
    {
        if (bodyMat == null)
        {
            bodyMat = MakeMaterial(new Color(0.42f, 0.52f, 0.46f));
            finMat = MakeMaterial(new Color(0.3f, 0.38f, 0.34f));
        }

        GameObject root = new GameObject("LakeFish");
        root.transform.position = position;
        root.transform.localScale = Vector3.one * Random.Range(0.7f, 1.3f);

        Part(root.transform, PrimitiveType.Sphere, bodyMat, Vector3.zero, new Vector3(0.2f, 0.22f, 0.62f));
        Part(root.transform, PrimitiveType.Cube, finMat, new Vector3(0f, 0.12f, 0.02f), new Vector3(0.02f, 0.1f, 0.2f));

        Transform tailPivot = new GameObject("Tail").transform;
        tailPivot.SetParent(root.transform, false);
        tailPivot.localPosition = new Vector3(0f, 0f, -0.28f);
        Part(tailPivot, PrimitiveType.Cube, finMat, new Vector3(0f, 0f, -0.1f), new Vector3(0.02f, 0.24f, 0.18f));

        LakeFish fish = root.AddComponent<LakeFish>();
        fish.lake = lake;
        fish.tail = tailPivot;
        fish.cruiseSpeed = Random.Range(0.8f, 1.6f);
        fish.speed = fish.cruiseSpeed;
        fish.age = Random.Range(0f, 10f);
        fish.nextJump = Time.time + Random.Range(10f, 40f);
        fish.PickTarget();
        return fish;
    }

    static void Part(Transform parent, PrimitiveType type, Material mat, Vector3 pos, Vector3 scale)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Material MakeMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        return mat;
    }

    public void GoTo(Vector3 point)
    {
        lure = point;
        speed = cruiseSpeed;
    }

    public void Release()
    {
        lure = null;
        speed = cruiseSpeed * 2.5f;
        PickTarget();
    }

    void PickTarget()
    {
        if (lake != null)
            lake.RandomUnderwaterPoint(out target);
    }

    void Update()
    {
        if (lake == null)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;

        if (jumpT >= 0f)
        {
            Jump();
            return;
        }

        speed = Mathf.MoveTowards(speed, cruiseSpeed, Time.deltaTime);
        Vector3 destination = lure ?? target;
        Vector3 toTarget = destination - transform.position;

        if (!lure.HasValue && toTarget.magnitude < 0.6f)
        {
            PickTarget();
            return;
        }

        if (toTarget.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(toTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 3f * Time.deltaTime);
        }

        float step = AtLure ? 0f : speed * Time.deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, destination, step);

        float wiggle = Mathf.Sin(age * (6f + speed * 4f)) * 25f;
        tail.localRotation = Quaternion.Euler(0f, wiggle, 0f);

        if (!lure.HasValue && Time.time > nextJump)
            StartJump();
    }

    void StartJump()
    {
        nextJump = Time.time + Random.Range(15f, 45f);
        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;

        Vector3 landing = transform.position + forward.normalized * 2.5f;
        if (!lake.Inside(landing, 2f) || lake.GroundY(landing) > lake.SurfaceY - 0.8f)
            return;

        jumpFrom = transform.position;
        jumpTo = new Vector3(landing.x, lake.SurfaceY - 0.6f, landing.z);
        jumpT = 0f;
    }

    void Jump()
    {
        jumpT += Time.deltaTime / 0.9f;
        float t = Mathf.Clamp01(jumpT);
        Vector3 previous = transform.position;
        Vector3 p = Vector3.Lerp(jumpFrom, jumpTo, t);
        p.y += Mathf.Sin(t * Mathf.PI) * (lake.SurfaceY + 0.9f - Mathf.Max(jumpFrom.y, jumpTo.y));
        transform.position = p;

        Vector3 motion = p - previous;
        if (motion.sqrMagnitude > 0.00001f)
            transform.rotation = Quaternion.LookRotation(motion);
        tail.localRotation = Quaternion.Euler(0f, Mathf.Sin(age * 30f) * 35f, 0f);

        if (t >= 1f)
        {
            jumpT = -1f;
            PickTarget();
        }
    }
}
