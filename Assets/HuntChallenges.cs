using UnityEngine;

public class HuntChallenges : MonoBehaviour
{
    public static HuntChallenges Instance;

    public float longShotDistance = 20f;
    public float treeShadeRadius = 4f;

    bool shadeBird;
    bool longShot;
    bool quietDeer;
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

    void OnGUI()
    {
        float scale = ScreenText.Scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

        float w = 270f;
        float h = 122f;
        float x = Screen.width / scale - w - 18f;
        float y = 240f;

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(x + 10f, y + 6f, w, 20f), "HUNT CHALLENGES");

        Line(x, y + 30f, shadeBird, "Kill a bird under a tree");
        Line(x, y + 52f, longShot, "Hit an animal from 20 m+");
        Line(x, y + 74f, quietDeer, "Kill a deer while crouching");
        Line(x, y + 96f, BirdChallenge.Won, "Win the bird challenge (cage)");

        GUI.color = Color.white;
        GUI.matrix = Matrix4x4.identity;
    }

    void Line(float x, float y, bool done, string text)
    {
        GUI.color = done ? new Color(0.55f, 1f, 0.55f) : Color.white;
        GUI.Label(new Rect(x + 10f, y, 260f, 20f), (done ? "[X]  " : "[  ]  ") + text);
    }
}