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
    bool addingWood;

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
        if (Inventory.IsOpen || CampfireBuild.Busy || addingWood) return;
        if (isLit)
        {
            TryFeedFire();
            return;
        }

        if (Inventory.Instance == null)
            return;

        if (!Inventory.Instance.LighterWorks)
        {
            if (Inventory.Instance.LighterDueBack)
                Notice("The rented lighter's time is up. Return it to the gun shop.");
            else
                Notice("You need a lighter. Buy or rent one at the castle gun shop.");
            return;
        }

        if (Inventory.Instance.wood < woodCost)
        {
            Notice("Need wood to lay a fire.");
            return;
        }

        if (CampfireBuild.Busy)
            return;

        if (!Inventory.Instance.UseWood(woodCost))
            return;

        if (!CampfireBuild.Begin(this, woodCost))
            Inventory.Instance.GiveWood(woodCost);
    }

    public void AdoptPile(GameObject pile)
    {
        if (pile == null)
            return;

        if (fireInstance != null && fireInstance != pile)
            Destroy(fireInstance);

        fireInstance = pile;
        fireInstance.name = "CampfireInstance";
        isLit = true;

        GameObject lightObj = new GameObject("CampfireLight");
        lightObj.transform.SetParent(fireInstance.transform, false);
        lightObj.transform.localPosition = Vector3.up * 0.45f;
        fireLight = lightObj.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.55f, 0.15f);
        fireLight.range = safeRadius;
        fireLight.intensity = 2.6f;
        CampfireFireEffects.Setup(fireInstance, fireLight, safeRadius);
        AddFuelFromWood(woodCost);
        Notice("Campfire lit.");
    }

    bool TryFeedFire()
    {
        if (addingWood || fireInstance == null || PlayerHealth.Instance == null) return false;
        if (Vector3.Distance(PlayerHealth.Instance.transform.position, fireInstance.transform.position) > 2.7f)
        {
            Notice("Move closer to the campfire to add wood.");
            return false;
        }
        if (Inventory.Instance == null || Inventory.Instance.wood < woodCost)
        {
            Notice("You need wood to feed the fire.");
            return false;
        }
        StartCoroutine(PlaceFuelLog());
        return true;
    }

    System.Collections.IEnumerator PlaceFuelLog()
    {
        addingWood = true;
        GameObject log = null;
        Material bark = null;
        try
        {
            Transform player = PlayerHealth.Instance.transform;
            GameObject pile = fireInstance;
            log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = "Added firewood";
            Destroy(log.GetComponent<Collider>());
            log.transform.localScale = new Vector3(0.12f, 0.28f, 0.10f);
            bark = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            bark.color = new Color(0.24f, 0.12f, 0.055f);
            log.GetComponent<Renderer>().sharedMaterial = bark;
            Vector3 start = player.position + player.forward * 0.45f + player.right * 0.25f + Vector3.up * 0.9f;
            Vector3 end = pile.transform.TransformPoint(new Vector3(Random.Range(-0.12f, 0.12f), 0.26f, 0f));
            Quaternion rest = pile.transform.rotation * Quaternion.Euler(90f, Random.Range(-25f, 25f), 0f);
            PlayerBodyAnimation.PlayLightFire();
            Notice("Placing wood on the fire...");
            for (float elapsed = 0f; elapsed < 1.1f; elapsed += Time.deltaTime)
            {
                if (pile == null || !isLit || PlayerHealth.Instance == null || PlayerHealth.Instance.isDead ||
                    Vector3.Distance(player.position, pile.transform.position) > 2.7f)
                    yield break;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / 1.1f);
                log.transform.position = Vector3.Lerp(start, end, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.12f);
                log.transform.rotation = Quaternion.Slerp(player.rotation * Quaternion.Euler(0f, 0f, 75f), rest, t);
                yield return null;
            }
            if (Inventory.Instance == null || !Inventory.Instance.UseWood(woodCost))
            {
                Notice("You need wood to feed the fire.");
                yield break;
            }
            log.transform.SetPositionAndRotation(end, rest);
            log.transform.SetParent(pile.transform, true);
            AddFuelFromWood(woodCost);
            Notice("Added wood to the fire.");
            Destroy(log, Mathf.Max(1f, secondsPerWood * woodCost));
            Destroy(bark, Mathf.Max(1f, secondsPerWood * woodCost));
            log = null;
            bark = null;
        }
        finally
        {
            if (log != null) Destroy(log);
            if (bark != null) Destroy(bark);
            addingWood = false;
        }
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
        if (CastleShop.AtButchery())
        {
            if (Inventory.Instance == null || !Inventory.Instance.CookOneMeat())
            {
                Notice("No raw meat to cook.");
                return;
            }

            Notice("The butcher cooked it. Press Q to eat.");
            return;
        }

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