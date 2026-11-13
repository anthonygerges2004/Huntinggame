using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class InventoryWorldDrop : MonoBehaviour
{
    public string itemId;
    public int amount;
    public float pickupRange = 1.8f;

    static Transform player;
    Vector3 basePos;
    float age;
    Material ownedMaterial;
    Material endMaterial;
    float collectTime;
    bool collecting;
    public static InventoryWorldDrop WoodFocus { get; private set; }
    static int focusFrame = -1;
    public static bool WoodInReach { get { RefreshWoodFocus(); return WoodFocus != null; } }
    public static bool CollectingWood => WoodFocus != null && WoodFocus.collecting;
    public static Vector3 PickupPoint => WoodFocus != null ? WoodFocus.transform.position : PlayerView.Position();

    void OnDisable() { if (WoodFocus == this) WoodFocus = null; collecting = false; }

    static void RefreshWoodFocus()
    {
        if (focusFrame == Time.frameCount) return;
        focusFrame = Time.frameCount;
        WoodFocus = null;
        if (Inventory.IsOpen || AnimalCarcass.PlayerNear || (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)) return;
        Ray ray = PlayerView.AimRay();
        Transform playerBody = PlayerView.Body();
        float nearest = 2.2f;
        foreach (RaycastHit hit in Physics.RaycastAll(ray, 2.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (playerBody != null && (hit.transform == playerBody || hit.transform.IsChildOf(playerBody))) continue;
            if (hit.distance > nearest) continue;
            nearest = hit.distance;
            InventoryWorldDrop drop = hit.collider.GetComponentInParent<InventoryWorldDrop>();
            WoodFocus = drop != null && drop.itemId == "Wood" && drop.amount > 0 ? drop : null;
        }
    }

    public int CollectAvailableWood(Inventory inventory)
    {
        if (itemId != "Wood" || amount <= 0 || inventory == null) return 0;
        int collected = inventory.CollectItem("Wood", amount);
        amount -= collected;
        return collected;
    }

    void TickWood()
    {
        if (amount <= 0) return;
        RefreshWoodFocus();
        if (WoodFocus != this || Inventory.Instance == null || (AxeSwing.Instance != null && AxeSwing.Instance.IsSwinging))
        { collectTime = 0; collecting = false; return; }
#if ENABLE_INPUT_SYSTEM
        bool held = Keyboard.current != null && Keyboard.current.eKey.isPressed;
#else
        bool held = Input.GetKey(KeyCode.E);
#endif
        if (!held) { collectTime = 0; collecting = false; return; }
        if (Inventory.Instance.SpaceForWood() <= 0) { collectTime = 0; collecting = false; return; }
        collecting = true;
        collectTime += Time.deltaTime;
        if (collectTime < 0.4f) return;
        int collected = CollectAvailableWood(Inventory.Instance);
        collectTime = 0;
        collecting = false;
        if (collected <= 0) return;
        Inventory.Instance.ShowNotice("Picked up " + collected + " wood.");
        if (amount <= 0) StartCoroutine(LiftCollectedLog());
    }

    System.Collections.IEnumerator LiftCollectedLog()
    {
        Collider collider = GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        Vector3 from = transform.position;
        for (float elapsed = 0; elapsed < 0.16f; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0, 1, elapsed / 0.16f);
            Vector3 hand = PlayerView.AimOrigin() - Vector3.up * 0.35f;
            transform.position = Vector3.Lerp(from, hand, t);
            transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.25f, t);
            yield return null;
        }
        Destroy(gameObject);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || itemId != "Wood") return;
        RefreshWoodFocus();
        if (WoodFocus != this) return;
        bool full = Inventory.Instance != null && Inventory.Instance.SpaceForWood() <= 0;
        string text = full ? "Bag full - make room for wood" : "Hold E to pick up wood  " + Mathf.RoundToInt(collectTime / 0.4f * 100f) + "%";
        ScreenText.Centered(text, Screen.height * 0.5f + 90f * ScreenText.Scale, 20);
    }

    void OnDestroy()
    {
        if (ownedMaterial == null) return;
        if (Application.isPlaying) Destroy(ownedMaterial);
        else DestroyImmediate(ownedMaterial);
    }

    public static void Spawn(Vector3 position, string itemId, int amount)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0)
            return;

        GameObject root = new GameObject(itemId + "Drop");
        root.transform.position = WorldGround.Sit(position);

        InventoryWorldDrop drop = root.AddComponent<InventoryWorldDrop>();
        drop.itemId = itemId;
        drop.amount = amount;

        if (itemId == "Wood")
        {
            Vector3 normal = Vector3.up;
            WorldGround.TryGetGround(position, out _, out normal);
            if (normal.sqrMagnitude < 0.01f) normal = Vector3.up;
            root.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            drop.BuildWoodVisual();
        }
        else CreateVisual(root.transform, itemId);
    }

    void BuildWoodVisual()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        ownedMaterial = new Material(shader) { color = new Color(0.24f, 0.15f, 0.075f) };
        endMaterial = new Material(shader) { color = new Color(0.72f, 0.51f, 0.28f) };
        AddLogPart("Bark", Vector3.up * 0.075f, new Vector3(0.14f, 0.24f, 0.14f), ownedMaterial);
        AddLogPart("Cut end", new Vector3(-0.24f, 0.075f, 0), new Vector3(0.13f, 0.004f, 0.13f), endMaterial);
        AddLogPart("Cut end", new Vector3(0.24f, 0.075f, 0), new Vector3(0.13f, 0.004f, 0.13f), endMaterial);
        AddLogPart("Heartwood", new Vector3(-0.245f, 0.075f, 0), new Vector3(0.055f, 0.001f, 0.055f), ownedMaterial);
        AddLogPart("Heartwood", new Vector3(0.245f, 0.075f, 0), new Vector3(0.055f, 0.001f, 0.055f), ownedMaterial);
        BoxCollider collider = gameObject.AddComponent<BoxCollider>();
        collider.center = Vector3.up * 0.075f;
        collider.size = new Vector3(0.5f, 0.15f, 0.15f);
    }
    void AddLogPart(string label, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        part.name = label;
        part.GetComponent<Collider>().enabled = false;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(transform, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(0, 0, 90);
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    static void CreateVisual(Transform parent, string itemId)
    {
        PrimitiveType shape = PrimitiveType.Cube;
        Color color = new Color(0.75f, 0.75f, 0.75f);
        Vector3 scale = new Vector3(0.28f, 0.12f, 0.28f);
        Quaternion tilt = Quaternion.identity;

        if (itemId == "Wood")
        {
            shape = PrimitiveType.Cylinder;
            color = new Color(0.38f, 0.29f, 0.16f);
            scale = new Vector3(0.12f, 0.20f, 0.12f);
            tilt = Quaternion.Euler(0f, 20f, 90f);
        }
        else if (itemId == "CookedMeat")
        {
            shape = PrimitiveType.Capsule;
            color = new Color(0.55f, 0.35f, 0.16f);
            scale = new Vector3(0.18f, 0.12f, 0.18f);
            tilt = Quaternion.Euler(0f, 15f, 90f);
        }
        else if (itemId == "RawMeat")
        {
            shape = PrimitiveType.Sphere;
            color = new Color(0.6f, 0.14f, 0.12f);
            scale = new Vector3(0.22f, 0.1f, 0.16f);
        }

        GameObject visual = GameObject.CreatePrimitive(shape);
        visual.transform.SetParent(parent, false);
        visual.transform.localRotation = tilt;
        visual.transform.localPosition = Vector3.up * 0.06f;
        visual.transform.localScale = scale;
        Destroy(visual.GetComponent<Collider>());

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        parent.GetComponent<InventoryWorldDrop>().ownedMaterial = mat;
        mat.SetColor("_BaseColor", color);
        visual.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void Start()
    {
        basePos = transform.position;
    }

    void Update()
    {
        if (itemId == "Wood") { TickWood(); return; }
        age += Time.deltaTime;
        transform.position = basePos + Vector3.up * (Mathf.Sin(age * 2.2f) * 0.015f);
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

        if (Inventory.IsOpen)
            return;
        int collected = Inventory.Instance.CollectItem(itemId, amount);
        if (collected <= 0)
            return;
        amount -= collected;
        Inventory.Instance.ShowNotice("+" + collected + " " + itemId);
        if (amount <= 0)
            Destroy(gameObject);
    }
}
