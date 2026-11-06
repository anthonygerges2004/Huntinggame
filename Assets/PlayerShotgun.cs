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

    public static float AimFovScale => Instance != null && Instance.active != null && Instance.equipped
        ? Instance.active.zoom
        : 1f;
    public static bool ScopeIsUp => Instance != null && Instance.equipped && Instance.active != null
        && Instance.active.scope && Instance.mount > 0.72f;

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
    }

    Vector3 rightGripLocal = new Vector3(0f, 0.01f, -0.02f);
    float fireTime = -1f;
    float mount;
    float recoil;
    float breakOpen;

    static readonly Vector3 ViewReadyPos = new Vector3(0.38f, -0.36f, 0.92f);
    static readonly Quaternion ViewReadyRot = Quaternion.Euler(8f, 3f, 0f);
    static readonly Vector3 ViewAimPos = new Vector3(0.04f, -0.15f, 0.78f);
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
            && !Inventory.IsOpen
            && !BirdChallenge.Active
            && !WoodBuilder.IsBuilding
            && (PlayerHealth.Instance == null || !PlayerHealth.Instance.isDead);
        aimHeld = canAim && Mouse.current != null && Mouse.current.rightButton.isPressed;

        TickAction(Time.deltaTime);
    }

    void LateUpdate()
    {
        if (!equipped)
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
    }

    void TickAction(float dt)
    {
        if (fireTime < 0f)
        {
            mount = Mathf.MoveTowards(mount, aimHeld ? 1f : 0f, dt * (aimHeld ? 5f : 3.5f));
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 5f);
            breakOpen = Mathf.MoveTowards(breakOpen, 0f, dt * 5f);
            return;
        }

        fireTime += dt;
        recoil = fireTime < 0.08f ? 1f : Mathf.Exp(-(fireTime - 0.08f) * 6.5f);

        if (reloading)
        {
            bool shotgun = active == null || active.id == HuntWeapon.Shotgun;
            float done = shotgun ? 1.38f : 0.7f;
            if (fireTime < 0.20f)
                breakOpen = 0f;
            else if (fireTime < done * 0.45f)
                breakOpen = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.20f, done * 0.45f, fireTime));
            else if (fireTime < done * 0.7f)
                breakOpen = 1f;
            else
                breakOpen = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(done * 0.7f, done, fireTime));

            mount = fireTime < done ? 1f : 0f;
            if (fireTime >= done)
                FinishReload();
            return;
        }

        if (fireTime > 0.28f)
            fireTime = -1f;
    }

    void FinishReload()
    {
        fireTime = -1f;
        reloading = false;
        shells = active != null ? active.magSize : 2;
        if (active != null)
            active.ammo = shells;
        mount = 0f;
        recoil = 0f;
        breakOpen = 0f;
    }

    public void Equip(HuntWeapon weapon)
    {
        if (built == null)
            return;

        if (active != null)
            active.ammo = shells;

        aimHeld = false;
        fireTime = -1f;
        reloading = false;
        mount = 0f;
        recoil = 0f;
        breakOpen = 0f;

        if (weapon == HuntWeapon.None || built == null)
        {
            active = null;
            equipped = false;
            viewGun = null;
            handGun = null;
            leftGrip = null;
            SetAllHidden();
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
        if (CameraViewSwitcher.Instance != null)
            CameraViewSwitcher.Instance.RefreshBodyRenderers();
    }

    public void Toggle()
    {
        if (Inventory.Instance != null)
        {
            Inventory.Instance.SelectHotbar(equipped ? -1 : 0, true);
            return;
        }

        Equip(equipped ? HuntWeapon.None : HuntWeapon.Shotgun);
    }

    public void TryFire()
    {
        if (!equipped || reloading)
            return;
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;
        if (fireTime >= 0f && fireTime < 0.18f)
            return;

        if (shells <= 0)
        {
            StartReload();
            return;
        }

        fireTime = 0f;
        mount = 1f;
        recoil = 1f;
        shells--;
        if (active != null)
            active.ammo = shells;
        if (active == null || active.id == HuntWeapon.Shotgun)
            HuntCampBoard.RegisterShotgunBlast();
        WildlifeAlarm.NotifyShot(PlayerView.Position());
        Flash();
        FirePellets();
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(ShotLine());

        if (shells <= 0)
            StartReload();
    }

    void StartReload()
    {
        reloading = true;
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
        pos += new Vector3(0.02f, 0.05f, -0.08f) * recoil;
        pos += new Vector3(0f, -0.05f, 0.04f) * breakOpen;
        rot = Quaternion.Euler(-16f * recoil + 34f * breakOpen, 2f * recoil, -2f * recoil) * rot;
        viewGun.localPosition = pos;
        viewGun.localRotation = rot;
        WeaponHold.SetWorldScale(viewGun, (active != null ? active.scale : 1f) * 0.9f);

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
        barrel = Quaternion.AngleAxis(-8f * recoil + 22f * breakOpen, right) * barrel;
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
        butt += up * 0.02f * recoil - barrel * 0.03f * recoil;

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

        built = new Rig[4];
        MakeRig(HuntWeapon.Shotgun, 2, 24, 10f, 120f, 0.75f, false, "Double barrel");
        MakeRig(HuntWeapon.Rifle, 5, 1, 0.45f, 240f, 0.42f, false, "Hunting rifle");
        MakeRig(HuntWeapon.Sniper, 4, 1, 0.04f, 500f, 0.15f, true, "Sniper");
        SetAllHidden();
    }

    void MakeRig(HuntWeapon id, int mag, int pelletCount, float spread, float gunRange, float zoom, bool scope, string label)
    {
        Rig rig = new Rig
        {
            id = id,
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
            FirstPersonHands.AttachTo(rig.view);
            rig.stock = view.stock;
            rig.muzzle = view.muzzle;
            rig.rightGrip = view.rightGrip;
            rig.scale = view.scale;
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
        if (active != null && active.id == HuntWeapon.Shotgun)
            return name + ". It breaks open to reload.";
        return name + ". Working the bolt.";
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

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * 0.5f;
        GUI.color = new Color(0.05f, 0.05f, 0.05f, alpha);
        GUI.DrawTexture(new Rect(cx - 140f, cy - 1f, 110f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx + 30f, cy - 1f, 110f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - 1f, cy - 140f, 2f, 110f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(cx - 1f, cy + 30f, 2f, 110f), Texture2D.whiteTexture);
        for (int i = 1; i <= 4; i++)
        {
            float mark = 28f * i;
            GUI.DrawTexture(new Rect(cx + mark - 1f, cy - 6f, 2f, 12f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - mark - 1f, cy - 6f, 2f, 12f), Texture2D.whiteTexture);
        }

        GUI.color = prev;
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

        GameObject flash = new GameObject("ShotgunFlash");
        flash.transform.SetParent(gun, false);
        flash.transform.localPosition = active != null ? active.muzzle : ShotgunVisual.MuzzleLocal;
        Light light = flash.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.72f, 0.28f);
        light.range = 8f;
        light.intensity = 14f;
        Destroy(flash, 0.16f);
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
