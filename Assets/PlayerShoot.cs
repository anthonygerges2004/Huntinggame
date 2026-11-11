using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShoot : MonoBehaviour
{
    public float range = 100f;

    void Update()
    {
        if (Mouse.current == null)
            return;

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (Inventory.IsOpen)
            return;

        bool held = Mouse.current.leftButton.isPressed && PlayerShotgun.IsEquipped
            && PlayerShotgun.Instance != null && PlayerShotgun.Instance.IsAutomatic && !BirdChallenge.Active;
        if (!Mouse.current.leftButton.wasPressedThisFrame && !held)
            return;

        if (WoodBuilder.IsBuilding)
            return;

        Ray ray = PlayerView.AimRay();

        if (BirdChallenge.Active)
        {
            if (BirdChallenge.TryUseShot())
            {
                WildlifeAlarm.NotifyShot(PlayerView.Position());
                ShootChallengeBird(ray);
            }
            return;
        }

        if (PlayerShotgun.IsEquipped)
        {
            if (PlayerShotgun.Instance != null)
                PlayerShotgun.Instance.TryFire();
            return;
        }
    }

    void ShootChallengeBird(Ray ray)
    {
        ChallengeBird closest = null;
        float closestDistance = float.MaxValue;
        foreach (RaycastHit hit in Physics.SphereCastAll(ray, 0.25f, range, ~0, QueryTriggerInteraction.Collide))
        {
            ChallengeBird bird = hit.collider.GetComponentInParent<ChallengeBird>();
            if (bird != null && hit.distance < closestDistance)
            {
                closest = bird;
                closestDistance = hit.distance;
            }
        }

        if (closest != null)
            closest.Hit();
    }
}
