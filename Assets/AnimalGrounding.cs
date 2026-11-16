using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Keeps living animal roots and hooves on the terrain after animation.
/// Fallen animals settle using their posed mesh while retaining the death path.
/// </summary>
[DefaultExecutionOrder(1000)]
public class AnimalGrounding : MonoBehaviour
{
    const float SoleLift = 0.03f;
    const float FollowRate = 6f;

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
    float hoverTime;
    Renderer[] bodies;
    Renderer[] carcassBodies;
    Mesh carcassPose;
    readonly List<Vector3> carcassVertices = new List<Vector3>();
    bool carcassGrounded;
    bool carcassWasFalling;
    Vector3 carcassPosition, carcassScale;
    Quaternion carcassRotation;

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
        if (animal != null && animal.kind == AnimalKind.Bird)
        {
            enabled = false;
            return;
        }
        model = FindModel();
        if (model == null)
        {
            // Root-only models still need the living-root height repair below.
            return;
        }

        feet = FindFeet(model, FootNames);
        if (feet.Length == 0)
            feet = FindFeet(model, FallbackFootNames);
        // No named foot bones: fall back to the bottom of the drawn body.
        if (feet.Length == 0)
            bodies = model.GetComponentsInChildren<Renderer>(true);
        carcassBodies = model.GetComponentsInChildren<Renderer>(true);

        // Scene-authored prefab instances can retain placement offsets of tens of
        // metres. The visual must share the animal's origin, or turning the animal
        // swings its mesh away from the AI, hit collider and sampled ground.
        baseLocal = Vector3.zero;
        model.localPosition = baseLocal;
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
        {
            Transform visualRoot = animator.transform;
            while (visualRoot.parent != transform)
                visualRoot = visualRoot.parent;
            return visualRoot;
        }

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
        GroundLivingRoot(animal);
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

        // Interpolating Y between sparse AI ticks cuts across hills and leaves the
        // visible animal above downhill terrain. Sample at the displayed XZ instead.
        // Preserve the root's intentional ground offset (for example a NavMesh base offset).
        bool interpolating = Mathf.Abs(smoothRoot.x - root.x) + Mathf.Abs(smoothRoot.z - root.z) > 0.001f;
        if (!dead && interpolating && WorldGround.TryGetGround(root, out Vector3 rootGround, out _)
            && WorldGround.TryGetGround(smoothRoot, out Vector3 displayedGround, out _))
            smoothRoot.y = displayedGround.y + (root.y - rootGround.y);

        // Put the model where it was left, in case the animator wrote its root this frame.
        PlaceModel();

        // Hooves are no longer support points once the animal rolls onto its side.
        if (dead)
        {
            GroundCarcass();
            return;
        }

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

        // Animation poses can move the feet in either direction. Correct the full
        // measured gap; capping upward correction leaves low poses underground.
        float target = shift - (lowestGap - SoleLift);
        // Let brief airborne gallop poses blend smoothly. Sustained hover uses
        // the faster follow rate below; large errors and penetration repair at once.
        hoverTime = lowestGap > 0.35f ? hoverTime + Time.deltaTime : 0f;
        if (!measured || Mathf.Abs(lowestGap - SoleLift) > 1f || lowestGap < -0.35f)
        {
            shift = target;
            measured = true;
        }
        else
        {
            float rate = hoverTime > 0.25f ? 18f : FollowRate;
            shift = Mathf.Lerp(shift, target, 1f - Mathf.Exp(-rate * Time.deltaTime));
        }

        PlaceModel();
    }

    public static void GroundLivingRoot(Animal owner)
    {
        if (owner == null || owner.Dead || owner.kind == AnimalKind.Bird) return;
        Vector3 position = owner.transform.position;
        if (!WorldGround.TryGetGround(position, out Vector3 ground, out _)) return;
        // Idle/dormant AI does not necessarily call MoveOnGround. Repair Y without
        // moving XZ, changing AI targets, or treating a stale height as an intentional offset.
        if (Mathf.Abs(position.y - ground.y) > 0.01f)
        {
            position.y = ground.y;
            owner.transform.position = position;
        }
    }

    void GroundCarcass()
    {
        // The animator is stopped at death. Re-evaluate while the fall moves or
        // rotates the root, then reuse the settled correction for a still carcass.
        bool falling = animal != null && animal.Falling;
        if (carcassGrounded && falling == carcassWasFalling && transform.position == carcassPosition &&
            transform.rotation == carcassRotation && transform.lossyScale == carcassScale)
            return;

        float gap = float.PositiveInfinity;
        foreach (Renderer renderer in carcassBodies)
        {
            if (renderer == null) continue;
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
            {
                if (carcassPose == null) carcassPose = new Mesh { name = "Carcass ground pose" };
                // Vertices are transformed with localToWorldMatrix below; don't
                // bake the renderer scale into them a second time.
                skin.BakeMesh(carcassPose, false);
                mesh = carcassPose;
            }
            else if (renderer is MeshRenderer)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null) mesh = filter.sharedMesh;
            }

            if (mesh != null && mesh.isReadable)
            {
                mesh.GetVertices(carcassVertices);
                Matrix4x4 matrix = renderer.localToWorldMatrix;
                // Sample during the short fall; check every vertex once it stops
                // so thin body parts cannot remain embedded on uneven terrain.
                int stride = falling ? Mathf.Max(1, carcassVertices.Count / 256) : 1;
                Vector3 lowest = Vector3.zero;
                float lowestY = float.PositiveInfinity;
                for (int i = 0; i < carcassVertices.Count; i++)
                {
                    Vector3 vertex = matrix.MultiplyPoint3x4(carcassVertices[i]);
                    if (vertex.y < lowestY) { lowestY = vertex.y; lowest = vertex; }
                    if (i % stride == 0) MeasureCarcassGap(vertex, ref gap);
                }
                if (!float.IsPositiveInfinity(lowestY)) MeasureCarcassGap(lowest, ref gap);
            }
            else
            {
                Vector3 bottom = renderer.bounds.center;
                bottom.y = renderer.bounds.min.y;
                MeasureCarcassGap(bottom, ref gap);
            }
        }
        if (float.IsPositiveInfinity(gap)) return;
        shift -= gap - SoleLift;
        PlaceModel();
        carcassGrounded = true;
        carcassWasFalling = falling;
        carcassPosition = transform.position;
        carcassRotation = transform.rotation;
        carcassScale = transform.lossyScale;
    }

    static void MeasureCarcassGap(Vector3 vertex, ref float gap)
    {
        if (WorldGround.TryGetGround(vertex, out Vector3 ground, out _))
            gap = Mathf.Min(gap, vertex.y - ground.y);
    }

    void OnDestroy()
    {
        if (carcassPose == null) return;
        if (Application.isPlaying) Destroy(carcassPose);
        else DestroyImmediate(carcassPose);
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
