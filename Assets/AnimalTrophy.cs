using UnityEngine;

/// <summary>
/// Every deer, wild horse and legendary animal rolls a quality when it spawns:
/// Common, Prime or Trophy, with its own size, weight and meat, and a small chance of a
/// rare coat (albino or melanistic) that is worth the most of all.
/// </summary>
public class AnimalTrophy : MonoBehaviour
{
    public enum Tier { Common, Prime, Trophy, Rare }
    public enum Coat { Normal, Albino, Melanistic }

    public Tier tier;
    public Coat coat;
    public float weightKg;
    public float sizeScale = 1f;

    public bool IsRare => coat != Coat.Normal;

    public static float XpMultiplier(Tier tier)
    {
        switch (tier)
        {
            case Tier.Prime: return 1.5f;
            case Tier.Trophy: return 2f;
            case Tier.Rare: return 3f;
            default: return 1f;
        }
    }

    /// <summary>Rolls the animal's quality. Called by Animal.Start after its model is attached.</summary>
    public static void Roll(Animal animal)
    {
        if (animal == null || animal.kind == AnimalKind.Bird || animal.GetComponent<AnimalTrophy>() != null)
            return;

        AnimalTrophy t = animal.gameObject.AddComponent<AnimalTrophy>();
        float r = Random.value;
        if (animal.kind == AnimalKind.Legendary)
            t.tier = Tier.Trophy;
        else if (r < 0.035f)
        {
            t.tier = Tier.Rare;
            t.coat = Random.value < 0.5f ? Coat.Albino : Coat.Melanistic;
        }
        else if (r < 0.14f)
            t.tier = Tier.Trophy;
        else if (r < 0.42f)
            t.tier = Tier.Prime;
        else
            t.tier = Tier.Common;

        switch (t.tier)
        {
            case Tier.Prime: t.sizeScale = Random.Range(1.05f, 1.1f); break;
            case Tier.Trophy:
            case Tier.Rare: t.sizeScale = Random.Range(1.12f, 1.2f); break;
            default: t.sizeScale = Random.Range(0.92f, 1.03f); break;
        }

        float baseKg;
        switch (animal.kind)
        {
            case AnimalKind.Large: baseKg = 320f; break;
            case AnimalKind.Legendary: baseKg = 230f; break;
            default: baseKg = 72f; break;
        }

        t.weightKg = baseKg * t.sizeScale * t.sizeScale * t.sizeScale * Random.Range(0.92f, 1.08f);
        animal.transform.localScale *= t.sizeScale;
        if (t.tier == Tier.Prime)
            animal.meatAmount += 1;
        else if (t.tier >= Tier.Trophy)
            animal.meatAmount += 2;

        if (t.coat != Coat.Normal)
            t.PaintCoat();
    }

    void PaintCoat()
    {
        Color tint = coat == Coat.Albino ? new Color(0.96f, 0.94f, 0.90f) : new Color(0.10f, 0.09f, 0.085f);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!(renderers[i] is SkinnedMeshRenderer))
                continue;

            // Drop the hide texture and paint a solid coat, so the colour reads at any distance.
            Material[] mats = renderers[i].materials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] == null)
                    continue;
                if (mats[m].HasProperty("_BaseMap"))
                    mats[m].SetTexture("_BaseMap", null);
                if (mats[m].HasProperty("_MainTex"))
                    mats[m].SetTexture("_MainTex", null);
                if (mats[m].HasProperty("_BaseColor"))
                    mats[m].SetColor("_BaseColor", tint);
                if (mats[m].HasProperty("_Color"))
                    mats[m].SetColor("_Color", tint);
            }

            renderers[i].materials = mats;
        }
    }

    /// <summary>"TROPHY DEER", "ALBINO DEER" and so on.</summary>
    public string Prefix()
    {
        if (coat == Coat.Albino)
            return "ALBINO ";
        if (coat == Coat.Melanistic)
            return "MELANISTIC ";
        if (tier == Tier.Trophy)
            return "TROPHY ";
        if (tier == Tier.Prime)
            return "PRIME ";
        return "";
    }
}
