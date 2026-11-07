using HuntingGame.Senses;
using NUnit.Framework;

// Loaded guns at close, mid, and far range, plus the cases where the best gun is empty.
public class GunChoiceFuzzyTests
{
    [Test]
    public void CloseTarget_PicksTheShotgun()
    {
        GunChoiceFuzzy.Choice choice = Full(16f);
        Assert.AreEqual(HuntWeapon.Shotgun, choice.weapon);
        Assert.Greater(choice.shotgun, choice.rifle);
    }

    [Test]
    public void MidRange_PicksTheRifle()
    {
        GunChoiceFuzzy.Choice choice = Full(80f);
        Assert.AreEqual(HuntWeapon.Rifle, choice.weapon);
        Assert.Greater(choice.rifle, choice.shotgun);
        Assert.Greater(choice.rifle, choice.sniper);
    }

    [Test]
    public void WorkingRange_KeepsTheRifle()
    {
        GunChoiceFuzzy.Choice choice = Full(40f);
        Assert.AreEqual(HuntWeapon.Rifle, choice.weapon);
        Assert.Greater(choice.rifle, choice.shotgun);

        choice = Full(150f);
        Assert.AreEqual(HuntWeapon.Rifle, choice.weapon);
        Assert.Greater(choice.rifle, choice.sniper);
    }

    [Test]
    public void FarTarget_PicksTheSniper()
    {
        GunChoiceFuzzy.Choice choice = Full(200f);
        Assert.AreEqual(HuntWeapon.Sniper, choice.weapon);
        Assert.Greater(choice.sniper, choice.rifle);
    }

    [Test]
    public void HeldRifle_StaysWhenTheSniperIsOnlySlightlyBetter()
    {
        GunChoiceFuzzy.Choice choice = GunChoiceFuzzy.Choose(new GunChoiceFuzzy.Sample
        {
            distance = 172f,
            shotgunRounds = 2,
            shotgunMag = 2,
            rifleRounds = 5,
            rifleMag = 5,
            sniperRounds = 4,
            sniperMag = 4,
            equipped = HuntWeapon.Rifle
        });

        Assert.AreEqual(HuntWeapon.Rifle, choice.weapon);
    }

    [Test]
    public void CloseShotgunEmpty_PicksTheRifle()
    {
        GunChoiceFuzzy.Choice choice = GunChoiceFuzzy.Choose(new GunChoiceFuzzy.Sample
        {
            distance = 16f,
            shotgunRounds = 0,
            shotgunMag = 2,
            rifleRounds = 5,
            rifleMag = 5,
            sniperRounds = 4,
            sniperMag = 4
        });

        Assert.AreEqual(HuntWeapon.Rifle, choice.weapon);
        Assert.AreEqual(0f, choice.shotgun);
    }

    [Test]
    public void FarSniperEmpty_PicksTheRifle()
    {
        GunChoiceFuzzy.Choice choice = GunChoiceFuzzy.Choose(new GunChoiceFuzzy.Sample
        {
            distance = 200f,
            shotgunRounds = 2,
            shotgunMag = 2,
            rifleRounds = 5,
            rifleMag = 5,
            sniperRounds = 0,
            sniperMag = 4
        });

        Assert.AreEqual(HuntWeapon.Rifle, choice.weapon);
        Assert.AreEqual(0f, choice.sniper);
    }

    [Test]
    public void NothingLoaded_PicksNoGun()
    {
        GunChoiceFuzzy.Choice choice = GunChoiceFuzzy.Choose(new GunChoiceFuzzy.Sample
        {
            distance = 80f,
            shotgunMag = 2,
            rifleMag = 5,
            sniperMag = 4
        });

        Assert.AreEqual(HuntWeapon.None, choice.weapon);
    }

    static GunChoiceFuzzy.Choice Full(float distance)
    {
        return GunChoiceFuzzy.Choose(new GunChoiceFuzzy.Sample
        {
            distance = distance,
            shotgunRounds = 2,
            shotgunMag = 2,
            rifleRounds = 5,
            rifleMag = 5,
            sniperRounds = 4,
            sniperMag = 4
        });
    }
}
