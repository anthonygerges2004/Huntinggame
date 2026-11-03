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

        PelletHit[] found;
        int foundCount;

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
            if (blocks.value == 0)
                blocks = ~0;
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        public void Fire(
            Vector3 origin,
            Vector3 forward,
            Vector3 up,
            Vector3 right,
            float spread,
            float range,
            int pellets,
            Transform ignore)
        {
            FireMarker.Begin();
            Shoot(origin, forward, up, right, spread, range, pellets, ignore);
            FireMarker.End();
        }

        void Shoot(
            Vector3 origin,
            Vector3 forward,
            Vector3 up,
            Vector3 right,
            float spread,
            float range,
            int pellets,
            Transform ignore)
        {
            foundCount = 0;
            int count = pellets < 1 ? 1 : pellets;
            if (count > 32)
                count = 32;

            for (int i = 0; i < count; i++)
            {
                Vector3 direction = forward;
                direction += up * Random.Range(-spread, spread) / 100f;
                direction += right * Random.Range(-spread, spread) / 100f;
                if (direction.sqrMagnitude < 0.0001f)
                    continue;

                direction.Normalize();
                Vector3 start = origin + direction * 0.4f;
                float distance = range - 0.4f;
                if (distance < 1f)
                    distance = 1f;

                if (!Cast(start, direction, distance, ignore, out RaycastHit hit))
                {
                    Debug.DrawRay(start, direction * distance, Color.red, 1.5f);
                    continue;
                }

                Debug.DrawLine(start, hit.point, Color.green, 1.5f);

                bool heavy = false;
                if (hit.collider.GetComponentInParent<HarvestableTree>() == null)
                {
                    Animal animal = hit.collider.GetComponentInParent<Animal>();
                    if (animal != null && !AlreadyHit(animal) && foundCount < found.Length)
                    {
                        found[foundCount].animal = animal;
                        foundCount++;
                        heavy = animal.kind == AnimalKind.Large || animal.kind == AnimalKind.Legendary;
                    }
                }

                if (WoundPool.Current != null)
                    WoundPool.Current.Mark(hit.point, heavy);
            }

            for (int i = 0; i < foundCount; i++)
            {
                if (found[i].animal != null)
                    found[i].animal.Hit();
            }
        }

        bool Cast(Vector3 start, Vector3 direction, float distance, Transform ignore, out RaycastHit hit)
        {
            if (!Physics.Raycast(start, direction, out hit, distance, blocks, QueryTriggerInteraction.Ignore))
                return false;

            if (ignore == null || hit.transform == null)
                return true;

            if (hit.transform != ignore && !hit.transform.IsChildOf(ignore))
                return true;

            Vector3 next = hit.point + direction * 0.25f;
            float left = distance - hit.distance - 0.25f;
            if (left < 0.2f)
                return false;

            if (!Physics.Raycast(next, direction, out hit, left, blocks, QueryTriggerInteraction.Ignore))
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
    }
}
