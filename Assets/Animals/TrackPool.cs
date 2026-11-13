using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace HuntingGame.Tracking
{
    public class TrackPool : MonoBehaviour
    {
        public const float RainFade = 2.5f;

        static readonly ProfilerMarker TickMarker = new ProfilerMarker("HuntingGame.Tracking");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        public static TrackPool Current { get; private set; }

        [SerializeField] TrackSettings settings;

        class Slot
        {
            public GameObject root;
            public Transform print;
            public Transform stick;
            public Renderer printRenderer;
            public Renderer stickRenderer;
            public MaterialPropertyBlock block;
            public TrackKind kind;
            public Vector3 baseScale;
            public Color baseColor;
            public float born;
            public float dieAt;
            public float life;
            public float aged;
            public float markedAt;
            public bool active;
        }

        Slot[] slots = System.Array.Empty<Slot>();
        int scan;
        int activeCount;
        float stepInterval = 0.55f;
        float minStepSqr = 0.72f;
        float branchInterval = 1.4f;
        float deerSize = 0.42f;
        float largeSize = 0.56f;
        float legendarySize = 0.78f;
        float deerLife = 75f;
        float largeLife = 90f;
        float legendaryLife = 120f;
        float bloodSize = 0.2f;
        float bloodLife = 34f;
        float branchLength = 0.48f;
        float branchLife = 46f;
        int bloodTrail = 5;
        Material mudMaterial;
        Material bloodMaterial;
        Material woodMaterial;
        bool ready;

        public float StepInterval => stepInterval;
        public float MinStepSqr => minStepSqr;
        public float BranchInterval => branchInterval;
        public float BloodSize => bloodSize;
        public float BranchLength => branchLength;
        public int BloodTrail => bloodTrail;
        public int ActiveCount => activeCount;
        public int MaxActive => slots.Length;
        public float SpacingSqr => minStepSqr;

        public bool TryFreshTrail(Vector3 from, float radius, float after, out Vector3 point, out float born)
        {
            point = from;
            born = after;
            float best = float.MinValue;
            foreach (Slot slot in slots)
            {
                if (slot == null || !slot.active || slot.born <= after || slot.dieAt <= Time.time) continue;
                float distance = Vector3.Distance(from, slot.root.transform.position);
                if (distance > radius || distance < 1.2f) continue;
                float score = slot.born - distance * 0.35f;
                if (score <= best) continue;
                best = score;
                point = slot.root.transform.position;
                born = slot.born;
            }
            return best > float.MinValue;
        }

        void Awake()
        {
            if (Current != null && Current != this)
            {
                enabled = false;
                return;
            }

            if (slots != null && slots.Length > 0)
                return;

            Current = this;
            if (settings == null)
                settings = ScriptableObject.CreateInstance<TrackSettings>();

            int count = settings.maxActive >= 8 ? settings.maxActive : settings.poolSize;
            if (count < 8)
                count = 8;

            stepInterval = settings.stepInterval < 0.15f ? 0.55f : settings.stepInterval;
            float minStep = settings.spacing < 0.4f ? 0.85f : settings.spacing;
            minStepSqr = minStep * minStep;
            branchInterval = settings.branchInterval < 0.3f ? 1.4f : settings.branchInterval;
            deerSize = settings.deerSize < 0.08f ? 0.42f : settings.deerSize;
            largeSize = settings.largeSize < 0.08f ? 0.56f : settings.largeSize;
            legendarySize = settings.legendarySize < 0.08f ? 0.78f : settings.legendarySize;
            deerLife = settings.footprintLifetime < 4f ? 75f : settings.footprintLifetime;
            largeLife = settings.largeLifetime < 4f ? 90f : settings.largeLifetime;
            legendaryLife = settings.legendaryLifetime < 4f ? 120f : settings.legendaryLifetime;
            bloodSize = settings.bloodSize < 0.06f ? 0.2f : settings.bloodSize;
            bloodLife = settings.bloodLifetime < 4f ? 34f : settings.bloodLifetime;
            branchLength = settings.branchLength < 0.1f ? 0.48f : settings.branchLength;
            branchLife = settings.branchLifetime < 4f ? 46f : settings.branchLifetime;
            bloodTrail = settings.bloodTrail < 1 ? 5 : settings.bloodTrail;

            mudMaterial = MakeFadeMaterial(new Color(0.27f, 0.18f, 0.1f, 0.95f));
            bloodMaterial = MakeFadeMaterial(new Color(0.46f, 0.05f, 0.04f, 0.95f));
            woodMaterial = MakeFadeMaterial(new Color(0.34f, 0.24f, 0.12f, 0.95f));
            ready = mudMaterial != null && bloodMaterial != null && woodMaterial != null;

            slots = new Slot[count];
            for (int i = 0; i < count; i++)
            {
                Slot slot = new Slot();
                slot.block = new MaterialPropertyBlock();
                slot.root = new GameObject("Track");
                slot.root.transform.SetParent(transform, false);

                slot.print = MakeQuad("Print", slot.root.transform, mudMaterial);
                slot.stick = MakeStick("Twig", slot.root.transform, woodMaterial);
                slot.printRenderer = slot.print.GetComponent<Renderer>();
                slot.stickRenderer = slot.stick.GetComponent<Renderer>();
                slot.print.gameObject.SetActive(false);
                slot.stick.gameObject.SetActive(false);
                slot.root.SetActive(false);
                slots[i] = slot;
            }
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;

            DestroyMaterial(mudMaterial);
            DestroyMaterial(bloodMaterial);
            DestroyMaterial(woodMaterial);
            mudMaterial = null;
            bloodMaterial = null;
            woodMaterial = null;
        }

        public void PrintFor(AnimalKind kind, out float size, out float life)
        {
            if (kind == AnimalKind.Legendary)
            {
                size = legendarySize;
                life = legendaryLife;
                return;
            }

            if (kind == AnimalKind.Large)
            {
                size = largeSize;
                life = largeLife;
                return;
            }

            size = deerSize;
            life = deerLife;
        }

        public void Drop(Vector3 position, float yawDegrees, bool legendary)
        {
            float size = legendary ? legendarySize : largeSize;
            float life = legendary ? legendaryLife : largeLife;
            Place(TrackKind.Footprint, position, yawDegrees, size, life);
        }

        public void Place(TrackKind kind, Vector3 position, float yawDegrees, float size, float lifetime)
        {
            Place(kind, position, yawDegrees, size, lifetime, Time.time);
        }

        public void Place(TrackKind kind, Vector3 position, float yawDegrees, float size, float lifetime, float now)
        {
            if (!ready || slots.Length == 0)
                return;

            if (size < 0.05f)
            {
                if (kind == TrackKind.Blood)
                    size = bloodSize;
                else if (kind == TrackKind.Branch)
                    size = branchLength;
                else
                    size = deerSize;
            }

            if (lifetime < 1f)
            {
                if (kind == TrackKind.Blood)
                    lifetime = bloodLife;
                else if (kind == TrackKind.Branch)
                    lifetime = branchLife;
                else
                    lifetime = deerLife;
            }

            Slot slot = TakeSlot();
            bool wasActive = slot.active;
            slot.kind = kind;
            slot.life = lifetime;
            slot.aged = 0f;
            slot.born = now;
            slot.markedAt = now;
            slot.dieAt = now + lifetime;
            slot.root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yawDegrees, 0f));

            bool stick = kind == TrackKind.Branch;
            slot.print.gameObject.SetActive(!stick);
            slot.stick.gameObject.SetActive(stick);
            if (!stick && slot.printRenderer != null)
                slot.printRenderer.sharedMaterial = kind == TrackKind.Blood ? bloodMaterial : mudMaterial;

            if (stick)
            {
                slot.baseColor = new Color(0.34f, 0.24f, 0.12f, 0.95f);
                slot.baseScale = new Vector3(size * 0.14f, size * 0.08f, size);
                slot.stick.localScale = slot.baseScale;
                slot.stick.localPosition = new Vector3(0f, 0.03f, 0f);
            }
            else if (kind == TrackKind.Blood)
            {
                slot.baseColor = new Color(0.46f, 0.05f, 0.04f, 0.95f);
                slot.baseScale = new Vector3(size * 0.85f, size, 1f);
                slot.print.localScale = slot.baseScale;
                slot.print.localPosition = Vector3.zero;
            }
            else
            {
                slot.baseColor = new Color(0.27f, 0.18f, 0.1f, 0.95f);
                slot.baseScale = new Vector3(size * 0.55f, size, 1f);
                slot.print.localScale = slot.baseScale;
                slot.print.localPosition = Vector3.zero;
            }

            slot.active = true;
            slot.root.SetActive(true);
            ApplyAge(slot, 0f);
            if (!wasActive)
                activeCount++;
        }

        void Update()
        {
            Tick(Time.time);
        }

        // Ages a few slots. Rain multiplies the time added so wet tracks die sooner.
        public void Tick(float now)
        {
            if (!ready || slots.Length == 0)
                return;

            TickMarker.Begin();
            float rate = 1f;
            WorldWeather weather = WorldWeather.Instance;
            if (weather != null && weather.Current == WorldWeather.Kind.Rain)
                rate = RainFade;

            int steps = 8 < slots.Length ? 8 : slots.Length;
            for (int i = 0; i < steps; i++)
            {
                if (scan >= slots.Length)
                    scan = 0;

                Slot slot = slots[scan];
                scan++;
                if (!slot.active)
                    continue;

                float dt = now - slot.markedAt;
                if (dt < 0f)
                    dt = 0f;
                slot.markedAt = now;
                slot.aged += dt * rate;

                float life01 = slot.life > 0.05f ? slot.aged / slot.life : 1f;
                if (life01 >= 1f)
                {
                    slot.active = false;
                    slot.root.SetActive(false);
                    activeCount--;
                    if (activeCount < 0)
                        activeCount = 0;
                    continue;
                }

                float remain = slot.life - slot.aged;
                if (remain < 0f)
                    remain = 0f;
                slot.dieAt = now + remain / rate;
                ApplyAge(slot, life01);
            }

            TickMarker.End();
        }

        static void ApplyAge(Slot slot, float life01)
        {
            if (life01 < 0f)
                life01 = 0f;
            if (life01 > 1f)
                life01 = 1f;

            // Stay readable, then fade and shrink through the rest of the life.
            float fade = life01 <= 0.4f ? 1f : 1f - (life01 - 0.4f) / 0.6f;
            float dull = 1f - life01 * 0.28f;
            Color color = slot.baseColor;
            color.r *= dull;
            color.g *= dull;
            color.b *= dull;
            color.a *= fade;

            Transform visual = slot.kind == TrackKind.Branch ? slot.stick : slot.print;
            visual.localScale = slot.baseScale * Mathf.Lerp(1f, 0.68f, life01);

            Renderer renderer = slot.kind == TrackKind.Branch ? slot.stickRenderer : slot.printRenderer;
            slot.block.SetColor(BaseColorId, color);
            slot.block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(slot.block);
        }

        Slot TakeSlot()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].active)
                    return slots[i];
            }

            int oldest = 0;
            float oldestDie = slots[0].dieAt;
            for (int i = 1; i < slots.Length; i++)
            {
                if (slots[i].dieAt >= oldestDie)
                    continue;

                oldestDie = slots[i].dieAt;
                oldest = i;
            }

            return slots[oldest];
        }

        static Transform MakeQuad(string name, Transform parent, Material material)
        {
            GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mark.name = name;
            mark.transform.SetParent(parent, false);
            mark.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            StripCollider(mark);
            Renderer renderer = mark.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (material != null)
                    renderer.sharedMaterial = material;
            }

            return mark.transform;
        }

        static Transform MakeStick(string name, Transform parent, Material material)
        {
            GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mark.name = name;
            mark.transform.SetParent(parent, false);
            StripCollider(mark);
            Renderer renderer = mark.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (material != null)
                    renderer.sharedMaterial = material;
            }

            return mark.transform;
        }

        static void StripCollider(GameObject mark)
        {
            Collider solid = mark.GetComponent<Collider>();
            if (solid == null)
                return;

            if (Application.isPlaying)
                Destroy(solid);
            else
                DestroyImmediate(solid);
        }

        static void DestroyMaterial(Material material)
        {
            if (material == null)
                return;

            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }

        static Material MakeFadeMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                return null;

            Material material = new Material(shader);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite"))
                material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }
    }
}
