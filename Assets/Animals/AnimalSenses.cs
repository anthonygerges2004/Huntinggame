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
        RaycastHit[] sightHits;
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
        float nextTrailSmell;

        public float Interval => interval;
        public float Threat { get; private set; }

        const float AlertOn = 0.28f;
        const float AlertOff = 0.16f;
        const float FleeOn = 0.58f;
        const float FleeOff = 0.40f;

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
            sightHits = new RaycastHit[8];

            if (settings == null)
                settings = SenseCatalog.For(animal);

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
            if (animal.kind == AnimalKind.Deer)
                TickDeer(scaleSqr);
            else
            {
                Smell(scaleSqr);
                SmellOldTrail(scaleSqr);
                HearFootsteps(scaleSqr);
                LookForPlayer(scaleSqr);
            }

            TickMarker.End();
        }

        int deerReaction;
        float deerAlarm;
        float trailWatchUntil;

        void TickDeer(float scaleSqr)
        {
            if (settings == null)
                return;

            float scale = Mathf.Sqrt(scaleSqr);
            Vector3 wind = FlatWind();
            float carry = WindSystem.Current != null
                ? Mathf.Lerp(0.55f, 1.25f, Mathf.Clamp01(WindSystem.Current.Strength))
                : 1f;

            Vector3 toPlayer = player.position - body.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            float hearRange = 0f;
            float noise = 0f;
            if (PlayerMove.IsMoving)
            {
                float surface = PlayerMove.SurfaceNoise < 0.05f ? 0.05f : PlayerMove.SurfaceNoise;
                if (PlayerMove.IsSprinting)
                {
                    hearRange = settings.sprintHearRange;
                    noise = 1f;
                }
                else if (PlayerMove.IsCrouching)
                {
                    hearRange = settings.crouchHearRange;
                    noise = 0.15f;
                }
                else
                {
                    hearRange = settings.walkHearRange;
                    noise = 0.5f;
                }

                hearRange *= surface * scale;
            }

            bool blocked = true;
            float viewDot = -1f;
            float viewClear = 0f;
            if (distance < 0.5f)
            {
                viewDot = 1f;
                blocked = false;
                viewClear = 1f;
            }
            else if (distance <= sightRange * scale * 1.05f && toPlayer.sqrMagnitude > 0.0001f)
            {
                Vector3 forward = body.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.0001f)
                    viewDot = Vector3.Dot(forward.normalized, toPlayer / distance);
                viewClear = BodyVisible();
                blocked = viewClear <= 0.001f;
            }

            DeerThreatFuzzy.Sample sample = new DeerThreatFuzzy.Sample
            {
                distance = distance,
                smellRange = smellRange * carry * scale,
                hearRange = hearRange,
                sightRange = sightRange * scale,
                windAlignment = 0f,
                viewDot = viewDot,
                viewEdgeDot = minDot,
                viewBlocked = blocked,
                viewClear = viewClear,
                noise = noise,
                timeOfDay = DayNightCycle.Instance != null ? DayNightCycle.Instance.TimeOfDayNormalized : -1f
            };

            if (distance > 0.05f && wind.sqrMagnitude > 0.0001f)
            {
                Vector3 towardDeer = body.position - player.position;
                towardDeer.y = 0f;
                sample.windAlignment = Vector3.Dot(towardDeer.normalized, wind);
            }

            float threat = DeerThreatFuzzy.Evaluate(sample);
            Vector3 point = player.position;
            // An old trail only matters when the hunter himself is not the stronger cue.
            // Otherwise the deer would stare at a footprint while you were the real threat.
            if (threat < 0.36f && TryTrailScent(scaleSqr, wind, out Vector3 scent))
            {
                threat = 0.36f;
                point = scent;
            }

            float sensed = threat;
            // A strong cue sets the alarm at once. After the hunter slips out of sight
            // it falls off, instead of dropping to calm on the same tick.
            if (Application.isPlaying)
                deerAlarm = Mathf.Max(sensed, deerAlarm - 0.45f * Time.deltaTime);
            else
                deerAlarm = sensed;

            Threat = sensed;
            ApplyDeerReaction(deerAlarm, point);
        }

        void ApplyDeerReaction(float threat, Vector3 point)
        {
            Threat = threat;
            if (deerReaction == 2)
            {
                if (threat < FleeOff)
                    deerReaction = threat >= AlertOff ? 1 : 0;
            }
            else if (deerReaction == 1)
            {
                if (threat >= FleeOn)
                    deerReaction = 2;
                else if (threat < AlertOff)
                    deerReaction = 0;
            }
            else if (threat >= FleeOn)
                deerReaction = 2;
            else if (threat >= AlertOn)
                deerReaction = 1;

            if (deerReaction == 0)
                return;

            if (brain == null)
                brain = GetComponent<AnimalBrain>();

            if (brain != null && brain.isActiveAndEnabled)
            {
                // A mild score must not keep refreshing a run that is already underway.
                if (deerReaction == 1 && brain.State == AnimalAiState.Flee)
                    return;

                brain.ReactToThreat(point, threat, deerReaction == 2);
                return;
            }

            if (deerReaction == 2)
                animal.FleeFrom(point);
        }

        Vector3 trailPoint;

        bool TryTrailScent(float scaleSqr, Vector3 wind, out Vector3 scent)
        {
            if (Time.time < trailWatchUntil)
            {
                scent = trailPoint;
                return true;
            }

            scent = body.position;
            if (smellSqr <= 0f || PlayerScentTrail.Instance == null || wind.sqrMagnitude < 0.0001f)
                return false;
            if (animal.Fleeing)
                return false;

            float carry = WindSystem.Current != null
                ? Mathf.Lerp(0.55f, 1.25f, Mathf.Clamp01(WindSystem.Current.Strength))
                : 1f;
            float range = Mathf.Sqrt(smellSqr * carry * carry * scaleSqr);
            if (!PlayerScentTrail.TryFindInvestigationPoint(
                    body.position, wind, smellDot, range * 0.65f, 50f, 110f, out scent))
                return false;

            trailPoint = scent;
            trailWatchUntil = Time.time + 4f;
            nextTrailSmell = Time.time + 8f;
            return true;
        }

        static Vector3 FlatWind()
        {
            Vector3 wind = WindSystem.Current != null ? WindSystem.Current.Direction : Vector3.forward;
            wind.y = 0f;
            if (wind.sqrMagnitude < 0.0001f)
                return Vector3.zero;
            return wind.normalized;
        }

        float BodyVisible()
        {
            Vector3 origin = body.position + Vector3.up * 1.2f;
            float head = LineOpen(origin, player.position + Vector3.up * 1.6f);
            float chest = LineOpen(origin, player.position + Vector3.up * 1.05f);
            float hips = LineOpen(origin, player.position + Vector3.up * 0.45f);
            return head * 0.5f + chest * 0.35f + hips * 0.15f;
        }

        float LineOpen(Vector3 origin, Vector3 target)
        {
            Vector3 direction = target - origin;
            float distance = direction.magnitude;
            if (distance < 0.5f)
                return 1f;

            direction /= distance;
            Vector3 start = origin + direction * 0.8f;
            float range = distance - 0.8f;
            if (range < 0.2f)
                return 1f;

            int hitCount = Physics.RaycastNonAlloc(
                start,
                direction,
                sightHits,
                range,
                blocks,
                QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            Transform nearestHit = null;
            for (int i = 0; i < hitCount; i++)
            {
                Transform hit = sightHits[i].transform;
                if (hit == null)
                    continue;
                if (hit == body || hit.IsChildOf(body))
                    continue;
                if (sightHits[i].distance >= nearest)
                    continue;
                nearest = sightHits[i].distance;
                nearestHit = hit;
            }

            if (nearestHit == null)
                return 1f;
            return nearestHit == player || nearestHit.IsChildOf(player) ? 1f : 0f;
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

        void SmellOldTrail(float scaleSqr)
        {
            if (smellSqr <= 0f || PlayerScentTrail.Instance == null)
                return;
            if (animal.kind != AnimalKind.Deer)
                return;
            if (animal.Fleeing || Time.time < nextTrailSmell)
                return;

            Vector3 wind = WindSystem.Current != null ? WindSystem.Current.Direction : Vector3.forward;
            wind.y = 0f;
            if (wind.sqrMagnitude < 0.0001f)
                return;
            wind.Normalize();

            float carry = WindSystem.Current != null
                ? Mathf.Lerp(0.55f, 1.25f, Mathf.Clamp01(WindSystem.Current.Strength))
                : 1f;
            float range = Mathf.Sqrt(smellSqr * carry * carry * scaleSqr);

            if (!PlayerScentTrail.TryFindInvestigationPoint(
                    body.position, wind, smellDot, range * 0.65f, 50f, 110f, out Vector3 scent))
                return;

            nextTrailSmell = Time.time + 8f;
            animal.NoticeThreat(scent);
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
