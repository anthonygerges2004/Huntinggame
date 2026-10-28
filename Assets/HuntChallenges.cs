using UnityEngine;

public class HuntChallenges : MonoBehaviour
{
    public static HuntChallenges Instance;

    public float longShotDistance = 20f;
    public float treeShadeRadius = 4f;

    bool shadeBird;
    bool longShot;
    bool quietDeer;

    public bool ShadeBirdDone => shadeBird;
    public bool LongShotDone => longShot;
    public bool QuietDeerDone => quietDeer;
    Transform player;

    void Awake()
    {
        Instance = this;
    }

    public static void ReportKill(Animal animal)
    {
        if (Instance != null && animal != null)
            Instance.CheckKill(animal);
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