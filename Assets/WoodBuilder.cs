using UnityEngine;
using UnityEngine.InputSystem;

public class WoodBuilder : MonoBehaviour
{
    public static bool IsBuilding;

    [Header("Pieces")]
    public int foundationCost = 2;
    public int wallCost = 2;

    [Header("Materials")]
    public Material logMaterial;
    public Material plankMaterial;
    public Material ghostMaterial;

    [Header("Placement")]
    public float placeDistance = 8f;
    public float gridSize = 3f;

    const float FoundationHeight = 0.3f;
    const float WallHeight = 3f;
    const int LogsPerWall = 8;

    GameObject foundationTemplate;
    GameObject wallTemplate;

    int selected;
    float extraYaw;
    GameObject ghost;
    int ghostFor = -1;

    void Awake()
    {
        if (logMaterial == null)
            logMaterial = Resources.Load<Material>("Building/LogMat");
        if (plankMaterial == null)
            plankMaterial = Resources.Load<Material>("Building/PlankMat");
        if (ghostMaterial == null)
            ghostMaterial = Resources.Load<Material>("Building/GhostMat");
        if (placeDistance < 8f)
            placeDistance = 8f;

        foundationTemplate = BuildFoundation();
        wallTemplate = BuildWall();
    }

    void OnDisable()
    {
        IsBuilding = false;
        DestroyGhost();
    }

    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null || Camera.main == null)
            return;

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            IsBuilding = !IsBuilding;
            if (!IsBuilding)
                DestroyGhost();
            return;
        }

        if (!IsBuilding)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                BuildPiece piece = LookedAtPiece(4f);
                if (piece != null)
                    piece.Hit();
            }
            return;
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            BuildPiece piece = LookedAtPiece(placeDistance);
            if (piece != null)
            {
                piece.Break();
                return;
            }
        }

        if (Mouse.current.scroll.ReadValue().y != 0f)
            selected = 1 - selected;

        if (Keyboard.current.rKey.wasPressedThisFrame)
            extraYaw += 90f;

        if (ghost == null || ghostFor != selected)
            MakeGhost();

        if (!FindPlacement(out Vector3 position, out Quaternion rotation))
        {
            ghost.SetActive(false);
            return;
        }

        ghost.SetActive(true);
        ghost.transform.SetPositionAndRotation(position, rotation);

        int cost = CostFor(selected);
        bool canAfford = Inventory.Instance != null && Inventory.Instance.wood >= cost;
        TintGhost(canAfford ? new Color(1f, 1f, 1f, 0.45f) : new Color(1f, 0.25f, 0.25f, 0.45f));

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (!canAfford || !Inventory.Instance.UseWood(cost))
        {
            Debug.Log("Need " + cost + " wood.");
            return;
        }

        GameObject placed = Instantiate(TemplateFor(selected), position, rotation);
        placed.name = selected == 0 ? "Foundation" : "Wall";
        placed.SetActive(true);
        placed.AddComponent<BuildPiece>().refund = cost;
    }

    public static BuildPiece LookedAtPiece(float range)
    {
        Transform cam = Camera.main.transform;
        Ray ray = new Ray(cam.position, cam.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
            return null;
        return hit.collider.GetComponentInParent<BuildPiece>();
    }

    bool FindPlacement(out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        Transform cam = Camera.main.transform;
        Ray ray = new Ray(cam.position, cam.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, placeDistance, ~0, QueryTriggerInteraction.Ignore))
            return false;

        float baseY = hit.normal.y > 0.5f ? hit.point.y : hit.collider.bounds.min.y;
        float g = gridSize;
        float cellX = Mathf.Floor(hit.point.x / g) * g + g * 0.5f;
        float cellZ = Mathf.Floor(hit.point.z / g) * g + g * 0.5f;

        if (selected == 0)
        {
            rotation = Quaternion.Euler(0f, extraYaw, 0f);
            position = new Vector3(cellX, baseY + FoundationHeight * 0.5f, cellZ);
            return true;
        }

        float yaw = Mathf.Round((cam.eulerAngles.y + extraYaw) / 90f) * 90f;
        bool facingZ = Mathf.Abs(Mathf.Sin(yaw * Mathf.Deg2Rad)) < 0.5f;
        rotation = Quaternion.Euler(0f, facingZ ? 0f : 90f, 0f);

        if (facingZ)
            position = new Vector3(cellX, 0f, Mathf.Round(hit.point.z / g) * g);
        else
            position = new Vector3(Mathf.Round(hit.point.x / g) * g, 0f, cellZ);

        position.y = baseY + WallHeight * 0.5f;
        return true;
    }

    GameObject BuildFoundation()
    {
        GameObject root = NewTemplate("FoundationTemplate");
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(3f, FoundationHeight, 3f);

        int planks = 6;
        float plankWidth = 3f / planks;
        for (int i = 0; i < planks; i++)
        {
            float z = -1.5f + plankWidth * (i + 0.5f);
            AddPart(root, PrimitiveType.Cube, plankMaterial,
                new Vector3(0f, 0.09f, z), Quaternion.identity,
                new Vector3(3f, 0.12f, plankWidth - 0.03f));
        }

        for (int i = -1; i <= 1; i += 2)
        {
            AddPart(root, PrimitiveType.Cylinder, logMaterial,
                new Vector3(i * 1.2f, -0.06f, 0f), Quaternion.Euler(90f, 0f, 0f),
                new Vector3(0.2f, 1.5f, 0.2f));
        }

        return root;
    }

    GameObject BuildWall()
    {
        GameObject root = NewTemplate("WallTemplate");
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.size = new Vector3(3f, WallHeight, 0.4f);

        float d = WallHeight / LogsPerWall;
        for (int i = 0; i < LogsPerWall; i++)
        {
            float y = -WallHeight * 0.5f + d * (i + 0.5f);
            AddPart(root, PrimitiveType.Cylinder, logMaterial,
                new Vector3(0f, y, 0f), Quaternion.Euler(0f, 0f, 90f),
                new Vector3(d * 1.05f, 1.65f, d * 1.05f));
        }

        for (int i = -1; i <= 1; i += 2)
        {
            AddPart(root, PrimitiveType.Cylinder, logMaterial,
                new Vector3(i * 1.35f, 0f, 0.22f), Quaternion.identity,
                new Vector3(0.22f, WallHeight * 0.5f, 0.22f));
        }

        return root;
    }

    GameObject NewTemplate(string templateName)
    {
        GameObject root = new GameObject(templateName);
        root.SetActive(false);
        root.transform.SetParent(transform, false);
        return root;
    }

    static void AddPart(GameObject root, PrimitiveType type, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        DestroyImmediate(part.GetComponent<Collider>());
        part.transform.SetParent(root.transform, false);
        part.transform.localPosition = position;
        part.transform.localRotation = rotation;
        part.transform.localScale = scale;
        if (material != null)
            part.GetComponent<Renderer>().sharedMaterial = material;
    }

    void MakeGhost()
    {
        DestroyGhost();
        ghost = Instantiate(TemplateFor(selected));
        ghost.name = "BuildPreview";
        ghostFor = selected;

        Collider[] colliders = ghost.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        if (ghostMaterial == null)
            return;

        Renderer[] renderers = ghost.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].sharedMaterial = ghostMaterial;
    }

    void TintGhost(Color color)
    {
        if (ghost == null || ghostMaterial == null)
            return;

        Renderer[] renderers = ghost.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Material mat = renderers[i].material;
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
        }
    }

    void DestroyGhost()
    {
        if (ghost != null)
            Destroy(ghost);
        ghost = null;
        ghostFor = -1;
    }

    GameObject TemplateFor(int index)
    {
        return index == 0 ? foundationTemplate : wallTemplate;
    }

    int CostFor(int index)
    {
        return index == 0 ? foundationCost : wallCost;
    }

    void OnGUI()
    {
        if (!IsBuilding)
            return;

        string[] names = { "Foundation", "Wall" };
        int[] costs = { foundationCost, wallCost };
        float w = 110f;
        float h = 46f;
        float gap = 8f;
        float startX = (Screen.width - (w * 2f + gap)) * 0.5f;
        float y = Screen.height - h - 40f;

        for (int i = 0; i < 2; i++)
        {
            Rect box = new Rect(startX + i * (w + gap), y, w, h);
            GUI.color = i == selected ? new Color(1f, 0.85f, 0.3f, 0.95f) : new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = i == selected ? Color.black : Color.white;
            GUI.Label(new Rect(box.x + 10f, box.y + 6f, w, 20f), names[i]);
            GUI.Label(new Rect(box.x + 10f, box.y + 24f, w, 20f), costs[i] + " wood");
        }

        GUI.color = Color.white;
        GUI.Label(new Rect(startX - 120f, y + h + 6f, 520f, 20f), "Scroll: change   R: rotate   Click: place   Right click: remove   B: exit");
    }
}
