using HuntingGame.Crafting;
using UnityEngine;
using UnityEngine.InputSystem;

public class Campfire : MonoBehaviour
{
    public static Campfire Instance;

    [Header("Campfire Settings")]
    public float safeRadius = 8f;
    public int woodCost = 1;
    [Tooltip("How long one wood keeps the fire burning.")]
    public float secondsPerWood = 55f;
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
    private float fuelSeconds;

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
    }

    void Update()
    {
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        if (Keyboard.current == null || GamePause.Active)
            return;

        if (Keyboard.current[fireKey].wasPressedThisFrame)
            ToggleFire();

        if (Keyboard.current[cookKey].wasPressedThisFrame)
            Cook();

        if (isLit)
            TickFuel();
    }

    void TickFuel()
    {
        fuelSeconds -= Time.deltaTime;
        if (fuelSeconds > 0f)
            return;

        fuelSeconds = 0f;
        SetLit(false, wentOutFromFuel: true);
    }

    void ToggleFire()
    {
        if (isLit)
        {
            if (TryFeedFire())
                return;

            SetLit(false);
            return;
        }

        if (Inventory.Instance == null || Inventory.Instance.wood < woodCost)
        {
            Debug.Log("Need wood to make a fire.");
            return;
        }

        if (SetLit(true))
        {
            Inventory.Instance.UseWood(woodCost);
            AddFuelFromWood(woodCost);
        }
    }

    bool TryFeedFire()
    {
        if (Inventory.Instance == null || Inventory.Instance.wood < woodCost)
            return false;

        if (!Inventory.Instance.UseWood(woodCost))
            return false;

        AddFuelFromWood(woodCost);
        Inventory.Instance.ShowNotice("Added wood to the fire.");
        return true;
    }

    void AddFuelFromWood(int woodPieces)
    {
        if (woodPieces <= 0 || secondsPerWood <= 0f)
            return;

        fuelSeconds += woodPieces * secondsPerWood;
    }

    bool SetLit(bool lit, bool wentOutFromFuel = false)
    {
        if (lit)
        {
            Vector3 targetPoint = PlayerView.AimOrigin() + PlayerView.AimDirection() * spawnDistance;
            Vector3 spawnPos = targetPoint;
            Vector3 groundNormal = Vector3.up;
            Quaternion spawnRot = Quaternion.identity;

            if (WorldGround.TryGetGround(targetPoint, out Vector3 groundPoint, out Vector3 terrainNormal))
            {
                spawnPos = groundPoint + terrainNormal * 0.02f;
                groundNormal = terrainNormal;
                spawnRot = Quaternion.FromToRotation(Vector3.up, groundNormal);
            }
            else if (Physics.Raycast(targetPoint + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 25f))
            {
                spawnPos = hit.point + hit.normal * 0.02f;
                groundNormal = hit.normal;
                spawnRot = Quaternion.FromToRotation(Vector3.up, groundNormal);
            }

            isLit = true;

            // Spawn campfire model
            if (fireModelPrefab != null)
            {
                fireInstance = Instantiate(fireModelPrefab, spawnPos, spawnRot);
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
            fireLight.intensity = 2.6f;

            if (fireInstance != null)
                CampfireFireEffects.Setup(fireInstance, fireLight, safeRadius);
            else
                CampfireFireEffects.Setup(lightObj, fireLight, safeRadius);

            if (Inventory.Instance != null)
                Inventory.Instance.ShowNotice("Campfire lit.");
            return true;
        }

        isLit = false;
        fuelSeconds = 0f;

        if (fireInstance != null)
            Destroy(fireInstance);

        if (fireLight != null)
            Destroy(fireLight.gameObject);

        fireInstance = null;
        fireLight = null;

        if (wentOutFromFuel && Inventory.Instance != null)
            Inventory.Instance.ShowNotice("Fire went out. Press F with wood to relight.");

        return true;
    }

    public float FuelSecondsRemaining => fuelSeconds;

    public Vector3 FireWorldPosition()
    {
        if (fireInstance != null)
            return fireInstance.transform.position;
        return transform.position;
    }

    void Cook()
    {
        if (CraftBook.Current == null)
        {
            if (!isLit)
            {
                Notice("Light a fire first, then press C to cook.");
                return;
            }

            if (!PlayerIsSafe())
            {
                Notice("Stand near the fire to cook.");
                return;
            }

            if (Inventory.Instance == null || !Inventory.Instance.CookOneMeat())
            {
                Notice("No raw meat to cook.");
                return;
            }

            Notice("Meat cooked. Press Q to eat.");
            return;
        }

        CraftResult result = CraftBook.Current.TryCookMeat();
        if (result == CraftResult.NeedFire)
            Notice("Light a fire first, then press C to cook.");
        else if (result == CraftResult.TooFar)
            Notice("Stand near the fire to cook.");
        else if (result == CraftResult.Missing)
            Notice("No raw meat to cook.");
        else
            Notice("Meat cooked. Press Q to eat.");
    }

    static void Notice(string text)
    {
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(text);
        else
            Debug.Log(text);
    }

    public static bool PlayerIsSafe()
    {
        if (Instance == null || !Instance.isLit)
            return false;

        Transform fireTransform = Instance.fireInstance != null
            ? Instance.fireInstance.transform
            : (Instance.fireLight != null ? Instance.fireLight.transform : null);
        if (fireTransform == null)
            return false;

        float dist = Vector3.Distance(fireTransform.position, PlayerView.Position());
        return dist <= Instance.safeRadius;
    }

    public static bool Covers(Vector3 worldPos)
    {
        if (Instance == null || !Instance.isLit)
            return false;

        Transform fireTransform = Instance.fireInstance != null
            ? Instance.fireInstance.transform
            : (Instance.fireLight != null ? Instance.fireLight.transform : null);
        if (fireTransform == null)
            return false;

        return Vector3.Distance(fireTransform.position, worldPos) <= Instance.safeRadius;
    }
}