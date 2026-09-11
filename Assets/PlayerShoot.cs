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

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        WildlifeAlarm.NotifyShot(cam.transform.position);

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, range))
        {
            Animal animal = hit.collider.GetComponent<Animal>();
            if (animal != null)
                animal.Hit();
        }
    }

    void OnGUI()
    {
        float size = 8f;
        float x = Screen.width * 0.5f;
        float y = Screen.height * 0.5f;
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(x - size, y - 1f, size * 2f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 1f, y - size, 2f, size * 2f), Texture2D.whiteTexture);
    }
}
