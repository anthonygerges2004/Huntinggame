using UnityEngine;
using UnityEngine.InputSystem;

public class Inventory : MonoBehaviour
{
    enum ItemKind
    {
        None,
        RawMeat,
        CookedMeat,
        Wood
    }

    struct Slot
    {
        public ItemKind kind;
        public int count;
    }

    public static Inventory Instance;
    public static bool IsOpen => Instance != null && Instance.inventoryOpen;

    public int meat;
    public int cookedMeat;
    public int wood;
    public bool hasPickaxe = true;
    public float meatSpoilSeconds = 90f;
    public float cookedHealAmount = 20f;
    public int maxStackSize = 64;

    const int GridColumns = 5;
    const int GridRows = 5;
    const int SlotCount = GridColumns * GridRows;

    Slot[] slots = new Slot[SlotCount];
    float spoilTimer;
    string notice;
    float noticeUntil;
    bool inventoryOpen;
    bool dragging;
    Slot draggedSlot;
    int dragFromIndex = -1;
    Rect inventoryRect;
    float slotSizeUi;
    float gapUi;
    float padUi;
    float headerUi;
    float footerUi;

    GUIStyle inventoryTextStyle;
    GUIStyle inventoryCountStyle;
    GUIStyle inventoryTitleStyle;

    void Awake()
    {
        Instance = this;
        spoilTimer = meatSpoilSeconds;
        Overhunt.ResetProgress();

        if (GetComponent<HuntWorldDressing>() == null)
            gameObject.AddComponent<HuntWorldDressing>();
        if (GetComponent<HuntChallenges>() == null)
            gameObject.AddComponent<HuntChallenges>();
        if (GetComponent<BirdChallenge>() == null)
            gameObject.AddComponent<BirdChallenge>();
        if (GetComponent<FoliageDistanceCull>() == null)
            gameObject.AddComponent<FoliageDistanceCull>();

        RecalculatePublicCounts();
    }

    void Update()
    {
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame)
            inventoryOpen = !inventoryOpen;

        if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
        {
            if (!EatCookedMeat())
                Debug.Log("No cooked meat to eat. Cook raw meat on a fire first.");
        }

        if (meat <= 0)
        {
            spoilTimer = meatSpoilSeconds;
            return;
        }

        spoilTimer -= Time.deltaTime;
        if (spoilTimer <= 0f)
        {
            RemoveItem(ItemKind.RawMeat, meat);
            spoilTimer = meatSpoilSeconds;
            Debug.Log("Raw meat spoiled. Cook it on a fire next time.");
        }
    }

    public void AddMeat(int amount)
    {
        int added = AddItem(ItemKind.RawMeat, amount);
        if (added > 0)
            spoilTimer = meatSpoilSeconds;
    }

    public void AddWood(int amount)
    {
        AddItem(ItemKind.Wood, amount);
    }

    public void AddCookedMeat(int amount)
    {
        AddItem(ItemKind.CookedMeat, amount);
    }

    public bool UseWood(int amount)
    {
        return RemoveItem(ItemKind.Wood, amount) == amount;
    }

    public bool CookOneMeat()
    {
        if (meat <= 0 || GetSpaceFor(ItemKind.CookedMeat) <= 0)
            return false;

        if (RemoveItem(ItemKind.RawMeat, 1) != 1)
            return false;

        AddItem(ItemKind.CookedMeat, 1);
        spoilTimer = meat > 0 ? meatSpoilSeconds : spoilTimer;
        return true;
    }

    public bool EatCookedMeat()
    {
        if (cookedMeat <= 0)
            return false;

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return false;

        if (RemoveItem(ItemKind.CookedMeat, 1) != 1)
            return false;

        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.Heal(cookedHealAmount);
        return true;
    }

    public bool RemoveOneSellableMeat()
    {
        if (RemoveItem(ItemKind.CookedMeat, 1) == 1)
            return true;

        return RemoveItem(ItemKind.RawMeat, 1) == 1;
    }

    int AddItem(ItemKind kind, int amount)
    {
        int remaining = Mathf.Max(0, amount);
        if (remaining <= 0 || kind == ItemKind.None)
            return 0;

        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i].kind != kind || slots[i].count >= maxStackSize)
                continue;

            int add = Mathf.Min(maxStackSize - slots[i].count, remaining);
            slots[i].count += add;
            remaining -= add;
        }

        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i].count > 0)
                continue;

            int add = Mathf.Min(maxStackSize, remaining);
            slots[i].kind = kind;
            slots[i].count = add;
            remaining -= add;
        }

        RecalculatePublicCounts();
        return amount - remaining;
    }

    int RemoveItem(ItemKind kind, int amount)
    {
        int need = Mathf.Max(0, amount);
        if (need <= 0 || kind == ItemKind.None)
            return 0;

        for (int i = 0; i < slots.Length && need > 0; i++)
        {
            if (slots[i].kind != kind || slots[i].count <= 0)
                continue;

            int take = Mathf.Min(slots[i].count, need);
            slots[i].count -= take;
            need -= take;
            if (slots[i].count <= 0)
            {
                slots[i].kind = ItemKind.None;
                slots[i].count = 0;
            }
        }

        RecalculatePublicCounts();
        return amount - need;
    }

    int GetSpaceFor(ItemKind kind)
    {
        int total = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].count <= 0)
                total += maxStackSize;
            else if (slots[i].kind == kind)
                total += maxStackSize - slots[i].count;
        }
        return total;
    }

    void RecalculatePublicCounts()
    {
        meat = 0;
        cookedMeat = 0;
        wood = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].count <= 0)
                continue;

            switch (slots[i].kind)
            {
                case ItemKind.RawMeat:
                    meat += slots[i].count;
                    break;
                case ItemKind.CookedMeat:
                    cookedMeat += slots[i].count;
                    break;
                case ItemKind.Wood:
                    wood += slots[i].count;
                    break;
            }
        }
    }

    public void ShowNotice(string text)
    {
        notice = text;
        noticeUntil = Time.time + 2f;
    }

    void OnGUI()
    {
        if (inventoryOpen)
        {
            DrawInventoryWindow();
            HandleInventoryDrag(Event.current);
            DrawDraggedSlot(Event.current.mousePosition);
        }

        if (Time.time <= noticeUntil && !string.IsNullOrEmpty(notice))
            ScreenText.Centered(notice, Screen.height * 0.7f);
    }

    void DrawInventoryWindow()
    {
        EnsureInventoryStyles();

        gapUi = Mathf.Max(12f, Screen.width * 0.006f);
        padUi = Mathf.Max(24f, Screen.width * 0.01f);
        headerUi = Mathf.Max(56f, Screen.height * 0.05f);
        footerUi = Mathf.Max(24f, Screen.height * 0.02f);

        float maxWidth = Screen.width * 0.92f;
        float maxHeight = Screen.height * 0.92f;
        float slotByWidth = (maxWidth - padUi * 2f - gapUi * (GridColumns - 1)) / GridColumns;
        float slotByHeight = (maxHeight - headerUi - footerUi - gapUi * (GridRows - 1)) / GridRows;
        slotSizeUi = Mathf.Max(140f, Mathf.Min(slotByWidth, slotByHeight));

        float gridWidth = GridColumns * slotSizeUi + (GridColumns - 1) * gapUi;
        float gridHeight = GridRows * slotSizeUi + (GridRows - 1) * gapUi;
        float width = gridWidth + padUi * 2f;
        float height = headerUi + gridHeight + footerUi;

        Color prevContent = GUI.contentColor;
        GUI.contentColor = Color.white;

        inventoryRect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        GUI.Box(inventoryRect, "Inventory", inventoryTitleStyle);

        for (int row = 0; row < GridRows; row++)
        {
            for (int col = 0; col < GridColumns; col++)
            {
                int index = row * GridColumns + col;
                float x = inventoryRect.x + padUi + col * (slotSizeUi + gapUi);
                float y = inventoryRect.y + headerUi + row * (slotSizeUi + gapUi);
                DrawSlot(new Rect(x, y, slotSizeUi, slotSizeUi), slots[index]);
            }
        }

        GUI.contentColor = prevContent;
    }

    void DrawSlot(Rect rect, Slot slot)
    {
        Color prev = GUI.color;
        GUI.color = slot.count > 0 ? ItemColor(slot.kind) : new Color(0.24f, 0.24f, 0.24f, 1f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = prev;

        if (slot.count <= 0)
            return;

        GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 24f), ItemShortName(slot.kind), inventoryTextStyle);
        GUI.Label(new Rect(rect.x + 8f, rect.y + rect.height - 30f, rect.width - 16f, 24f), slot.count.ToString(), inventoryCountStyle);
    }

    void DrawDraggedSlot(Vector2 mouse)
    {
        if (!dragging || draggedSlot.count <= 0)
            return;

        float preview = Mathf.Clamp(slotSizeUi * 0.85f, 92f, 180f);
        Rect rect = new Rect(mouse.x - preview * 0.5f, mouse.y - preview * 0.5f, preview, preview);
        Slot previewSlot = draggedSlot;
        DrawSlot(rect, previewSlot);
    }

    void HandleInventoryDrag(Event e)
    {
        if (e == null)
            return;

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            int index = SlotIndexAtPoint(e.mousePosition);
            if (index >= 0 && slots[index].count > 0)
            {
                dragging = true;
                draggedSlot = slots[index];
                dragFromIndex = index;
                slots[index] = default;
                RecalculatePublicCounts();
                e.Use();
            }
            return;
        }

        if (!dragging)
            return;

        if (e.type == EventType.MouseUp && e.button == 0)
        {
            int target = SlotIndexAtPoint(e.mousePosition);
            if (target >= 0)
                PlaceDraggedIntoSlot(target);
            else if (!inventoryRect.Contains(e.mousePosition))
                DropDraggedStack();
            else
                ReturnDraggedToSource();

            e.Use();
        }
    }

    int SlotIndexAtPoint(Vector2 point)
    {
        if (!inventoryRect.Contains(point))
            return -1;

        float localX = point.x - (inventoryRect.x + padUi);
        float localY = point.y - (inventoryRect.y + headerUi);
        if (localX < 0f || localY < 0f)
            return -1;

        int col = Mathf.FloorToInt(localX / (slotSizeUi + gapUi));
        int row = Mathf.FloorToInt(localY / (slotSizeUi + gapUi));
        if (col < 0 || col >= GridColumns || row < 0 || row >= GridRows)
            return -1;

        float offsetX = localX - col * (slotSizeUi + gapUi);
        float offsetY = localY - row * (slotSizeUi + gapUi);
        if (offsetX > slotSizeUi || offsetY > slotSizeUi)
            return -1;

        return row * GridColumns + col;
    }

    void PlaceDraggedIntoSlot(int target)
    {
        if (!dragging || target < 0 || target >= slots.Length)
            return;

        if (target == dragFromIndex)
        {
            slots[dragFromIndex] = draggedSlot;
            ClearDrag();
            RecalculatePublicCounts();
            return;
        }

        Slot targetSlot = slots[target];
        if (targetSlot.count <= 0)
        {
            slots[target] = draggedSlot;
            ClearDrag();
            RecalculatePublicCounts();
            return;
        }

        if (targetSlot.kind == draggedSlot.kind && targetSlot.count < maxStackSize)
        {
            int move = Mathf.Min(maxStackSize - targetSlot.count, draggedSlot.count);
            targetSlot.count += move;
            draggedSlot.count -= move;
            slots[target] = targetSlot;

            if (draggedSlot.count > 0)
                slots[dragFromIndex] = draggedSlot;

            ClearDrag();
            RecalculatePublicCounts();
            return;
        }

        slots[dragFromIndex] = targetSlot;
        slots[target] = draggedSlot;
        ClearDrag();
        RecalculatePublicCounts();
    }

    void DropDraggedStack()
    {
        DropItemToWorld(draggedSlot.kind, draggedSlot.count);
        ShowNotice("Dropped " + draggedSlot.count + " " + ItemShortName(draggedSlot.kind));
        ClearDrag();
        RecalculatePublicCounts();
    }

    void ReturnDraggedToSource()
    {
        slots[dragFromIndex] = draggedSlot;
        ClearDrag();
        RecalculatePublicCounts();
    }

    void ClearDrag()
    {
        dragging = false;
        draggedSlot = default;
        dragFromIndex = -1;
    }

    string ItemShortName(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.RawMeat:
                return "Raw";
            case ItemKind.CookedMeat:
                return "Cooked";
            case ItemKind.Wood:
                return "Wood";
            default:
                return "";
        }
    }

    Color ItemColor(ItemKind kind)
    {
        switch (kind)
        {
            case ItemKind.RawMeat:
                return new Color(0.53f, 0.18f, 0.16f, 1f);
            case ItemKind.CookedMeat:
                return new Color(0.55f, 0.38f, 0.16f, 1f);
            case ItemKind.Wood:
                return new Color(0.34f, 0.25f, 0.14f, 1f);
            default:
                return new Color(0.24f, 0.24f, 0.24f, 1f);
        }
    }

    void EnsureInventoryStyles()
    {
        if (inventoryTextStyle != null)
            return;

        inventoryTextStyle = new GUIStyle(GUI.skin.label);
        inventoryTextStyle.fontSize = 20;
        inventoryTextStyle.normal.textColor = Color.white;
        inventoryTextStyle.hover.textColor = Color.white;
        inventoryTextStyle.active.textColor = Color.white;
        inventoryTextStyle.focused.textColor = Color.white;
        inventoryTextStyle.onNormal.textColor = Color.white;
        inventoryTextStyle.onHover.textColor = Color.white;
        inventoryTextStyle.onActive.textColor = Color.white;
        inventoryTextStyle.onFocused.textColor = Color.white;
        inventoryTextStyle.alignment = TextAnchor.UpperLeft;

        inventoryCountStyle = new GUIStyle(inventoryTextStyle);
        inventoryCountStyle.fontSize = 24;
        inventoryCountStyle.fontStyle = FontStyle.Bold;
        inventoryCountStyle.alignment = TextAnchor.UpperRight;

        inventoryTitleStyle = new GUIStyle(GUI.skin.box);
        inventoryTitleStyle.fontSize = 26;
        inventoryTitleStyle.alignment = TextAnchor.UpperCenter;
        inventoryTitleStyle.normal.textColor = Color.white;
        inventoryTitleStyle.hover.textColor = Color.white;
        inventoryTitleStyle.active.textColor = Color.white;
        inventoryTitleStyle.focused.textColor = Color.white;
        inventoryTitleStyle.onNormal.textColor = Color.white;
        inventoryTitleStyle.onHover.textColor = Color.white;
        inventoryTitleStyle.onActive.textColor = Color.white;
        inventoryTitleStyle.onFocused.textColor = Color.white;
    }

    void DropItemToWorld(ItemKind kind, int amount)
    {
        if (amount <= 0 || kind == ItemKind.None)
            return;

        Vector3 origin = Camera.main != null
            ? Camera.main.transform.position + Camera.main.transform.forward * 1.8f
            : transform.position + transform.forward * 1.8f;
        origin = WorldGround.Snap(origin);

        if (kind == ItemKind.RawMeat)
            MeatPickup.Spawn(origin, amount);
        else
            InventoryWorldDrop.Spawn(origin, kind.ToString(), amount);
    }

    public float SpoilSecondsLeft()
    {
        return meat > 0 ? spoilTimer : 0f;
    }
}
