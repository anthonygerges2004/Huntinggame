using UnityEngine;

public partial class Inventory
{
    static readonly HuntWeapon[] RankWeaponOrder =
        { HuntWeapon.Pistol, HuntWeapon.Shotgun, HuntWeapon.SMG, HuntWeapon.Rifle, HuntWeapon.Sniper };
    readonly bool[] rankWeaponClaimed = new bool[WeaponVisual.Count];
    readonly bool[,] ownedSkins = new bool[WeaponVisual.Count, GunSkins.Count];
    readonly int[] selectedSkins = new int[WeaponVisual.Count];
    float nextRankRewardCheck;

    public void GrantRankWeapons()
    {
        foreach (HuntWeapon weapon in RankWeaponOrder)
        {
            int i = (int)weapon;
            if (rankWeaponClaimed[i] || !HuntProgress.WeaponUnlocked(weapon)) continue;
            if (OwnsWeapon(weapon)) { rankWeaponClaimed[i] = true; continue; }
            if (!TryAddWeapon(weapon)) continue; // A full bag never discards a pending reward.
            rankWeaponClaimed[i] = true;
            ShowNotice("Rank reward: " + WeaponVisual.DisplayName(weapon) + " added to your bag for free.");
        }
    }

    public int SelectedSkin(HuntWeapon weapon) => ValidSkinWeapon(weapon) ? selectedSkins[(int)weapon] : 0;

    static bool ValidSkinWeapon(HuntWeapon weapon) => (int)weapon > 0 && (int)weapon < WeaponVisual.Count;

    public bool OwnsSkin(HuntWeapon weapon, int skin) => ValidSkinWeapon(weapon)
        && skin >= 0 && skin < GunSkins.Count && (skin == 0 || ownedSkins[(int)weapon, skin]);

    public bool BuyOrEquipSkin(HuntWeapon weapon, int skin, int price)
    {
        if (!ValidSkinWeapon(weapon) || skin < 0 || skin >= GunSkins.Count) return false;
        if (!HuntProgress.WeaponUnlocked(weapon) || !OwnsWeapon(weapon))
        {
            ShowNotice("Unlock and collect this gun before buying its skins.");
            return false;
        }
        if (!OwnsSkin(weapon, skin))
        {
            if (price <= 0 || !SpendCoins(price))
            {
                ShowNotice("Need " + price + " coins for this skin.");
                return false;
            }
            ownedSkins[(int)weapon, skin] = true;
        }
        selectedSkins[(int)weapon] = skin;
        if (PlayerShotgun.Instance != null) PlayerShotgun.Instance.RefreshSkin(weapon);
        ShowNotice(GunSkins.Name(skin) + " skin equipped on " + WeaponVisual.DisplayName(weapon) + ".");
        return true;
    }
}
