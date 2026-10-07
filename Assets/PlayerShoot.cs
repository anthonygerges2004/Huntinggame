using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShoot : MonoBehaviour
{
    public float range = 100f;

    Camera cam;

    void Start()
    {
        cam = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        if (cam == null || Mouse.current == null)
            return;

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        WildlifeAlarm.NotifyShot(cam.transform.position);

        if (WoodBuilder.IsBuilding)
            return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);

        if (BirdChallenge.Active)
        {
            if (BirdChallenge.TryUseShot())
                ShootChallengeBird(ray);
            return;
        }

        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            if (hit.collider.GetComponentInParent<HarvestableTree>() != null)
                return;

            Animal animal = hit.collider.GetComponentInParent<Animal>();
            if (animal != null)
                animal.Hit();
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

    void OnGUI()
    {
        float size = 12f;
        float thick = 3f;
        float x = Screen.width * 0.5f;
        float y = Screen.height * 0.5f;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(x - size, y - thick * 0.5f, size * 2f, thick), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - thick * 0.5f, y - size, thick, size * 2f), Texture2D.whiteTexture);
    }
}
