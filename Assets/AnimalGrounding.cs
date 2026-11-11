using UnityEngine;

/// <summary>
/// Keeps a walking animal's hooves on the terrain. The animal's own position is already
/// snapped to the ground, but the imported model could sit above it once the animator
/// rebinds with an avatar, which made living deer hover while they walked and ran.
/// Runs after the animator and only shifts the model child, never the animal itself,
/// so movement, AI and hit boxes are untouched.
/// </summary>
[DefaultExecutionOrder(1000)]
public class AnimalGrounding : MonoBehaviour
{
    const float MaxShift = 2.5f;
    const float SoleLift = 0.03f;
    const float FollowRate = 6f;
    const float ActiveRange = 800f; // far deer on hillsides were left hovering beyond the old 260 m

    static readonly string[] FootNames =
    {
        "toe.L", "toe.R", "front_toe.L", "front_toe.R",
    };

    static readonly string[] FallbackFootNames =
    {
        "foot.L", "foot.R", "front_foot.L", "front_foot.R",
    };

    Animal animal;
    Transform model;
    Transform[] feet = System.Array.Empty<Transform>();
    Vector3 baseLocal;
    Quaternion baseLocalRot = Quaternion.identity;
    Vector3 smoothRoot;
    Quaternion smoothRot = Quaternion.identity;
    bool smoothed;
    float shift;
    bool measured;
    Renderer[] bodies;

    public static void Ensure(Animal animal)
    {
        if (animal == null || animal.kind == AnimalKind.Bird)
            return;
        if (animal.GetComponent<AnimalGrounding>() == null)
            animal.gameObject.AddComponent<AnimalGrounding>();
    }

    void Start()
    {
        animal = GetComponent<Animal>();
        model = FindModel();
        if (model == null)
        {
            enabled = false;
            return;
        }

        feet = FindFeet(model, FootNames);
        if (feet.Length == 0)
            feet = FindFeet(model, FallbackFootNames);
        // No named foot bones: fall back to the bottom of the drawn body.
        if (feet.Length == 0)
            bodies = model.GetComponentsInChildren<Renderer>(true);

        baseLocal = model.localPosition;
        baseLocalRot = model.localRotation;
    }

    Transform FindModel()
    {
        Transform child = transform.Find(AnimalVisuals.ModelChildName);
        if (child != null)
            return child;

        // A model whose animator sits on the animal itself cannot be shifted without
        // fighting the ground snap, so only a separate child model is handled.
        Animator animator = GetComponentInChildren<Animator>(true);
        if (animator != null && animator.transform != transform)
            return animator.transform;

        return null;
    }

    static Transform[] FindFeet(Transform root, string[] names)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        System.Collections.Generic.List<Transform> found = new System.Collections.Generic.List<Transform>(4);
        for (int n = 0; n < names.Length; n++)
        {
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == names[n])
                {
                    found.Add(all[i]);
                    break;
                }
            }
        }

        return found.ToArray();
    }

    void LateUpdate()
    {
        if (model == null)
            return;

        // Animals 45-120 m away are simulated only a few times a second, so the body jumped a
        // couple of metres per tick and looked like it teleported. The model glides after the
        // simulated body instead; up close it follows tightly, and a real jump (respawn) snaps.
        Vector3 root = transform.position;
        Quaternion rootRot = transform.rotation;
        float playerDist = (PlayerView.Position() - root).magnitude;
        bool dead = animal != null && animal.Dead;
        if (!smoothed || dead || (root - smoothRoot).sqrMagnitude > 15f * 15f || model.parent != transform)
        {
            smoothRoot = root;
            smoothRot = rootRot;
            smoothed = true;
        }
        else
        {
            float rate = Mathf.Lerp(28f, 9f, Mathf.InverseLerp(30f, 50f, playerDist));
            float a = 1f - Mathf.Exp(-rate * Time.deltaTime);
            smoothRoot = Vector3.Lerp(smoothRoot, root, a);
            smoothRot = Quaternion.Slerp(smoothRot, rootRot, a);
        }

        // Put the model where it was left, in case the animator wrote its root this frame.
        PlaceModel();

        // A dead or dropping animal keeps its last correction; its feet no longer point down.
        if (dead)
            return;

        if (playerDist > ActiveRange)
            return;

        float lowestGap = float.PositiveInfinity;
        for (int i = 0; i < feet.Length; i++)
        {
            Transform foot = feet[i];
            if (foot == null)
                continue;

            Vector3 p = foot.position;
            if (!WorldGround.TryGetGround(p, out Vector3 ground, out _))
                continue;

            float gap = p.y - ground.y;
            if (gap < lowestGap)
                lowestGap = gap;
        }

        if (bodies != null)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                Renderer r = bodies[i];
                if (r == null || !r.enabled)
                    continue;
                Vector3 c = r.bounds.center;
                if (!WorldGround.TryGetGround(c, out Vector3 ground, out _))
                    continue;
                float gap = r.bounds.min.y - ground.y;
                if (gap < lowestGap)
                    lowestGap = gap;
            }
        }

        if (float.IsPositiveInfinity(lowestGap))
            return;

        float target = Mathf.Clamp(shift - (lowestGap - SoleLift), -MaxShift, MaxShift);
        if (!measured)
        {
            // First look: snap straight down so the deer never starts in the air.
            shift = target;
            measured = true;
        }
        else
        {
            // Slow follow so a gallop, when all four hooves leave the ground, does not bob the body.
            shift = Mathf.Lerp(shift, target, 1f - Mathf.Exp(-FollowRate * Time.deltaTime));
        }

        PlaceModel();
    }

    void PlaceModel()
    {
        if (model.parent == transform)
        {
            model.SetPositionAndRotation(
                smoothRoot + smoothRot * Vector3.Scale(transform.lossyScale, baseLocal) + Vector3.up * shift,
                smoothRot * baseLocalRot);
        }
        else
            model.position = transform.TransformPoint(baseLocal) + Vector3.up * shift;
    }
}
