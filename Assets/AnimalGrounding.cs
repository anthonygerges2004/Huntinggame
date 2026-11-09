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
    const float ActiveRange = 260f;

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
    float shift;
    bool measured;

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
        if (feet.Length == 0)
        {
            enabled = false;
            return;
        }

        baseLocal = model.localPosition;
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

        // Put the model where it was left, in case the animator wrote its root this frame.
        model.position = transform.TransformPoint(baseLocal) + Vector3.up * shift;

        // A dead or dropping animal keeps its last correction; its feet no longer point down.
        if (animal != null && animal.Dead)
            return;

        if ((PlayerView.Position() - transform.position).sqrMagnitude > ActiveRange * ActiveRange)
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

        model.position = transform.TransformPoint(baseLocal) + Vector3.up * shift;
    }
}
