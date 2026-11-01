using UnityEngine;
using UnityEngine.InputSystem;

public class Trader : MonoBehaviour
{
    public static Trader Instance;

    public float tradeRange = 4f;
    public int meatSellPriceWood = 1;
    public float legendaryClaimSeconds = 180f;

    bool legendaryTakenByPlayer;
    bool legendaryTakenByTrader;
    float rivalTimer;

    void Awake()
    {
        Instance = this;
        rivalTimer = legendaryClaimSeconds;
    }

    void Update()
    {
        TryRivalClaim();

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (Keyboard.current == null)
            return;

        if (Vector3.Distance(transform.position, PlayerView.Position()) > tradeRange)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            SellMeat();

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            SellWoodForPickaxeHint();

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            BuyPickaxe();
    }

    void SellMeat()
    {
        if (Inventory.Instance == null)
            return;

        if (!Inventory.Instance.RemoveOneSellableMeat())
        {
            Notice("No meat to sell.");
            return;
        }

        Inventory.Instance.AddWood(meatSellPriceWood);
        Notice("Sold meat for wood.");
    }

    void SellWoodForPickaxeHint()
    {
        Notice("Press 1 to sell meat. Press 3 to buy a pickaxe for 5 wood.");
    }

    void BuyPickaxe()
    {
        if (Inventory.Instance == null)
            return;

        if (Inventory.Instance.hasPickaxe)
        {
            Notice("You already have a pickaxe.");
            return;
        }

        if (!Inventory.Instance.UseWood(5))
        {
            Notice("Need 5 wood to buy a pickaxe.");
            return;
        }

        Inventory.Instance.hasPickaxe = true;
        Notice("Bought a pickaxe.");
    }

    static void Notice(string text)
    {
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(text);
        else
            Debug.Log(text);
    }

    void TryRivalClaim()
    {
        if (legendaryTakenByPlayer || legendaryTakenByTrader)
            return;

        rivalTimer -= Time.deltaTime;
        if (rivalTimer > 0f)
            return;

        Animal[] animals = FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        for (int i = 0; i < animals.Length; i++)
        {
            if (animals[i].kind == AnimalKind.Legendary)
                Destroy(animals[i].gameObject);
        }

        legendaryTakenByTrader = true;
        Debug.Log("The trader reached the legendary animal first.");
    }

    public void LegendaryTakenByPlayer()
    {
        legendaryTakenByPlayer = true;
        Debug.Log("You got the legendary animal before the trader.");
    }
}
