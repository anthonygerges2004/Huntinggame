using UnityEngine;
using UnityEngine.InputSystem;

public class Campfire : MonoBehaviour
{
    public static Campfire Instance;

    [Header("Campfire Settings")]
    public float safeRadius = 8f;
    public int woodCost = 1;
    public Key fireKey = Key.F;
    public Key cookKey = Key.C;
    public float spawnDistance = 3f;

    [Header("Prefab Assignment")]
    [Tooltip("Drag your campfire 3D prefab here from your Assets folder")]
    public GameObject fireModelPrefab;

    [Header("Status")]
    public bool isLit;

    private Light fireLight;
    private GameObject fireInstance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (GetComponent<Trader>() == null)
            gameObject.AddComponent<Trader>();

        // Fallback search if not assigned in Inspector
        if (fireModelPrefab == null)
        {
            fireModelPrefab = Resources.Load<GameObject>("campfire");
        }
    }

    void Update()
    {
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

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

        if (Inventory.Instance == null || Inventory.Instance.wood < woodCost)
        {
            Debug.Log("Need wood to make a fire.");
            return;
        }

        if (SetLit(true))
            Inventory.Instance.UseWood(woodCost);
    }

    bool SetLit(bool lit)
    {
        if (lit)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogError("Campfire: No camera tagged as 'MainCamera' found in the scene!");
                return false;
            }

            // Raycast downward from in front of camera to snap precisely to ground height
            Vector3 targetPoint = mainCam.transform.position + (mainCam.transform.forward * spawnDistance);
            Vector3 spawnPos = targetPoint;

            if (Physics.Raycast(targetPoint + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 10f))
            {
                spawnPos = hit.point;
            }

            isLit = true;

            // Spawn campfire model
            if (fireModelPrefab != null)
            {
                fireInstance = Instantiate(fireModelPrefab, spawnPos, Quaternion.identity);
                fireInstance.name = "CampfireInstance";
                fireInstance.SetActive(true);

                // Remove colliders so player can walk through
                Collider[] colliders = fireInstance.GetComponentsInChildren<Collider>();
                foreach (Collider col in colliders)
                {
                    Destroy(col);
                }
            }
            else
            {
                Debug.LogWarning("Campfire: fireModelPrefab is missing! Drag your prefab into the Campfire script in the Inspector.");
            }

            // Add point light slightly above campfire base
            GameObject lightObj = new GameObject("CampfireLight");
            if (fireInstance != null)
            {
                lightObj.transform.SetParent(fireInstance.transform);
                lightObj.transform.localPosition = Vector3.up * 0.5f;
            }
            else
            {
                lightObj.transform.position = spawnPos + Vector3.up * 0.5f;
            }

            fireLight = lightObj.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.55f, 0.15f);
            fireLight.range = safeRadius;
            fireLight.intensity = 2f;
            return true;
        }

        isLit = false;

        if (fireInstance != null)
            Destroy(fireInstance);

        if (fireLight != null)
            Destroy(fireLight.gameObject);

        fireInstance = null;
        fireLight = null;
        return true;
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

        Debug.Log("Meat cooked. Press Q to eat cooked meat.");
    }

    public static bool PlayerIsSafe()
    {
        if (Instance == null || !Instance.isLit || Camera.main == null)
            return false;

        Transform fireTransform = Instance.fireInstance != null
            ? Instance.fireInstance.transform
            : (Instance.fireLight != null ? Instance.fireLight.transform : null);
        if (fireTransform == null)
            return false;

        float dist = Vector3.Distance(fireTransform.position, Camera.main.transform.position);
        return dist <= Instance.safeRadius;
    }
}