using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>A frozen copy of the picked-up animal, with no AI, physics or animation updates.</summary>
[DefaultExecutionOrder(2200)]
public sealed class CarcassBackCarry : MonoBehaviour
{
    Inventory inventory;
    HuntGoods goods;
    Transform body;
    public Vector3 backOffset = new Vector3(0f, 0.12f, -0.32f);
    public Vector3 carryEuler = new Vector3(0f, 90f, 90f);
    public float pickupSeconds = 1.15f;
    public float dropSeconds = 0.8f;
    [Range(0f, 1f)] public float handWeight = 0.85f;
    public Transform leftGrip;
    public Transform rightGrip;
    Transform chest, leftUpper, leftLower, leftHand, rightUpper, rightLower, rightHand;
    Vector3 centerOffset, pickupPosition, lastPosition, sway, swayVelocity;
    Quaternion pickupRotation;
    Quaternion chestBasis;
    float phase, gait;
    bool dropping, released;
    static readonly List<CarcassBackCarry> carried = new List<CarcassBackCarry>();
    public static bool IsCarrying => Inventory.Instance != null && Inventory.Instance.CarcassCount > 0;
    public static bool IsHandling
    {
        get
        {
            foreach (CarcassBackCarry carry in carried)
                if (carry != null && !carry.released && (carry.dropping || carry.phase < carry.pickupSeconds)) return true;
            return false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCarries() => carried.Clear();
    Renderer[] visuals;
    readonly List<Mesh> ownedMeshes = new List<Mesh>();

    public static CarcassBackCarry Attach(Inventory owner, AnimalCarcass carcass, int stackIndex)
    {
        Transform player = PlayerView.Body();
        if (player == null) player = owner.transform;
        GameObject root = new GameObject("Back carried " + carcass.goods.Title);
        root.transform.SetPositionAndRotation(carcass.transform.position, carcass.transform.rotation);
        root.transform.SetParent(player, true);
        CarcassBackCarry carry = root.AddComponent<CarcassBackCarry>();
        carry.inventory = owner;
        carry.goods = carcass.goods;
        carry.body = player;
        carry.CacheRig();
        carried.Add(carry);

        // Use the highest-detail LOD once; Renderer.enabled does not identify the active LOD.
        var lowerLods = new HashSet<Renderer>();
        var firstLods = new HashSet<Renderer>();
        foreach (LODGroup group in carcass.GetComponentsInChildren<LODGroup>())
        {
            LOD[] lods = group.GetLODs();
            for (int i = 0; i < lods.Length; i++)
                foreach (Renderer renderer in lods[i].renderers)
                    if (i == 0) firstLods.Add(renderer);
                    else lowerLods.Add(renderer);
        }
        // Never clone gameplay components or colliders.
        foreach (Renderer source in carcass.GetComponentsInChildren<Renderer>())
        {
            if (!source.enabled || source.forceRenderingOff) continue;
            if (lowerLods.Contains(source) && !firstLods.Contains(source)) continue;
            Mesh mesh = null;
            if (source is SkinnedMeshRenderer skin)
            {
                if (skin.sharedMesh == null) continue;
                mesh = new Mesh { name = "Carried animal pose" };
                skin.BakeMesh(mesh);
                carry.ownedMeshes.Add(mesh);
            }
            else if (source is MeshRenderer)
            {
                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    // A dropped carry owns its snapshot meshes. Copy them before destroying it
                    // on re-pickup, rather than keeping references to soon-to-be-destroyed assets.
                    mesh = Instantiate(filter.sharedMesh);
                    carry.ownedMeshes.Add(mesh);
                }
            }
            if (mesh == null) continue;
            GameObject part = new GameObject(source.name);
            part.layer = player.gameObject.layer;
            part.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            part.transform.localScale = source.transform.lossyScale;
            part.transform.SetParent(root.transform, true);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = source.sharedMaterials;
            renderer.shadowCastingMode = source.shadowCastingMode;
            renderer.receiveShadows = source.receiveShadows;
        }

        carry.visuals = root.GetComponentsInChildren<Renderer>(true);
        // Animal forward runs across the shoulders, with its underside toward the back.
        root.transform.rotation = player.rotation * Quaternion.Euler(0f, 90f, 90f);
        if (carry.visuals.Length > 0)
        {
            Bounds bounds = carry.visuals[0].bounds;
            foreach (Renderer renderer in carry.visuals) bounds.Encapsulate(renderer.bounds);
            float scale = Mathf.Min(1f, 2.4f / Mathf.Max(0.01f, bounds.size.x, bounds.size.y, bounds.size.z));
            root.transform.localScale *= scale;
            bounds = carry.visuals[0].bounds;
            foreach (Renderer renderer in carry.visuals) bounds.Encapsulate(renderer.bounds);
            Vector3 center = player.position + player.rotation * new Vector3(0f, 1.45f + stackIndex * 0.12f, -0.45f - stackIndex * 0.15f);
            root.transform.position += center - bounds.center;
        }
        carry.centerOffset = Quaternion.Inverse(root.transform.rotation) * (VisualBounds(carry.visuals).center - root.transform.position);
        carry.pickupPosition = carcass.transform.position;
        carry.pickupRotation = carcass.transform.rotation;
        carry.lastPosition = player.position;
        // Visual starts on the ground and is lifted into place during the pickup phase.
        if (Application.isPlaying)
            root.transform.SetPositionAndRotation(carry.pickupPosition, carry.pickupRotation);
        else carry.phase = carry.pickupSeconds;
        return carry;
    }

    static Bounds VisualBounds(Renderer[] renderers)
    {
        Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, Vector3.zero);
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    void CacheRig()
    {
        SurvivalistBody visible = body.GetComponent<SurvivalistBody>();
        Animator rig = visible != null ? visible.VisibleAnimator : null;
        if (rig == null)
            foreach (Animator candidate in body.GetComponentsInChildren<Animator>())
                if (candidate.avatar != null && candidate.avatar.isValid && candidate.isHuman) { rig = candidate; break; }
        if (rig == null || !rig.isHuman) return;
        chest = rig.GetBoneTransform(HumanBodyBones.UpperChest);
        if (chest == null) chest = rig.GetBoneTransform(HumanBodyBones.Chest);
        if (chest == null) chest = rig.GetBoneTransform(HumanBodyBones.Spine);
        if (chest != null) chestBasis = Quaternion.Inverse(body.rotation) * chest.rotation;
        leftUpper = rig.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        leftLower = rig.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        leftHand = rig.GetBoneTransform(HumanBodyBones.LeftHand);
        rightUpper = rig.GetBoneTransform(HumanBodyBones.RightUpperArm);
        rightLower = rig.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightHand = rig.GetBoneTransform(HumanBodyBones.RightHand);
    }

    void Update()
    {
        if (released || GamePause.Active || Inventory.IsOpen || RideableHorse.IsRiding) return;
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead) return;
        // Only the latest load handles the key; older inventory goods remain intact.
        if (carried.Count > 0 && carried[carried.Count - 1] == this && phase >= pickupSeconds && !dropping
            && Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame)
        {
            dropping = true;
            phase = 0f;
        }
    }

    void LateUpdate()
    {
        if (released) return;
        if (inventory == null || body == null || !inventory.ContainsCarcass(goods))
        {
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
            return;
        }
        float dt = GamePause.Active ? 0f : Time.deltaTime;
        phase += dt;
        Vector3 movement = body.position - lastPosition;
        lastPosition = body.position;
        float speed = dt > 0f ? Vector3.ProjectOnPlane(movement, Vector3.up).magnitude / dt : 0f;
        gait += dt * Mathf.Clamp(speed * 2.3f, 0f, 9f);
        Vector3 swayTarget = new Vector3(Mathf.Sin(gait * 2f) * Mathf.Min(speed, 3f) * 0.006f,
            Mathf.Abs(Mathf.Sin(gait)) * Mathf.Min(speed, 3f) * 0.008f, 0f);
        if (dt > 0f) sway = Vector3.SmoothDamp(sway, swayTarget, ref swayVelocity, 0.14f, 0.3f, dt);
        // Calibrate imported bone axes once, then follow chest motion in that basis.
        Vector3 center = chest != null ? chest.position + body.rotation * (backOffset + sway)
            : body.TransformPoint(new Vector3(0f, 1.45f, -0.45f) + sway);
        Quaternion rotation = (chest != null ? chest.rotation * Quaternion.Inverse(chestBasis) : body.rotation)
            * Quaternion.Euler(carryEuler + new Vector3(0f, Mathf.Sin(gait) * Mathf.Min(speed, 3f), 0f));
        Vector3 position = center - rotation * centerOffset;
        float lift = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, pickupSeconds, phase));
        if (!dropping && lift < 1f)
        {
            position = Vector3.Lerp(pickupPosition, position, lift) + Vector3.up * Mathf.Sin(lift * Mathf.PI) * 0.18f;
            rotation = Quaternion.Slerp(pickupRotation, rotation, lift);
        }
        if (dropping)
        {
            float lower = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phase / dropSeconds));
            position += body.forward * lower * 0.6f - Vector3.up * lower * 0.35f;
            if (phase >= dropSeconds)
            {
                Release();
                return;
            }
        }
        transform.SetPositionAndRotation(position, rotation);
        bool visible = !PlayerView.IsFirstPerson();
        foreach (Renderer renderer in visuals) renderer.enabled = visible;
        // This runs AFTER SurvivalistBody's retarget pass. Animator IK would be overwritten.
        if (carried.Count > 0 && carried[carried.Count - 1] == this && !RideableHorse.IsRiding)
        {
            float gripWeight = handWeight * (dropping ? 1f - phase / dropSeconds : Mathf.Clamp01(phase / 0.3f));
            bool weaponAction = PlayerShotgun.IsAiming || PlayerBodyAnimation.IsActing();
            if (!weaponAction)
            {
                Vector3 left = leftGrip != null ? leftGrip.position : center - body.right * 0.38f + body.forward * 0.22f;
                Vector3 right = rightGrip != null ? rightGrip.position : center + body.right * 0.38f + body.forward * 0.22f;
                if (!dropping && lift < 1f)
                {
                    left = Vector3.Lerp(pickupPosition - body.right * 0.3f, left, lift);
                    right = Vector3.Lerp(pickupPosition + body.right * 0.3f, right, lift);
                }
                SolveArm(leftUpper, leftLower, leftHand, left, -body.right + body.forward * 0.3f, gripWeight);
                SolveArm(rightUpper, rightLower, rightHand, right, body.right + body.forward * 0.3f, gripWeight);
                if (leftGrip != null && leftHand != null) leftHand.rotation = Quaternion.Slerp(leftHand.rotation, leftGrip.rotation, gripWeight);
                if (rightGrip != null && rightHand != null) rightHand.rotation = Quaternion.Slerp(rightHand.rotation, rightGrip.rotation, gripWeight);
            }
        }
    }

    // Analytic two-bone IK; works with bone references from either humanoid or generic rigs.
    public static void SolveArm(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 elbowHint, float weight)
    {
        if (upper == null || lower == null || hand == null || weight <= 0f) return;
        target = Vector3.Lerp(hand.position, target, Mathf.Clamp01(weight));
        float a = Vector3.Distance(upper.position, lower.position), b = Vector3.Distance(lower.position, hand.position);
        if (a < 0.001f || b < 0.001f) return;
        Vector3 direction = target - upper.position;
        if (direction.sqrMagnitude < 0.000001f) return;
        float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(a - b) + 0.001f, a + b - 0.001f);
        direction.Normalize();
        Vector3 bend = Vector3.ProjectOnPlane(elbowHint, direction).normalized;
        if (bend.sqrMagnitude < 0.001f) bend = Vector3.ProjectOnPlane(Vector3.up, direction).normalized;
        float along = (a * a + distance * distance - b * b) / (2f * distance);
        Vector3 elbow = upper.position + direction * along + bend * Mathf.Sqrt(Mathf.Max(0f, a * a - along * along));
        Vector3 reachable = upper.position + direction * distance;
        upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, elbow - upper.position) * upper.rotation;
        lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, reachable - lower.position) * lower.rotation;
    }

    void Release()
    {
        if (!inventory.ReleaseCarcass(goods)) { dropping = false; return; }
        released = true;
        carried.Remove(this);
        transform.SetParent(null, true);
        foreach (Renderer renderer in visuals) renderer.enabled = true;
        AnimalCarcass carcass = gameObject.AddComponent<AnimalCarcass>();
        carcass.goods = goods;
        Bounds bounds = VisualBounds(visuals);
        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        collider.center = transform.InverseTransformPoint(bounds.center);
        // A local-space bound avoids oversized colliders on rotated snapshots.
        Bounds local = new Bounds();
        bool first = true;
        foreach (MeshFilter mesh in GetComponentsInChildren<MeshFilter>())
            foreach (Vector3 corner in BoundsCorners(mesh.sharedMesh.bounds))
            {
                Vector3 point = transform.InverseTransformPoint(mesh.transform.TransformPoint(corner));
                if (first) { local = new Bounds(point, Vector3.zero); first = false; }
                else local.Encapsulate(point);
            }
        collider.center = local.center;
        collider.size = Vector3.Max(local.size, Vector3.one * 0.1f);
        Rigidbody physics = gameObject.AddComponent<Rigidbody>();
        physics.mass = Mathf.Clamp(goods.weightKg, 1f, 150f);
        physics.interpolation = RigidbodyInterpolation.Interpolate;
        physics.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        foreach (Collider playerCollider in body.GetComponentsInChildren<Collider>())
            Physics.IgnoreCollision(collider, playerCollider);
        inventory.ShowNotice("Dropped the " + goods.Title.ToLowerInvariant() + ". E to pick it up again.");
        inventory.ApplySelectedWeapon(false);
    }

    static IEnumerable<Vector3> BoundsCorners(Bounds bounds)
    {
        for (int i = 0; i < 8; i++)
            yield return bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
    }

    void OnDestroy()
    {
        carried.Remove(this);
        if (Application.isPlaying && inventory != null && !released) inventory.ApplySelectedWeapon(false);
        foreach (Mesh mesh in ownedMeshes)
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
    }
}
