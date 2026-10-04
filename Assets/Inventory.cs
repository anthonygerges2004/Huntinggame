using UnityEngine;
using UnityEngine.InputSystem;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance;

    public int meat;
    public int cookedMeat;
    public int wood;
    public bool hasPickaxe = true;
    public float meatSpoilSeconds = 90f;
    public float cookedHealAmount = 20f;

    float spoilTimer;
    string notice;
    float noticeUntil;

    void Awake()
    {
        Instance = this;
        spoilTimer = meatSpoilSeconds;
        Overhunt.ResetProgress();

        if (GetComponent<HuntWorldDressing>() == null)
            gameObject.AddComponent<HuntWorldDressing>();
    }

    void Update()
    {
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (Keyboard.current != null &&
            Keyboard.current.qKey.wasPressedThisFrame)
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
            meat = 0;
            spoilTimer = meatSpoilSeconds;
            Debug.Log("Raw meat spoiled. Cook it on a fire next time.");
        }
    }

    public void AddMeat(int amount)
    {
        meat += amount;
        spoilTimer = meatSpoilSeconds;
    }

    public void AddWood(int amount)
    {
        wood += amount;
    }

    public bool UseWood(int amount)
    {
        if (wood < amount)
            return false;

        wood -= amount;
        return true;
    }

    public bool CookOneMeat()
    {
        if (meat <= 0)
            return false;

        meat -= 1;
        cookedMeat += 1;
        spoilTimer = meatSpoilSeconds;
        return true;
    }

    public bool EatCookedMeat()
    {
        if (cookedMeat <= 0)
            return false;

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return false;

        cookedMeat -= 1;
        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.Heal(cookedHealAmount);
        return true;
    }

    public void ShowNotice(string text)
    {
        notice = text;
        noticeUntil = Time.time + 2f;
    }

    void OnGUI()
    {
        if (Time.time > noticeUntil || string.IsNullOrEmpty(notice))
            return;

        ScreenText.Centered(notice, Screen.height * 0.7f);
    }

    public float SpoilSecondsLeft()
    {
        return meat > 0 ? spoilTimer : 0f;
    }
}
