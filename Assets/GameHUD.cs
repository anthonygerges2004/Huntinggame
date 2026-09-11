using UnityEngine;
using TMPro;

public class GameHUD : MonoBehaviour
{
    public TMP_Text hudText;

    void Update()
    {
        if (hudText == null)
            return;

        string health = PlayerHealth.Instance != null
            ? Mathf.CeilToInt(PlayerHealth.Instance.health).ToString()
            : "-";

        string time = DayNightCycle.Instance != null
            ? DayNightCycle.Instance.TimeLabel()
            : "-";

        int meat = Inventory.Instance != null ? Inventory.Instance.meat : 0;
        int cooked = Inventory.Instance != null ? Inventory.Instance.cookedMeat : 0;
        int wood = Inventory.Instance != null ? Inventory.Instance.wood : 0;
        bool pickaxe = Inventory.Instance != null && Inventory.Instance.hasPickaxe;
        bool fire = Campfire.PlayerIsSafe();
        float spoil = Inventory.Instance != null ? Inventory.Instance.SpoilSecondsLeft() : 0f;

        hudText.text =
            "Health: " + health +
            "   Time: " + time +
            "\nMeat: " + meat + " (spoils in " + Mathf.CeilToInt(spoil) + "s)" +
            "   Cooked: " + cooked +
            "   Wood: " + wood +
            "\nPickaxe: " + (pickaxe ? "Yes" : "No") +
            "   Fire nearby: " + (fire ? "Yes" : "No") +
            "\nF = fire   C = cook   E = chop tree   1 = sell meat at trader";
    }
}
