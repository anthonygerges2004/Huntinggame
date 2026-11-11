using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A low-poly villager built from simple shapes, so it matches RPG Poly Pack Lite.
/// Wanderers walk between doors, stalls and the square of their Settlement; guards stand
/// watch; patrols pace between points; fishers sit at the end of a jetty; travellers walk
/// the roads between settlements. Distant villagers stop updating and hide.
/// </summary>
public partial class Villager : MonoBehaviour
{
    public enum Role { Wanderer, Guard, Patrol, Fisher, Traveler, Shopkeeper }

    public Role role;
    public Settlement settlement;
    public float walkSpeed = 1.3f;
    public Vector3[] path;            // Patrol / Traveler points
    public bool snapToGround = true;  // Fishers on a jetty keep their height
    [Tooltip("Hermit and other story NPCs stay visible from far away.")]
    public bool keepVisibleWhenFar;

    public Transform body, legL, legR, armL, armR, head;

    const float CullDistance = 170f;

    readonly List<Vector3> route = new List<Vector3>();
    int routeIndex;
    int pathIndex;
    int pathStep = 1;
    float waitUntil;
    float phase;
    float nextCull;
    float lookTimer;
    float headYaw;
    float headTarget;
    bool visible = true;
    Vector3 bodyRest;
    Renderer[] renderers;

    void Start()
    {
        renderers = GetComponentsInChildren<Renderer>();
        phase = Random.value * 10f;
        waitUntil = Time.time + Random.Range(0f, 5f);
        if (body != null)
            bodyRest = body.localPosition;
        if (path != null && path.Length > 1)
            pathIndex = Random.Range(0, path.Length);
        SetupOccupation();
    }

    void Update()
    {
        if (Time.time >= nextCull)
        {
            nextCull = Time.time + 0.5f + Random.value * 0.2f;
            bool near = keepVisibleWhenFar ||
                        Vector3.Distance(PlayerView.Position(), transform.position) < CullDistance;
            if (near != visible)
            {
                visible = near;
                foreach (Renderer r in renderers)
                {
                    if (r != null)
                        r.enabled = near;
                }
            }
        }

        if (!visible)
            return;

        float dt = Time.deltaTime;
        bool walking = false;
        switch (role)
        {
            case Role.Wanderer:
                walking = occupation >= 0 ? TickOccupation(dt) : TickWanderer(dt);
                break;
            case Role.Patrol:
            case Role.Traveler:
                walking = TickPath(dt);
                break;
        }

        Animate(walking, dt);
        AnimateOccupation(walking);
    }

    bool TickWanderer(float dt)
    {
        if (settlement == null)
            return false;

        if (routeIndex >= route.Count)
        {
            if (Time.time < waitUntil)
                return false;
            settlement.PickRoute(transform.position, route);
            routeIndex = 0;
            if (route.Count == 0)
            {
                waitUntil = Time.time + 3f;
                return false;
            }
        }

        if (MoveToward(route[routeIndex], dt))
        {
            routeIndex++;
            if (routeIndex >= route.Count)
                waitUntil = Time.time + Random.Range(3f, 11f);
        }

        return true;
    }

    bool TickPath(float dt)
    {
        if (path == null || path.Length < 2)
            return false;
        if (Time.time < waitUntil)
            return false;

        if (MoveToward(path[pathIndex], dt))
        {
            int next = pathIndex + pathStep;
            if (next < 0 || next >= path.Length)
            {
                pathStep = -pathStep;
                next = pathIndex + pathStep;
                waitUntil = Time.time + Random.Range(role == Role.Patrol ? 2f : 6f, role == Role.Patrol ? 5f : 14f);
            }

            pathIndex = next;
        }

        return true;
    }

    // Returns true on arrival.
    bool MoveToward(Vector3 target, float dt)
    {
        Vector3 p = transform.position;
        Vector3 to = target - p;
        to.y = 0f;
        float distance = to.magnitude;
        if (distance < 0.35f)
            return true;

        Vector3 dir = to / distance;
        Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 240f * dt);

        float step = Mathf.Min(distance, walkSpeed * dt);
        p += dir * step;
        if (snapToGround && WorldGround.TryGetGround(p, out Vector3 g, out _))
            p.y = g.y;
        transform.position = p;
        return false;
    }

    void Animate(bool walking, float dt)
    {
        if (legL == null || legR == null || armL == null || armR == null)
            return;

        float swing = 0f;
        float bob = 0f;
        if (walking)
        {
            phase += dt * walkSpeed * 4.4f;
            swing = Mathf.Sin(phase);
            bob = Mathf.Abs(Mathf.Cos(phase)) * 0.035f;
        }
        else
        {
            phase = Mathf.MoveTowards(phase, Mathf.Round(phase / Mathf.PI) * Mathf.PI, dt * 4f);
        }

        float legAngle = swing * 28f;
        float armAngle = swing * 24f;
        legL.localRotation = Quaternion.Euler(legAngle, 0f, 0f);
        legR.localRotation = Quaternion.Euler(-legAngle, 0f, 0f);

        switch (role)
        {
            case Role.Guard:
                // Spear arm held forward, the other at rest.
                armR.localRotation = Quaternion.Euler(-35f, 0f, 0f);
                armL.localRotation = Quaternion.Euler(0f, 0f, 6f + Mathf.Sin(Time.time * 1.3f) * 1.5f);
                break;
            case Role.Shopkeeper:
                float greeting = Mathf.Clamp01(1f - Vector3.Distance(PlayerView.Position(), transform.position) / 5f);
                armR.localRotation = Quaternion.Euler(-35f * greeting + Mathf.Sin(Time.time * 2f + phase) * 8f * greeting, 0f, 12f * greeting);
                armL.localRotation = Quaternion.Euler(-15f * greeting, 0f, -5f);
                break;
            case Role.Fisher:
                armR.localRotation = Quaternion.Euler(-70f + Mathf.Sin(Time.time * 0.9f) * 4f, 0f, 0f);
                armL.localRotation = Quaternion.Euler(-60f, 0f, -10f);
                break;
            default:
                float breathe = walking ? 0f : Mathf.Sin(Time.time * 1.6f + phase) * 2f;
                armL.localRotation = Quaternion.Euler(-armAngle, 0f, -4f - breathe);
                armR.localRotation = Quaternion.Euler(armAngle, 0f, 4f + breathe);
                break;
        }

        if (body != null)
            body.localPosition = bodyRest + Vector3.up * bob;

        if (head != null)
        {
            lookTimer -= dt;
            if (lookTimer <= 0f)
            {
                lookTimer = Random.Range(1.5f, 4.5f);
                headTarget = walking ? 0f : Random.Range(-45f, 45f);
            }

            headYaw = Mathf.MoveTowards(headYaw, headTarget, 60f * dt);
            head.localRotation = Quaternion.Euler(0f, headYaw, 0f);
        }
    }

    // ------------------------------------------------------------------ construction

    /// <summary>Colours to dress villagers in. Create once and reuse so materials are shared.</summary>
    public class Palette
    {
        public Material[] skin;
        public Material[] cloth;
        public Material[] trousers;
        public Material[] hair;
        public Material dark;
        public Material straw;
        public Material metal;
        public Material wood;
    }

    /// <summary>Builds a villager's body under a new GameObject. Works in the editor and at runtime.</summary>
    public static Villager Create(Transform parent, Vector3 position, Quaternion rotation, Role role,
                                  Palette palette, System.Random rng, string name)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(position, rotation);
        Villager v = root.AddComponent<Villager>();
        v.role = role;
        v.walkSpeed = role == Role.Patrol ? 1.1f : role == Role.Traveler ? 1.5f : 1.15f + (float)rng.NextDouble() * 0.4f;

        Material skin = Pick(palette.skin, rng);
        Material shirt = role == Role.Guard || role == Role.Patrol ? palette.cloth[0] : Pick(palette.cloth, rng);
        Material pants = Pick(palette.trousers, rng);
        Material hair = Pick(palette.hair, rng);
        bool dress = role == Role.Wanderer && rng.NextDouble() < 0.4;
        float height = 0.92f + (float)rng.NextDouble() * 0.14f;

        Transform body = Empty("Body", root.transform, Vector3.zero);
        body.localScale = Vector3.one * height;
        v.body = body;

        // Legs swing from the hips.
        v.legL = Empty("LegL", body, new Vector3(-0.1f, 0.82f, 0f));
        v.legR = Empty("LegR", body, new Vector3(0.1f, 0.82f, 0f));
        foreach (Transform leg in new[] { v.legL, v.legR })
        {
            Part(PrimitiveType.Capsule, leg, new Vector3(0f, -0.4f, 0f), new Vector3(0.15f, 0.4f, 0.15f), pants);
            Part(PrimitiveType.Cube, leg, new Vector3(0f, -0.78f, 0.04f), new Vector3(0.14f, 0.08f, 0.24f), palette.dark);
        }

        // Torso, belt and (sometimes) a skirt.
        Part(PrimitiveType.Capsule, body, new Vector3(0f, 1.15f, 0f), new Vector3(0.42f, 0.34f, 0.3f), shirt);
        Part(PrimitiveType.Cylinder, body, new Vector3(0f, 0.93f, 0f), new Vector3(0.43f, 0.03f, 0.32f), palette.dark);
        if (dress)
            Part(PrimitiveType.Cylinder, body, new Vector3(0f, 0.7f, 0f), new Vector3(0.5f, 0.24f, 0.42f), shirt);

        // Arms swing from the shoulders.
        v.armL = Empty("ArmL", body, new Vector3(-0.27f, 1.42f, 0f));
        v.armR = Empty("ArmR", body, new Vector3(0.27f, 1.42f, 0f));
        foreach (Transform arm in new[] { v.armL, v.armR })
        {
            Part(PrimitiveType.Capsule, arm, new Vector3(0f, -0.27f, 0f), new Vector3(0.12f, 0.28f, 0.12f), shirt);
            Part(PrimitiveType.Sphere, arm, new Vector3(0f, -0.57f, 0f), Vector3.one * 0.1f, skin);
        }

        // Head with eyes, nose and hair or a hat.
        v.head = Empty("Head", body, new Vector3(0f, 1.5f, 0f));
        Part(PrimitiveType.Sphere, v.head, new Vector3(0f, 0.15f, 0f), Vector3.one * 0.28f, skin);
        Part(PrimitiveType.Sphere, v.head, new Vector3(-0.055f, 0.17f, 0.125f), Vector3.one * 0.04f, palette.dark);
        Part(PrimitiveType.Sphere, v.head, new Vector3(0.055f, 0.17f, 0.125f), Vector3.one * 0.04f, palette.dark);
        Part(PrimitiveType.Sphere, v.head, new Vector3(0f, 0.13f, 0.14f), new Vector3(0.05f, 0.06f, 0.05f), skin);

        if (role == Role.Guard || role == Role.Patrol)
        {
            Part(PrimitiveType.Sphere, v.head, new Vector3(0f, 0.21f, -0.01f), new Vector3(0.31f, 0.24f, 0.31f), palette.metal);
            Part(PrimitiveType.Cube, v.head, new Vector3(0f, 0.15f, 0.15f), new Vector3(0.03f, 0.1f, 0.02f), palette.metal);
            // Spear in the right hand.
            Part(PrimitiveType.Cylinder, v.armR, new Vector3(0f, -0.55f, 0.05f), new Vector3(0.035f, 1.1f, 0.035f), palette.wood);
            Part(PrimitiveType.Cube, v.armR, new Vector3(0f, 0.6f, 0.05f), new Vector3(0.06f, 0.18f, 0.02f), palette.metal);
        }
        else
        {
            double look = rng.NextDouble();
            if (look < 0.35)
            {
                // Straw hat.
                Part(PrimitiveType.Cylinder, v.head, new Vector3(0f, 0.25f, 0f), new Vector3(0.46f, 0.01f, 0.46f), palette.straw);
                Part(PrimitiveType.Cylinder, v.head, new Vector3(0f, 0.3f, 0f), new Vector3(0.25f, 0.05f, 0.25f), palette.straw);
            }
            else if (look < 0.55)
            {
                // Hood.
                Part(PrimitiveType.Sphere, v.head, new Vector3(0f, 0.17f, -0.035f), new Vector3(0.32f, 0.31f, 0.31f), Pick(palette.cloth, rng));
            }
            else
            {
                // Hair cap, longer at the back for some.
                Part(PrimitiveType.Sphere, v.head, new Vector3(0f, 0.22f, -0.025f), new Vector3(0.295f, 0.2f, 0.295f), hair);
                if (dress)
                    Part(PrimitiveType.Capsule, v.head, new Vector3(0f, 0.08f, -0.11f), new Vector3(0.22f, 0.14f, 0.1f), hair);
            }

            if (role == Role.Fisher)
                Part(PrimitiveType.Cylinder, v.armR, new Vector3(0f, -0.6f, 0.75f), new Vector3(0.02f, 0.85f, 0.02f), palette.wood,
                     Quaternion.Euler(80f, 0f, 0f));
        }

        return v;
    }

    static Material Pick(Material[] options, System.Random rng)
    {
        return options[rng.Next(options.Length)];
    }

    static Transform Empty(string name, Transform parent, Vector3 localPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }

    static void Part(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material mat,
                     Quaternion? localRotation = null)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        Collider c = part.GetComponent<Collider>();
        if (c != null)
        {
            if (Application.isPlaying)
                Destroy(c);
            else
                DestroyImmediate(c);
        }

        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation ?? Quaternion.identity;
        part.transform.localScale = localScale;
        MeshRenderer renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }
}
