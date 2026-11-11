using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Fits working shops to the village's existing awnings without rebuilding the scene.</summary>
public class VillageMarket : MonoBehaviour
{
    readonly List<Material> materials = new List<Material>();
    Settlement village;
    Material timber, cream, green, red;

    public static void Install(Settlement settlement)
    {
        if (settlement.title != "Main village" && settlement.title != "Castle town" &&
            settlement.title != "Lakeside hamlet") return;
        if (settlement.GetComponent<VillageMarket>() != null) return;
        VillageMarket market = settlement.gameObject.AddComponent<VillageMarket>();
        market.village = settlement;
    }

    IEnumerator Start()
    {
        // Let the saved villagers initialize before assigning shopkeepers.
        yield return null;
        timber = MaterialFor(new Color(0.34f, 0.20f, 0.10f));
        cream = MaterialFor(new Color(0.94f, 0.83f, 0.59f));
        green = MaterialFor(new Color(0.28f, 0.43f, 0.18f));
        red = MaterialFor(new Color(0.56f, 0.18f, 0.13f));
        List<Transform> awnings = new List<Transform>();
        foreach (Transform candidate in FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
        {
            if (!candidate.name.StartsWith("rpgpp_lt_awning_standing_")) continue;
            Vector3 flat = candidate.position - village.center;
            flat.y = 0f;
            if (flat.sqrMagnitude > 35f * 35f) continue;
            // A mesh child may share the prefab's name. Only decorate the outermost root.
            if (candidate.parent != null && candidate.parent.name.StartsWith("rpgpp_lt_awning_standing_")) continue;
            awnings.Add(candidate);
        }
        awnings.Sort((a, b) => a.position.x != b.position.x ? a.position.x.CompareTo(b.position.x) : a.position.z.CompareTo(b.position.z));
        for (int i = 0; i < awnings.Count; i++) FitShop(awnings[i], i);
    }

    void FitShop(Transform awning, int index)
    {
        if (awning.GetComponentInChildren<CastleShop>() != null) return;
        Renderer[] renderers = awning.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        Vector3 center = WorldGround.Snap(bounds.center);
        Vector3 inward = village.center - center;
        inward.y = 0f;
        if (inward.sqrMagnitude < 0.01f) return;
        inward.Normalize();
        bool food = index % 2 == 1 || village.title == "Lakeside hamlet";
        GameObject root = new GameObject(food ? "Village food shop" : "Village timber shop");
        root.transform.SetPositionAndRotation(center, Quaternion.LookRotation(inward));
        root.transform.SetParent(transform, true);
        // All additions use world metres, independently of the imported prefab's scale.
        Transform decor = root.transform;
        float front = Mathf.Clamp(Mathf.Min(bounds.extents.x, bounds.extents.z), 0.8f, 2f);
        Vector3 interaction = WorldGround.Snap(center + inward * (front + 0.8f));
        GameObject trade = new GameObject("Trade here");
        trade.transform.SetParent(decor, true);
        trade.transform.position = interaction;
        trade.AddComponent<CastleShop>().Setup(food ? CastleShop.Kind.Meat : CastleShop.Kind.Wood, 2.2f, false);

        // Retain the original canopy, counter, baskets and crates; add a hanging identity sign.
        float signY = Mathf.Clamp(bounds.max.y - center.y - 0.4f, 1.8f, 3f);
        Box(decor, "Shop sign", new Vector3(0f, signY, front), new Vector3(2.3f, 0.5f, 0.09f), timber);
        SignText.Make(decor, food ? "FOOD MARKET" : "TIMBER & FURNITURE", new Vector3(0f, signY, front + 0.05f),
                      Quaternion.Euler(0f, 180f, 0f), new Vector2(2.1f, 0.42f), cream.color);

        // A compact goods shelf sits along the side of the existing stall.
        float side = Mathf.Clamp(bounds.extents.magnitude * 0.45f, 1.3f, 2f);
        Box(decor, "Goods shelf", new Vector3(side, 0.65f, 0f), new Vector3(0.7f, 0.12f, 1.1f), timber);
        for (int i = 0; i < 2; i++)
            Box(decor, "Shelf support", new Vector3(side, 0.32f, i == 0 ? -0.4f : 0.4f), new Vector3(0.1f, 0.64f, 0.1f), timber);
        for (int i = 0; i < 3; i++)
            Box(decor, food ? "Wrapped meat" : "Cut timber", new Vector3(side, 0.79f + (food ? 0f : i * 0.10f), (i - 1) * (food ? 0.28f : 0.04f)),
                food ? new Vector3(0.4f, 0.15f, 0.22f) : new Vector3(0.55f, 0.08f, 0.9f), food ? red : timber);
        Box(decor, "Shop banner", new Vector3(-side, 1.65f, 0f), new Vector3(0.4f, 0.6f, 0.04f), food ? red : green);
        Box(decor, "Banner post", new Vector3(-side, 0.95f, 0f), new Vector3(0.06f, 1.9f, 0.06f), timber);

        Villager keeper = null;
        float nearest = float.MaxValue;
        foreach (Villager person in FindObjectsByType<Villager>(FindObjectsInactive.Exclude))
        {
            if (person.settlement != village || person.role != Villager.Role.Wanderer) continue;
            float distance = (person.transform.position - center).sqrMagnitude;
            if (distance < nearest) { keeper = person; nearest = distance; }
        }
        if (keeper != null)
        {
            keeper.role = Villager.Role.Shopkeeper;
            keeper.transform.SetPositionAndRotation(WorldGround.Snap(center - inward * 0.6f), Quaternion.LookRotation(inward));
            keeper.gameObject.name = village.title + (food ? " food seller" : " carpenter and timber seller");
        }
    }

    Material MaterialFor(Color color)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        material.color = color;
        materials.Add(material);
        return material;
    }

    static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }

    void OnDestroy()
    {
        foreach (Material material in materials) if (material != null) Destroy(material);
    }
}
