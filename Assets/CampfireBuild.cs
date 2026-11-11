using UnityEngine;

/// <summary>
/// Lays a crib of logs in front of the player, then lights it with the lighter.
/// </summary>
public class CampfireBuild : MonoBehaviour
{
    public static bool Busy { get; private set; }

    const float PlaceDelay = 0.28f;
    const float PlaceEach = 0.45f;
    const int LogCount = 4;
    const float FlickTime = 0.75f;
    const float CatchTime = 0.35f;

    static readonly Vector3[] LogPos =
    {
        new Vector3(0f, 0.055f, -0.1f),
        new Vector3(0f, 0.055f, 0.1f),
        new Vector3(-0.1f, 0.17f, 0f),
        new Vector3(0.1f, 0.17f, 0f)
    };

    static readonly Vector3[] LogEuler =
    {
        new Vector3(0f, 0f, 90f),
        new Vector3(0f, 0f, 90f),
        new Vector3(90f, 0f, 0f),
        new Vector3(90f, 0f, 0f)
    };

    Campfire fire;
    int woodSpent;
    float age;
    bool finished;
    bool froze;
    Transform logsRoot;
    Transform[] logs;
    Transform lighter;
    Transform lid;
    Light wick;
    static Material woodMat;
    static Material steelMat;

    public static bool Begin(Campfire campfire, int woodSpent)
    {
        if (Busy || campfire == null || PlayerHealth.Instance == null)
            return false;

        Transform player = PlayerHealth.Instance.transform;
        Vector3 spot = player.position + player.forward * 1.25f;
        if (WaterBodies.InsideWater(spot))
        {
            Notice("Too wet there to lay a fire.");
            return false;
        }

        if (!WorldGround.TryGetGround(spot, out Vector3 ground, out Vector3 normal) || normal.y < 0.65f)
        {
            Notice("Need flatter ground to lay the wood.");
            return false;
        }

        Vector3 face = Vector3.ProjectOnPlane(player.position - ground, normal);
        if (face.sqrMagnitude < 0.01f)
            face = player.forward;
        Quaternion rot = Quaternion.LookRotation(face.normalized, normal);

        GameObject pile = new GameObject("CampfirePile");
        pile.transform.SetPositionAndRotation(ground + normal * 0.02f, rot);
        CampfireBuild build = pile.AddComponent<CampfireBuild>();
        build.fire = campfire;
        build.woodSpent = woodSpent;
        build.froze = !PlayerMove.Frozen;
        if (build.froze)
            PlayerMove.Frozen = true;
        Busy = true;
        PlayerBodyAnimation.PlayLightFire();
        Notice("Laying the wood.");
        return true;
    }

    void Awake()
    {
        EnsureMaterials();
        logsRoot = new GameObject("LaidLogs").transform;
        logsRoot.SetParent(transform, false);
        logs = new Transform[LogCount];
        for (int i = 0; i < LogCount; i++)
        {
            GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Collider solid = log.GetComponent<Collider>();
            if (solid != null)
                Destroy(solid);
            log.name = "LaidLog";
            log.transform.SetParent(logsRoot, false);
            log.transform.localScale = new Vector3(0.11f, 0.29f, 0.11f);
            log.GetComponent<Renderer>().sharedMaterial = woodMat;
            log.SetActive(false);
            logs[i] = log.transform;
        }

        BuildLighter();
    }

    void Update()
    {
        if (finished)
            return;

        if (GamePause.Active)
            return;

        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        LayLogs();
        HoldLighter();

        float done = PlaceDelay + LogCount * PlaceEach + FlickTime + CatchTime;
        if (age >= done)
            Finish();
    }

    void LayLogs()
    {
        Vector3 hand = HandPoint();
        for (int i = 0; i < LogCount; i++)
        {
            float start = PlaceDelay + i * PlaceEach;
            float t = (age - start) / PlaceEach;
            Transform log = logs[i];
            if (t <= 0f)
            {
                log.gameObject.SetActive(false);
                continue;
            }

            log.gameObject.SetActive(true);
            Quaternion rest = logsRoot.rotation * Quaternion.Euler(LogEuler[i]);
            Vector3 home = logsRoot.TransformPoint(LogPos[i]);
            if (t >= 1f)
            {
                log.localPosition = LogPos[i];
                log.localRotation = Quaternion.Euler(LogEuler[i]);
                continue;
            }

            float k = Mathf.SmoothStep(0f, 1f, t);
            float arc = Mathf.Sin(k * Mathf.PI) * 0.16f;
            log.position = Vector3.Lerp(hand, home, k) + Vector3.up * arc;
            log.rotation = Quaternion.Slerp(Quaternion.LookRotation(transform.forward, transform.up), rest, k);
        }
    }

    void HoldLighter()
    {
        float flickStart = PlaceDelay + LogCount * PlaceEach;
        float t = (age - flickStart) / FlickTime;
        if (lighter == null)
            return;

        bool show = t > 0f;
        lighter.gameObject.SetActive(show);
        if (!show)
            return;

        float k = Mathf.Clamp01(t);
        float open = Mathf.Clamp01(k / 0.35f);
        if (lid != null)
            lid.localRotation = Quaternion.Euler(-58f * open, 0f, 0f);

        Vector3 hand = HandPoint() + transform.up * 0.08f;
        Vector3 wickAt = transform.position + transform.up * 0.3f + transform.forward * 0.12f;
        float reach = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.15f) / 0.7f));
        lighter.position = Vector3.Lerp(hand, wickAt, reach);
        lighter.rotation = Quaternion.LookRotation(transform.forward, transform.up) * Quaternion.Euler(-35f - 25f * reach, 0f, 0f);

        if (wick != null)
        {
            bool flame = k > 0.42f;
            wick.enabled = flame;
            wick.intensity = flame ? 1.4f + Mathf.Sin(Time.time * 40f) * 0.25f : 0f;
        }
    }

    void Finish()
    {
        if (finished)
            return;

        finished = true;
        if (lighter != null)
            Destroy(lighter.gameObject);

        for (int i = 0; i < LogCount; i++)
        {
            if (logs[i] == null)
                continue;
            logs[i].gameObject.SetActive(true);
            logs[i].localPosition = LogPos[i];
            logs[i].localRotation = Quaternion.Euler(LogEuler[i]);
        }

        Busy = false;
        ReleaseFeet();
        if (fire != null)
            fire.AdoptPile(gameObject);
        Destroy(this);
    }

    void OnDestroy()
    {
        Busy = false;
        ReleaseFeet();
        if (finished)
            return;

        if (woodSpent > 0 && Inventory.Instance != null)
            Inventory.Instance.GiveWood(woodSpent);
    }

    void ReleaseFeet()
    {
        if (!froze)
            return;

        froze = false;
        PlayerMove.Frozen = false;
    }

    Vector3 HandPoint()
    {
        return transform.position + transform.forward * 0.72f + transform.up * 0.95f;
    }

    void BuildLighter()
    {
        EnsureMaterials();
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Collider solid = body.GetComponent<Collider>();
        if (solid != null)
            Destroy(solid);
        body.name = "Lighter";
        body.transform.SetParent(transform, true);
        body.transform.localScale = new Vector3(0.028f, 0.07f, 0.016f);
        body.GetComponent<Renderer>().sharedMaterial = steelMat;
        lighter = body.transform;

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Collider capSolid = cap.GetComponent<Collider>();
        if (capSolid != null)
            Destroy(capSolid);
        cap.name = "Lid";
        cap.transform.SetParent(lighter, false);
        cap.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        cap.transform.localScale = new Vector3(1f, 0.35f, 1f);
        cap.GetComponent<Renderer>().sharedMaterial = steelMat;
        lid = cap.transform;

        GameObject glow = new GameObject("Wick");
        glow.transform.SetParent(lighter, false);
        glow.transform.localPosition = new Vector3(0f, 0.85f, 0.2f);
        wick = glow.AddComponent<Light>();
        wick.type = LightType.Point;
        wick.color = new Color(1f, 0.62f, 0.2f);
        wick.range = 1.4f;
        wick.intensity = 0f;
        wick.enabled = false;
        lighter.gameObject.SetActive(false);
    }

    static void EnsureMaterials()
    {
        if (woodMat != null && steelMat != null)
            return;

        woodMat = MakeMat(new Color(0.34f, 0.2f, 0.1f), 0.2f);
        steelMat = MakeMat(new Color(0.55f, 0.56f, 0.58f), 0.7f);
    }

    static Material MakeMat(Color color, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material mat = new Material(shader);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        mat.SetFloat("_Smoothness", smoothness);
        return mat;
    }

    static void Notice(string text)
    {
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(text);
    }
}
