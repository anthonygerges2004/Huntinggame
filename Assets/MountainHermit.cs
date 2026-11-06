using System.Collections.Generic;
using HuntingGame.Wind;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Old hermit on the mountainside — press E nearby for hunting and survival advice.
/// </summary>
public class MountainHermit : MonoBehaviour
{
    public static MountainHermit Instance { get; private set; }
    public static bool HasMetPlayer { get; private set; }
    public static Vector3 SpawnPosition { get; private set; }

    public float talkRange = 3.8f;
    public float hintRange = 6f;

    Villager villager;
    RideableHorse hermitHorse;
    int adviceIndex;

    public static void EnsureSpawn(Vector3 camp)
    {
        MountainHermit live = Instance;
        if (live == null)
            live = Object.FindAnyObjectByType<MountainHermit>();
        if (live != null)
        {
            Instance = live;
            return;
        }

        Instance = null;
        if (camp == Vector3.zero)
            camp = WorldGround.Snap(new Vector3(48f, 0f, 57f));

        Vector3 spot = FindHermitSpot(camp);
        spot = WorldGround.Sit(spot, 0.05f);
        SpawnPosition = spot;

        GameObject root = new GameObject("MountainHermit");
        MountainHermit hermit = root.AddComponent<MountainHermit>();
        hermit.Build(spot, camp);
        Instance = hermit;

        float flatDist = FlatDistance(spot, camp);
        Debug.Log("Mountain hermit at " + spot + " — about " + Mathf.RoundToInt(flatDist) +
                  " m uphill from camp. Look for the tall gold post and smoke.");

        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(
                "Hermit ~" + Mathf.RoundToInt(flatDist) + " m uphill from camp — follow the compass (HERMIT).");
    }

    static Vector3 FindHermitSpot(Vector3 camp)
    {
        Vector3 uphill = Vector3.forward;
        if (WorldGround.TryGetGround(camp, out _, out Vector3 slopeNormal))
        {
            uphill = new Vector3(slopeNormal.x, 0f, slopeNormal.z);
            if (uphill.sqrMagnitude > 0.01f)
                uphill.Normalize();
        }

        Vector3 across = Vector3.Cross(Vector3.up, uphill);
        float campHeight = SampleHeight(camp);
        Vector3 best = Vector3.zero;
        float bestScore = float.MinValue;

        float[] distances = { 72f, 95f, 118f, 140f };
        for (int d = 0; d < distances.Length; d++)
        {
            for (int side = -1; side <= 1; side++)
            {
                Vector3 p = camp + uphill * distances[d] + across * (side * 18f);
                p = WorldGround.Snap(p);
                if (WaterBodies.InsideWater(p))
                    continue;

                float h = SampleHeight(p);
                if (h < campHeight + 4f)
                    continue;

                float score = h - distances[d] * 0.02f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }
        }

        if (best != Vector3.zero)
            return best;

        return WorldGround.Snap(camp + uphill * 95f);
    }

    static float SampleHeight(Vector3 p)
    {
        Terrain t = WorldGround.TerrainAt(p);
        if (t == null)
            return p.y;
        return t.SampleHeight(p) + t.transform.position.y;
    }

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        Vector3 d = a - b;
        d.y = 0f;
        return d.magnitude;
    }

    public static float DistanceFromPlayer()
    {
        if (Instance == null)
            return float.MaxValue;
        Transform player = PlayerView.Body();
        if (player == null)
            return float.MaxValue;
        return FlatDistance(player.position, Instance.transform.position);
    }

    public static float BearingFromPlayer()
    {
        if (Instance == null)
            return 0f;
        Transform player = PlayerView.Body();
        if (player == null)
            return 0f;
        Vector3 d = Instance.transform.position - player.position;
        d.y = 0f;
        if (d.sqrMagnitude < 0.01f)
            return 0f;
        return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            HasMetPlayer = false;
        }
    }

    void Build(Vector3 position, Vector3 lookAt)
    {
        transform.position = position;
        Vector3 flat = lookAt - position;
        flat.y = 0f;
        if (flat.sqrMagnitude > 0.1f)
            transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);

        System.Random rng = new System.Random(5192);
        Villager.Palette palette = BuildPalette();
        villager = Villager.Create(transform, position, transform.rotation, Villager.Role.Wanderer, palette, rng, "Old Hermit");
        villager.walkSpeed = 0f;
        villager.snapToGround = true;
        villager.keepVisibleWhenFar = true;

        if (villager.body != null)
        {
            villager.body.localScale = Vector3.one * 0.9f;
            villager.body.localRotation = Quaternion.Euler(14f, 0f, 0f);
        }

        if (villager.head != null)
        {
            Material grey = palette.hair[0];
            GameObject beard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(beard.GetComponent<Collider>());
            beard.transform.SetParent(villager.head, false);
            beard.transform.localPosition = new Vector3(0f, 0.02f, 0.12f);
            beard.transform.localScale = new Vector3(0.14f, 0.16f, 0.08f);
            beard.GetComponent<Renderer>().sharedMaterial = grey;
        }

        if (villager.armL != null)
        {
            Material wood = palette.wood;
            GameObject staff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(staff.GetComponent<Collider>());
            staff.transform.SetParent(villager.armL, false);
            staff.transform.localPosition = new Vector3(0f, -0.35f, 0.12f);
            staff.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            staff.transform.localScale = new Vector3(0.04f, 0.55f, 0.04f);
            staff.GetComponent<Renderer>().sharedMaterial = wood;
        }

        SpawnHorse(position);
        BuildTrailMarker(position);
    }

    void BuildTrailMarker(Vector3 ground)
    {
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "HermitTrailMarker";
        Object.Destroy(pole.GetComponent<Collider>());
        pole.transform.SetParent(transform, false);
        pole.transform.position = ground + Vector3.up * 5f;
        pole.transform.localScale = new Vector3(0.35f, 5f, 0.35f);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.92f, 0.72f, 0.22f);
        pole.GetComponent<Renderer>().sharedMaterial = mat;

        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(top.GetComponent<Collider>());
        top.name = "HermitMarkerLight";
        top.transform.SetParent(pole.transform, false);
        top.transform.localPosition = new Vector3(0f, 1.05f, 0f);
        top.transform.localScale = Vector3.one * 0.55f;
        top.GetComponent<Renderer>().sharedMaterial = mat;

        Light glow = top.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.range = 28f;
        glow.intensity = 1.6f;
        glow.color = new Color(1f, 0.82f, 0.45f);

        GameObject smokeRoot = new GameObject("HermitSmoke");
        smokeRoot.transform.SetParent(transform, false);
        smokeRoot.transform.position = ground + Vector3.up * 2.2f;
        ParticleSystem ps = smokeRoot.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = 4f;
        main.startSpeed = 0.6f;
        main.startSize = 0.9f;
        main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        emission.rateOverTime = 6f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;
    }

    void SpawnHorse(Vector3 hermitPos)
    {
        Vector3 horsePos = hermitPos + transform.right * 2.4f + transform.forward * -1.8f;
        horsePos = WorldGround.Snap(horsePos);
        GameObject horse = RideableHorse.SpawnByPath(horsePos, 1.7f, null);
        if (horse == null)
            return;

        horse.name = "HermitHorse";
        horse.transform.rotation = transform.rotation;
        hermitHorse = horse.GetComponent<RideableHorse>();
        if (hermitHorse != null)
            hermitHorse.ConfigureHermitSteed();

        AmbientCreature graze = horse.GetComponent<AmbientCreature>();
        if (graze != null)
        {
            graze.home = horsePos;
            graze.radius = 2f;
        }
    }

    static Villager.Palette BuildPalette()
    {
        return new Villager.Palette
        {
            skin = new[] { Mat("hermit_skin", new Color(0.78f, 0.62f, 0.50f)) },
            cloth = new[] { Mat("hermit_robe", new Color(0.42f, 0.40f, 0.36f)) },
            trousers = new[] { Mat("hermit_pants", new Color(0.32f, 0.30f, 0.28f)) },
            hair = new[] { Mat("hermit_hair", new Color(0.72f, 0.72f, 0.70f)) },
            dark = Mat("hermit_dark", new Color(0.12f, 0.10f, 0.09f)),
            straw = Mat("hermit_straw", new Color(0.55f, 0.50f, 0.42f)),
            metal = Mat("hermit_metal", new Color(0.45f, 0.45f, 0.48f)),
            wood = Mat("hermit_wood", new Color(0.38f, 0.26f, 0.14f)),
        };
    }

    static Material Mat(string name, Color color)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.name = name;
        mat.color = color;
        return mat;
    }

    public static bool InTalkRange()
    {
        return Instance != null && Instance.IsPlayerNear(Instance.talkRange);
    }

    public static bool TryTalk()
    {
        if (Instance == null || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame)
            return false;
        if (Inventory.IsOpen || BirdChallenge.Active || RideableHorse.IsRiding)
            return false;
        if (!Instance.IsPlayerNear(Instance.talkRange))
            return false;

        Instance.Speak();
        return true;
    }

    bool IsPlayerNear(float range)
    {
        Transform player = PlayerView.Body();
        if (player == null)
            return false;
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= range * range;
    }

    void Speak()
    {
        HasMetPlayer = true;
        string line = PickAdvice();
        adviceIndex++;
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice("Hermit: " + line);
        else
            Debug.Log("Hermit: " + line);

        Transform player = PlayerView.Body();
        if (player != null && villager != null)
        {
            Vector3 look = player.position - villager.transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
            {
                Quaternion face = Quaternion.LookRotation(look.normalized, Vector3.up);
                villager.transform.rotation = Quaternion.Slerp(villager.transform.rotation, face, 0.35f);
            }
        }
    }

    string PickAdvice()
    {
        var tips = new List<string>();

        if (WindSystem.Current != null)
            tips.Add("Watch the smoke at camp — wind carries your scent. Circle downwind before you shoot.");

        if (DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight)
            tips.Add("At night, spoiled meat draws the big hunters. Keep your fire between you and the bait.");

        if (VillageReputation.Trust <= -10)
            tips.Add("The village is nervous. Drop bait outside the fence, not on their doorsteps.");
        else if (VillageReputation.Trust >= 20)
            tips.Add("The folk remember kindness. They will not forget who guarded their pens.");

        if (HuntCampBoard.Instance != null && !HuntCampBoard.Instance.DailyDone)
            tips.Add("Read the camp board each day — one strange rule, but it teaches a hunter's habit.");

        tips.Add("Deer cross the river gaps at dawn and dusk. Wait there if you want a clean shot.");
        tips.Add("Crouch in grass. The birds may still see you, but deer hear your boots first.");
        tips.Add("Cook your meat at the fire. A full belly steadies the hands.");
        tips.Add("My old horse beside me is fast — borrow him, but dismount before you shoot.");
        tips.Add("Press E on a village horse when you must cross ground fast — dismount before you shoot.");
        tips.Add("Tracks lie after rain. Fresh mud by the gap rivers is worth a slow walk.");

        int pick = adviceIndex % tips.Count;
        if (tips.Count > 1)
            pick = (adviceIndex + Random.Range(0, tips.Count)) % tips.Count;
        return tips[pick];
    }

    void OnGUI()
    {
        if (Inventory.IsOpen || BirdChallenge.Active)
            return;

        if (!HasMetPlayer)
        {
            float dist = DistanceFromPlayer();
            if (dist < 220f && dist > talkRange)
            {
                float y = Screen.height * 0.5f + 48f * ScreenText.Scale;
                ScreenText.Centered("Hermit ahead — " + Mathf.RoundToInt(dist) + " m (gold post + smoke)", y, 20);
            }
        }

        if (!IsPlayerNear(hintRange))
            return;

        float talkY = Screen.height * 0.5f + 72f * ScreenText.Scale;
        ScreenText.Centered("Press E — ask the hermit", talkY, 22);
    }
}
