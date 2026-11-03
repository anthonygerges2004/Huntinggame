using HuntingGame.Animals;
using HuntingGame.Wind;
using Unity.Profiling;
using UnityEngine;

namespace HuntingGame.Senses
{
    [RequireComponent(typeof(Animal))]
    public class AnimalSenses : MonoBehaviour, IAnimalSenses
    {
        static readonly ProfilerMarker TickMarker = new ProfilerMarker("HuntingGame.Senses");

        [SerializeField] AnimalSenseSettings settings;
        [SerializeField] LayerMask blocks = ~0;

        Animal animal;
        AnimalBrain brain;
        Transform body;
        Transform player;
        RaycastHit[] hits;
        float interval;
        float sightRange = 32f;
        float sightDegrees = 80f;
        float hearWalk = 8f;
        float smellRange = 26f;
        float smellDegrees = 70f;
        float sightSqr;
        float sprintSqr;
        float walkSqr;
        float crouchSqr;
        float smellSqr;
        float reducedScaleSqr;
        float minDot;
        float smellDot;
        bool speciesApplied;

        public float Interval => interval;

        public void SetWatch(Transform target)
        {
            player = target;
        }

        void Awake()
        {
            animal = GetComponent<Animal>();
            brain = GetComponent<AnimalBrain>();
            body = transform;
            hits = new RaycastHit[1];

            if (settings == null)
                settings = SenseCatalog.For(animal != null ? animal.kind : AnimalKind.Deer);

            CopyRanges();
        }

        void CopyRanges()
        {
            interval = settings.tickInterval < 0.05f ? 0.05f : settings.tickInterval;
            float scale = settings.reducedRangeScale < 0.1f ? 0.1f : settings.reducedRangeScale;
            reducedScaleSqr = scale * scale;
            sightRange = settings.sightRange < 1f ? 1f : settings.sightRange;
            sightDegrees = settings.sightDegrees < 10f ? 10f : settings.sightDegrees;
            hearWalk = settings.walkHearRange;
            smellRange = settings.smellRange < 0f ? 0f : settings.smellRange;
            smellDegrees = settings.smellDegrees < 10f ? 10f : settings.smellDegrees;
            sightSqr = sightRange * sightRange;
            sprintSqr = settings.sprintHearRange * settings.sprintHearRange;
            walkSqr = hearWalk * hearWalk;
            crouchSqr = settings.crouchHearRange * settings.crouchHearRange;
            smellSqr = smellRange * smellRange;
            minDot = Mathf.Cos(sightDegrees * 0.5f * Mathf.Deg2Rad);
            smellDot = Mathf.Cos(smellDegrees * 0.5f * Mathf.Deg2Rad);
        }

        void ApplySpeciesSenses()
        {
            if (speciesApplied)
                return;

            if (brain == null)
                return;

            speciesApplied = true;
            brain.Bind();
            AnimalSenseSettings speciesSenses = brain.Species != null ? brain.Species.senses : null;
            if (speciesSenses == null || speciesSenses == settings)
                return;

            settings = speciesSenses;
            CopyRanges();
        }

        void Start()
        {
            if (player == null && PlayerHealth.Instance != null)
                player = PlayerHealth.Instance.transform;
        }

        public void Tick(AnimalAiBand band)
        {
            ApplySpeciesSenses();
            if (band == AnimalAiBand.Dormant || player == null || animal == null || animal.Dead)
                return;

            TickMarker.Begin();
            float scaleSqr = band == AnimalAiBand.Reduced ? reducedScaleSqr : 1f;
            Smell(scaleSqr);
            HearFootsteps(scaleSqr);
            LookForPlayer(scaleSqr);
            TickMarker.End();
        }

        void Smell(float scaleSqr)
        {
            if (smellSqr <= 0f)
                return;

            Vector3 wind = WindSystem.Current != null ? WindSystem.Current.Direction : Vector3.forward;
            wind.y = 0f;
            if (wind.sqrMagnitude < 0.0001f)
                return;
            wind.Normalize();

            float carry = WindSystem.Current != null
                ? Mathf.Lerp(0.55f, 1.25f, Mathf.Clamp01(WindSystem.Current.Strength))
                : 1f;
            float limit = smellSqr * carry * carry * scaleSqr;

            Vector3 toAnimal = body.position - player.position;
            toAnimal.y = 0f;
            float distanceSqr = toAnimal.sqrMagnitude;
            if (distanceSqr > limit || distanceSqr < 0.25f)
                return;

            float distance = Mathf.Sqrt(distanceSqr);
            if (Vector3.Dot(toAnimal / distance, wind) < smellDot)
                return;

            animal.NoticeThreat(player.position);
        }

        void HearFootsteps(float scaleSqr)
        {
            float limit = 0f;
            if (PlayerMove.IsSprinting)
                limit = sprintSqr;
            else if (PlayerMove.IsCrouching && PlayerMove.IsMoving)
                limit = crouchSqr;
            else if (PlayerMove.IsMoving)
                limit = walkSqr;

            if (limit <= 0f)
                return;

            float surface = PlayerMove.SurfaceNoise;
            if (surface < 0.05f)
                surface = 0.05f;
            limit *= surface * surface * scaleSqr;

            Vector3 offset = body.position - player.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= limit)
                animal.NoticeThreat(player.position);
        }

        void LookForPlayer(float scaleSqr)
        {
            Vector3 toPlayer = player.position - body.position;
            toPlayer.y = 0f;
            float distanceSqr = toPlayer.sqrMagnitude;
            if (distanceSqr > sightSqr * scaleSqr || distanceSqr < 0.25f)
                return;

            Vector3 forward = body.forward;
            forward.y = 0f;
            float forwardSqr = forward.sqrMagnitude;
            if (forwardSqr < 0.0001f)
                return;

            float dot = Vector3.Dot(forward / Mathf.Sqrt(forwardSqr), toPlayer / Mathf.Sqrt(distanceSqr));
            if (dot < minDot)
                return;

            Vector3 direction = player.position + Vector3.up * 1.1f - (body.position + Vector3.up * 1.2f);
            float distance = direction.magnitude;
            if (distance < 0.5f)
            {
                animal.NoticeThreat(player.position);
                return;
            }

            direction /= distance;
            // Start outside the animal's own capsule so the ray is not blocked by itself.
            Vector3 origin = body.position + Vector3.up * 1.2f + direction * 0.8f;
            float range = distance - 0.8f;
            if (range < 0.2f)
            {
                animal.NoticeThreat(player.position);
                return;
            }

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                hits,
                range,
                blocks,
                QueryTriggerInteraction.Ignore);

            if (hitCount == 0)
            {
                animal.NoticeThreat(player.position);
                return;
            }

            Transform hit = hits[0].transform;
            if (hit == player || hit.IsChildOf(player))
                animal.NoticeThreat(player.position);
        }

        void OnDrawGizmosSelected()
        {
            Vector3 eye = transform.position + Vector3.up * 1.2f;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;

            Gizmos.color = new Color(1f, 0.82f, 0.2f, 0.95f);
            DrawWedge(eye, forward, sightDegrees, sightRange);

            Gizmos.color = new Color(0.35f, 0.55f, 1f, 0.85f);
            Gizmos.DrawWireSphere(transform.position, hearWalk);

            Vector3 wind = WindSystem.Current != null ? WindSystem.Current.Direction : Vector3.forward;
            wind.y = 0f;
            if (wind.sqrMagnitude < 0.0001f)
                wind = Vector3.forward;
            Gizmos.color = new Color(0.35f, 0.9f, 0.45f, 0.95f);
            DrawWedge(transform.position + Vector3.up * 0.4f, -wind, smellDegrees, smellRange);
        }

        static void DrawWedge(Vector3 origin, Vector3 forward, float degrees, float radius)
        {
            if (radius <= 0.1f || forward.sqrMagnitude < 0.0001f)
                return;

            forward.Normalize();
            Quaternion swing = Quaternion.AngleAxis(degrees * 0.5f, Vector3.up);
            Vector3 left = swing * forward;
            Vector3 right = Quaternion.Inverse(swing) * forward;
            Gizmos.DrawRay(origin, left * radius);
            Gizmos.DrawRay(origin, forward * radius);
            Gizmos.DrawRay(origin, right * radius);
            Gizmos.DrawLine(origin + left * radius, origin + forward * radius);
            Gizmos.DrawLine(origin + forward * radius, origin + right * radius);
        }
    }
}
