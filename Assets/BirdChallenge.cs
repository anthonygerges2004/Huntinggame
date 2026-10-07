using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BirdChallenge : MonoBehaviour
{
    public static BirdChallenge Instance;
    public static bool Active { get; private set; }
    public static bool Won { get; private set; }

    public int birdsToWin = 5;
    public int maxShots = 8;
    public float timeLimit = 60f;
    public int rewardMeat = 5;
    public int rewardWood = 5;
    public float useRange = 3f;
    public int birdsAtOnce = 3;
    public int poleCount = 6;

    static Material rockMat;
    static Material rockDarkMat;
    static Material flagMat;

    Transform player;
    CharacterController controller;
    Vector3 cagePoint;
    Vector3 seatPoint;
    Vector3 returnPoint;
    readonly List<Vector3> polePerches = new List<Vector3>();
    readonly List<Vector3> perches = new List<Vector3>();
    readonly List<ChallengeBird> birds = new List<ChallengeBird>();
    bool ready;
    int kills;
    int shots;
    float timeLeft;
    float respawnTimer;

    void Awake()
    {
        Instance = this;
        Active = false;
        Won = false;
        PlayerMove.Frozen = false;
    }

    IEnumerator Start()
    {
        yield return null;
        yield return null;

        PlayerMove move = FindAnyObjectByType<PlayerMove>();
        if (move == null)
            yield break;

        player = move.transform;
        controller = move.GetComponent<CharacterController>();

        GameObject tent = GameObject.Find("FCP_Tent_Type1_Color1");
        Vector3 camp = WorldGround.Snap(tent != null ? tent.transform.position : player.position);

        cagePoint = FindSpot(camp, 12f, Vector3.zero);
        Vector3 awayFromCamp = Flat(cagePoint - camp).normalized;
        if (awayFromCamp.sqrMagnitude < 0.01f)
            awayFromCamp = Vector3.forward;

        BuildCage(cagePoint, camp);

        Vector3 rockPoint = FindSpot(cagePoint, 5f, awayFromCamp);
        seatPoint = BuildRock(rockPoint);

        BuildPoles(awayFromCamp);
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

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            if (Active)
            {
                Finish(false, "Challenge stopped.");
                return;
            }

            if (PlayerNearCage() && !WoodBuilder.IsBuilding)
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

        if (shots >= maxShots && kills < birdsToWin)
        {
            Finish(false, "Out of shots!");
            return;
        }

        birds.RemoveAll(b => b == null);
        if (birds.Count < birdsAtOnce)
        {
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f)
            {
                SpawnBird();
                respawnTimer = 1.2f;
            }
        }
    }

    bool PlayerNearCage()
    {
        return Flat(player.position - cagePoint).magnitude <= useRange;
    }

    void Begin()
    {
        Active = true;
        kills = 0;
        shots = 0;
        timeLeft = timeLimit;
        respawnTimer = 0f;
        returnPoint = player.position;

        CollectPerches();

        Vector3 center = Vector3.zero;
        foreach (Vector3 p in perches)
            center += p;
        center /= perches.Count;

        Teleport(seatPoint + Vector3.up * 1.05f);
        Vector3 look = Flat(center - seatPoint);
        if (look.sqrMagnitude > 0.01f)
            player.rotation = Quaternion.LookRotation(look);
        PlayerMove.Frozen = true;

        for (int i = 0; i < birdsAtOnce; i++)
            SpawnBird();
    }

    void Finish(bool win, string message)
    {
        Active = false;
        PlayerMove.Frozen = false;

        foreach (ChallengeBird bird in birds)
        {
            if (bird != null)
                Destroy(bird.gameObject);
        }
        birds.Clear();

        if (PlayerHealth.Instance == null || !PlayerHealth.Instance.isDead)
            Teleport(returnPoint);

        if (Inventory.Instance == null)
            return;

        if (win && !Won)
        {
            Won = true;
            Inventory.Instance.AddMeat(rewardMeat);
            Inventory.Instance.AddWood(rewardWood);
            Inventory.Instance.ShowNotice("Bird challenge won!  +" + rewardMeat + " meat  +" + rewardWood + " wood");
        }
        else if (win)
        {
            Inventory.Instance.ShowNotice("Bird challenge won again!");
        }
        else if (message.Length > 0)
        {
            Inventory.Instance.ShowNotice(message + "  Press T at the cage to try again.");
        }
    }

    public static bool TryUseShot()
    {
        if (Instance == null || !Active || Instance.shots >= Instance.maxShots)
            return false;

        Instance.shots++;
        return true;
    }

    public void BirdKilled(ChallengeBird bird)
    {
        if (!Active)
            return;

        birds.Remove(bird);
        kills++;
        if (kills >= birdsToWin)
            Finish(true, "");
        else
            respawnTimer = 1.2f;
    }

    public void RequestHop(ChallengeBird bird)
    {
        int index = FreePerch(bird.PerchIndex);
        if (index >= 0)
            bird.FlyTo(index, perches[index]);
        else
            bird.FlyTo(bird.PerchIndex, perches[bird.PerchIndex]);
    }

    void SpawnBird()
    {
        int index = FreePerch(-1);
        if (index < 0)
            return;

        birds.Add(ChallengeBird.Spawn(this, index, perches[index], seatPoint + Vector3.up * 1.6f));
    }

    int FreePerch(int except)
    {
        List<int> free = new List<int>();
        for (int i = 0; i < perches.Count; i++)
        {
            if (i == except)
                continue;

            bool taken = false;
            foreach (ChallengeBird bird in birds)
            {
                if (bird != null && bird.PerchIndex == i)
                {
                    taken = true;
                    break;
                }
            }

            if (!taken)
                free.Add(i);
        }

        return free.Count == 0 ? -1 : free[Random.Range(0, free.Count)];
    }

    void CollectPerches()
    {
        perches.Clear();
        perches.AddRange(polePerches);

        List<HarvestableTree> trees = new List<HarvestableTree>();
        foreach (HarvestableTree tree in FindObjectsByType<HarvestableTree>(FindObjectsInactive.Exclude))
        {
            float d = Flat(tree.transform.position - seatPoint).magnitude;
            if (d >= 10f && d <= 45f)
                trees.Add(tree);
        }

        trees.Sort((a, b) =>
            Flat(a.transform.position - seatPoint).sqrMagnitude.CompareTo(
                Flat(b.transform.position - seatPoint).sqrMagnitude));

        int added = 0;
        foreach (HarvestableTree tree in trees)
        {
            if (added >= 6)
                break;
            if (TryTreePerch(tree, out Vector3 perch))
            {
                perches.Add(perch);
                added++;
            }
        }
    }

    bool TryTreePerch(HarvestableTree tree, out Vector3 perch)
    {
        perch = Vector3.zero;
        bool hasBounds = false;
        Bounds bounds = new Bounds();
        foreach (Renderer r in tree.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled)
                continue;
            if (hasBounds)
                bounds.Encapsulate(r.bounds);
            else
                bounds = r.bounds;
            hasBounds = true;
        }

        if (!hasBounds)
            return false;

        Vector3 trunk = tree.transform.position;
        float height = bounds.max.y - trunk.y;
        if (height < 3f)
            return false;

        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z);
        Vector3 toSeat = Flat(seatPoint - trunk).normalized;
        perch = trunk + toSeat * radius * 0.7f + Vector3.up * height * 0.55f;
        return true;
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

    static Vector3 FindSpot(Vector3 from, float distance, Vector3 preferred)
    {
        Vector3 fallback = WorldGround.Snap(from + (preferred.sqrMagnitude > 0.01f ? preferred : Vector3.forward) * distance);
        float startAngle = preferred.sqrMagnitude > 0.01f ? Mathf.Atan2(preferred.z, preferred.x) : 0f;

        for (int i = 0; i < 12; i++)
        {
            float step = (i % 2 == 0 ? 1f : -1f) * ((i + 1) / 2) * 30f * Mathf.Deg2Rad;
            float angle = startAngle + step;
            Vector3 candidate = WorldGround.Snap(from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance);
            if (WorldGround.IsMeadow(candidate))
                return candidate;
        }

        return fallback;
    }

    void BuildPoles(Vector3 forward)
    {
        Material log = Resources.Load<Material>("Building/LogMat");
        GameObject root = new GameObject("BirdChallengePerches");
        float baseAngle = Mathf.Atan2(forward.z, forward.x);

        for (int i = 0; i < poleCount; i++)
        {
            float spread = poleCount <= 1 ? 0f : Mathf.Lerp(-70f, 70f, i / (float)(poleCount - 1));
            float angle = baseAngle + spread * Mathf.Deg2Rad;
            float radius = 15f + (i % 3) * 3.5f;
            Vector3 ground = WorldGround.Snap(seatPoint + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            float height = 4.5f + (i % 2) * 1.5f;

            Transform pole = new GameObject("Perch").transform;
            pole.SetParent(root.transform, false);
            pole.position = ground;
            pole.rotation = Quaternion.LookRotation(Flat(seatPoint - ground).normalized + Vector3.up * 0.0001f);

            Part(pole, PrimitiveType.Cylinder, log, new Vector3(0f, height * 0.5f, 0f), Quaternion.identity, new Vector3(0.32f, height * 0.5f, 0.32f));
            Part(pole, PrimitiveType.Cylinder, log, new Vector3(0f, height - 0.3f, 0.6f), Quaternion.Euler(90f, 0f, 0f), new Vector3(0.14f, 0.7f, 0.14f));
            Part(pole, PrimitiveType.Cylinder, log, new Vector3(0f, height * 0.65f, -0.4f), Quaternion.Euler(-60f, 0f, 0f), new Vector3(0.1f, 0.5f, 0.1f));

            polePerches.Add(pole.TransformPoint(new Vector3(0f, height - 0.18f, 1f)));
        }
    }

    void BuildCage(Vector3 position, Vector3 camp)
    {
        Material log = Resources.Load<Material>("Building/LogMat");
        Material plank = Resources.Load<Material>("Building/PlankMat");
        CreateMaterials();

        Transform cage = new GameObject("BirdChallengeCage").transform;
        cage.position = position;
        Vector3 face = Flat(camp - position);
        if (face.sqrMagnitude > 0.01f)
            cage.rotation = Quaternion.LookRotation(face);

        BoxCollider box = cage.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.85f, 0f);
        box.size = new Vector3(1.6f, 1.7f, 1.6f);

        Part(cage, PrimitiveType.Cube, plank, new Vector3(0f, 0.08f, 0f), Quaternion.identity, new Vector3(1.6f, 0.16f, 1.6f));
        Part(cage, PrimitiveType.Cube, plank, new Vector3(0f, 1.65f, 0f), Quaternion.identity, new Vector3(1.7f, 0.12f, 1.7f));

        for (int side = 0; side < 4; side++)
        {
            Quaternion turn = Quaternion.Euler(0f, side * 90f, 0f);
            for (int b = 0; b < 6; b++)
            {
                float x = Mathf.Lerp(-0.72f, 0.72f, b / 5f);
                float thick = b == 0 || b == 5 ? 0.12f : 0.06f;
                Part(cage, PrimitiveType.Cylinder, log, turn * new Vector3(x, 0.86f, 0.72f), Quaternion.identity, new Vector3(thick, 0.72f, thick));
            }
        }

        Transform inside = new GameObject("CagedBird").transform;
        inside.SetParent(cage, false);
        inside.localPosition = new Vector3(0f, 0.16f, 0f);
        ChallengeBird.BuildVisual(inside, out _, out _);

        Part(cage, PrimitiveType.Cylinder, log, new Vector3(0.75f, 2.4f, 0.75f), Quaternion.identity, new Vector3(0.07f, 0.8f, 0.07f));
        Part(cage, PrimitiveType.Cube, flagMat, new Vector3(0.75f, 2.95f, 1.2f), Quaternion.identity, new Vector3(0.04f, 0.45f, 0.85f));
    }

    Vector3 BuildRock(Vector3 position)
    {
        CreateMaterials();

        Transform rock = new GameObject("BirdChallengeRock").transform;
        rock.position = position - Vector3.up * 0.15f;

        BoxCollider box = rock.gameObject.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 0.55f, 0f);
        box.size = new Vector3(2f, 1.1f, 1.8f);

        Part(rock, PrimitiveType.Sphere, rockMat, new Vector3(0f, 0.5f, 0f), Quaternion.identity, new Vector3(2.3f, 1.2f, 2.1f));
        Part(rock, PrimitiveType.Sphere, rockDarkMat, new Vector3(0.7f, 0.45f, 0.4f), Quaternion.Euler(0f, 30f, 10f), new Vector3(1.3f, 0.9f, 1.4f));
        Part(rock, PrimitiveType.Sphere, rockDarkMat, new Vector3(-0.7f, 0.35f, -0.5f), Quaternion.Euler(0f, -20f, -8f), new Vector3(1.2f, 0.7f, 1.0f));

        return rock.position + Vector3.up * 1.1f;
    }

    static void CreateMaterials()
    {
        if (rockMat != null)
            return;

        rockMat = MakeMaterial(new Color(0.52f, 0.52f, 0.5f));
        rockDarkMat = MakeMaterial(new Color(0.42f, 0.42f, 0.4f));
        flagMat = MakeMaterial(new Color(1f, 0.82f, 0.15f));
    }

    static Material MakeMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        return mat;
    }

    static void Part(Transform parent, PrimitiveType type, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        part.transform.localRotation = rot;
        part.transform.localScale = scale;
        if (mat != null)
            part.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    void OnGUI()
    {
        if (!ready || player == null)
            return;

        float scale = ScreenText.Scale;

        if (Active)
        {
            string status = "Birds " + kills + "/" + birdsToWin +
                            "     Time " + Mathf.CeilToInt(timeLeft) +
                            "     Shots left " + (maxShots - shots);
            ScreenText.Centered(status, 40f * scale, 26);
            ScreenText.Centered("Press T to quit", Screen.height - 40f * scale, 18);
            return;
        }

        if (PlayerNearCage())
        {
            ScreenText.Centered("Press T to start the Bird Challenge", Screen.height * 0.5f + 60f * scale);
            ScreenText.Centered(birdsToWin + " birds  -  " + Mathf.RoundToInt(timeLimit) + " seconds  -  " + maxShots + " shots", Screen.height * 0.5f + 95f * scale, 18);
        }
    }
}
