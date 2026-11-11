using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Preview a table before paying, using Enter so placement cannot fire the player's gun.</summary>
public class FurniturePlacement : MonoBehaviour
{
    public static bool Active => current != null;
    static FurniturePlacement current;
    GameObject preview;
    Material previewMaterial;
    bool canPlace;
    const int Price = 6;

    public static void Begin()
    {
        if (Active || Inventory.Instance == null) return;
        if (Inventory.Instance.wood < Price)
        {
            Inventory.Instance.ShowNotice("Need 6 wood for a table.");
            return;
        }
        current = new GameObject("Place purchased table").AddComponent<FurniturePlacement>();
        current.previewMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        current.preview = BuildTable(current.previewMaterial, false);
        current.preview.name = "Table placement preview";
    }

    void Update()
    {
        if (Keyboard.current == null || Camera.main == null) return;
        if (Keyboard.current.backspaceKey.wasPressedThisFrame || (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead))
        {
            Destroy(gameObject);
            return;
        }
        preview.SetActive(!Inventory.IsOpen);
        if (Inventory.IsOpen) return;
        Vector3 forward = Camera.main.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) forward = Camera.main.transform.up;
        forward.y = 0f;
        forward.Normalize();
        Vector3 target = PlayerView.Position() + forward * 4f;
        canPlace = WorldGround.TryGetGround(target, out Vector3 ground, out Vector3 normal) && normal.y > 0.95f;
        preview.transform.SetPositionAndRotation(ground, Quaternion.LookRotation(forward));
        if (canPlace)
        {
            // Terrain is the support, not an obstruction. Keep the furniture clear of other objects.
            foreach (Collider obstacle in Physics.OverlapBox(ground + Vector3.up * 0.55f, new Vector3(0.85f, 0.5f, 0.55f), preview.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (!(obstacle is TerrainCollider)) { canPlace = false; break; }
            for (int i = 0; i < 4 && canPlace; i++)
            {
                Vector3 foot = ground + preview.transform.rotation * new Vector3(i % 2 == 0 ? -0.65f : 0.65f, 0f, i < 2 ? -0.35f : 0.35f);
                if (!WorldGround.TryGetGround(foot, out Vector3 support, out _) || Mathf.Abs(support.y - ground.y) > 0.15f) canPlace = false;
            }
        }
        previewMaterial.color = canPlace ? new Color(0.3f, 0.75f, 0.35f) : new Color(0.85f, 0.2f, 0.15f);
        if (!Keyboard.current.enterKey.wasPressedThisFrame) return;
        if (!canPlace)
        {
            Inventory.Instance?.ShowNotice("Choose a clear, flat spot for the table.");
            return;
        }
        if (Inventory.Instance == null || !Inventory.Instance.UseWood(Price))
        {
            Inventory.Instance?.ShowNotice("Need 6 wood for a table.");
            return;
        }
        Material wood = new Material(previewMaterial.shader);
        wood.color = new Color(0.43f, 0.27f, 0.13f);
        GameObject table = BuildTable(wood, true);
        table.name = "Purchased wooden table";
        table.transform.SetPositionAndRotation(preview.transform.position, preview.transform.rotation);
        Inventory.Instance.ShowNotice("Bought and placed a wooden table.");
        Destroy(gameObject);
    }

    void LateUpdate()
    {
        GameHUD hud = GameHUD.Instance;
        if (hud == null || Inventory.IsOpen) return;
        hud.ShowShop("WOODEN TABLE", canPlace ? "PLACE IT ON CLEAR, LEVEL GROUND" : "<color=#FF6A5C>FIND CLEAR, LEVEL GROUND</color>",
                     new[] { "Enter  Buy and place   6 wood", "Backspace  Cancel" });
    }

    void OnGUI()
    {
        if (GameHUD.Instance != null || Inventory.IsOpen) return;
        ScreenText.Centered("WOODEN TABLE - 6 wood", Screen.height * 0.73f, 24);
        ScreenText.Centered("Enter: buy and place   |   Backspace: cancel", Screen.height * 0.78f, 18);
        if (!canPlace) ScreenText.Centered("Find clear, level ground", Screen.height * 0.82f, 18);
    }

    static GameObject BuildTable(Material material, bool solid)
    {
        GameObject root = new GameObject("Wooden table");
        Part(root.transform, new Vector3(0f, 1f, 0f), new Vector3(1.6f, 0.12f, 1f), material, solid);
        for (int i = 0; i < 4; i++)
            Part(root.transform, new Vector3(i % 2 == 0 ? -0.65f : 0.65f, 0.5f, i < 2 ? -0.35f : 0.35f), new Vector3(0.12f, 1f, 0.12f), material, solid);
        return root;
    }

    static void Part(Transform parent, Vector3 position, Vector3 size, Material material, bool solid)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (!solid)
        {
            part.GetComponent<Collider>().enabled = false;
            Destroy(part.GetComponent<Collider>());
        }
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = size;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    void OnDestroy()
    {
        if (current == this) current = null;
        if (preview != null) Destroy(preview);
        if (previewMaterial != null) Destroy(previewMaterial);
    }
}
