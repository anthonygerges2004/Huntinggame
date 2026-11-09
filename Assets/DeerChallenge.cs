using System.Collections;
using System.Collections.Generic;
using HuntingGame.Animals;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Stand challenge, same shape as the bird cage: walk up, press Y, shoot from the post.
/// Two deer, sniper only, head or chest, at least 100 m. Limited shots and time.
/// </summary>
public class DeerChallenge : MonoBehaviour
{
    public static DeerChallenge Instance;
    public static bool Active { get; private set; }

    /// <summary>Where the sniper post stands, for the compass.</summary>
    public static bool TryGetPost(out Vector3 point)
    {
        point = Instance != null ? Instance.standPoint : Vector3.zero;
        return Instance != null && Instance.standPoint != Vector3.zero;
    }

    public int deerToWin = 2;
    public int maxShots = 10;
    public float timeLimit = 90f;
    public float minDistance = 100f;
    public int rewardMeat = 4;
    public int rewardWood = 4;
    public float useRange = 3.5f;

    Transform player;
    CharacterController controller;
    Vector3 standPoint;
    Vector3 lookDir;
    Vector3 returnPoint;
    readonly List<Animal> targets = new List<Animal>();
    bool ready;
    int kills;
    int shots;
    float timeLeft;
    float lastShotTime;

    void Awake()
    {
        Instance = this;
        Active = false;
    }

    IEnumerator Start()
    {
        yield return null;
        yield return null;
        yield return null;

        PlayerMove move = FindAnyObjectByType<PlayerMove>();
        if (move == null)
            yield break;

        player = move.transform;
        controller = move.GetComponent<CharacterController>();

        GameObject tent = GameObject.Find("FCP_Tent_Type1_Color1");
        Vector3 camp = WorldGround.Snap(tent != null ? tent.transform.position : player.position);
        PlaceStand(camp);
        ready = true;
    }

    void OnDestroy()
    {
        if (Active)
            PlayerMove.Frozen = false;
        Active = false;
    }

    void Update()
    {
        if (!ready || player == null || Keyboard.current == null)
            return;

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
        {
            if (Active)
                Finish(false, "");
            return;
        }

        if (Keyboard.current.yKey.wasPressedThisFrame && !Inventory.IsOpen)
        {
            if (Active)
            {
                Finish(false, "Challenge stopped.");
                return;
            }

            if (NearStand() && !WoodBuilder.IsBuilding && !BirdChallenge.Active)
            {
                Begin();
                return;
            }
        }

        if (!Active)
            return;

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            Finish(false, "Time's up!");
            return;
        }

        if (shots >= maxShots && kills < deerToWin && Time.time > lastShotTime + 0.45f)
            Finish(false, "Out of shots!");
    }

    public static bool AllowFire()
    {
        if (Instance == null || !Active)
            return true;

        return Instance.shots < Instance.maxShots;
    }

    public static void CountShot()
    {
        if (Instance == null || !Active)
            return;

        Instance.shots++;
        Instance.lastShotTime = Time.time;
    }

    public static void NotifyKill(Animal animal, bool vital, float distance)
    {
        if (Instance == null || !Active || animal == null || animal.kind != AnimalKind.Deer)
            return;
        if (!Instance.IsTarget(animal))
            return;

        Instance.targets.Remove(animal);
        if (vital && distance >= Instance.minDistance)
        {
            Instance.kills++;
            if (Instance.kills >= Instance.deerToWin)
            {
                Instance.Finish(true, "");
                return;
            }

            if (Inventory.Instance != null)
                Inventory.Instance.ShowNotice("Clean hit. One more deer.");
        }
        else if (Inventory.Instance != null)
        {
            Inventory.Instance.ShowNotice(vital
                ? "Too close. They have to be " + Mathf.RoundToInt(Instance.minDistance) + " m out."
                : "Head or chest, with the sniper.");
        }

        if (Active)
            Instance.SpawnDeer(110f + Instance.targets.Count * 20f);
    }

    bool IsTarget(Animal animal)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == animal)
                return true;
        }

        return false;
    }

    bool NearStand()
    {
        return Flat(player.position - standPoint).magnitude <= useRange;
    }

    void Begin()
    {
        Active = true;
        kills = 0;
        shots = 0;
        timeLeft = timeLimit;
        returnPoint = player.position;

        Teleport(standPoint + Vector3.up * 0.1f);
        if (lookDir.sqrMagnitude > 0.01f)
            player.rotation = Quaternion.LookRotation(lookDir);
        PlayerMove.Frozen = true;

        if (Inventory.Instance != null)
        {
            int key = Inventory.Instance.HotbarKeyFor(HuntWeapon.Sniper);
            if (key > 0)
                Inventory.Instance.SelectHotbar(key - 1, true);
        }

        ClearTargets();
        SpawnDeer(110f);
        SpawnDeer(130f);
        SpawnDeer(150f);

        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice("Sniper. Head or chest. They are past 100 m.");
    }

    void Finish(bool win, string message)
    {
        Active = false;
        PlayerMove.Frozen = false;
        ClearTargets();

        if (PlayerHealth.Instance == null || !PlayerHealth.Instance.isDead)
            Teleport(returnPoint);

        if (Inventory.Instance == null)
            return;

        if (win)
        {
            HuntChallenges.CompleteSniperChallenge(minDistance);
            Inventory.Instance.AddMeat(rewardMeat);
            Inventory.Instance.AddWood(rewardWood);
            Inventory.Instance.ShowNotice("Sniper challenge won!  +" + rewardMeat + " meat  +" + rewardWood + " wood");
        }
        else if (message.Length > 0)
        {
            Inventory.Instance.ShowNotice(message + "  Press Y at the stand to try again.");
        }
    }

    void SpawnDeer(float distance)
    {
        Vector3 spot = WorldGround.Snap(standPoint + lookDir * distance);
        if (WorldGround.InsideWater(spot))
            return;

        GameObject body = new GameObject("ChallengeDeer");
        body.transform.position = spot;
        Vector3 side = Vector3.Cross(Vector3.up, lookDir);
        if (side.sqrMagnitude < 0.001f)
            side = Vector3.right;
        body.transform.rotation = Quaternion.LookRotation(side.normalized);

        body.AddComponent<CapsuleCollider>();
        Animal animal = body.AddComponent<Animal>();
        animal.kind = AnimalKind.Deer;
        animal.meatAmount = 2;
        targets.Add(animal);
        StartCoroutine(HoldStill(animal));
    }

    IEnumerator HoldStill(Animal animal)
    {
        yield return null;
        if (animal == null)
            yield break;

        AnimalAiAgent agent = animal.GetComponent<AnimalAiAgent>();
        if (agent != null)
            agent.enabled = false;
        animal.SetSuspendSelfTick(true);

        Vector3 side = Vector3.Cross(Vector3.up, lookDir);
        if (side.sqrMagnitude > 0.001f)
            animal.transform.rotation = Quaternion.LookRotation(side.normalized);
    }

    void ClearTargets()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
                Destroy(targets[i].gameObject);
        }

        targets.Clear();
    }

    void PlaceStand(Vector3 camp)
    {
        standPoint = WorldGround.Snap(camp + Vector3.forward * 28f);
        lookDir = Vector3.forward;
        float best = -1f;

        for (int i = 0; i < 12; i++)
        {
            float rad = i * 30f * Mathf.Deg2Rad;
            Vector3 outward = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            Vector3 stand = WorldGround.Snap(camp + outward * 28f);
            if (!WorldGround.IsMeadow(stand) || WorldGround.InsideWater(stand))
                continue;
            if (!LaneClear(stand, outward))
                continue;

            float score = 1f - Mathf.Abs(i - 6) * 0.05f;
            if (score > best)
            {
                best = score;
                standPoint = stand;
                lookDir = outward;
            }
        }

        BuildMarker(standPoint);
    }

    bool LaneClear(Vector3 stand, Vector3 look)
    {
        for (int i = 0; i < 3; i++)
        {
            Vector3 spot = WorldGround.Snap(stand + look * (110f + i * 20f));
            if (WorldGround.InsideWater(spot))
                return false;
            if (Mathf.Abs(spot.y - stand.y) > 28f)
                return false;
        }

        return true;
    }

    void BuildMarker(Vector3 point)
    {
        if (GameObject.Find("DeerChallengeStand") != null)
            return;

        GameObject root = new GameObject("DeerChallengeStand");
        root.transform.position = point;
        root.transform.rotation = Quaternion.LookRotation(lookDir);
        Post(root.transform, new Vector3(-0.7f, 0.7f, 0f), new Vector3(0.12f, 1.4f, 0.12f));
        Post(root.transform, new Vector3(0.7f, 0.7f, 0f), new Vector3(0.12f, 1.4f, 0.12f));
        Post(root.transform, new Vector3(0f, 1.35f, 0f), new Vector3(1.7f, 0.08f, 0.12f));
    }

    static void Post(Transform parent, Vector3 localPos, Vector3 scale)
    {
        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = "Post";
        post.transform.SetParent(parent, false);
        post.transform.localPosition = localPos;
        post.transform.localScale = scale;
        Collider solid = post.GetComponent<Collider>();
        if (solid != null)
            Destroy(solid);
    }

    void Teleport(Vector3 position)
    {
        if (controller != null)
            controller.enabled = false;
        player.position = position;
        Physics.SyncTransforms();
        if (controller != null)
            controller.enabled = true;
    }

    static Vector3 Flat(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    void OnGUI()
    {
        if (!ready || player == null)
            return;

        float scale = ScreenText.Scale;
        if (Active)
        {
            string status = "Deer " + kills + "/" + deerToWin +
                            "     Time " + Mathf.CeilToInt(timeLeft) +
                            "     Shots left " + (maxShots - shots);
            ScreenText.Centered(status, 40f * scale, 26);
            ScreenText.Centered("Sniper  ·  head or chest  ·  100 m", 72f * scale, 18);
            ScreenText.Centered("Press Y to quit", Screen.height - 40f * scale, 18);
            return;
        }

        if (NearStand() && !BirdChallenge.Active)
        {
            ScreenText.Centered("Press Y to start the Sniper Challenge", Screen.height * 0.5f + 60f * scale);
            ScreenText.Centered(deerToWin + " deer, head or chest, 100 m  -  " +
                                Mathf.RoundToInt(timeLimit) + " seconds  -  " + maxShots + " shots",
                Screen.height * 0.5f + 95f * scale, 18);
        }
    }
}
