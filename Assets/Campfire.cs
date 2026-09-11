using UnityEngine;
using UnityEngine.InputSystem;

public class Campfire : MonoBehaviour
{
    public static Campfire Instance;

    public float safeRadius = 8f;
    public int woodCost = 1;
    public Key fireKey = Key.F;
    public Key cookKey = Key.C;

    public bool isLit;
    Light fireLight;
    GameObject fireObject;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current[fireKey].wasPressedThisFrame)
            ToggleFire();

        if (Keyboard.current[cookKey].wasPressedThisFrame)
            Cook();
    }

    void ToggleFire()
    {
        if (isLit)
        {
            SetLit(false);
            return;
        }

        if (Inventory.Instance == null || !Inventory.Instance.UseWood(woodCost))
        {
            Debug.Log("Need wood to make a fire.");
            return;
        }

        if (Camera.main != null)
        {
            Vector3 feet = Camera.main.transform.position;
            feet.y = transform.position.y;
            if (Mathf.Abs(feet.y) < 0.01f)
                feet.y = 0.2f;
            transform.position = new Vector3(Camera.main.transform.position.x, 0.2f, Camera.main.transform.position.z);
        }

        SetLit(true);
    }

    void SetLit(bool lit)
    {
        isLit = lit;

        if (lit && fireObject == null)
        {
            fireObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fireObject.name = "FireVisual";
            fireObject.transform.SetParent(transform);
            fireObject.transform.localPosition = Vector3.zero;
            fireObject.transform.localScale = new Vector3(0.6f, 0.2f, 0.6f);

            fireLight = fireObject.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.55f, 0.15f);
            fireLight.range = safeRadius;
        }

        if (fireObject != null)
            fireObject.SetActive(lit);

        if (fireLight != null)
        {
            fireLight.enabled = lit;
            fireLight.intensity = lit ? 3f : 0f;
        }
    }

    void Cook()
    {
        if (!isLit)
        {
            Debug.Log("Light a fire first, then press C to cook.");
            return;
        }

        if (Inventory.Instance == null || !Inventory.Instance.CookOneMeat())
        {
            Debug.Log("No raw meat to cook.");
            return;
        }

        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.Heal(15f);
    }

    public static bool PlayerIsSafe()
    {
        if (Instance == null || !Instance.isLit)
            return false;

        if (Camera.main == null)
            return false;

        float dist = Vector3.Distance(Instance.transform.position, Camera.main.transform.position);
        return dist <= Instance.safeRadius;
    }
}
