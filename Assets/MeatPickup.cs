using UnityEngine;

public class MeatPickup : MonoBehaviour
{
    public int amount = 2;
    public float pickupRange = 1.8f;
    public float spoilSeconds = 90f;

    static Material meatMat;
    static Material fatMat;
    static Material boneMat;
    static Material spoiledMat;
    static Transform player;

    float age;
    bool spoiling;
    Vector3 basePos;

    public static void Spawn(Vector3 position, int amount)
    {
        if (amount <= 0)
            return;

        CreateMaterials();

        GameObject root = new GameObject("MeatDrop");
        root.transform.position = WorldGround.Snap(position) + Vector3.up * 0.2f;

        int pieces = Mathf.Clamp(amount, 1, 4);
        for (int i = 0; i < pieces; i++)
        {
            float angle = i * Mathf.PI * 2f / pieces;
            Vector3 offset = pieces == 1 ? Vector3.zero : new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.3f;
            BuildPiece(root.transform, offset, i * 70f);
        }

        MeatPickup pickup = root.AddComponent<MeatPickup>();
        pickup.amount = amount;
        if (Inventory.Instance != null)
            pickup.spoilSeconds = Inventory.Instance.meatSpoilSeconds;
    }

    static void BuildPiece(Transform parent, Vector3 offset, float yaw)
    {
        Transform piece = new GameObject("Meat").transform;
        piece.SetParent(parent, false);
        piece.localPosition = offset;
        piece.localRotation = Quaternion.Euler(0f, yaw, 0f);

        AddPart(piece, PrimitiveType.Sphere, meatMat, Vector3.zero, Quaternion.identity, new Vector3(0.42f, 0.2f, 0.3f));
        AddPart(piece, PrimitiveType.Sphere, fatMat, new Vector3(0f, 0.06f, 0f), Quaternion.identity, new Vector3(0.3f, 0.1f, 0.2f));
        AddPart(piece, PrimitiveType.Cylinder, boneMat, new Vector3(0.27f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), new Vector3(0.06f, 0.12f, 0.06f));
        AddPart(piece, PrimitiveType.Sphere, boneMat, new Vector3(0.39f, 0f, 0f), Quaternion.identity, new Vector3(0.1f, 0.1f, 0.1f));
    }

    static void AddPart(Transform parent, PrimitiveType type, Material mat, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = pos;
        part.transform.localRotation = rot;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void CreateMaterials()
    {
        if (meatMat != null)
            return;

        meatMat = MakeMaterial(new Color(0.62f, 0.13f, 0.12f));
        fatMat = MakeMaterial(new Color(0.85f, 0.55f, 0.5f));
        boneMat = MakeMaterial(new Color(0.93f, 0.9f, 0.82f));
        spoiledMat = MakeMaterial(new Color(0.3f, 0.32f, 0.12f));
    }

    static Material MakeMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        return mat;
    }

    void Start()
    {
        basePos = transform.position;
    }

    void Update()
    {
        age += Time.deltaTime;

        transform.position = basePos + Vector3.up * (Mathf.Sin(age * 2f) * 0.05f);
        transform.Rotate(0f, 40f * Time.deltaTime, 0f);

        if (!spoiling && age > spoilSeconds * 0.75f)
        {
            spoiling = true;
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r.sharedMaterial == meatMat || r.sharedMaterial == fatMat)
                    r.sharedMaterial = spoiledMat;
            }
        }

        if (age >= spoilSeconds)
        {
            Destroy(gameObject);
            return;
        }

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (player == null)
        {
            PlayerMove move = FindAnyObjectByType<PlayerMove>();
            if (move == null)
                return;
            player = move.transform;
        }

        Vector3 toPlayer = player.position - basePos;
        float height = Mathf.Abs(toPlayer.y);
        toPlayer.y = 0f;
        if (toPlayer.magnitude > pickupRange || height > 2.5f)
            return;

        if (Inventory.Instance == null)
            return;

        Inventory.Instance.AddMeat(amount);
        Inventory.Instance.ShowNotice("+" + amount + " meat");
        Destroy(gameObject);
    }
}
