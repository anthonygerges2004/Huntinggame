using System.Collections.Generic;
using HuntingGame.Tracking;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>A persistent relationship and a local, evidence-driven wilderness hunter.</summary>
public class RivalHunter : MonoBehaviour
{
    public static RivalHunter Instance { get; private set; }
    public enum Disposition { Competitor, Neutral, Ally }
    public enum Activity { Searching, FollowingTracks, Stalking, Aiming, Helping, Wounded }
    public Activity activity { get; private set; }
    public int Trust { get; private set; }
    public Disposition Relationship => Trust >= 30 ? Disposition.Ally : Trust <= -25 ? Disposition.Competitor : Disposition.Neutral;
    public static int AdjustedTrust(int current, int change) => Mathf.Clamp(current + change, -100, 100);
    public float walkSpeed = 2.3f;
    public float sightRange = 38f;
    public float trapInterval = 50f;
    const string SaveKey = "Hunt.RivalHunter.Trust.v1";
    readonly List<Material> materials = new List<Material>();
    readonly List<Snare> traps = new List<Snare>();
    readonly List<AnimalCarcass> claims = new List<AnimalCarcass>();
    class Snare { public GameObject root; public float expires; }
    Villager model;
    CharacterController motor;
    Animal target;
    Animal[] wildlife = System.Array.Empty<Animal>();
    Vector3 home, destination;
    float nextThink, nextTrap, trailBorn = -1f, aimTime, phase, health = 100f;
    float rescueTime, woundedAt, immuneUntil, nextGift, nextHint, idleUntil, stuckTime;
    bool haveTrail;

    public static void EnsureSpawn(Vector3 camp)
    {
        if (Instance != null || FindAnyObjectByType<RivalHunter>() != null) return;
        Vector3 spot = camp;
        bool found = false;
        for (int i = 0; i < 16; i++)
        {
            float angle = i * 137.5f * Mathf.Deg2Rad;
            Vector3 p = WorldGround.Snap(camp + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 22f);
            if (WaterBodies.InsideWater(p) || Physics.CheckCapsule(p + Vector3.up * 0.4f, p + Vector3.up * 1.5f, 0.3f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            spot = p; found = true; break;
        }
        if (!found) return;
        GameObject root = new GameObject("Rowan - Wilderness Hunter");
        root.transform.position = spot;
        root.AddComponent<RivalHunter>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Trust = Mathf.Clamp(PlayerPrefs.GetInt(SaveKey, 0), -100, 100);
        home = destination = transform.position;
        nextTrap = Time.time + trapInterval;
        BuildModel();
        motor = gameObject.AddComponent<CharacterController>();
        motor.height = 1.8f; motor.radius = 0.28f; motor.center = Vector3.up * 0.9f;
        motor.stepOffset = 0.25f; motor.slopeLimit = 45;
        Say("Rowan is hunting near camp. Approach and press K to speak.");
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (Snare snare in traps) if (snare.root != null) Destroy(snare.root);
        foreach (Material mat in materials) if (mat != null) Destroy(mat);
    }

    void ChangeTrust(int amount, string reason)
    {
        Trust = AdjustedTrust(Trust, amount);
        if (Relationship == Disposition.Ally) claims.Clear();
        PlayerPrefs.SetInt(SaveKey, Trust);
        PlayerPrefs.Save();
        Say(reason + "  Rowan: " + Relationship + " (" + Trust + ")");
    }

    void Update()
    {
        if (Time.timeScale == 0f || PlayerView.Body() == null) return;
        if (Time.time >= nextThink)
        {
            nextThink = Time.time + 1.25f;
            wildlife = FindObjectsByType<Animal>();
            TickTraps();
            TickDanger();
            if (activity != Activity.Wounded) Think();
        }
        bool canInteract = !Inventory.IsOpen && (PlayerHealth.Instance == null || !PlayerHealth.Instance.isDead);
        if (activity == Activity.Wounded)
        {
            bool safe = !DangerNear(7f);
            rescueTime = canInteract && Nearby(transform.position, 3.2f) && HeldK() && safe ? rescueTime + Time.deltaTime : 0;
            if (rescueTime >= 3f)
            {
                health = 100; immuneUntil = Time.time + 75; activity = Activity.Searching;
                if (model.body != null) model.body.localRotation = Quaternion.identity;
                ChangeTrust(35, "You helped Rowan back to their feet. Thank you, hunter.");
            }
            else if (Time.time - woundedAt > 150f)
            {
                health = 70; immuneUntil = Time.time + 75; activity = Activity.Searching;
                if (model.body != null) model.body.localRotation = Quaternion.identity;
                ChangeTrust(-10, "Rowan recovered alone and remembers being left behind.");
            }
            return;
        }
        if (canInteract && PressedK()) Interact();
        if (Time.time < idleUntil) { Animate(false); return; }
        if (target != null && !target.Dead && CanSee(target) && Vector3.Distance(transform.position, target.transform.position) < 14f)
        {
            activity = Activity.Aiming;
            Face(target.transform.position);
            aimTime += Time.deltaTime;
            Animate(false);
            if (aimTime >= (Relationship == Disposition.Competitor ? 2.3f : 3f)) TakeShot();
        }
        else
        {
            aimTime = 0;
            Animate(Move(destination));
        }
    }

    void Think()
    {
        if (target != null && (target.Dead || !CanSee(target) || target.Limping)) target = null;
        if (Relationship == Disposition.Ally)
        {
            target = BestVisible(true);
            destination = PlayerView.Position() - PlayerView.Body().forward * 5f;
            activity = Activity.Helping;
            if (target != null) destination = target.transform.position;
            return;
        }
        if (target == null)
        {
            target = BestVisible(false);
            aimTime = 0;
            if (target != null && Nearby(transform.position, 65f))
                Say("Rowan spotted " + target.name + " and is stalking it.");
        }
        if (target != null) { destination = target.transform.position; activity = Activity.Stalking; return; }
        TrackPool pool = TrackPool.Current;
        if (pool != null && pool.TryFreshTrail(transform.position, 35f, trailBorn, out Vector3 track, out float born))
        {
            destination = track; trailBorn = born; haveTrail = true; activity = Activity.FollowingTracks;
            if (Time.time >= nextTrap && traps.Count < 2) SetTrap(track);
        }
        else if (Vector3.Distance(transform.position, destination) < 2f || activity == Activity.Stalking)
        {
            Vector2 offset = Random.insideUnitCircle * 65f;
            destination = WorldGround.Snap(home + new Vector3(offset.x, 0, offset.y));
            activity = Activity.Searching;
            if (WaterBodies.InsideWater(destination)) destination = home;
        }
    }

    Animal BestVisible(bool predatorsOnly)
    {
        Animal best = null;
        float score = float.MinValue;
        foreach (Animal animal in wildlife)
        {
            if (!Eligible(animal) || (predatorsOnly && !Predator(animal)) || !CanSee(animal)) continue;
            AnimalTrophy trophy = animal.GetComponent<AnimalTrophy>();
            float value = (trophy == null ? 0 : (int)trophy.tier * 15f) - Vector3.Distance(transform.position, animal.transform.position);
            if (value <= score) continue;
            score = value; best = animal;
        }
        return best;
    }
    static bool Eligible(Animal a) => a != null && a.isActiveAndEnabled && !a.Dead && !a.Limping && a.IsPresent()
        && a.kind != AnimalKind.Bird && a.kind != AnimalKind.Legendary && a.GetComponent<RideableHorse>() == null;
    static bool Predator(Animal a) => a != null && a.kind == AnimalKind.Game && (a.quarry == QuarryKind.Wolf || a.quarry == QuarryKind.Boar);

    bool CanSee(Animal animal)
    {
        Vector3 eye = transform.position + Vector3.up * 1.5f;
        Vector3 delta = animal.transform.position + Vector3.up * 0.7f - eye;
        if (delta.magnitude > sightRange) return false;
        float nearest = delta.magnitude;
        Collider blocker = null;
        foreach (RaycastHit hit in Physics.RaycastAll(eye, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.transform == transform || hit.distance > nearest) continue;
            nearest = hit.distance; blocker = hit.collider;
        }
        return blocker == null || blocker.GetComponentInParent<Animal>() == animal;
    }

    void TakeShot()
    {
        Animal prey = target;
        aimTime = 0;
        if (!Eligible(prey) || !CanSee(prey)) return;
        GameAudio.Gunshot(HuntWeapon.Rifle);
        prey.RivalKill((prey.transform.position - transform.position).normalized);
        Claim(prey);
        target = null;
        idleUntil = Time.time + 12f;
        destination = home;
        activity = Activity.Searching;
    }

    void Claim(Animal prey)
    {
        AnimalCarcass carcass = prey.GetComponent<AnimalCarcass>();
        if (carcass == null) return;
        if (Relationship == Disposition.Ally)
        {
            Say("Rowan: This catch is yours. Take the carcass.");
            return;
        }
        claims.Add(carcass);
        Say("Rowan claimed a catch. Taking it will damage your relationship.");
    }
    public static void NotifyLoot(AnimalCarcass carcass)
    {
        if (Instance == null || !Instance.claims.Remove(carcass)) return;
        Instance.ChangeTrust(-20, "You took Rowan's claimed catch.");
    }
    public static bool IsClaimed(AnimalCarcass carcass) => Instance != null && Instance.claims.Contains(carcass);

    void SetTrap(Vector3 point)
    {
        if (!haveTrail || WaterBodies.InsideWater(point)) return;
        GameObject root = new GameObject("Rowan's snare - K to disarm");
        root.transform.position = WorldGround.Snap(point) + Vector3.up * 0.07f;
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            Part(root.transform, new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * 0.3f,
                new Vector3(0.07f, 0.05f, 0.24f), trapMaterial, Quaternion.Euler(0, i * 45f, 0));
        }
        traps.Add(new Snare { root = root, expires = Time.time + 180f });
        nextTrap = Time.time + Mathf.Max(20f, trapInterval * (Relationship == Disposition.Competitor ? 0.65f : 1f));
    }
    void TickTraps()
    {
        for (int i = traps.Count - 1; i >= 0; i--)
        {
            Snare trap = traps[i];
            if (trap.root == null || Time.time >= trap.expires) { if (trap.root != null) Destroy(trap.root); traps.RemoveAt(i); continue; }
            foreach (Animal animal in wildlife)
            {
                if (!Eligible(animal) || animal.kind != AnimalKind.Game || Predator(animal)) continue;
                if (Vector3.Distance(animal.transform.position, trap.root.transform.position) > 1.1f) continue;
                animal.RivalKill(Vector3.down); Claim(animal);
                Destroy(trap.root); traps.RemoveAt(i); break;
            }
        }
        claims.RemoveAll(c => c == null);
    }

    bool DangerNear(float range)
    {
        foreach (Animal a in wildlife)
            if (a != null && !a.Dead && a.isActiveAndEnabled && Predator(a) && Vector3.Distance(a.transform.position, transform.position) < range) return true;
        return false;
    }
    void TickDanger()
    {
        if (Time.time < immuneUntil || activity == Activity.Wounded || !DangerNear(3.5f)) return;
        health -= 25f;
        if (health > 0) return;
        activity = Activity.Wounded; target = null; woundedAt = Time.time; rescueTime = 0;
        if (model.body != null) model.body.localRotation = Quaternion.Euler(0, 0, 65);
        Say("Rowan is wounded! Clear nearby predators, then hold K beside them to help.");
    }

    void Interact()
    {
        if (Nearby(transform.position, 3.2f))
        {
            idleUntil = Time.time + 5f;
            Face(PlayerView.Position());
            if (Relationship == Disposition.Ally)
            {
                if (Time.time < nextHint) return;
                nextHint = Time.time + 15f;
                Animal prey = BestVisible(false);
                if (prey == null) Say("Rowan: No fresh sighting. Let's search for tracks.");
                else Say("Rowan: " + prey.name + " is " + Mathf.RoundToInt(Vector3.Distance(PlayerView.Position(), prey.transform.position)) + " m away, bearing " + Bearing(prey.transform.position) + " degrees.");
            }
            else if (Time.time >= nextGift && Inventory.Instance != null && Inventory.Instance.SpendCooked(1))
            {
                nextGift = Time.time + 90f;
                ChangeTrust(10, "You shared a cooked meal with Rowan.");
            }
            else Say("Rowan: Share a cooked meal with K to build trust. Respect my snares and claimed catches.");
            return;
        }
        for (int i = 0; i < traps.Count; i++)
        {
            if (traps[i].root == null || !Nearby(traps[i].root.transform.position, 2.5f)) continue;
            Destroy(traps[i].root); traps.RemoveAt(i);
            ChangeTrust(-15, "You dismantled Rowan's snare.");
            break;
        }
    }

    bool Nearby(Vector3 point, float range)
    {
        if (Vector3.Distance(PlayerView.Position(), point) > range) return false;
        Vector3 origin = PlayerView.Position() + Vector3.up * 1.2f;
        Vector3 delta = point + Vector3.up * 1.2f - origin;
        Transform player = PlayerView.Body();
        foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (player != null && (hit.transform == player || hit.transform.IsChildOf(player))) continue;
            return false;
        }
        return true;
    }
    bool Move(Vector3 goal)
    {
        Vector3 heading = Vector3.ProjectOnPlane(goal - transform.position, Vector3.up);
        if (heading.magnitude < 1.5f) { motor.Move(Vector3.down * 3f * Time.deltaTime); return false; }
        foreach (float angle in new[] { 0f, 40f, -40f, 80f, -80f })
        {
            Vector3 direction = Quaternion.Euler(0, angle, 0) * heading.normalized;
            Vector3 next = transform.position + direction * 0.8f;
            if (WaterBodies.InsideWater(next)) continue;
            if (WorldGround.TryGetGround(next, out Vector3 ground, out Vector3 normal)
                && (normal.y < 0.7f || Mathf.Abs(ground.y - transform.position.y) > 0.65f)) continue;
            if (Physics.SphereCast(transform.position + Vector3.up * 0.9f, 0.25f, direction, out _, 0.8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
            Face(transform.position + direction);
            motor.Move((direction * walkSpeed + Vector3.down * 4f) * Time.deltaTime);
            stuckTime = 0;
            return true;
        }
        motor.Move(Vector3.down * 3f * Time.deltaTime);
        stuckTime += Time.deltaTime;
        if (stuckTime > 6f)
        {
            target = null;
            Vector2 offset = Random.insideUnitCircle * 20f;
            destination = WorldGround.Snap(transform.position + new Vector3(offset.x, 0, offset.y));
            stuckTime = 0;
        }
        return false;
    }
    void Face(Vector3 point)
    {
        Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
        if (direction.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 8f);
    }
    void Animate(bool walking)
    {
        phase += Time.deltaTime * 7f;
        float swing = walking ? Mathf.Sin(phase) * 25f : 0;
        if (model.legL != null) model.legL.localRotation = Quaternion.Euler(swing, 0, 0);
        if (model.legR != null) model.legR.localRotation = Quaternion.Euler(-swing, 0, 0);
        if (model.armR != null) model.armR.localRotation = Quaternion.Euler(activity == Activity.Aiming ? -65 : -15, 0, 0);
    }

    Material trapMaterial;
    Material Mat(Color color) { Material m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = color }; materials.Add(m); return m; }
    void BuildModel()
    {
        Material skin = Mat(new Color(0.66f, 0.46f, 0.32f)), cloth = Mat(new Color(0.23f, 0.32f, 0.22f));
        Material dark = Mat(new Color(0.13f, 0.12f, 0.10f)), wood = Mat(new Color(0.33f, 0.2f, 0.1f));
        trapMaterial = Mat(new Color(0.5f, 0.4f, 0.23f));
        var palette = new Villager.Palette { skin = new[] { skin }, cloth = new[] { cloth }, trousers = new[] { dark }, hair = new[] { dark }, dark = dark, wood = wood, metal = dark, straw = trapMaterial };
        model = Villager.Create(transform, transform.position, transform.rotation, Villager.Role.Guard, palette, new System.Random(742), "Rowan");
        model.enabled = false; // Rival AI owns movement and animation; no competing villager tick.
        Part(model.body, new Vector3(0, 1.2f, -0.22f), new Vector3(0.35f, 0.4f, 0.18f), wood, Quaternion.identity);
        Part(model.armR, new Vector3(0, -0.45f, 0.3f), new Vector3(0.07f, 0.09f, 0.85f), wood, Quaternion.identity);
    }
    static void Part(Transform parent, Vector3 position, Vector3 scale, Material material, Quaternion rotation)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.GetComponent<Collider>().enabled = false; Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false); part.transform.localPosition = position;
        part.transform.localScale = scale; part.transform.localRotation = rotation;
        part.GetComponent<Renderer>().sharedMaterial = material;
    }
    void OnGUI()
    {
        if (Inventory.IsOpen || Time.timeScale == 0 || PlayerView.Body() == null) return;
        float distance = Vector3.Distance(PlayerView.Position(), transform.position);
        if (activity == Activity.Wounded)
        {
            ScreenText.Centered("ROWAN NEEDS HELP - " + Mathf.RoundToInt(distance) + " m - bearing " + Bearing(transform.position) + " degrees", 125f * ScreenText.Scale);
            if (distance < 3.2f) ScreenText.Centered(DangerNear(7f) ? "Clear nearby predators first" : "Hold K to help: " + Mathf.RoundToInt(rescueTime / 3f * 100f) + "%", Screen.height * 0.5f + 120 * ScreenText.Scale);
        }
        else if (distance < 5f)
            ScreenText.Centered("ROWAN | " + Relationship + " | " + activity + " | K: " + (Relationship == Disposition.Ally ? "ask for tracks" : "talk / share 1 cooked meat"), Screen.height * 0.5f + 120 * ScreenText.Scale);
        else foreach (Snare snare in traps)
            if (snare.root != null && Vector3.Distance(PlayerView.Position(), snare.root.transform.position) < 2.5f)
            { ScreenText.Centered("Rowan's snare | K: dismantle (loses trust)", Screen.height * 0.5f + 120 * ScreenText.Scale); break; }
    }
    static int Bearing(Vector3 point) { Vector3 d = point - PlayerView.Position(); return Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f)); }
    static void Say(string message) { if (Inventory.Instance != null) Inventory.Instance.ShowNotice(message); }
    static bool PressedK()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.K);
#endif
    }
    static bool HeldK()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.kKey.isPressed;
#else
        return Input.GetKey(KeyCode.K);
#endif
    }
}
