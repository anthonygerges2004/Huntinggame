using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A downed animal waiting to be dealt with. Walk up to it:
///   tap E    take the whole carcass into the bag, for the tanner to skin properly
///   hold E   skin it here yourself: quicker, but a rough cut (less meat, cheaper pelt)
/// The carcass keeps the animal's species, quality and the condition left by the killing shot.
/// </summary>
public class AnimalCarcass : MonoBehaviour
{
    public const float Reach = 2.8f;
    public const float HoldToSkin = 2.4f;
    const float TapWindow = 0.4f;

    static readonly List<AnimalCarcass> all = new List<AnimalCarcass>();
    static AnimalCarcass holdTarget;
    static float holdTime;
    static bool holding;

    public HuntGoods goods;

    /// <summary>The carcass the player is standing at, if any.</summary>
    public static AnimalCarcass Focus { get; private set; }
    public static bool PlayerNear => Focus != null;
    public static float SkinProgress01 => holding && holdTime > TapWindow ? Mathf.Clamp01(holdTime / HoldToSkin) : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlayMode()
    {
        all.Clear();
        Focus = null;
        holdTarget = null;
        holding = false;
        holdTime = 0f;
    }

    public static AnimalCarcass Create(Animal animal, HuntGoods goods)
    {
        if (animal == null || goods == null)
            return null;
        AnimalCarcass c = animal.gameObject.AddComponent<AnimalCarcass>();
        c.goods = goods;
        return c;
    }

    void OnEnable()
    {
        if (!all.Contains(this))
            all.Add(this);
    }

    void OnDisable()
    {
        all.Remove(this);
        if (Focus == this)
            Focus = null;
    }

    /// <summary>Called by the inventory every frame while the bag is closed.</summary>
    public static void Tick(Inventory inv)
    {
        Focus = Nearest();
        Keyboard kb = Keyboard.current;
        if (Focus == null || kb == null || inv == null)
        {
            holding = false;
            holdTime = 0f;
            return;
        }

        if (kb.eKey.wasPressedThisFrame)
        {
            holding = true;
            holdTime = 0f;
            holdTarget = Focus;
        }

        if (holding)
        {
            if (holdTarget != Focus)
                holding = false;
            else if (kb.eKey.isPressed)
            {
                holdTime += Time.deltaTime;
                if (holdTime >= HoldToSkin)
                {
                    holding = false;
                    inv.FieldSkin(Focus);
                    return;
                }
            }
            else
            {
                if (holdTime <= TapWindow)
                    inv.TakeCarcass(Focus);
                else if (inv != null)
                    inv.ShowNotice("Skinning stopped.");
                holding = false;
            }
        }

        if (Focus != null && GameHUD.Instance != null)
        {
            HuntGoods g = Focus.goods;
            string title = g.Title + "  ·  " + Mathf.RoundToInt(g.weightKg) + " KG";
            if (RivalHunter.IsClaimed(Focus)) title = "ROWAN'S CLAIM · " + title;
            string detail = SkinProgress01 > 0f
                ? "Skinning…  " + Mathf.RoundToInt(SkinProgress01 * 100f) + "%"
                : "E  take carcass     hold E  skin here";
            if (RivalHunter.IsClaimed(Focus)) detail += "  (taking this loses trust)";
            GameHUD.Instance.ShowWorldTag(Focus.transform.position + Vector3.up * 1.4f, title, detail);
        }
    }

    static AnimalCarcass Nearest()
    {
        Vector3 me = PlayerView.Position();
        AnimalCarcass best = null;
        float bestSq = Reach * Reach;
        for (int i = 0; i < all.Count; i++)
        {
            AnimalCarcass c = all[i];
            if (c == null || c.goods == null)
                continue;
            Vector3 d = c.transform.position - me;
            if (Mathf.Abs(d.y) > 2.5f)
                continue;
            d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = c;
            }
        }

        return best;
    }
}
