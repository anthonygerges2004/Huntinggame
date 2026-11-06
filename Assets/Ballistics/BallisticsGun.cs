using HuntingGame.Wounds;
using Unity.Profiling;
using UnityEngine;

namespace HuntingGame.Ballistics
{
    public class BallisticsGun : MonoBehaviour, IBallistics
    {
        static readonly ProfilerMarker FireMarker = new ProfilerMarker("HuntingGame.Ballistics");

        public static BallisticsGun Current { get; private set; }

        [SerializeField] BallisticsSettings settings;
        [SerializeField] LayerMask blocks = ~0;

        struct PelletHit
        {
            public Animal animal;
        }

        class Pellet
        {
            public bool active;
            public Vector3 position;
            public Vector3 velocity;
            public float traveled;
            public float range;
            public Transform ignore;
            public Transform visual;
        }

        PelletHit[] found;
        Pellet[] pellets;
        int foundCount;
        int activeCount;
        int poolCount;
        float muzzleSpeed = 360f;
        float gravity = 9.81f;
        float drag = 0.65f;
        float pelletRadius = 0.02f;
        Material leadMaterial;

        void Awake()
        {
            if (Current != null && Current != this)
            {
                enabled = false;
                return;
            }

            Current = this;
            if (settings == null)
                settings = ScriptableObject.CreateInstance<BallisticsSettings>();

            int count = settings.maxHits < 8 ? 8 : settings.maxHits;
            found = new PelletHit[count];
            muzzleSpeed = settings.muzzleSpeed < 40f ? 360f : settings.muzzleSpeed;
            gravity = settings.gravity < 0f ? 9.81f : settings.gravity;
            drag = settings.drag < 0f ? 0f : settings.drag;
            pelletRadius = settings.pelletRadius < 0.005f ? 0.02f : settings.pelletRadius;
            if (blocks.value == 0)
                blocks = ~0;

            poolCount = 32;
            pellets = new Pellet[poolCount];
            leadMaterial = MakeLeadMaterial();
            for (int i = 0; i < poolCount; i++)
            {
                Pellet pellet = new Pellet();
                pellet.visual = MakePellet(i);
                pellets[i] = pellet;
            }
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;

            if (leadMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(leadMaterial);
            else
                DestroyImmediate(leadMaterial);
            leadMaterial = null;
        }

        void Update()
        {
            if (activeCount == 0)
                return;

            FireMarker.Begin();
            StepPellets(Time.deltaTime);
            FireMarker.End();
        }

        public void Fire(
            Vector3 origin,
            Vector3 forward,
            Vector3 up,
            Vector3 right,
            float spread,
            float range,
            int pelletsToFire,
            Transform ignore)
        {
            FireMarker.Begin();
            foundCount = 0;
            int count = pelletsToFire < 1 ? 1 : pelletsToFire;
            if (count > poolCount)
                count = poolCount;
            if (range < 2f)
                range = 2f;

            for (int i = 0; i < count; i++)
            {
                Vector3 direction = forward;
                direction += up * Random.Range(-spread, spread) / 100f;
                direction += right * Random.Range(-spread, spread) / 100f;
                if (direction.sqrMagnitude < 0.0001f)
                    continue;

                direction.Normalize();
                Pellet pellet = TakePellet();
                pellet.active = true;
                pellet.position = origin + direction * 0.55f;
                pellet.velocity = direction * muzzleSpeed;
                pellet.traveled = 0f;
                pellet.range = range;
                pellet.ignore = ignore;
                pellet.visual.position = pellet.position;
                pellet.visual.gameObject.SetActive(true);
                activeCount++;
            }

            FireMarker.End();
        }

        void StepPellets(float dt)
        {
            if (dt < 0.0001f)
                return;
            if (dt > 0.05f)
                dt = 0.05f;

            float damp = drag * dt;
            if (damp > 0.5f)
                damp = 0.5f;

            for (int i = 0; i < poolCount; i++)
            {
                Pellet pellet = pellets[i];
                if (!pellet.active)
                    continue;

                pellet.velocity -= pellet.velocity * damp;
                pellet.velocity.y -= gravity * dt;

                Vector3 step = pellet.velocity * dt;
                float distance = step.magnitude;
                if (distance < 0.0001f)
                {
                    Stop(pellet);
                    continue;
                }

                float remain = pellet.range - pellet.traveled;
                if (distance > remain)
                    distance = remain;

                Vector3 direction = step / step.magnitude;
                if (Cast(pellet.position, direction, distance, pellet.ignore, out RaycastHit hit))
                {
                    Debug.DrawLine(pellet.position, hit.point, Color.green, 0.35f);
                    Resolve(hit, direction, pellet.velocity.magnitude);
                    Stop(pellet);
                    continue;
                }

                pellet.position += direction * distance;
                pellet.traveled += distance;
                pellet.visual.position = pellet.position;
                Debug.DrawLine(pellet.position - direction * distance, pellet.position, Color.yellow, 0.2f);

                if (pellet.traveled >= pellet.range - 0.05f)
                    Stop(pellet);
            }
        }

        void Resolve(RaycastHit hit, Vector3 direction, float speed)
        {
            bool heavy = false;
            if (hit.collider.GetComponentInParent<HarvestableTree>() == null)
            {
                Animal animal = hit.collider.GetComponentInParent<Animal>();
                if (animal != null && !animal.Dead && !AlreadyHit(animal) && foundCount < found.Length)
                {
                    found[foundCount].animal = animal;
                    foundCount++;
                    heavy = animal.kind == AnimalKind.Large || animal.kind == AnimalKind.Legendary;
                    animal.Hit(direction, hit.point, hit.normal);
                    PushBody(animal, direction, speed);
                }
            }

            if (WoundPool.Current != null)
                WoundPool.Current.Mark(hit.point, heavy);
        }

        static void PushBody(Animal animal, Vector3 direction, float speed)
        {
            if (animal == null)
                return;

            Rigidbody body = animal.GetComponent<Rigidbody>();
            if (body == null || body.isKinematic)
                return;

            float kick = speed * 0.02f;
            if (kick < 2.5f)
                kick = 2.5f;
            if (kick > 9f)
                kick = 9f;
            body.linearVelocity = direction * kick + Vector3.down * 1.4f;
            body.angularVelocity = Vector3.Cross(Vector3.up, direction) * 8f;
        }

        bool Cast(Vector3 start, Vector3 direction, float distance, Transform ignore, out RaycastHit hit)
        {
            if (!Physics.SphereCast(start, pelletRadius, direction, out hit, distance, blocks, QueryTriggerInteraction.Ignore))
                return false;

            if (ignore == null || hit.transform == null)
                return true;

            if (hit.transform != ignore && !hit.transform.IsChildOf(ignore))
                return true;

            Vector3 next = hit.point + direction * 0.35f;
            float left = distance - hit.distance - 0.35f;
            if (left < 0.2f)
                return false;

            if (!Physics.SphereCast(next, pelletRadius, direction, out hit, left, blocks, QueryTriggerInteraction.Ignore))
                return false;

            return hit.transform != ignore && !hit.transform.IsChildOf(ignore);
        }

        bool AlreadyHit(Animal animal)
        {
            for (int i = 0; i < foundCount; i++)
            {
                if (found[i].animal == animal)
                    return true;
            }

            return false;
        }

        Pellet TakePellet()
        {
            for (int i = 0; i < poolCount; i++)
            {
                if (!pellets[i].active)
                    return pellets[i];
            }

            Stop(pellets[0]);
            return pellets[0];
        }

        void Stop(Pellet pellet)
        {
            if (!pellet.active)
                return;

            pellet.active = false;
            pellet.visual.gameObject.SetActive(false);
            activeCount--;
            if (activeCount < 0)
                activeCount = 0;
        }

        Transform MakePellet(int index)
        {
            GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mark.name = "Lead" + index;
            mark.transform.SetParent(transform, false);
            mark.transform.localScale = Vector3.one * 0.045f;
            Collider solid = mark.GetComponent<Collider>();
            if (solid != null)
                Destroy(solid);

            Renderer renderer = mark.GetComponent<Renderer>();
            if (renderer != null && leadMaterial != null)
            {
                renderer.sharedMaterial = leadMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            mark.SetActive(false);
            return mark.transform;
        }

        static Material MakeLeadMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                return null;

            Color lead = new Color(0.12f, 0.12f, 0.13f, 1f);
            Material material = new Material(shader);
            material.color = lead;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", lead);
            return material;
        }
    }
}
