using UnityEngine;
using UnityEngine.InputSystem;

public class FishingSpot : MonoBehaviour
{
    public float castRange = 12f;
    public float minWait = 3f;
    public float maxWait = 8f;
    public float biteWindow = 1.2f;

    static Material bobberMat;

    Transform player;
    Camera cam;
    LakeWater lake;
    bool fishing;
    float waitUntil;
    float biteStart = -1f;
    Vector3 castPoint;
    GameObject bobber;
    LakeFish fish;

    bool Biting => biteStart >= 0f;

    void Awake()
    {
        lake = GetComponent<LakeWater>();
        if (lake == null)
            lake = gameObject.AddComponent<LakeWater>();
    }

    void Start()
    {
        PlayerMove move = FindAnyObjectByType<PlayerMove>();
        if (move != null)
        {
            player = move.transform;
            cam = move.GetComponentInChildren<Camera>();
        }
    }

    void OnDisable()
    {
        StopFishing();
    }

    void Update()
    {
        if (player == null || cam == null || Keyboard.current == null)
            return;

        if ((PlayerHealth.Instance != null && PlayerHealth.Instance.isDead) || BirdChallenge.Active)
        {
            StopFishing();
            return;
        }

        bool gPressed = Keyboard.current.gKey.wasPressedThisFrame;

        if (!fishing)
        {
            if (gPressed && CanCast(out Vector3 point))
                StartFishing(point);
            return;
        }

        if (Vector3.Distance(player.position, castPoint) > castRange + 3f)
        {
            StopFishing();
            Notice("You walked away. The fish is gone.");
            return;
        }

        if (!Biting && Time.time >= waitUntil && (fish == null || fish.AtLure))
            biteStart = Time.time;

        float bob = Biting ? -0.12f : Mathf.Sin(Time.time * 3f) * 0.03f;
        bobber.transform.position = castPoint + Vector3.up * bob;

        if (gPressed)
        {
            if (Biting)
            {
                lake.Catch(fish);
                fish = null;
                if (Inventory.Instance != null)
                    Inventory.Instance.AddMeat(1);
                Notice("You caught a fish! +1 raw meat");
            }
            else
            {
                Notice("Too early! Wait for the float to sink.");
            }
            StopFishing();
            return;
        }

        if (Biting && Time.time > biteStart + biteWindow)
        {
            StopFishing();
            Notice("The fish got away. Press G to try again.");
        }
    }

    bool CanCast(out Vector3 point)
    {
        point = Vector3.zero;
        if (WaterBodies.TryGetSwim(player.position, out _, out _))
            return false;

        Plane surface = new Plane(Vector3.up, new Vector3(0f, lake.SurfaceY, 0f));
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (!surface.Raycast(ray, out float distance) || distance > castRange)
            return false;

        point = ray.GetPoint(distance);
        return lake.Inside(point, 0.5f) && lake.GroundY(point) < lake.SurfaceY - 0.3f;
    }

    void StartFishing(Vector3 point)
    {
        fishing = true;
        castPoint = point;
        biteStart = -1f;
        waitUntil = Time.time + Random.Range(minWait, maxWait);

        fish = lake.ClosestFreeFish(point);
        if (fish != null)
            fish.GoTo(point + Vector3.down * 0.35f);

        if (bobberMat == null)
        {
            bobberMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            bobberMat.SetColor("_BaseColor", new Color(0.9f, 0.15f, 0.1f));
        }

        bobber = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bobber.name = "FishingFloat";
        Destroy(bobber.GetComponent<Collider>());
        bobber.transform.localScale = Vector3.one * 0.25f;
        bobber.transform.position = point;
        bobber.GetComponent<Renderer>().sharedMaterial = bobberMat;
    }

    void StopFishing()
    {
        fishing = false;
        biteStart = -1f;
        if (fish != null)
            fish.Release();
        fish = null;
        if (bobber != null)
            Destroy(bobber);
    }

    void Notice(string text)
    {
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(text);
    }

    void OnGUI()
    {
        if (player == null || cam == null || BirdChallenge.Active)
            return;

        float y = Screen.height * 0.5f + 60f * ScreenText.Scale;
        if (fishing)
            ScreenText.Centered(Biting ? "Fish on! Press G now!" : "Waiting for a bite...", y);
        else if (CanCast(out _))
            ScreenText.Centered("Press G to fish", y);
        else if (WaterBodies.TryGetSwim(player.position, out _, out _))
            ScreenText.Centered("Swimming:  Space = up   Ctrl = dive", Screen.height - 70f * ScreenText.Scale, 18);
    }
}
