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

        if (cam != null)
        {
            viewGun = ShotgunVisual.Build(cam).transform;
            FirstPersonHands.AttachTo(viewGun);
        }

        handGun = ShotgunVisual.Build(transform).transform;
        rightGripLocal = new Vector3(
            0f,
            ShotgunVisual.StockLocal.y,
            Mathf.Lerp(ShotgunVisual.StockLocal.z, 0.02f, 0.72f));
        leftGrip = new GameObject("LeftGrip").transform;
        leftGrip.SetParent(handGun, false);
        leftGrip.localPosition = new Vector3(
            0f,
            Mathf.Lerp(ShotgunVisual.StockLocal.y, ShotgunVisual.MuzzleLocal.y, 0.58f),
            Mathf.Lerp(ShotgunVisual.StockLocal.z, ShotgunVisual.MuzzleLocal.z, 0.62f));

        if (WeaponHold.Instance != null)
            WeaponHold.Instance.AttachToShoulder(handGun, ShotgunVisual.FittedScale);

        equipped = true;
        SetVisible(true);
        if (CameraViewSwitcher.Instance != null)
            CameraViewSwitcher.Instance.RefreshBodyRenderers();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (!Inventory.IsOpen && !BirdChallenge.Active && !WoodBuilder.IsBuilding)
                Toggle();
        }

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
        SetGunVisible(viewGun, firstPerson);
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
            if (fireTime < 0.20f)
                breakOpen = 0f;
            else if (fireTime < 0.48f)
                breakOpen = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.20f, 0.48f, fireTime));
            else if (fireTime < 0.82f)
                breakOpen = 1f;
            else if (fireTime < 1.08f)
                breakOpen = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.82f, 1.08f, fireTime));
            else
                breakOpen = 0f;

            if (fireTime < 1.10f)
                mount = 1f;
            else if (fireTime < 1.38f)
                mount = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.10f, 1.38f, fireTime));
            else
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
        shells = 2;
        mount = 0f;
        recoil = 0f;
        breakOpen = 0f;
    }

    public void Toggle()
    {
        equipped = !equipped;
        aimHeld = false;
        fireTime = -1f;
        reloading = false;
        shells = 2;
        mount = 0f;
        recoil = 0f;
        breakOpen = 0f;
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(equipped ? "Double barrel. Two shots, then it breaks open." : "Axe ready.");
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
        WildlifeAlarm.NotifyShot(PlayerView.Position());
        Flash();
        FirePellets();
        if (Inventory.Instance != null)
            Inventory.Instance.ShowNotice(shells > 0 ? "Shot. 1 shell left." : "Shot. The gun breaks open to reload.");

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
        WeaponHold.SetWorldScale(viewGun, ShotgunVisual.FittedScale * 0.9f);

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

        hold.AttachToShoulder(handGun, ShotgunVisual.FittedScale);

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

        handGun.SetPositionAndRotation(butt - rot * ShotgunVisual.StockLocal, rot);
        WeaponHold.SetWorldScale(handGun, ShotgunVisual.FittedScale);

        Vector3 rightHandTarget = handGun.TransformPoint(rightGripLocal);
        Vector3 leftHandTarget = leftGrip != null
            ? leftGrip.position
            : handGun.TransformPoint(new Vector3(0f, 0.03f, 0.28f));
        hold.HandsOnWeapon(rightHandTarget, leftHandTarget, barrel);
    }

    void ClearLeftoverGuns()
    {
        Transform[] kids = GetComponentsInChildren<Transform>(true);
        for (int i = kids.Length - 1; i >= 0; i--)
        {
            if (kids[i] != null && kids[i].name == ShotgunVisual.RootName)
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
        flash.transform.localPosition = ShotgunVisual.MuzzleLocal;
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
