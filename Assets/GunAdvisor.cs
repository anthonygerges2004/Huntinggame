using HuntingGame.Senses;
using UnityEngine;

/// <summary>
/// Looks at the animal in front of the hunter and asks the fuzzy gun picker
/// which inventory weapon fits that distance and the rounds still loaded.
/// </summary>
public class GunAdvisor : MonoBehaviour
{
    public static string Line { get; private set; } = "";

    float nextLook;

    void LateUpdate()
    {
        if (Time.time < nextLook)
            return;

        nextLook = Time.time + 0.2f;
        Line = BuildLine();
    }

    static string BuildLine()
    {
        if (PlayerShotgun.Instance == null || !TryDistance(out float distance))
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
        });

        int meters = Mathf.RoundToInt(distance);
        if (choice.weapon == HuntWeapon.None)
            return (choice.loadedButOutOfReach ? "Too far" : "Out of ammo") + "  ·  " + meters + " m";

        string name = choice.weapon == HuntWeapon.Shotgun ? "Shotgun"
            : choice.weapon == HuntWeapon.Rifle ? "Rifle" : "Sniper";
        if (guns.Equipped == choice.weapon)
            return name + " fits  ·  " + meters + " m";

        int key = Inventory.Instance != null ? Inventory.Instance.HotbarKeyFor(choice.weapon) : 0;
        string keyText = key > 0 ? key + "  " : "";
        return "Use " + keyText + name + "  ·  " + meters + " m";
    }

    static bool TryDistance(out float distance)
    {
        distance = 0f;
        Ray ray = PlayerView.AimRay();
        if (Physics.Raycast(ray, out RaycastHit aimed, 520f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            Animal hit = aimed.collider.GetComponentInParent<Animal>();
            if (hit != null && !hit.Dead)
            {
                distance = aimed.distance;
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
            found = true;
        }

        if (!found)
            return false;

        distance = bestLength;
        return true;
    }
}
