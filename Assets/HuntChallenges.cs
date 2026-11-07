using UnityEngine;

public class HuntChallenges : MonoBehaviour
{
    public static HuntChallenges Instance;

    public float longShotDistance = 20f;
    public float treeShadeRadius = 4f;

    bool shadeBird;
    bool longShot;
    bool quietDeer;
    bool sniperVital;

    public bool ShadeBirdDone => shadeBird;
    public bool LongShotDone => longShot;
    public bool QuietDeerDone => quietDeer;
    public bool SniperVitalDone => sniperVital;
    Transform player;

    void Awake()
    {
        Instance = this;
        if (GetComponent<HuntCampBoard>() == null)
            gameObject.AddComponent<HuntCampBoard>();
    }

    public static void ReportKill(Animal animal)
    {
        if (Instance != null && animal != null)
            Instance.CheckKill(animal);
    }

    public static void CompleteSniperChallenge(float distance)
    {
        if (Instance == null || Instance.sniperVital)
            return;

        Instance.sniperVital = true;
        Instance.Announce("Sniper vitals, " + Mathf.RoundToInt(distance) + " m");
    }

    void CheckKill(Animal animal)
    {
        if (player == null)
        {
            PlayerMove move = FindAnyObjectByType<PlayerMove>();
            if (move == null)
                return;
            player = move.transform;
        }

        float distance = Vector3.Distance(player.position, animal.transform.position);

        if (!shadeBird && animal.kind == AnimalKind.Bird && PlayerUnderTree())
        {
            shadeBird = true;
            Announce("Shade bird");
        }

        if (!longShot && distance >= longShotDistance)
        {
            longShot = true;
            Announce("Long shot");
        }

        if (!quietDeer && animal.kind == AnimalKind.Deer && PlayerMove.IsCrouching)
        {
            quietDeer = true;
            Announce("Quiet deer");
        }

        HuntCampBoard.TryCompleteDaily(animal);
    }

    bool PlayerUnderTree()
    {
        HarvestableTree[] trees = FindObjectsByType<HarvestableTree>(FindObjectsInactive.Exclude);
        foreach (HarvestableTree tree in trees)
        {
            Vector3 offset = tree.transform.position - player.position;
            offset.y = 0f;
            if (offset.magnitude <= treeShadeRadius)
                return true;
        }
        return false;
    }

    void Announce(string challenge)
    {
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice("Challenge complete: " + challenge + "!");
    }

}