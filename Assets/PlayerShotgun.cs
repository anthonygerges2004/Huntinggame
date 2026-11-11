using HuntingGame.Ballistics;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(150)]
public class PlayerShotgun : MonoBehaviour
{
    public static PlayerShotgun Instance { get; private set; }
    public static bool IsEquipped => Instance != null && Instance.equipped;
    public static bool IsAiming => Instance != null && Instance.aimHeld;
    public static float Mount => Instance != null ? Instance.mount : 0f;
    public static float RecoilKick => Instance != null ? Instance.recoil : 0f;
    public static float PumpPull => Instance != null ? Instance.breakOpen : 0f;
    public static float ViewKick { get; private set; }
    public static float ViewRoll { get; private set; }
    public static float GunPresent { get; private set; } = 1f;
    public static float AxePresent { get; private set; }

    public static float AimFovScale => Instance != null && Instance.active != null && Instance.equipped
        ? Instance.active.zoom
        : 1f;
    public static bool ScopeIsUp => Instance != null && Instance.equipped && Instance.active != null
        && Instance.active.scope && Instance.mount > 0.72f;

    public static bool IsReloading => Instance != null && Instance.equipped && Instance.reloading;
    public static string ActiveLabel => Instance != null && Instance.equipped && Instance.active != null
        ? Instance.active.label
        : "";

    /// <summary>0 at the start of a reload, 1 when the gun is ready again.</summary>
    public static float Reload01
    {
        get
        {
            if (Instance == null || !Instance.reloading)
                return 1f;
            return Mathf.Clamp01(Instance.fireTime / Instance.ReloadSeconds());
        }
    }

    float ReloadSeconds()
    {
        return active == null || active.id == HuntWeapon.Shotgun ? 2.15f : 0.7f;
    }

    public int pellets = 24;
    public float spreadAngle = 10f;
    public float range = 120f;
    public float readyPitch = 30f;

    bool equipped;
    bool aimHeld;
    bool reloading;
    int shells = 2;
    Transform viewGun;
    Transform handGun;
    public Transform HandGun => equipped ? handGun : null;
    Transform leftGrip;
    Transform cam;
    Rig active;
    Rig[] built;
    Texture2D scopeMask;

    class Rig
    {
        public HuntWeapon id;
        public Transform view;
        public Transform hand;
        public Transform leftGrip;
        public Vector3 stock;
        public Vector3 muzzle;
        public Vector3 rightGrip;
        public float scale;
        public int ammo;
        public int magSize;
        public int pelletCount;
        public float spread;
        public float gunRange;
        public float zoom;
        public bool scope;
        public string label;
        public int reserve;
        public bool auto;
        public float cycle;
    }

    Vector3 rightGripLocal = new Vector3(0f, 0.01f, -0.02f);
    float fireTime = -1f;
    float mount;
    float recoil;
    float breakOpen;
    float follow;
    float kickScale = 1f;
    float rollSign = 1f;
    float gunClock = -1f;
    float gunFrom = 1f;
    float gunTo = 1f;
    float gunDur = 0.45f;
    bool gunHolster;
    bool axeWanted;
    float axeClock = -1f;
    float axeFrom;
    float axeTo;
    float axeDur = 0.4f;
    Light flashLight;
    float flashLeft;

    // Held close and large, like a real first-person view down the gun.
    static readonly Vector3 ViewReadyPos = new Vector3(0.27f, -0.27f, 0.66f);
    static readonly Quaternion ViewReadyRot = Quaternion.Euler(8f, 3f, 0f);
    static readonly Vector3 ViewAimPos = new Vector3(0.035f, -0.125f, 0.56f);
    const float ViewGunScale = 1.7f;
    static readonly Quaternion ViewAimRot = Quaternion.Euler(0f, 0.2f, 0f);

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Camera playerCam = CameraViewSwitcher.Instance != null
            ? CameraViewSwitcher.Instance.PlayerCamera
            : null;
        if (playerCam == null)
            playerCam = PlayerView.GetCamera();
        if (playerCam == null)
            playerCam = GetComponentInChildren<Camera>();
        if (playerCam != null)
            cam = playerCam.transform;

        ClearLeftoverGuns();
        BuildGuns();
        SeedCartridges();
        if (Inventory.Instance != null)
            Inventory.Instance.ApplySelectedWeapon(false);
        else
            Equip(HuntWeapon.Shotgun);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (scopeMask != null)
            Destroy(scopeMask);
    }

    void Update()
    {
        bool canAim = equipped
            && !CampfireBuild.Busy
            && !Inventory.IsOpen
            && !BirdChallenge.Active
            && !WoodBuilder.IsBuilding
            && (PlayerHealth.Instance == null || !PlayerHealth.Instance.isDead);
        aimHeld = canAim && Mouse.current != null && Mouse.current.rightButton.isPressed;

        // R tops the gun up early instead of waiting for it to run dry.
        if (canAim && !reloading && GunPresent > 0.9f && active != null && shells < active.magSize
            && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame
            && (fireTime < 0f || fireTime > 0.18f))
        {
            if (active.reserve <= 0)
            {
                if (Inventory.Instance != null)
                    Inventory.Instance.ShowNotice("Out of cartridges. The gun shop in the castle sells them.");
            }
            else
                StartReload();
        }

        TickAction(Time.deltaTime);
        TickHands(Time.deltaTime);
    }

    void LateUpdate()
    {
        if (!equipped || CampfireBuild.Busy)
        {
            SetVisible(false);
            return;
        }

        bool firstPerson = CameraViewSwitcher.Instance != null && CameraViewSwitcher.Instance.IsFirstPerson;
        bool scoped = firstPerson && active != null && active.scope && mount > 0.72f;
        SetGunVisible(viewGun, firstPerson && !scoped);
        SetGunVisible(handGun, !firstPerson);

        if (firstPerson)
            PoseFirstPerson();
        else
            PoseInHand();

        DriveBreak(viewGun);
        DriveBreak(handGun);
    }

    void DriveBreak(Transform gun)
    {
        if (gun == null)
            return;

        ShotgunBreak action = gun.GetComponent<ShotgunBreak>();
        if (action == null)
            return;

        bool shotgun = active != null && active.id == HuntWeapon.Shotgun;
        float load = 0f;
        if (shotgun && reloading)
        {
            float done = ReloadSeconds();
            load = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(done * 0.42f, done * 0.68f, fireTime));
        }

        action.SetOpen(shotgun ? breakOpen : 0f, load);
    }

    void TickAction(float dt)
    {
        TickFlash(dt);
        if (fireTime < 0f)
        {
            mount = Mathf.MoveTowards(mount, aimHeld ? 1f : 0f, dt * (aimHeld ? 5f : 3.5f));
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 6f);
            breakOpen = Mathf.MoveTowards(breakOpen, 0f, dt * 5f);
            follow = Mathf.MoveTowards(follow, 0f, dt * 6f);
            ApplyViewKick();
            return;
        }

        fireTime += dt;
        float window = KickWindow();
        float rise = 0.032f;
        if (fireTime < rise)
            recoil = Mathf.SmoothStep(0f, 1f, fireTime / rise);
        else
        {
            float u = Mathf.Clamp01((fireTime - rise) / Mathf.Max(0.05f, window - rise));
            float recover = Mathf.Exp(-u * 3.4f);
            float settle = Mathf.Sin(u * Mathf.PI) * 0.14f * (1f - u);
            recoil = Mathf.Clamp01(recover + settle);
        }

        ApplyViewKick();

        if (reloading)
        {
            follow = Mathf.MoveTowards(follow, 0f, dt * 8f);
            float done = ReloadSeconds();
            float openStart = active != null && active.id == HuntWeapon.Shotgun ? 0.18f : 0.2f;
            float openEnd = active != null && active.id == HuntWeapon.Shotgun ? done * 0.3f : done * 0.45f;
            float shutStart = active != null && active.id == HuntWeapon.Shotgun ? done * 0.78f : done * 0.7f;
            if (fireTime < openStart)
                breakOpen = 0f;
            else if (fireTime < openEnd)
                breakOpen = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(openStart, openEnd, fireTime));
            else if (fireTime < shutStart)
                breakOpen = 1f;
            else
                breakOpen = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(shutStart, done, fireTime));

            mount = fireTime < done ? 1f : 0f;
            if (fireTime >= done)
                FinishReload();
            return;
        }

        float followStart = 0.05f;
        float followEnd = window * 0.9f;
        if (fireTime > followStart && fireTime < followEnd)
        {
            float along = Mathf.InverseLerp(followStart, followEnd, fireTime);
            float amount = active != null && active.id == HuntWeapon.Shotgun ? 1f : 0.42f;
            follow = Mathf.Sin(along * Mathf.PI) * amount;
        }
        else
            follow = 0f;

        if (fireTime > window)
            fireTime = -1f;
    }

    float KickWindow()
    {
        if (active == null || active.id == HuntWeapon.Shotgun)
            return 0.56f;
        if (active.id == HuntWeapon.Sniper)
            return 0.36f;
        if (active.auto)
            return 0.16f;
        return 0.44f;
    }

    float RefireLock()
    {
        if (active == null)
            return 0.38f;
        return active.cycle > 0f ? active.cycle : 0.16f;
    }

    /// <summary>True while the equipped gun fires for as long as the trigger is held.</summary>
    public bool IsAutomatic => equipped && active != null && active.auto;

    float WeaponKick()
    {
        if (active != null && active.auto)
            return aimHeld ? 0.22f : 0.34f;
        if (active != null && active.id == HuntWeapon.Pistol)
            return aimHeld ? 0.45f : 0.6f;
        if (active == null || active.id == HuntWeapon.Shotgun)
            return aimHeld ? 0.8f : 1.08f;
        if (active.id == HuntWeapon.Sniper)
            return aimHeld ? 0.36f : 0.6f;
        return aimHeld ? 0.55f : 0.82f;
    }

    void ApplyViewKick()
    {
        float degrees = 2f;
        if (active == null || active.id == HuntWeapon.Shotgun)
            degrees = 3.5f;
        else if (active.id == HuntWeapon.Sniper)
            degrees = aimHeld ? 0.7f : 1.45f;
        ViewKick = recoil * degrees * kickScale;
        ViewRoll = recoil * rollSign * 1.2f * kickScale;
    }

    void TickFlash(float dt)
    {
        if (flashLight == null)
            return;

        flashLeft -= dt;
        if (flashLeft <= 0f)
        {
            Destroy(flashLight.gameObject);
            flashLight = null;
            return;
        }

        float life = Mathf.Clamp01(flashLeft / 0.045f);
        flashLight.intensity = 32f * life;
        flashLight.range = 4f + 10f * life;
    }

    void FinishReload()
    {
        fireTime = -1f;
        reloading = false;
        mount = 0f;
        recoil = 0f;
        breakOpen = 0f;
        if (active == null)
            return;

        int room = active.magSize - shells;
        int take = Mathf.Min(room, active.reserve);
        if (take <= 0)
        {
            if (Inventory.Instance != null)
                Inventory.Instance.ShowNotice("Out of cartridges. The gun shop in the castle sells them.");
            return;
        }

        active.reserve -= take;
        shells += take;
        active.ammo = shells;
        if (active.reserve <= 4 && Inventory.Instance != null)
            Inventory.Instance.ShowNotice(active.reserve == 0
                ? "That was the last of your cartridges."
                : active.reserve + " cartridges left.");
    }

    public int CartridgeCount(HuntWeapon weapon)
    {
        Rig rig = RigFor(weapon);
        return rig != null ? rig.reserve : 0;
    }

    public void AddCartridges(HuntWeapon weapon, int amount)
    {
        Rig rig = RigFor(weapon);
        if (rig == null || amount == 0)
            return;

        rig.reserve = Mathf.Clamp(rig.reserve + amount, 0, 240);
    }

    Rig RigFor(HuntWeapon weapon)
    {
        if (built == null || weapon == HuntWeapon.None)
            return null;

        int index = (int)weapon;
        if (index < 0 || index >= built.Length)
            return null;

        return built[index];
    }

    void SeedCartridges()
    {
        // Only the starting pistol comes with spare magazines; bought guns come with a box each.
        SetReserve(HuntWeapon.Pistol, 48);
        SetReserve(HuntWeapon.Shotgun, 18);
        SetReserve(HuntWeapon.SMG, 60);
        SetReserve(HuntWeapon.Rifle, 60);
        SetReserve(HuntWeapon.Sniper, 10);
    }

    void SetReserve(HuntWeapon weapon, int amount)
    {
        Rig rig = RigFor(weapon);
        if (rig != null)
            rig.reserve = amount;
    }

    public HuntWeapon Equipped => equipped && active != null ? active.id : HuntWeapon.None;

    public bool TryRounds(HuntWeapon weapon, out int rounds, out int mag, out float gunRange)
    {
        rounds = 0;
        mag = 0;
        gunRange = 0f;
        if (built == null || weapon == HuntWeapon.None)
            return false;

        int index = (int)weapon;
        if (index < 0 || index >= built.Length || built[index] == null)
            return false;

        Rig rig = built[index];
        // A gun you don't own can't be suggested or switched to.
        if (Inventory.Instance != null && !Inventory.Instance.OwnsWeapon(weapon))
            return false;
        mag = rig.magSize;
        gunRange = rig.gunRange;
        rounds = active == rig ? shells : rig.ammo;
        if (rounds < 0)
            rounds = 0;
        return true;
    }

    public void Equip(HuntWeapon weapon)
    {
        if (built == null)
            return;

        if (active != null)
            active.ammo = shells;

        bool axe = weapon == HuntWeapon.None && Inventory.AxeInHand;
        bool sameGun = weapon != HuntWeapon.None && active != null && active.id == weapon && equipped && !gunHolster;
        if (sameGun)
        {
            SetAxe(false);
            return;
        }

        aimHeld = false;
        fireTime = -1f;
        reloading = false;
        mount = 0f;
        recoil = 0f;
        breakOpen = 0f;
        SetAxe(axe);

        if (weapon == HuntWeapon.None)
        {
            if (active != null)
                BeginGunHolster();
            else
                GunPresent = 0f;
            return;
        }

        active = built[(int)weapon];
        if (active == null)
        {
            equipped = false;
            return;
        }

        equipped = true;
        viewGun = active.view;
        handGun = active.hand;
        leftGrip = active.leftGrip;
        rightGripLocal = active.rightGrip;
        shells = active.ammo;
        pellets = active.pelletCount;
        spreadAngle = active.spread;
        range = active.gunRange;
        SetAllHidden();
        SetVisible(true);
        BeginGunDraw(weapon);
        if (CameraViewSwitcher.Instance != null)
            CameraViewSwitcher.Instance.RefreshBodyRenderers();
    }

    void BeginGunDraw(HuntWeapon weapon)
    {
        gunHolster = false;
        gunFrom = 0f;
        gunTo = 1f;
        gunDur = weapon == HuntWeapon.Shotgun ? 0.66f : weapon == HuntWeapon.Sniper ? 0.82f : weapon == HuntWeapon.Pistol ? 0.34f : 0.5f;
        gunClock = 0f;
        GunPresent = 0f;
    }

    void BeginGunHolster()
    {
        if (gunHolster)
            return;

        gunHolster = true;
        gunFrom = GunPresent;
        gunTo = 0f;
        gunDur = 0.3f;
        gunClock = 0f;
    }

    void FinishHolster()
    {
        gunHolster = false;
        active = null;
        equipped = false;
        viewGun = null;
        handGun = null;
        leftGrip = null;
        GunPresent = 0f;
        SetAllHidden();
    }

    void SetAxe(bool want)
    {
        if (axeWanted == want)
            return;

        axeWanted = want;
        axeFrom = AxePresent;
        axeTo = want ? 1f : 0f;
        axeDur = want ? 0.42f : 0.24f;
        axeClock = 0f;
    }

    void TickHands(float dt)
    {
        if (gunClock >= 0f)
        {
            gunClock += dt;
            float along = Mathf.Clamp01(gunClock / Mathf.Max(0.05f, gunDur));
            float eased = 1f - Mathf.Pow(1f - along, 3f);
            GunPresent = Mathf.Lerp(gunFrom, gunTo, eased);
            if (along >= 1f)
            {
                gunClock = -1f;
                GunPresent = gunTo;
                if (gunHolster)
                    FinishHolster();
            }
        }

        if (axeClock >= 0f)
        {
            axeClock += dt;
            float along = Mathf.Clamp01(axeClock / Mathf.Max(0.05f, axeDur));
            float eased = 1f - Mathf.Pow(1f - along, 3f);
            AxePresent = Mathf.Lerp(axeFrom, axeTo, eased);
            if (along >= 1f)
            {
                axeClock = -1f;
                AxePresent = axeTo;
            }
        }
    }

    void DrawBlends(out float place, out float level)
    {
        float present = Mathf.Clamp01(GunPresent);
        float rise = 1f - Mathf.Pow(1f - present, 3f);
        if (active != null && active.id == HuntWeapon.Sniper)
        {
            place = 1f - Mathf.Pow(1f - Mathf.Clamp01(present / 0.48f), 2f);
            level = rise;
            return;
        }

        place = rise;
        level = rise;
    }

    void DrawHip(out Vector3 pos, out Quaternion rot)
    {
        if (active != null && active.id == HuntWeapon.Shotgun)
        {
            pos = new Vector3(0.28f, -0.62f, -0.06f);
            rot = Quaternion.Euler(72f, 46f, 26f);
            return;
        }

        if (active != null && active.id == HuntWeapon.Sniper)
        {
            pos = new Vector3(0.05f, -0.22f, -0.2f);
            rot = Quaternion.Euler(62f, 8f, -6f);
            return;
        }

        pos = new Vector3(0.12f, -0.52f, 0.02f);
        rot = Quaternion.Euler(76f, 14f, 8f);
    }

    public void Toggle()
    {
        if (Inventory.Instance != null)
        {
            Inventory.Instance.SelectHotbar(equipped ? -1 : 0, true);
            return;
        }

        Equip(equipped ? HuntWeapon.None : HuntWeapon.Pistol);
    }

    public bool TryMuzzle(out Vector3 world)
    {
        Transform gun = CameraViewSwitcher.Instance != null && CameraViewSwitcher.Instance.IsFirstPerson
            ? viewGun
            : handGun;
        if (gun == null || active == null)
        {
            world = gun != null ? gun.position : transform.position;
            return gun != null;
        }

        world = gun.TransformPoint(active.muzzle);
        return true;
    }

    public void TryFire()
    {
        if (!DeerChallenge.AllowFire())
            return;
        if (GunPresent < 0.84f)
            return;
        if (!equipped || reloading)
            return;
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;
        if (fireTime >= 0f && fireTime < RefireLock())
            return;

        if (shells <= 0)
        {
            if (active == null || active.reserve <= 0)
            {
                if (Inventory.Instance != null)
                    Inventory.Instance.ShowNotice("Out of cartridges. The gun shop in the castle sells them.");
                return;
            }

            StartReload();
            return;
        }

        fireTime = 0f;
        mount = 1f;
        recoil = 0f;
        kickScale = WeaponKick();
        rollSign = Random.value < 0.5f ? -1f : 1f;
        DeerChallenge.CountShot();
        HuntFeedback.ShotFired();
        shells--;
        if (active != null)
            active.ammo = shells;
        if (active == null || active.id == HuntWeapon.Shotgun)
            HuntCampBoard.RegisterShotgunBlast();
        WildlifeAlarm.NotifyShot(PlayerView.Position());
        Flash();
        GameAudio.Gunshot(active != null ? active.id : HuntWeapon.Shotgun);
        FirePellets();
        // The HUD weapon panel shows the rounds left, so only the empty gun needs a word.
        if (Inventory.Instance != null && shells <= 0)
            Inventory.Instance.ShowNotice(ShotLine());

        if (shells <= 0 && active != null && active.reserve > 0)
            StartReload();
        else if (shells <= 0 && Inventory.Instance != null)
            Inventory.Instance.ShowNotice("Out of cartridges. The gun shop in the castle sells them.");
    }

    void StartReload()
    {
        if (!reloading)
            GameAudio.Reload(active != null ? active.id : HuntWeapon.Shotgun);
        reloading = true;
        if (fireTime < 0f)
            fireTime = 0f;
        mount = 1f;
    }

    void FirePellets()
    {
        GetAim(out Vector3 origin, out Vector3 forward, out Vector3 up, out Vector3 right);
        if (BallisticsGun.Current == null)
            return;

        float spread = aimHeld ? spreadAngle * 0.28f : spreadAngle;
        BallisticsGun.Current.Fire(origin, forward, up, right, spread, range, pellets, transform);
    }

    void GetAim(out Vector3 origin, out Vector3 forward, out Vector3 up, out Vector3 right)
    {
        Camera cam = Camera.main;
        if (PlayerView.IsFirstPerson() && cam != null)
        {
            origin = cam.transform.position;
            forward = cam.transform.forward;
            up = cam.transform.up;
            right = cam.transform.right;
            return;
        }

        origin = PlayerView.AimOrigin();
        forward = PlayerView.AimDirection();
        right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;
        right.Normalize();
        up = Vector3.Cross(forward, right).normalized;
    }

    void PoseFirstPerson()
    {
        if (viewGun == null)
            return;

        Vector3 pos = Vector3.Lerp(ViewReadyPos, ViewAimPos, mount);
        Quaternion rot = Quaternion.Slerp(ViewReadyRot, ViewAimRot, mount);
        float kick = recoil * kickScale;
        bool shotgun = active != null && active.id == HuntWeapon.Shotgun;
        float open = shotgun ? breakOpen : 0f;
        float bolt = shotgun ? 0f : breakOpen;
        pos += new Vector3(0.045f * rollSign, 0.07f, -0.18f) * kick;
        pos += new Vector3(0f, -0.02f, -0.09f) * follow;
        pos += new Vector3(-0.07f, 0.045f, 0.04f) * open;
        pos += new Vector3(0f, -0.05f, 0.04f) * bolt;
        rot = Quaternion.Euler(-26f * kick - 14f * open + 34f * bolt, 5f * rollSign * kick + 38f * open, -7f * rollSign * kick) * rot;
        DrawBlends(out float place, out float level);
        DrawHip(out Vector3 hipPos, out Quaternion hipRot);
        float plant = Mathf.Sin(Mathf.Clamp01((GunPresent - 0.75f) / 0.25f) * Mathf.PI);
        pos = Vector3.Lerp(pos + hipPos, pos, place);
        pos.y += plant * 0.025f;
        rot = Quaternion.Slerp(hipRot * rot, rot, level);
        rot = Quaternion.Euler(-8f * plant, 0f, 4f * plant) * rot;
        viewGun.localPosition = pos;
        viewGun.localRotation = rot;
        WeaponHold.SetWorldScale(viewGun, (active != null ? active.scale : 1f) * ViewGunScale);

        if (cam != null)
        {
            Camera fp = cam.GetComponent<Camera>();
            if (fp != null)
                fp.nearClipPlane = 0.02f;
        }
    }

    void PoseInHand()
    {
        if (handGun == null)
            return;

        WeaponHold hold = WeaponHold.Instance;
        if (hold == null)
            return;

        hold.AttachToShoulder(handGun, active != null ? active.scale : 1f);

        Vector3 up = transform.up;
        Vector3 right = transform.right;
        Vector3 fwd = transform.forward;
        Vector3 aim = PlayerView.AimDirection();
        if (aim.sqrMagnitude < 0.0001f)
            aim = fwd;
        aim.Normalize();

        Vector3 lowBarrel = (fwd * 0.78f - up * 0.42f + right * 0.05f).normalized;
        Vector3 barrel = Vector3.Slerp(lowBarrel, aim, mount).normalized;
        float kick = recoil * kickScale;
        bool shotgun = active != null && active.id == HuntWeapon.Shotgun;
        float open = shotgun ? breakOpen : 0f;
        float bolt = shotgun ? 0f : breakOpen;
        barrel = Quaternion.AngleAxis(-16f * kick + 22f * bolt - 18f * open, right) * barrel;
        if (open > 0.001f)
            barrel = Quaternion.AngleAxis(16f * open, up) * barrel;
        DrawBlends(out float place, out float level);
        Vector3 hip = shotgun
            ? (right * 0.62f - up * 0.7f + fwd * 0.12f).normalized
            : (active != null && active.id == HuntWeapon.Sniper
                ? (fwd * 0.25f - up * 0.72f + right * 0.18f).normalized
                : (fwd * 0.16f - up * 0.92f + right * 0.22f).normalized);
        barrel = Vector3.Slerp(hip, barrel, level).normalized;
        Vector3 side = Vector3.Cross(up, barrel);
        if (side.sqrMagnitude < 0.0001f)
            side = right;
        Vector3 rolledUp = Vector3.Cross(barrel, side).normalized;
        Vector3 gunUp = Vector3.Slerp(up, rolledUp, 0.2f + 0.8f * mount);
        Quaternion rot = Quaternion.LookRotation(barrel, gunUp);

        Transform shoulder = hold.ShoulderAnchor();
        Vector3 pocket = shoulder.position + right * 0.07f - up * 0.03f;
        Vector3 lowButt = pocket + fwd * 0.08f - up * 0.05f + right * 0.02f;
        Vector3 aimButt = pocket + aim * 0.02f + up * 0.015f;
        Vector3 butt = Vector3.Lerp(lowButt, aimButt, mount);
        butt = Vector3.Lerp(pocket - up * 0.42f + right * 0.12f, butt, place);
        butt += up * (0.06f * kick) - barrel * (0.1f * kick + 0.05f * follow);
        // A pistol is held out at arm's length, not shouldered like a long gun.
        if (active != null && (active.id == HuntWeapon.Pistol))
            butt += barrel * Mathf.Lerp(0.22f, 0.42f, mount) - up * 0.06f;

        handGun.SetPositionAndRotation(butt - rot * (active != null ? active.stock : Vector3.zero), rot);
        WeaponHold.SetWorldScale(handGun, active != null ? active.scale : 1f);

        Vector3 rightHandTarget = handGun.TransformPoint(rightGripLocal);
        Vector3 leftHandTarget = leftGrip != null
            ? leftGrip.position
            : handGun.TransformPoint(new Vector3(0f, 0.03f, 0.28f));
        hold.HandsOnWeapon(rightHandTarget, leftHandTarget, barrel);
    }

    void BuildGuns()
    {
        if (built != null)
            return;

        built = new Rig[WeaponVisual.Count];
        MakeRig(HuntWeapon.Pistol, 12, 1, 1.3f, 70f, 0.62f, false, WeaponVisual.DisplayName(HuntWeapon.Pistol), false, 0.2f);
        MakeRig(HuntWeapon.Shotgun, 6, 18, 9f, 110f, 0.75f, false, WeaponVisual.DisplayName(HuntWeapon.Shotgun), false, 0.62f);
        MakeRig(HuntWeapon.SMG, 30, 1, 2.1f, 110f, 0.6f, false, WeaponVisual.DisplayName(HuntWeapon.SMG), true, 0.075f);
        MakeRig(HuntWeapon.Rifle, 30, 1, 0.75f, 260f, 0.42f, false, WeaponVisual.DisplayName(HuntWeapon.Rifle), true, 0.115f);
        MakeRig(HuntWeapon.Sniper, 5, 1, 0.04f, 550f, 0.15f, true, WeaponVisual.DisplayName(HuntWeapon.Sniper), false, 0.95f);
        SetAllHidden();
    }

    void MakeRig(HuntWeapon id, int mag, int pelletCount, float spread, float gunRange, float zoom, bool scope, string label, bool auto, float cycle)
    {
        Rig rig = new Rig
        {
            id = id,
            auto = auto,
            cycle = cycle,
            magSize = mag,
            ammo = mag,
            pelletCount = pelletCount,
            spread = spread,
            gunRange = gunRange,
            zoom = zoom,
            scope = scope,
            label = label
        };

        WeaponModel hand = WeaponVisual.Build(id, transform);
        rig.hand = hand.root.transform;
        rig.stock = hand.stock;
        rig.muzzle = hand.muzzle;
        rig.rightGrip = hand.rightGrip;
        rig.scale = hand.scale;
        rig.leftGrip = new GameObject("LeftGrip").transform;
        rig.leftGrip.SetParent(rig.hand, false);
        rig.leftGrip.localPosition = hand.leftGrip;

        if (cam != null)
        {
            WeaponModel view = WeaponVisual.Build(id, cam);
            rig.view = view.root.transform;
            // No first-person arms: just the gun.
            rig.stock = view.stock;
            rig.muzzle = view.muzzle;
            rig.rightGrip = view.rightGrip;
            rig.scale = view.scale;
        }

        if (id == HuntWeapon.Shotgun)
        {
            ShotgunBreak handBreak = rig.hand.GetComponent<ShotgunBreak>();
            if (handBreak != null)
                handBreak.GrabForeend(rig.leftGrip);
            if (rig.view != null)
            {
                ShotgunBreak viewBreak = rig.view.GetComponent<ShotgunBreak>();
                if (viewBreak != null)
                    viewBreak.GrabForeend(null);
            }
        }

        built[(int)id] = rig;
    }

    void SetAllHidden()
    {
        if (built == null)
            return;

        for (int i = 0; i < built.Length; i++)
        {
            if (built[i] == null)
                continue;
            SetGunVisible(built[i].view, false);
            SetGunVisible(built[i].hand, false);
        }
    }

    string ShotLine()
    {
        string name = active != null ? active.label : "Shot";
        if (shells > 0)
            return name + ". " + shells + " left.";
        return name + ". Reloading.";
    }

    void OnGUI()
    {
        bool firstPerson = CameraViewSwitcher.Instance == null || CameraViewSwitcher.Instance.IsFirstPerson;
        if (!equipped || !firstPerson || active == null || !active.scope || mount < 0.25f || Inventory.IsOpen)
            return;

        EnsureScopeMask();
        float alpha = Mathf.InverseLerp(0.25f, 0.8f, mount);
        float side = Mathf.Min(Screen.width, Screen.height) * 0.96f;
        float x = (Screen.width - side) * 0.5f;
        float y = (Screen.height - side) * 0.5f;
        Color prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, y), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, y + side, Screen.width, Screen.height - (y + side)), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, y, x, side), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + side, y, Screen.width - (x + side), side), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x, y, side, side), scopeMask);

        DrawReticle(alpha, side * 0.46f);
        GUI.color = prev;
    }

    /// <summary>
    /// A hunting scope reticle: thin crosshairs through the centre, heavy outer posts, mil-dots for
    /// judging range, holdover marks for long shots, and an illuminated centre dot.
    /// </summary>
    static void DrawReticle(float alpha, float radius)
    {
        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        float u = Mathf.Max(1f, Screen.height / 900f); // line weight scales with resolution
        Texture2D w = Texture2D.whiteTexture;
        float post = radius * 0.42f;   // where the heavy posts stop, toward the centre

        // Thin crosshairs, full width.
        GUI.color = new Color(0.03f, 0.03f, 0.03f, alpha);
        GUI.DrawTexture(new Rect(cx - radius, cy - 0.5f * u, radius * 2f, u), w);
        GUI.DrawTexture(new Rect(cx - 0.5f * u, cy - radius, u, radius * 2f), w);

        // Heavy posts from the edge inward (left, right, bottom; the top stays thin).
        float t = 5f * u;
        GUI.DrawTexture(new Rect(cx - radius, cy - t * 0.5f, radius - post, t), w);
        GUI.DrawTexture(new Rect(cx + post, cy - t * 0.5f, radius - post, t), w);
        GUI.DrawTexture(new Rect(cx - t * 0.5f, cy + post, t, radius - post), w);

        // Mil-dots along both axes inside the posts.
        float step = post / 5f;
        float dot = 3.5f * u;
        for (int i = 1; i <= 4; i++)
        {
            float d = step * i;
            GUI.DrawTexture(new Rect(cx + d - dot * 0.5f, cy - dot * 0.5f, dot, dot), w);
            GUI.DrawTexture(new Rect(cx - d - dot * 0.5f, cy - dot * 0.5f, dot, dot), w);
            GUI.DrawTexture(new Rect(cx - dot * 0.5f, cy - d - dot * 0.5f, dot, dot), w);
        }

        // Holdover marks below the centre for 200, 300 and 400 m shots.
        for (int i = 1; i <= 3; i++)
        {
            float y = cy + step * i;
            float half = step * (0.55f - i * 0.1f);
            GUI.DrawTexture(new Rect(cx - half, y - 0.5f * u, half * 2f, u * 1.5f), w);
        }

        // Illuminated centre dot with a soft glow.
        GUI.color = new Color(1f, 0.22f, 0.12f, 0.35f * alpha);
        float glow = 9f * u;
        GUI.DrawTexture(new Rect(cx - glow * 0.5f, cy - glow * 0.5f, glow, glow), w);
        GUI.color = new Color(1f, 0.3f, 0.18f, alpha);
        float core = 4f * u;
        GUI.DrawTexture(new Rect(cx - core * 0.5f, cy - core * 0.5f, core, core), w);
    }

    void EnsureScopeMask()
    {
        if (scopeMask != null)
            return;

        const int n = 256;
        scopeMask = new Texture2D(n, n, TextureFormat.RGBA32, false);
        scopeMask.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[n * n];
        Vector2 center = new Vector2(n * 0.5f, n * 0.5f);
        float radius = n * 0.46f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float edge = Mathf.InverseLerp(radius - 2f, radius + 10f, dist);
                pixels[y * n + x] = new Color(0f, 0f, 0f, edge);
            }
        }

        scopeMask.SetPixels(pixels);
        scopeMask.Apply();
    }

    void ClearLeftoverGuns()
    {
        ClearNamedGuns(transform);
        if (cam != null && !cam.IsChildOf(transform))
            ClearNamedGuns(cam);
    }

    static void ClearNamedGuns(Transform root)
    {
        if (root == null)
            return;

        Transform[] kids = root.GetComponentsInChildren<Transform>(true);
        for (int i = kids.Length - 1; i >= 0; i--)
        {
            if (kids[i] == null)
                continue;
            string name = kids[i].name;
            if (name == ShotgunVisual.RootName || name == "Rifle" || name == "Sniper")
                Destroy(kids[i].gameObject);
        }
    }

    void Flash()
    {
        Transform gun = CameraViewSwitcher.Instance != null && CameraViewSwitcher.Instance.IsFirstPerson
            ? viewGun
            : handGun;
        if (gun == null)
            return;

        if (flashLight != null)
            Destroy(flashLight.gameObject);

        GameObject flash = new GameObject("ShotgunFlash");
        flash.transform.SetParent(gun, false);
        flash.transform.localPosition = active != null ? active.muzzle : ShotgunVisual.MuzzleLocal;
        flashLight = flash.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = new Color(1f, 0.78f, 0.42f);
        flashLight.range = 14f;
        flashLight.intensity = 32f;
        flashLeft = 0.07f;
    }

    void SetVisible(bool on)
    {
        SetGunVisible(viewGun, on);
        SetGunVisible(handGun, on);
    }

    static void SetGunVisible(Transform gun, bool on)
    {
        if (gun == null)
            return;

        gun.gameObject.SetActive(on);
        if (!on)
            return;

        Renderer[] renderers = gun.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = true;
        }
    }

}
