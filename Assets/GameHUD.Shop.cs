using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shop and world-label layer of the HUD, in the same style as everything else:
//   right of centre   the stall you are standing at: title, your wood, deal status, offers
//                     ("1  8 shotgun shells   2 wood" becomes key cap, label, price)
//   in the world      floating name tags (gun shop sign, working villagers)
// Callers ask every frame from Update or LateUpdate; anything not asked for is hidden.
public partial class GameHUD
{
    const int ShopRows = 8;
    const int WorldTags = 10;

    CanvasGroup shopGroup;
    RectTransform shopPanel;
    TMP_Text shopTitle, shopStatus, shopWood;
    readonly List<(RectTransform root, Image cap, TMP_Text key, TMP_Text label, TMP_Text price)> shopRows =
        new List<(RectTransform, Image, TMP_Text, TMP_Text, TMP_Text)>();
    int shopAskedFrame = -10;
    string shopTitleWanted, shopStatusWanted;
    readonly List<string> shopLinesWanted = new List<string>();

    readonly List<(RectTransform root, TMP_Text title, TMP_Text detail, Image bg)> tags =
        new List<(RectTransform, TMP_Text, TMP_Text, Image)>();
    readonly List<(Vector3 world, string title, string detail)> tagRequests = new List<(Vector3, string, string)>();

    static readonly Regex KeyLine = new Regex(@"^(\S{1,10})\s{2,}(.+)$");
    static readonly Regex Cost = new Regex(@"^(\d+)\s+(wood|coins?)", RegexOptions.IgnoreCase);

    // ---------------------------------------------------------------- requests

    /// <summary>Shows the shop panel this frame. Lines look like "1  Label   price".</summary>
    public void ShowShop(string title, string status, IList<string> lines)
    {
        shopAskedFrame = Time.frameCount;
        shopTitleWanted = title;
        shopStatusWanted = status;
        shopLinesWanted.Clear();
        if (lines != null)
            shopLinesWanted.AddRange(lines);
    }

    /// <summary>Shows a floating label over a world position this frame.</summary>
    public void ShowWorldTag(Vector3 world, string title, string detail)
    {
        tagRequests.Add((world, title, detail));
    }

    // ---------------------------------------------------------------- build

    void BuildShop()
    {
        const float width = 440f;
        GameObject groupGo = new GameObject("Shop", typeof(RectTransform), typeof(CanvasGroup));
        groupGo.transform.SetParent(canvas.transform, false);
        RectTransform group = groupGo.GetComponent<RectTransform>();
        Place(group, new Vector2(1f, 0.5f), new Vector2(-Margin, -60f), new Vector2(width, 100f), new Vector2(1f, 0.5f));
        shopGroup = groupGo.GetComponent<CanvasGroup>();
        shopGroup.alpha = 0f;

        shopPanel = Panel(group, "Card", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 100f), PanelColor, 12f);
        shopPanel.pivot = new Vector2(0.5f, 0.5f);
        shopPanel.anchoredPosition = Vector2.zero;

        shopTitle = Text(shopPanel, "", 18f, TextMain, TextAlignmentOptions.MidlineLeft, true);
        shopTitle.characterSpacing = 8f;
        Place(shopTitle.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -16f), new Vector2(260f, 22f), new Vector2(0f, 1f));

        shopWood = Text(shopPanel, "", 13f, Gold, TextAlignmentOptions.MidlineRight, true);
        shopWood.characterSpacing = 4f;
        Place(shopWood.rectTransform, new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(160f, 18f), new Vector2(1f, 1f));

        shopStatus = Text(shopPanel, "", 11.5f, TextDim, TextAlignmentOptions.MidlineLeft, true);
        shopStatus.characterSpacing = 3f;
        shopStatus.richText = true;
        Place(shopStatus.rectTransform, new Vector2(0f, 1f), new Vector2(18f, -42f), new Vector2(width - 36f, 16f), new Vector2(0f, 1f));

        for (int i = 0; i < ShopRows; i++)
        {
            RectTransform row = Img(shopPanel, "Row" + i, roundSprite, TileColor, 6f).rectTransform;
            Place(row, new Vector2(0f, 1f), new Vector2(12f, -66f - i * 34f), new Vector2(width - 24f, 30f), new Vector2(0f, 1f));
            Image cap = Img(row, "Cap", roundSprite, new Color(0.93f, 0.91f, 0.86f, 0.95f), 4f);
            Place(cap.rectTransform, new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(26f, 20f), new Vector2(0f, 0.5f));
            TMP_Text key = Text(cap.rectTransform, "", 11.5f, new Color(0.08f, 0.08f, 0.09f), TextAlignmentOptions.Center, true);
            Stretch(key.rectTransform);
            TMP_Text label = Text(row, "", 14f, TextMain, TextAlignmentOptions.MidlineLeft, false);
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.richText = true;
            Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(44f, 0f), new Vector2(width - 24f - 44f - 110f, 20f), new Vector2(0f, 0.5f));
            TMP_Text price = Text(row, "", 13f, Gold, TextAlignmentOptions.MidlineRight, true);
            price.overflowMode = TextOverflowModes.Ellipsis;
            Place(price.rectTransform, new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(150f, 20f), new Vector2(1f, 0.5f));
            row.gameObject.SetActive(false);
            shopRows.Add((row, cap, key, label, price));
        }

        for (int i = 0; i < WorldTags; i++)
        {
            Image bg = Img(canvas.transform, "WorldTag" + i, roundSprite, PanelColor, 8f);
            RectTransform root = bg.rectTransform;
            Place(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 42f), new Vector2(0.5f, 0f));
            TMP_Text title = Text(root, "", 12f, Gold, TextAlignmentOptions.Center, true);
            title.characterSpacing = 4f;
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -5f), new Vector2(240f, 16f), new Vector2(0.5f, 1f));
            TMP_Text detail = Text(root, "", 11f, TextDim, TextAlignmentOptions.Center, false);
            Place(detail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(240f, 16f), new Vector2(0.5f, 0f));
            root.gameObject.SetActive(false);
            tags.Add((root, title, detail, bg));
        }
    }

    // ---------------------------------------------------------------- render (LateUpdate, after every caller)

    void LateUpdate()
    {
        if (!built)
        {
            tagRequests.Clear();
            return;
        }

        RenderShop(Time.unscaledDeltaTime);
        RenderTags();
    }

    void RenderShop(float dt)
    {
        bool asked = shopAskedFrame >= Time.frameCount - 1 && !Inventory.IsOpen;
        shopGroup.alpha = Mathf.MoveTowards(shopGroup.alpha, asked ? 1f : 0f, dt * 10f);
        if (!asked)
            return;

        shopTitle.text = shopTitleWanted ?? "";
        shopStatus.text = shopStatusWanted ?? "";
        int wood = Inventory.Instance != null ? Inventory.Instance.wood : 0;
        int coins = Inventory.Coins;
        shopWood.richText = true;
        shopWood.text = "<color=#F5C466>" + coins + " COINS</color>   <color=#8C8C86>" + wood + " WOOD</color>";

        int shown = 0;
        for (int i = 0; i < shopLinesWanted.Count && shown < shopRows.Count; i++)
        {
            string line = shopLinesWanted[i];
            if (string.IsNullOrEmpty(line))
                continue;

            var row = shopRows[shown++];
            row.root.gameObject.SetActive(true);

            Match m = KeyLine.Match(line);
            string key = m.Success ? m.Groups[1].Value : "";
            string rest = m.Success ? m.Groups[2].Value : line;
            string label = rest;
            string price = "";
            int split = rest.LastIndexOf("   ", System.StringComparison.Ordinal);
            if (split > 0)
            {
                label = rest.Substring(0, split).Trim();
                price = rest.Substring(split).Trim();
            }

            bool hasKey = key.Length > 0;
            row.cap.gameObject.SetActive(hasKey);
            row.key.text = key;
            row.cap.rectTransform.sizeDelta = new Vector2(Mathf.Max(26f, key.Length * 8f + 10f), 20f);
            float labelX = hasKey ? row.cap.rectTransform.sizeDelta.x + 16f : 12f;
            row.label.rectTransform.anchoredPosition = new Vector2(labelX, 0f);
            row.label.text = label;
            row.label.color = hasKey ? TextMain : TextDim;

            Match cost = Cost.Match(price);
            int have = cost.Success && cost.Groups[2].Value.StartsWith("c", System.StringComparison.OrdinalIgnoreCase) ? coins : wood;
            bool cantAfford = cost.Success && int.Parse(cost.Groups[1].Value) > have;
            row.price.text = price.ToUpperInvariant();
            row.price.color = cantAfford ? WarnColor : Gold;
        }

        for (int i = shown; i < shopRows.Count; i++)
        {
            if (shopRows[i].root.gameObject.activeSelf)
                shopRows[i].root.gameObject.SetActive(false);
        }

        float height = 66f + shown * 34f + 10f;
        shopPanel.sizeDelta = new Vector2(shopPanel.sizeDelta.x, height);
    }

    void RenderTags()
    {
        bool show = !Inventory.IsOpen && cam != null && rootGroup.alpha > 0.01f;
        int used = 0;
        if (show)
        {
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            for (int i = 0; i < tagRequests.Count && used < tags.Count; i++)
            {
                Vector3 screen = cam.WorldToScreenPoint(tagRequests[i].world);
                if (screen.z <= 0.3f)
                    continue;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                    continue;

                var tag = tags[used++];
                tag.root.gameObject.SetActive(true);
                tag.root.anchoredPosition = local;
                tag.title.text = tagRequests[i].title ?? "";
                tag.detail.text = tagRequests[i].detail ?? "";
                bool twoLines = !string.IsNullOrEmpty(tagRequests[i].detail);
                float w = Mathf.Max(tag.title.preferredWidth, twoLines ? tag.detail.preferredWidth : 0f) + 28f;
                tag.root.sizeDelta = new Vector2(Mathf.Clamp(w, 90f, 320f), twoLines ? 42f : 26f);
                tag.title.rectTransform.anchoredPosition = new Vector2(0f, twoLines ? -5f : -5f);
            }
        }

        for (int i = used; i < tags.Count; i++)
        {
            if (tags[i].root.gameObject.activeSelf)
                tags[i].root.gameObject.SetActive(false);
        }

        tagRequests.Clear();
    }
}
