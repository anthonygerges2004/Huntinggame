using UnityEngine;
using UnityEngine.InputSystem;

namespace DeerHunt
{
    /// <summary>
    /// Rifle. Left click fires a ray from the camera center, right click opens the scope,
    /// R reloads. Spread shrinks while zoomed or crouched. Recoil is a small kick on the look.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        [SerializeField] float damage = 40f;
        [SerializeField] float range = 220f;
        [SerializeField] float fireCooldown = 0.4f;
        [SerializeField] int magazineSize = 5;
        [SerializeField] int reserveAmmo = 25;
        [SerializeField] float reloadSeconds = 1.5f;
        [SerializeField] float hipSpread = 1.15f;
        [SerializeField] float zoomSpread = 0.08f;
        [SerializeField] float crouchSpreadScale = 0.6f;
        [SerializeField] float recoilKick = 1.4f;
        [SerializeField] float normalFov = 60f;
        [SerializeField] float scopeFov = 16f;
        [SerializeField] float zoomSpeed = 8f;
        [SerializeField] LayerMask hitMask = ~0;

        Camera view;
        PlayerController player;
        float nextShot;
        float reloadTimer;
        int loaded;
        int reserve;
        float fov;
        bool zoomed;
        Texture2D scopeMask;
        Transform gun;

        public bool IsZoomed => zoomed;
        public bool Reloading => reloadTimer > 0f;
        public int Loaded => loaded;
        public int Reserve => reserve;
        public int MagazineSize => magazineSize;

        /// <summary>Raised at the muzzle position so deer can hear a nearby shot.</summary>
        public static event System.Action<Vector3> ShotFired;

        void Awake()
        {
            view = GetComponent<Camera>();
            if (view == null)
                view = GetComponentInParent<Camera>();
            player = GetComponentInParent<PlayerController>();
            loaded = magazineSize;
            reserve = reserveAmmo;
            int ignore = LayerMask.NameToLayer("Ignore Raycast");
            if (ignore >= 0)
                hitMask &= ~(1 << ignore);
            if (view != null)
            {
                normalFov = view.fieldOfView;
                fov = normalFov;
            }

            BuildGun();
        }

        void Update()
        {
            bool playing = GameManager.Instance == null || GameManager.Instance.Playing;
            if (!playing || Keyboard.current == null || Mouse.current == null)
                return;

            if (reloadTimer > 0f)
            {
                reloadTimer -= Time.deltaTime;
                if (reloadTimer <= 0f)
                    FinishReload();
            }

            zoomed = Mouse.current.rightButton.isPressed && reloadTimer <= 0f;
            float target = zoomed ? scopeFov : normalFov;
            fov = Mathf.Lerp(fov, target, Time.deltaTime * zoomSpeed);
            if (view != null)
                view.fieldOfView = fov;
            if (gun != null)
                gun.gameObject.SetActive(!zoomed || fov > scopeFov + 8f);

            if (Keyboard.current.rKey.wasPressedThisFrame)
                StartReload();

            if (Mouse.current.leftButton.wasPressedThisFrame)
                TryFire();
        }

        void TryFire()
        {
            if (reloadTimer > 0f || Time.time < nextShot)
                return;

            if (loaded <= 0)
            {
                StartReload();
                return;
            }

            nextShot = Time.time + fireCooldown;
            loaded--;
            if (player != null)
                player.AddRecoil(zoomed ? recoilKick * 0.45f : recoilKick);

            Vector3 origin = transform.position;
            Vector3 direction = SpreadDirection();
            DeerHuntFx.Muzzle(origin + transform.forward * 0.6f, direction);
            DeerHuntFx.PlayShot(origin);
            ShotFired?.Invoke(origin);

            if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
                ResolveHit(hit);
        }

        Vector3 SpreadDirection()
        {
            float degrees = zoomed ? zoomSpread : hipSpread;
            if (player != null && player.IsCrouching)
                degrees *= crouchSpreadScale;

            Vector2 kick = Random.insideUnitCircle * degrees;
            Quaternion spread = Quaternion.Euler(kick.y, kick.x, 0f);
            return spread * transform.forward;
        }

        void ResolveHit(RaycastHit hit)
        {
            HitZone zone = hit.collider.GetComponent<HitZone>();
            DeerController deer = hit.collider.GetComponentInParent<DeerController>();
            if (deer != null && zone != null)
            {
                Health health = deer.GetComponent<Health>();
                if (health == null || health.Dead)
                    return;
                // Attach the wound before TakeShot rotates the animal in reaction.
                DeerHuntFx.Blood(hit.point, hit.normal, deer.transform);
                deer.TakeShot(zone, hit.point, transform.position, damage);
                DeerHuntFx.PlayHit(hit.point);
                return;
            }

            DeerHuntFx.Dust(hit.point, hit.normal);
        }

        void StartReload()
        {
            if (reloadTimer > 0f || loaded >= magazineSize || reserve <= 0)
                return;

            reloadTimer = reloadSeconds;
            zoomed = false;
        }

        void FinishReload()
        {
            int need = magazineSize - loaded;
            int take = Mathf.Min(need, reserve);
            loaded += take;
            reserve -= take;
            reloadTimer = 0f;
        }

        void BuildGun()
        {
            gun = new GameObject("RifleView").transform;
            gun.SetParent(transform, false);
            gun.localPosition = new Vector3(0.22f, -0.22f, 0.55f);
            gun.localRotation = Quaternion.identity;

            Part(gun, new Vector3(0f, 0.02f, 0.15f), new Vector3(0.04f, 0.05f, 0.55f), new Color(0.15f, 0.16f, 0.17f));
            Part(gun, new Vector3(0f, -0.01f, -0.22f), new Vector3(0.04f, 0.07f, 0.28f), new Color(0.28f, 0.16f, 0.08f));
            Part(gun, new Vector3(0f, 0.07f, 0.05f), new Vector3(0.03f, 0.03f, 0.18f), new Color(0.1f, 0.11f, 0.12f));
        }

        static void Part(Transform parent, Vector3 localPos, Vector3 scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = scale;
            Renderer renderer = part.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = color;
            renderer.sharedMaterial = mat;
        }

        void OnGUI()
        {
            if (!zoomed || fov > normalFov - 4f)
                return;

            EnsureMask();
            float side = Mathf.Min(Screen.width, Screen.height) * 0.92f;
            float x = (Screen.width - side) * 0.5f;
            float y = (Screen.height - side) * 0.5f;
            Color prev = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, y), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, y + side, Screen.width, Screen.height - (y + side)), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, y, x, side), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + side, y, Screen.width - (x + side), side), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y, side, side), scopeMask);

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            GUI.color = new Color(0.85f, 0.9f, 0.85f, 1f);
            GUI.DrawTexture(new Rect(cx - 90f, cy - 1f, 70f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx + 20f, cy - 1f, 70f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 90f, 2f, 70f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1f, cy + 20f, 2f, 70f), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void EnsureMask()
        {
            if (scopeMask != null)
                return;

            const int n = 128;
            scopeMask = new Texture2D(n, n, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[n * n];
            Vector2 center = new Vector2(n * 0.5f, n * 0.5f);
            float radius = n * 0.46f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float edge = Mathf.InverseLerp(radius - 2f, radius + 8f, dist);
                    pixels[y * n + x] = new Color(0f, 0f, 0f, edge);
                }
            }

            scopeMask.SetPixels(pixels);
            scopeMask.Apply();
        }

        void OnDestroy()
        {
            if (scopeMask != null)
                Destroy(scopeMask);
        }
    }
}
