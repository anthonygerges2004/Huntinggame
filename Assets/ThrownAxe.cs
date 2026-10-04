using UnityEngine;
using UnityEngine.InputSystem;

public class ThrownAxe : MonoBehaviour
{
    public float pickupRange = 2.5f;

    Rigidbody body;
    bool landed;
    float age;

    public void Launch(Vector3 velocity, Vector3 spin)
    {
        AddCollider();

        body = gameObject.AddComponent<Rigidbody>();
        body.mass = 1.5f;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.linearVelocity = velocity;
        body.angularVelocity = spin;

        IgnorePlayer();
    }

    void AddCollider()
    {
        Collider[] existing = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < existing.Length; i++)
            Destroy(existing[i]);

        Bounds local = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        MeshFilter[] filters = GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < filters.Length; i++)
        {
            if (filters[i].sharedMesh == null)
                continue;

            Bounds b = filters[i].sharedMesh.bounds;
            Transform t = filters[i].transform;
            for (int c = 0; c < 8; c++)
            {
                Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3(
                    (c & 1) == 0 ? -1f : 1f,
                    (c & 2) == 0 ? -1f : 1f,
                    (c & 4) == 0 ? -1f : 1f));
                Vector3 point = transform.InverseTransformPoint(t.TransformPoint(corner));
                if (!any)
                {
                    local = new Bounds(point, Vector3.zero);
                    any = true;
                }
                else
                {
                    local.Encapsulate(point);
                }
            }
        }

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        if (any)
        {
            box.center = local.center;
            box.size = Vector3.Max(local.size, Vector3.one * 0.05f);
        }
    }

    void IgnorePlayer()
    {
        if (PlayerHealth.Instance == null)
            return;

        Collider mine = GetComponent<BoxCollider>();
        Collider[] player = PlayerHealth.Instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < player.Length; i++)
            Physics.IgnoreCollision(mine, player[i]);
    }

    void OnCollisionEnter(Collision collision)
    {
        HitSomething(collision.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!landed && other.GetComponentInParent<Animal>() != null)
            HitSomething(other);
    }

    void HitSomething(Collider other)
    {
        if (landed)
            return;

        landed = true;

        Animal animal = other.GetComponentInParent<Animal>();
        if (animal != null)
        {
            animal.Hit();
            Debug.Log("Axe hit " + animal.kind + "!");
        }

        if (body != null)
        {
            body.linearVelocity *= 0.15f;
            body.angularVelocity *= 0.15f;
        }
    }

    void Update()
    {
        age += Time.deltaTime;

        if (transform.position.y < -200f)
        {
            ReturnToPlayer();
            return;
        }

        if (age < 0.4f || !InPickupRange())
            return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            ReturnToPlayer();
    }

    bool InPickupRange()
    {
        Camera cam = Camera.main;
        return cam != null && Vector3.Distance(cam.transform.position, transform.position) <= pickupRange;
    }

    void ReturnToPlayer()
    {
        if (AxeSwing.Instance != null)
            AxeSwing.Instance.GiveBack();
        else
            AxeSwing.HasAxe = true;
        Destroy(gameObject);
    }

    void OnGUI()
    {
        if (age < 0.4f || !InPickupRange())
            return;

        GUI.color = Color.white;
        GUI.Label(new Rect(Screen.width * 0.5f - 80f, Screen.height * 0.5f + 30f, 220f, 24f), "Press E to pick up axe");
    }
}
