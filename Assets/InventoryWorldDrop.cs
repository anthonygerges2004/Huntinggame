using UnityEngine;

public class InventoryWorldDrop : MonoBehaviour
{
    public string itemId;
    public int amount;
    public float pickupRange = 1.8f;

    static Transform player;
    Vector3 basePos;
    float age;

    public static void Spawn(Vector3 position, string itemId, int amount)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0)
            return;

        GameObject root = new GameObject(itemId + "Drop");
        root.transform.position = WorldGround.Snap(position) + Vector3.up * 0.2f;

        InventoryWorldDrop drop = root.AddComponent<InventoryWorldDrop>();
        drop.itemId = itemId;
        drop.amount = amount;

        CreateVisual(root.transform, itemId);
    }

    static void CreateVisual(Transform parent, string itemId)
    {
        PrimitiveType shape = PrimitiveType.Cube;
        Color color = new Color(0.75f, 0.75f, 0.75f);

        if (itemId == "Wood")
        {
            shape = PrimitiveType.Cylinder;
            color = new Color(0.38f, 0.29f, 0.16f);
        }
        else if (itemId == "CookedMeat")
        {
            shape = PrimitiveType.Capsule;
            color = new Color(0.55f, 0.35f, 0.16f);
        }
        else if (itemId == "RawMeat")
        {
            shape = PrimitiveType.Sphere;
            color = new Color(0.6f, 0.14f, 0.12f);
        }

        GameObject visual = GameObject.CreatePrimitive(shape);
        visual.transform.SetParent(parent, false);
        visual.transform.localScale = new Vector3(0.38f, 0.28f, 0.38f);
        Destroy(visual.GetComponent<Collider>());

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        visual.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void Start()
    {
        basePos = transform.position;
    }

    void Update()
    {
        age += Time.deltaTime;
        transform.position = basePos + Vector3.up * (Mathf.Sin(age * 2.2f) * 0.05f);
        transform.Rotate(0f, 45f * Time.deltaTime, 0f);

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

        if (itemId == "Wood")
        {
            Inventory.Instance.AddWood(amount);
            Inventory.Instance.ShowNotice("+" + amount + " wood");
        }
        else if (itemId == "CookedMeat")
        {
            Inventory.Instance.AddCookedMeat(amount);
            Inventory.Instance.ShowNotice("+" + amount + " cooked meat");
        }
        else
        {
            Inventory.Instance.AddMeat(amount);
            Inventory.Instance.ShowNotice("+" + amount + " meat");
        }

        Destroy(gameObject);
    }
}
