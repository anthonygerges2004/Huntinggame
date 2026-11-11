using HuntingGame.Animals;
using HuntingGame.Senses;
using HuntingGame.Wind;
using UnityEngine;

/// <summary>
/// Looks at the animal in front of the hunter and asks the fuzzy gun picker
/// which inventory weapon fits that distance, that animal, and the rounds still loaded.
/// </summary>
public class GunAdvisor : MonoBehaviour
{
    public static string Line { get; private set; } = "";

    [Tooltip("Seconds between looks. 0.2 is five times a second.")]
    public float lookInterval = 0.2f;

    public GunChoiceFuzzy.Bands bands = GunChoiceFuzzy.Bands.Standard();

    [Tooltip("Print each gun's score when a target is in front of the hunter.")]
    public bool logScores;

    [Tooltip("Equip the recommended gun through the hotbar. Off leaves the choice as a hint.")]
    public bool autoSwitch;

    [Header("Hypothetical")]
    public float hypotheticalDistance = 16f;
    public float hypotheticalSize = 0.5f;
    public float hypotheticalSpeed;
    public float hypotheticalAlert;
    public float hypotheticalWind = 0.2f;
    public int hypotheticalShotgunRounds = 2;
    public int hypotheticalRifleRounds = 5;
    public int hypotheticalSniperRounds = 4;

    float nextLook;

    void Awake()
    {
        if (bands.shotgunCloseGone <= bands.shotgunCloseFull)
            bands = GunChoiceFuzzy.Bands.Standard();
    }

    void LateUpdate()
    {
        if (Time.time < nextLook)
            return;

        float interval = lookInterval > 0.02f ? lookInterval : 0.2f;
        nextLook = Time.time + interval;
        Line = BuildLine();
    }

    string BuildLine()
    {
        if (PlayerShotgun.Instance == null || !TryAim(out Animal animal, out float distance))
            return "";

        PlayerShotgun guns = PlayerShotgun.Instance;
        if (!guns.TryRounds(HuntWeapon.Shotgun, out int shotgunRounds, out int shotgunMag, out float shotgunRange))
            return "";

        guns.TryRounds(HuntWeapon.Rifle, out int rifleRounds, out int rifleMag, out float rifleRange);
        guns.TryRounds(HuntWeapon.Sniper, out int sniperRounds, out int sniperMag, out float sniperRange);

        GunChoiceFuzzy.Choice choice = GunChoiceFuzzy.Choose(new GunChoiceFuzzy.Sample
        {
            distance = distance,
            shotgunRounds = shotgunRounds,
            shotgunMag = shotgunMag,
            shotgunRange = shotgunRange,
            rifleRounds = rifleRounds,
            rifleMag = rifleMag,
            rifleRange = rifleRange,
            sniperRounds = sniperRounds,
            sniperMag = sniperMag,
            sniperRange = sniperRange,
            equipped = guns.Equipped
        }, TargetOf(animal), bands);

        if (logScores)
            Debug.Log(ScoreLine(choice, animal.kind.ToString(), distance));

        if (autoSwitch && !Inventory.IsOpen && choice.weapon != HuntWeapon.None && choice.weapon != guns.Equipped)
            TrySwitch(choice.weapon);

        int meters = Mathf.RoundToInt(distance);
        string who = animal.kind.ToString();
        if (choice.weapon == HuntWeapon.None)
            return (choice.loadedButOutOfReach ? "Too far" : "Out of ammo") + "  ·  " + who + "  ·  " + meters + " m";

        string name = WeaponName(choice.weapon);
        if (guns.Equipped == choice.weapon)
            return name + " fits  ·  " + who + "  ·  " + meters + " m";

        int key = Inventory.Instance != null ? Inventory.Instance.HotbarKeyFor(choice.weapon) : 0;
        string keyText = key > 0 ? key + "  " : "";
        return "Use " + keyText + name + "  ·  " + who + "  ·  " + meters + " m";
    }

    static void TrySwitch(HuntWeapon weapon)
    {
        if (Inventory.Instance == null)
            return;

        int key = Inventory.Instance.HotbarKeyFor(weapon);
        if (key > 0)
            Inventory.Instance.SelectHotbar(key - 1, false);
    }

    static GunChoiceFuzzy.Target TargetOf(Animal animal)
    {
        float wind = WindSystem.Current != null ? WindSystem.Current.Strength : 0.2f;
        return new GunChoiceFuzzy.Target
        {
            size = SizeOf(animal),
            speed = SpeedOf(animal),
            alertness = AlertOf(animal),
            wind = wind
        };
    }

    static float SizeOf(Animal animal)
    {
        switch (animal.kind)
        {
            case AnimalKind.Bird:
                return FlyerProfile.Of(animal.flyer).gunSize;
            case AnimalKind.Game:
                return QuarryProfile.Of(animal.quarry).gunSize;
            case AnimalKind.Large:
            case AnimalKind.Legendary:
                return 1f;
            default:
                return 0.5f;
        }
    }

    static float SpeedOf(Animal animal)
    {
        if (animal.Fleeing && !animal.Limping)
            return 1f;
        if (animal.Limping)
            return 0.35f;
        return 0f;
    }

    static float AlertOf(Animal animal)
    {
        AnimalBrain brain = animal.GetComponent<AnimalBrain>();
        if (brain == null)
            return animal.Fleeing ? 1f : 0f;

        switch (brain.State)
        {
            case AnimalAiState.Alert:
                return 0.65f;
            case AnimalAiState.Flee:
                return 1f;
            default:
                return animal.Fleeing ? 1f : 0f;
        }
    }

    static bool TryAim(out Animal aimed, out float distance)
    {
        aimed = null;
        distance = 0f;
        Ray ray = PlayerView.AimRay();
        if (Physics.Raycast(ray, out RaycastHit hit, 520f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Animal struck = hit.collider.GetComponentInParent<Animal>();
            if (struck != null && !struck.Dead)
            {
                aimed = struck;
                distance = hit.distance;
                return true;
            }
        }

        Animal[] animals = Object.FindObjectsByType<Animal>(FindObjectsInactive.Exclude);
        float bestAngle = 8f;
        float bestLength = 520f;
        bool found = false;
        for (int i = 0; i < animals.Length; i++)
        {
            Animal animal = animals[i];
            if (animal == null || animal.Dead)
                continue;

            Vector3 to = animal.transform.position + Vector3.up * 1.05f - ray.origin;
            float length = to.magnitude;
            if (length < 1.5f || length > 500f)
                continue;

            float angle = Vector3.Angle(ray.direction, to);
            if (angle > 8f)
                continue;

            bool tighter = angle < bestAngle - 0.5f;
            bool sameAimAndCloser = angle <= bestAngle + 0.5f && length < bestLength;
            if (found && !tighter && !sameAimAndCloser)
                continue;

            bestAngle = angle;
            bestLength = length;
            aimed = animal;
            found = true;
        }

        if (!found)
            return false;

        distance = bestLength;
        return true;
    }

    [ContextMenu("Score Hypothetical")]
    void ScoreHypothetical()
    {
        if (bands.shotgunCloseGone <= bands.shotgunCloseFull)
            bands = GunChoiceFuzzy.Bands.Standard();

        GunChoiceFuzzy.Choice choice = GunChoiceFuzzy.Choose(new GunChoiceFuzzy.Sample
        {
            distance = hypotheticalDistance,
            shotgunRounds = hypotheticalShotgunRounds,
            shotgunMag = 2,
            shotgunRange = 120f,
            rifleRounds = hypotheticalRifleRounds,
            rifleMag = 5,
            rifleRange = 240f,
            sniperRounds = hypotheticalSniperRounds,
            sniperMag = 4,
            sniperRange = 500f
        }, new GunChoiceFuzzy.Target
        {
            size = hypotheticalSize,
            speed = hypotheticalSpeed,
            alertness = hypotheticalAlert,
            wind = hypotheticalWind
        }, bands);

        Debug.Log(ScoreLine(choice, "hypothetical", hypotheticalDistance));
    }

    static string WeaponName(HuntWeapon weapon)
    {
        if (weapon == HuntWeapon.Shotgun)
            return "Shotgun";
        if (weapon == HuntWeapon.Rifle)
            return "Rifle";
        if (weapon == HuntWeapon.Sniper)
            return "Sniper";
        return "None";
    }

    static string ScoreLine(GunChoiceFuzzy.Choice choice, string who, float distance)
    {
        int meters = Mathf.RoundToInt(distance);
        return "Gun scores (" + who + ", " + meters + " m)  shotgun " + choice.shotgun.ToString("0.00")
            + "  rifle " + choice.rifle.ToString("0.00")
            + "  sniper " + choice.sniper.ToString("0.00")
            + "  → " + WeaponName(choice.weapon);
    }
}
