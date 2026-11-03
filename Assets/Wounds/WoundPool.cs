using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace HuntingGame.Wounds
{
    public class WoundPool : MonoBehaviour, IWoundLog
    {
        static readonly ProfilerMarker TickMarker = new ProfilerMarker("HuntingGame.Wounds");

        public static WoundPool Current { get; private set; }

        [SerializeField] WoundSettings settings;

        class Slot
        {
            public GameObject root;
            public float dieAt;
            public bool active;
        }

        Slot[] slots = System.Array.Empty<Slot>();
        int cursor;
        int scan;
        float life = 40f;
        float size = 0.55f;
        float heavySize = 0.9f;
        bool ready;

        void Awake()
        {
            if (Current != null && Current != this)
            {
                enabled = false;
                return;
            }

            Current = this;
            if (settings == null)
                settings = ScriptableObject.CreateInstance<WoundSettings>();

            int count = settings.poolSize < 8 ? 8 : settings.poolSize;
            life = settings.lifetime < 4f ? 4f : settings.lifetime;
            size = settings.size < 0.15f ? 0.15f : settings.size;
            heavySize = settings.heavySize < size ? size : settings.heavySize;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            Material material = null;
            if (shader != null)
            {
                material = new Material(shader);
                Color blood = new Color(0.42f, 0.05f, 0.04f, 1f);
                material.color = blood;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", blood);
            }

            slots = new Slot[count];
            for (int i = 0; i < count; i++)
            {
                GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mark.name = "Wound";
                mark.transform.SetParent(transform, false);
                Collider solid = mark.GetComponent<Collider>();
                if (solid != null)
                    Destroy(solid);

                Renderer renderer = mark.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    if (material != null)
                        renderer.sharedMaterial = material;
                }

                mark.SetActive(false);
                slots[i] = new Slot { root = mark };
            }

            ready = material != null;
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        public void Mark(Vector3 point, bool heavy)
        {
            if (!ready || slots.Length == 0)
                return;

            Slot slot = slots[cursor];
            cursor++;
            if (cursor >= slots.Length)
                cursor = 0;

            Vector3 ground = WorldGround.Snap(point);
            ground.y += 0.04f;
            float yaw = Mathf.Atan2(point.x, point.z) * Mathf.Rad2Deg;
            float span = heavy ? heavySize : size;

            slot.root.transform.SetPositionAndRotation(ground, Quaternion.Euler(90f, yaw, 0f));
            slot.root.transform.localScale = new Vector3(span * 0.72f, span, 1f);
            slot.dieAt = Time.time + life;
            slot.active = true;
            slot.root.SetActive(true);
        }

        void Update()
        {
            if (!ready)
                return;

            TickMarker.Begin();
            int steps = 8 < slots.Length ? 8 : slots.Length;
            float now = Time.time;
            for (int i = 0; i < steps; i++)
            {
                if (scan >= slots.Length)
                    scan = 0;

                Slot slot = slots[scan];
                if (slot.active && now >= slot.dieAt)
                {
                    slot.active = false;
                    slot.root.SetActive(false);
                }

                scan++;
            }

            TickMarker.End();
        }
    }
}
