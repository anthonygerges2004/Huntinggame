using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Career layer of the HUD:
//   bottom left, above vitals   rank strip with XP bar, and the Hunter's Instinct chip
//   challenges card             today's three contracts under the fixed challenges
//   pause menu                  JOURNAL page: rank, perks, records per species, contracts
public partial class GameHUD
{
    const float CareerStripHeight = 30f;
    const float ContractsBlockHeight = 104f;

    TMP_Text rankTitle, rankXp;
    RectTransform rankFill;
    int shownXp = -1;
    float xpFlash;

    Image instinctChipBg;
    TMP_Text instinctLabel;

    sealed class ContractRow
    {
        public Image dot;
        public TMP_Text label;
        public TMP_Text reward;
    }

    readonly ContractRow[] contractRows = new ContractRow[3];

    // Journal page
    TMP_Text jRankTitle, jRankXp, jNextPerk;
    RectTransform jRankFill;
    readonly List<(Image dot, TMP_Text text)> jPerks = new List<(Image, TMP_Text)>();
    readonly List<TMP_Text[]> jRecordRows = new List<TMP_Text[]>();
    readonly List<(Image dot, TMP_Text text, TMP_Text reward)> jContracts = new List<(Image, TMP_Text, TMP_Text)>();
    static readonly AnimalKind[] JournalSpecies = { AnimalKind.Deer, AnimalKind.Bird, AnimalKind.Large, AnimalKind.Legendary };
    static readonly string[] JournalSpeciesNames = { "Deer", "Birds", "Wild horse", "Legendary tiger" };

    // ---------------------------------------------------------------- rank strip & instinct chip

    void BuildCareer()
    {
        const float width = 320f;
        float bottom = Margin + vitalsHeight + 8f;
        RectTransform strip = Panel(canvas.transform, "Rank", new Vector2(0f, 0f), new Vector2(Margin, bottom),
                                    new Vector2(width, CareerStripHeight), PanelColor, 8f);

        rankTitle = Text(strip, "", 12.5f, Gold, TextAlignmentOptions.MidlineLeft, true);
        rankTitle.characterSpacing = 5f;
        rankTitle.richText = true;
        Place(rankTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(14f, 2f), new Vector2(200f, 18f), new Vector2(0f, 0.5f));

        rankXp = Text(strip, "", 11.5f, TextDim, TextAlignmentOptions.MidlineRight, true);
        Place(rankXp.rectTransform, new Vector2(1f, 0.5f), new Vector2(-14f, 2f), new Vector2(140f, 18f), new Vector2(1f, 0.5f));

        RectTransform track = Img(strip, "XpTrack", roundSprite, TrackColor, 1.5f).rectTransform;
        Place(track, new Vector2(0f, 0f), new Vector2(12f, 4f), new Vector2(width - 24f, 3f), new Vector2(0f, 0f));
        rankFill = Img(track, "Fill", roundSprite, Gold, 1.5f).rectTransform;
        Stretch(rankFill);

        RectTransform chip = Panel(canvas.transform, "Instinct", new Vector2(0f, 0f), new Vector2(Margin + width + 8f, bottom),
                                   new Vector2(176f, CareerStripHeight), PanelColor, 8f);
        instinctChipBg = chip.GetComponent<Image>();
        KeyCap(chip, "Tab", new Vector2(6f, -5f));
        instinctLabel = Text(chip, "INSTINCT", 11.5f, TextMain, TextAlignmentOptions.MidlineLeft, true);
        instinctLabel.characterSpacing = 4f;
        Place(instinctLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(54f, 0f), new Vector2(120f, 18f), new Vector2(0f, 0.5f));
    }

    void UpdateCareer(float dt)
    {
        HuntProgress.TickContracts();

        int xp = HuntProgress.Xp;
        if (xp != shownXp)
        {
            if (shownXp >= 0)
                xpFlash = 1f;
            shownXp = xp;
            HuntProgress.Rank rank = HuntProgress.Current;
            rankTitle.text = "<color=#8C8C86>RANK " + (HuntProgress.RankIndex + 1) + "</color>  " + rank.title;
            rankXp.text = HuntProgress.IsMaxRank
                ? xp + " XP"
                : xp + " / " + HuntProgress.Ranks[HuntProgress.RankIndex + 1].xp + " XP";
        }

        float target = HuntProgress.RankProgress01;
        float current = rankFill.anchorMax.x;
        if (target < current)
            current = 0f; // ranked up: refill from empty
        rankFill.anchorMax = new Vector2(Mathf.MoveTowards(current, target, dt * 0.8f), 1f);
        xpFlash = Mathf.MoveTowards(xpFlash, 0f, dt * 1.5f);
        rankXp.color = Color.Lerp(TextDim, Gold, xpFlash);

        string label;
        Color color;
        if (HuntExtras.InstinctActive)
        {
            label = "SENSING";
            color = Gold;
            instinctChipBg.color = new Color(0.26f, 0.18f, 0.06f, 0.9f);
        }
        else if (HuntExtras.InstinctCooldownLeft > 0f)
        {
            label = "INSTINCT  " + Mathf.CeilToInt(HuntExtras.InstinctCooldownLeft) + "s";
            color = TextDim;
            instinctChipBg.color = PanelColor;
        }
        else
        {
            label = "INSTINCT READY";
            color = TextMain;
            instinctChipBg.color = PanelColor;
        }

        if (instinctLabel.text != label)
            instinctLabel.text = label;
        instinctLabel.color = color;
    }

    // ---------------------------------------------------------------- contracts on the challenges card

    void BuildContractRows(RectTransform panel, float width, float pad, float top)
    {
        Image rule = Img(panel, "ContractsRule", null, new Color(1f, 1f, 1f, 0.07f));
        Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(pad, top + 4f), new Vector2(width - pad * 2f, 1f), new Vector2(0f, 1f));

        TMP_Text title = Text(panel, "TODAY'S CONTRACTS", 11.5f, TextDim, TextAlignmentOptions.MidlineLeft, true);
        title.characterSpacing = 5f;
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(pad, top - 6f), new Vector2(220f, 14f), new Vector2(0f, 1f));

        for (int i = 0; i < contractRows.Length; i++)
        {
            float y = top - 28f - i * 22f;
            ContractRow row = new ContractRow();
            row.dot = Img(panel, "ContractDot", ringSprite, TextDim);
            Place(row.dot.rectTransform, new Vector2(0f, 1f), new Vector2(pad + 6f, y - 8f), new Vector2(10f, 10f), new Vector2(0.5f, 0.5f));
            row.label = Text(panel, "", 13f, TextMain, TextAlignmentOptions.MidlineLeft, false);
            row.label.richText = true;
            row.label.overflowMode = TextOverflowModes.Ellipsis;
            Place(row.label.rectTransform, new Vector2(0f, 1f), new Vector2(pad + 20f, y), new Vector2(width - pad * 2f - 70f, 16f), new Vector2(0f, 1f));
            row.reward = Text(panel, "", 12f, Gold, TextAlignmentOptions.MidlineRight, true);
            Place(row.reward.rectTransform, new Vector2(1f, 1f), new Vector2(-pad, y), new Vector2(60f, 16f), new Vector2(1f, 1f));
            contractRows[i] = row;
        }
    }

    void UpdateContractRows()
    {
        List<HuntProgress.Contract> list = HuntProgress.Contracts;
        for (int i = 0; i < contractRows.Length; i++)
        {
            ContractRow row = contractRows[i];
            bool has = i < list.Count;
            row.dot.enabled = has;
            if (!has)
            {
                row.label.text = "";
                row.reward.text = "";
                continue;
            }

            HuntProgress.Contract c = list[i];
            string progress = c.need > 1 ? "  <color=#8C8C86>" + Mathf.Min(c.have, c.need) + "/" + c.need + "</color>" : "";
            string text = c.Done ? "<s>" + c.text + "</s>" : c.text + progress;
            if (row.label.text != text)
                row.label.text = text;
            row.label.color = c.Done ? TextDim : TextMain;
            row.dot.sprite = c.Done ? discSprite : ringSprite;
            row.dot.color = c.Done ? GoodColor : new Color(1f, 1f, 1f, 0.45f);
            string reward = c.Done ? "DONE" : "+" + c.reward;
            if (row.reward.text != reward)
                row.reward.text = reward;
            row.reward.color = c.Done ? GoodColor : Gold;
        }
    }

    // ---------------------------------------------------------------- journal page

    void BuildJournalPage(RectTransform page, float width)
    {
        PageTitle(page, "JOURNAL", width);

        jRankTitle = Text(page, "", 18f, Gold, TextAlignmentOptions.MidlineLeft, true);
        jRankTitle.characterSpacing = 6f;
        jRankTitle.richText = true;
        Place(jRankTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -42f), new Vector2(width * 0.65f, 24f), new Vector2(0f, 1f));
        jRankXp = Text(page, "", 13f, TextDim, TextAlignmentOptions.MidlineRight, true);
        Place(jRankXp.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -44f), new Vector2(width * 0.4f, 20f), new Vector2(1f, 1f));
        RectTransform track = Img(page, "RankTrack", roundSprite, TrackColor, 2f).rectTransform;
        Place(track, new Vector2(0f, 1f), new Vector2(0f, -72f), new Vector2(width, 5f), new Vector2(0f, 1f));
        jRankFill = Img(track, "Fill", roundSprite, Gold, 2f).rectTransform;
        Stretch(jRankFill);
        jNextPerk = Text(page, "", 12f, TextDim, TextAlignmentOptions.MidlineLeft, false);
        jNextPerk.richText = true;
        Place(jNextPerk.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -84f), new Vector2(width, 16f), new Vector2(0f, 1f));

        SectionTitle(page, "PERKS", -112f, width);
        float colW = width * 0.5f;
        for (int i = 1; i < HuntProgress.Ranks.Length; i++)
        {
            int n = i - 1;
            float x = (n % 2) * colW;
            float y = -134f - (n / 2) * 22f;
            Image dot = Img(page, "PerkDot", discSprite, TextDim);
            Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(x + 5f, y - 8f), new Vector2(8f, 8f), new Vector2(0.5f, 0.5f));
            TMP_Text text = Text(page, "", 12.5f, TextMain, TextAlignmentOptions.MidlineLeft, true);
            text.richText = true;
            Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(x + 16f, y), new Vector2(colW - 18f, 16f), new Vector2(0f, 1f));
            jPerks.Add((dot, text));
        }

        SectionTitle(page, "RECORDS", -214f, width);
        string[] headers = { "SPECIES", "KILLS", "HEAVIEST", "LONGEST", "TROPHIES" };
        float[] cols = { 0f, 0.38f, 0.53f, 0.71f, 0.87f };
        jRecordRows.Add(RecordRow(page, width, -234f, headers, cols, true));
        for (int i = 0; i < JournalSpecies.Length; i++)
            jRecordRows.Add(RecordRow(page, width, -256f - i * 22f, new[] { JournalSpeciesNames[i], "", "", "", "" }, cols, false));

        SectionTitle(page, "TODAY'S CONTRACTS", -354f, width);
        for (int i = 0; i < 3; i++)
        {
            float y = -376f - i * 22f;
            Image dot = Img(page, "ContractDot", ringSprite, TextDim);
            Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(5f, y - 8f), new Vector2(10f, 10f), new Vector2(0.5f, 0.5f));
            TMP_Text text = Text(page, "", 13.5f, TextMain, TextAlignmentOptions.MidlineLeft, false);
            text.richText = true;
            Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(18f, y), new Vector2(width - 100f, 16f), new Vector2(0f, 1f));
            TMP_Text reward = Text(page, "", 12.5f, Gold, TextAlignmentOptions.MidlineRight, true);
            Place(reward.rectTransform, new Vector2(1f, 1f), new Vector2(0f, y), new Vector2(90f, 16f), new Vector2(1f, 1f));
            jContracts.Add((dot, text, reward));
        }
    }

    void SectionTitle(RectTransform page, string text, float y, float width)
    {
        TMP_Text t = Text(page, text, 11f, TextDim, TextAlignmentOptions.MidlineLeft, true);
        t.characterSpacing = 6f;
        Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(width, 14f), new Vector2(0f, 1f));
        Image rule = Img(page, "Rule", null, new Color(1f, 1f, 1f, 0.06f));
        Place(rule.rectTransform, new Vector2(0f, 1f), new Vector2(0f, y - 16f), new Vector2(width, 1f), new Vector2(0f, 1f));
    }

    TMP_Text[] RecordRow(RectTransform page, float width, float y, string[] values, float[] cols, bool header)
    {
        TMP_Text[] cells = new TMP_Text[values.Length];
        for (int c = 0; c < values.Length; c++)
        {
            float x = cols[c] * width;
            float w = (c + 1 < cols.Length ? cols[c + 1] * width : width) - x;
            TextAlignmentOptions align = c == 0 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
            TMP_Text t = Text(page, values[c], header ? 10.5f : 13.5f, header ? TextDim : (c == 0 ? TextMain : Gold), align, header || c > 0);
            if (header)
                t.characterSpacing = 4f;
            Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w - 4f, 18f), new Vector2(0f, 1f));
            cells[c] = t;
        }

        return cells;
    }

    void UpdateJournalPage()
    {
        HuntProgress.Rank rank = HuntProgress.Current;
        jRankTitle.text = "<color=#8C8C86>RANK " + (HuntProgress.RankIndex + 1) + "</color>  " + rank.title;
        if (HuntProgress.IsMaxRank)
        {
            jRankXp.text = HuntProgress.Xp + " XP";
            jNextPerk.text = "Highest rank reached. Every perk is yours.";
        }
        else
        {
            HuntProgress.Rank next = HuntProgress.Ranks[HuntProgress.RankIndex + 1];
            jRankXp.text = HuntProgress.Xp + " / " + next.xp + " XP";
            jNextPerk.text = "Next: <color=#F5C466>" + next.title + "</color> unlocks " + next.perk.ToLowerInvariant()
                             + ". " + next.perkText;
        }

        jRankFill.anchorMax = new Vector2(HuntProgress.RankProgress01, 1f);

        for (int i = 0; i < jPerks.Count; i++)
        {
            int rankIndex = i + 1;
            HuntProgress.Rank r = HuntProgress.Ranks[rankIndex];
            bool owned = HuntProgress.Has(rankIndex);
            jPerks[i].dot.color = owned ? GoodColor : new Color(1f, 1f, 1f, 0.2f);
            jPerks[i].text.text = owned
                ? r.perk
                : "<color=#6E6E68>" + r.perk + "  ·  RANK " + (rankIndex + 1) + "</color>";
        }

        for (int i = 0; i < JournalSpecies.Length; i++)
        {
            HuntProgress.SpeciesRecord rec = HuntProgress.RecordFor(JournalSpecies[i]);
            TMP_Text[] row = jRecordRows[i + 1];
            bool any = rec.kills > 0;
            row[1].text = rec.kills.ToString();
            row[2].text = any && rec.heaviest > 0f ? Mathf.RoundToInt(rec.heaviest) + " kg" : "—";
            row[3].text = any ? Mathf.RoundToInt(rec.longest) + " m" : "—";
            row[4].text = any ? rec.trophies + (rec.rare > 0 ? " · " + rec.rare + " rare" : "") : "—";
            row[0].color = any ? TextMain : TextDim;
        }

        List<HuntProgress.Contract> list = HuntProgress.Contracts;
        for (int i = 0; i < jContracts.Count; i++)
        {
            bool has = i < list.Count;
            jContracts[i].dot.enabled = has;
            if (!has)
            {
                jContracts[i].text.text = "";
                jContracts[i].reward.text = "";
                continue;
            }

            HuntProgress.Contract c = list[i];
            jContracts[i].dot.sprite = c.Done ? discSprite : ringSprite;
            jContracts[i].dot.color = c.Done ? GoodColor : new Color(1f, 1f, 1f, 0.45f);
            jContracts[i].text.text = c.Done
                ? "<s>" + c.text + "</s>"
                : c.text + (c.need > 1 ? "  <color=#8C8C86>" + Mathf.Min(c.have, c.need) + "/" + c.need + "</color>" : "");
            jContracts[i].text.color = c.Done ? TextDim : TextMain;
            jContracts[i].reward.text = c.Done ? "DONE" : "+" + c.reward + " XP";
            jContracts[i].reward.color = c.Done ? GoodColor : Gold;
        }
    }
}
