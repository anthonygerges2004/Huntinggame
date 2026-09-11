using UnityEngine;

public class Inventory : MonoBehaviour
{
    public static Inventory Instance;

    public int meat;
    public int cookedMeat;
    public int wood;
    public bool hasPickaxe = true;
    public float meatSpoilSeconds = 90f;

    float spoilTimer;

    void Awake()
    {
        Instance = this;
        spoilTimer = meatSpoilSeconds;
    }

    void Update()
    {
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

    public float SpoilSecondsLeft()
    {
        return meat > 0 ? spoilTimer : 0f;
    }
}
