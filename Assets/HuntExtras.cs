using System.Collections;
using System.Collections.Generic;
using HuntingGame.Animals;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The extra layer of the hunt:
///   Tab  Hunter's Instinct: a sense pulse that marks nearby animals (species, weight, distance)
///        for a few seconds, then recharges.
///        Kill cam: long shots, trophies and rare kills play out in slow motion with letterbox bars.
///        Buck fever: aiming at big game raises a heartbeat you can hear, and it shakes the scope.
///        Rare sightings: an albino or melanistic animal nearby gets announced once.
///   P    Photo mode: saves a screenshot without the HUD to Pictures/HuntingGame.
/// </summary>
public class HuntExtras : MonoBehaviour
{
    public static HuntExtras Instance { get; private set; }
    public static float BuckFever => Instance != null && !HuntProgress.NoBuckFever ? Instance.fever : 0f;
    public static bool InstinctActive => Instance != null && Time.unscaledTime < Instance.instinctUntil;
    public static float InstinctCooldownLeft => Instance != null ? Mathf.Max(0f, Instance.instinctReadyAt - Time.unscaledTime) : 0f;

    const float InstinctSeconds = 8f;
    const int MaxMarkers = 14;

    Canvas canvas;
    RectTransform canvasRect;
    CanvasGroup worldGroup;
    Camera cam;

    // Instinct
    float instinctUntil = -1f;
    float instinctReadyAt;
    readonly List<Animal> sensed = new List<Animal>();
    readonly List<Marker> markers = new List<Marker>();
    Image pulseRing;
    Image instinctTint;
    float pulseAge = 100f;

    // Kill cam
    RectTransform barTop, barBottom;
    float slowUntil = -1f;
    bool slowActive;
    float baseFixedDelta = 0.02f;
    static readonly List<(Animal animal, float distance)> pendingKills = new List<(Animal, float)>();

    // Buck fever
    float fever;
    AudioSource heart;
    AudioClip beat;
    float beatTimer;

    // Rare sightings
    readonly HashSet<AnimalTrophy> announced = new HashSet<AnimalTrophy>();
    float nextRareScan;

    // Photo
    Image flash;
    bool takingPhoto;

    static Sprite ringSprite;

    sealed class Marker
    {
        public RectTransform rect;
        public Image diamond;
        public TMP_Text label;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlayMode()
    {
        pendingKills.Clear();
    }

    /// <summary>Called by HuntFeedback on every kill.</summary>
    public static void NotifyKill(Animal animal, float distance)
    {
        pendingKills.Add((animal, distance));
    }

    void Awake()
    {
        Instance = this;
        baseFixedDelta = Time.fixedDeltaTime > 0.0001f ? Time.fixedDeltaTime : 0.02f;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        EndSlowMotion();
        if (canvas != null)
            Destroy(canvas.gameObject);
    }

    void Start()
    {
        BuildUi();
        BuildHeart();
    }

    void Update()
    {
        if (canvas == null)
            return;

        float dt = Time.unscaledDeltaTime;
        bool dead = PlayerHealth.Instance != null && PlayerHealth.Instance.isDead;
        bool blocked = dead || Inventory.IsOpen;
        if (cam == null || !cam.isActiveAndEnabled)
            cam = Camera.main;

        HuntProgress.TickContracts();
        worldGroup.alpha = Mathf.MoveTowards(worldGroup.alpha, blocked ? 0f : 1f, dt * 8f);

        Keyboard kb = Keyboard.current;
        if (!blocked && kb != null)
        {
            if (kb.tabKey.wasPressedThisFrame)
                TryInstinct();
            if (kb.pKey.wasPressedThisFrame && !takingPhoto)
                StartCoroutine(TakePhoto());
        }

        UpdateInstinct(dt);
        UpdateKillCam(dt, dead);
        UpdateFever(dt, blocked);
        UpdateRareSightings();
        if (flash.color.a > 0f)
            flash.color = new Color(1f, 1f, 1f, Mathf.MoveTowards(flash.color.a, 0f, dt * 2.5f));
    }

    // ---------------------------------------------------------------- instinct

    void TryInstinct()
    {
        float now = Time.unscaledTime;
        if (now < instinctReadyAt)
        {
            if (Inventory.Instance != null)
                Inventory.Instance.ShowNotice("Your senses need " + Mathf.CeilToInt(instinctReadyAt - now) + "s to recover.");
            return;
        }

        instinctUntil = now + InstinctSeconds;
        instinctReadyAt = now + HuntProgress.InstinctCooldown;
        pulseAge = 0f;

        sensed.Clear();
        Vector3 me = PlayerView.Position();
        float range = HuntProgress.InstinctRange;
        Animal[] animals = Object.FindObjectsByType<Animal>();
        for (int i = 0; i < animals.Length; i++)
        {
            Animal a = animals[i];
            if (a == null || a.Dead || !a.IsPresent())
                continue;
            if ((a.transform.position - me).sqrMagnitude <= range * range)
                sensed.Add(a);
        }

        sensed.Sort((x, y) => (x.transform.position - me).sqrMagnitude.CompareTo((y.transform.position - me).sqrMagnitude));
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(sensed.Count == 0
                ? "Hunter's Instinct: nothing stirs within " + Mathf.RoundToInt(range) + " m."
                : "Hunter's Instinct: " + sensed.Count + (sensed.Count == 1 ? " animal" : " animals") + " nearby.");
    }

    void UpdateInstinct(float dt)
    {
        pulseAge += dt;
        float ring = Mathf.Clamp01(pulseAge / 0.9f);
        pulseRing.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(60f, 1900f, ring);
        pulseRing.color = new Color(0.98f, 0.82f, 0.48f, (1f - ring) * 0.55f);

        float left = instinctUntil - Time.unscaledTime;
        bool active = left > 0f;
        float fade = active ? Mathf.Clamp01(left) : 0f;
        instinctTint.color = new Color(0.98f, 0.78f, 0.42f, 0.07f * fade);

        Vector3 me = PlayerView.Position();
        int shown = 0;
        if (active && cam != null)
        {
            for (int i = 0; i < sensed.Count && shown < markers.Count; i++)
            {
                Animal a = sensed[i];
                if (a == null || a.Dead)
                    continue;

                float lift = a.kind == AnimalKind.Bird
                    ? 0.4f
                    : a.kind == AnimalKind.Game
                        ? QuarryProfile.Of(a.quarry).markerHeight * a.transform.localScale.y
                        : 2.1f * a.transform.localScale.y;
                Vector3 head = a.transform.position + Vector3.up * lift;
                Vector3 screen = cam.WorldToScreenPoint(head);
                if (screen.z <= 0.5f)
                    continue;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local))
                    continue;

                Marker m = markers[shown++];
                m.rect.gameObject.SetActive(true);
                m.rect.anchoredPosition = local;
                float dist = Vector3.Distance(me, a.transform.position);

                AnimalTrophy trophy = a.GetComponent<AnimalTrophy>();
                AnimalBrain brain = a.GetComponent<AnimalBrain>();
                bool alert = a.Fleeing || (brain != null && (brain.State == AnimalAiState.Alert || brain.State == AnimalAiState.Flee));
                Color c = MarkerColor(trophy);
                c.a = fade;
                m.diamond.color = c;
                string weight = trophy != null ? Mathf.RoundToInt(trophy.weightKg) + " kg  ·  " : "";
                m.label.text = "<b>" + HuntFeedback.KindTitle(a) + "</b>\n<size=80%>" + weight + Mathf.RoundToInt(dist) + " m"
                               + (alert ? "  ·  <color=#FF6A5C>ALERT</color>" : "") + "</size>";
                Color t = Color.white;
                t.a = fade;
                m.label.color = t;
            }
        }

        for (int i = shown; i < markers.Count; i++)
        {
            if (markers[i].rect.gameObject.activeSelf)
                markers[i].rect.gameObject.SetActive(false);
        }
    }

    static Color MarkerColor(AnimalTrophy trophy)
    {
        if (trophy == null)
            return new Color(0.85f, 0.85f, 0.82f);
        if (trophy.IsRare)
            return new Color(0.80f, 0.62f, 1f);
        switch (trophy.tier)
        {
            case AnimalTrophy.Tier.Trophy: return new Color(0.96f, 0.77f, 0.40f);
            case AnimalTrophy.Tier.Prime: return new Color(0.70f, 0.90f, 0.62f);
            default: return new Color(0.85f, 0.85f, 0.82f);
        }
    }

    // ---------------------------------------------------------------- kill cam

    void UpdateKillCam(float dt, bool dead)
    {
        for (int i = 0; i < pendingKills.Count; i++)
        {
            Animal a = pendingKills[i].animal;
            AnimalTrophy trophy = a != null ? a.GetComponent<AnimalTrophy>() : null;
            bool cinematic = pendingKills[i].distance >= 90f
                             || (trophy != null && trophy.tier >= AnimalTrophy.Tier.Trophy)
                             || (a != null && a.kind == AnimalKind.Legendary);
            if (cinematic && !dead && !GamePause.Active)
                BeginSlowMotion(1.1f);
        }

        pendingKills.Clear();

        if (slowActive && (Time.unscaledTime >= slowUntil || GamePause.Active || dead))
            EndSlowMotion();

        float bars = slowActive ? 1f : 0f;
        float h = Mathf.MoveTowards(barTop.sizeDelta.y, bars * 74f, dt * 400f);
        barTop.sizeDelta = new Vector2(0f, h);
        barBottom.sizeDelta = new Vector2(0f, h);
    }

    void BeginSlowMotion(float realSeconds)
    {
        slowUntil = Time.unscaledTime + realSeconds;
        if (slowActive)
            return;
        slowActive = true;
        Time.timeScale = 0.3f;
        Time.fixedDeltaTime = baseFixedDelta * 0.3f;
    }

    void EndSlowMotion()
    {
        if (!slowActive)
            return;
        slowActive = false;
        Time.fixedDeltaTime = baseFixedDelta;

        // Pause and death own the clock; only hand normal time back if neither has it.
        bool dead = PlayerHealth.Instance != null && PlayerHealth.Instance.isDead;
        if (!GamePause.Active && !dead)
            Time.timeScale = 1f;
    }

    // ---------------------------------------------------------------- buck fever

    readonly RaycastHit[] feverHits = new RaycastHit[12];

    void UpdateFever(float dt, bool blocked)
    {
        float target = 0f;
        if (!blocked && PlayerShotgun.IsAiming)
        {
            Animal a = AnimalInSights(out float distance);
            if (a != null)
            {
                target = 0.3f;
                AnimalTrophy trophy = a.GetComponent<AnimalTrophy>();
                if (a.kind == AnimalKind.Legendary)
                    target += 0.6f;
                else if (trophy != null)
                    target += trophy.IsRare ? 0.55f : trophy.tier == AnimalTrophy.Tier.Trophy ? 0.35f : trophy.tier == AnimalTrophy.Tier.Prime ? 0.15f : 0f;
                if (distance < 40f)
                    target += 0.12f;
                if (a.kind == AnimalKind.Bird)
                    target *= 0.4f;
            }
        }

        // Holding your breath calms the heart a little.
        float rise = ScopeSteady.Holding ? 0.15f : 0.45f;
        fever = Mathf.MoveTowards(fever, Mathf.Clamp01(target), dt * (target > fever ? rise : 0.3f));

        float audible = HuntProgress.NoBuckFever ? fever * 0.35f : fever;
        if (heart == null || audible < 0.05f || GamePause.Active)
        {
            beatTimer = 0f;
            return;
        }

        beatTimer -= dt;
        if (beatTimer <= 0f)
        {
            float bpm = Mathf.Lerp(72f, 150f, fever);
            beatTimer = 60f / bpm;
            heart.pitch = Mathf.Lerp(0.95f, 1.12f, fever);
            heart.PlayOneShot(beat, Mathf.Lerp(0.15f, 0.85f, audible));
        }
    }

    Animal AnimalInSights(out float distance)
    {
        distance = 0f;
        Ray ray = PlayerView.AimRay();
        int count = Physics.RaycastNonAlloc(ray, feverHits, 450f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        Animal found = null;
        for (int i = 0; i < count; i++)
        {
            Collider c = feverHits[i].collider;
            if (c == null || feverHits[i].distance >= best || c.GetComponentInParent<PlayerMove>() != null)
                continue;
            best = feverHits[i].distance;
            found = c.GetComponentInParent<Animal>();
        }

        if (found == null || found.Dead)
            return null;
        distance = best;
        return found;
    }

    void BuildHeart()
    {
        // A synthesized "lub-dub": two low thumps with a fast decay.
        const int rate = 22050;
        int samples = Mathf.RoundToInt(rate * 0.55f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)rate;
            data[i] = Thump(t, 0f, 1f) + Thump(t, 0.22f, 0.7f);
        }

        beat = AudioClip.Create("Heartbeat", samples, 1, rate, false);
        beat.SetData(data, 0);
        heart = gameObject.AddComponent<AudioSource>();
        heart.playOnAwake = false;
        heart.spatialBlend = 0f;
        heart.priority = 20;
    }

    static float Thump(float t, float start, float gain)
    {
        float x = t - start;
        if (x < 0f || x > 0.2f)
            return 0f;
        float env = Mathf.Exp(-x * 26f) * Mathf.Clamp01(x * 400f);
        float freq = Mathf.Lerp(62f, 38f, x / 0.2f);
        return Mathf.Sin(2f * Mathf.PI * freq * x) * env * gain * 0.9f;
    }

    // ---------------------------------------------------------------- rare sightings

    void UpdateRareSightings()
    {
        if (Time.unscaledTime < nextRareScan)
            return;
        nextRareScan = Time.unscaledTime + 1.5f;

        Vector3 me = PlayerView.Position();
        AnimalTrophy[] all = Object.FindObjectsByType<AnimalTrophy>();
        for (int i = 0; i < all.Length; i++)
        {
            AnimalTrophy t = all[i];
            if (t == null || !t.IsRare || announced.Contains(t))
                continue;
            Animal a = t.GetComponent<Animal>();
            if (a == null || a.Dead || !a.IsPresent())
                continue;

            Vector3 delta = t.transform.position - me;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist > 140f)
                continue;

            announced.Add(t);
            string coat = t.coat == AnimalTrophy.Coat.Albino ? "AN ALBINO" : "A MELANISTIC";
            float bearing = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            HuntFeedback.PushBanner("RARE SIGHTING",
                coat + " " + HuntFeedback.SpeciesTitle(a) + " IS NEAR   ·   " + Mathf.RoundToInt(dist) + " M " + BearingName(bearing), true);
        }
    }

    static string BearingName(float yaw)
    {
        string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        int i = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 45f) % 8;
        return names[i];
    }

    // ---------------------------------------------------------------- photo mode

    IEnumerator TakePhoto()
    {
        takingPhoto = true;
        List<Canvas> hidden = new List<Canvas>();
        Canvas[] canvases = Object.FindObjectsByType<Canvas>();
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            if (c != null && c.enabled && c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                c.enabled = false;
                hidden.Add(c);
            }
        }

        yield return new WaitForEndOfFrame();
        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();

        for (int i = 0; i < hidden.Count; i++)
        {
            if (hidden[i] != null)
                hidden[i].enabled = true;
        }

        string message;
        try
        {
            string folder = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures), "HuntingGame");
            System.IO.Directory.CreateDirectory(folder);
            string file = System.IO.Path.Combine(folder, "Hunt_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
            System.IO.File.WriteAllBytes(file, shot.EncodeToPNG());
            message = "Photo saved to Pictures/HuntingGame.";
        }
        catch (System.Exception e)
        {
            message = "Could not save the photo: " + e.Message;
        }

        Destroy(shot);
        flash.color = new Color(1f, 1f, 1f, 0.85f);
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(message);
        takingPhoto = false;
    }

    // ---------------------------------------------------------------- ui

    void BuildUi()
    {
        canvas = UIKit.MakeCanvas("HuntExtrasUI", 45, false);
        canvasRect = canvas.GetComponent<RectTransform>();
        CanvasGroup rootGroup = canvas.GetComponent<CanvasGroup>();
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        RectTransform world = UIKit.Group(canvas.transform, "World");
        UIKit.Stretch(world);
        worldGroup = world.gameObject.AddComponent<CanvasGroup>();

        instinctTint = UIKit.Img(world, "InstinctTint", null, new Color(1f, 0.8f, 0.4f, 0f));
        UIKit.Stretch(instinctTint.rectTransform);

        if (ringSprite == null)
            ringSprite = MakeRing(128);
        pulseRing = UIKit.Img(world, "Pulse", ringSprite, new Color(1f, 0.8f, 0.5f, 0f));
        UIKit.Place(pulseRing.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f), new Vector2(0.5f, 0.5f));

        for (int i = 0; i < MaxMarkers; i++)
        {
            RectTransform m = UIKit.Group(world, "Marker" + i);
            UIKit.Place(m, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10f, 10f), new Vector2(0.5f, 0.5f));
            Image d = UIKit.Img(m, "Diamond", UIKit.Round, Color.white, 2f);
            UIKit.Place(d.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(11f, 11f), new Vector2(0.5f, 0.5f));
            d.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            TMP_Text label = UIKit.Text(m, "", 12f, Color.white, TextAlignmentOptions.Bottom, false, 2f);
            UIKit.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(260f, 36f), new Vector2(0.5f, 0f));
            m.gameObject.SetActive(false);
            markers.Add(new Marker { rect = m, diamond = d, label = label });
        }

        // Letterbox bars for the kill cam (outside the world group, so they show regardless).
        barTop = UIKit.Img(canvas.transform, "BarTop", null, Color.black).rectTransform;
        barTop.anchorMin = new Vector2(0f, 1f);
        barTop.anchorMax = new Vector2(1f, 1f);
        barTop.pivot = new Vector2(0.5f, 1f);
        barTop.anchoredPosition = Vector2.zero;
        barTop.sizeDelta = Vector2.zero;
        barBottom = UIKit.Img(canvas.transform, "BarBottom", null, Color.black).rectTransform;
        barBottom.anchorMin = new Vector2(0f, 0f);
        barBottom.anchorMax = new Vector2(1f, 0f);
        barBottom.pivot = new Vector2(0.5f, 0f);
        barBottom.anchoredPosition = Vector2.zero;
        barBottom.sizeDelta = Vector2.zero;

        flash = UIKit.Img(canvas.transform, "PhotoFlash", null, new Color(1f, 1f, 1f, 0f));
        UIKit.Stretch(flash.rectTransform);
    }

    static Sprite MakeRing(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.DontSave;
        Color32[] px = new Color32[size * size];
        float c = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(c, c));
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - (c - 4f)) / 3f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
}
