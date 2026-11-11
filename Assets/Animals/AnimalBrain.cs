using HuntingGame.Wind;
using Unity.Profiling;
using UnityEngine;

namespace HuntingGame.Animals
{
    [RequireComponent(typeof(Animal))]
    public class AnimalBrain : MonoBehaviour, IAnimalBrain
    {
        static readonly ProfilerMarker TickMarker = new ProfilerMarker("HuntingGame.AnimalBrain");

        [SerializeField] SpeciesData speciesOverride;

        Animal animal;
        SpeciesData species;
        AnimalAiState state = AnimalAiState.Idle;
        Vector3 grazePoint;
        Vector3 lastThreat;
        Vector3 heading = Vector3.forward;
        float timer = 1f;
        float lastNotice = -1000f;
        float gradedFleeSeconds;
        bool gradedFlight;
        float calmScale = 1f;
        bool hasThreat;
        bool homing;
        bool bound;
        bool alertHolds;

        public AnimalAiState State => state;
        public SpeciesData Species => species;

        void Awake()
        {
            animal = GetComponent<Animal>();
        }

        public void Bind()
        {
            if (animal == null)
                animal = GetComponent<Animal>();
            if (animal == null)
                return;

            if (speciesOverride != null)
                species = speciesOverride;
            else
                species = SpeciesCatalog.For(animal);

            if (species.grazeSpeed > 0f)
                animal.moveSpeed = species.grazeSpeed;
            if (species.fleeSpeed > 0f)
                animal.fleeSpeed = species.fleeSpeed;
            if (species.fleeSeconds > 0f)
                animal.fleeDuration = species.fleeSeconds;

            if (bound)
                return;

            bound = true;
            state = AnimalAiState.Idle;
            float span = species.idleSeconds > 0.05f ? species.idleSeconds : 1f;
            timer = Random.Range(0f, span);
            heading = transform.forward;
        }

        /// <summary>
        /// Deer fuzzy threat. Flee is urgent and scales speed and how long they stay gone.
        /// A middling score only makes them watch, and they relax if it drops.
        /// </summary>
        public void ReactToThreat(Vector3 threat, float level, bool flee)
        {
            if (animal == null || animal.Dead || state == AnimalAiState.Dead)
                return;
            if (species == null)
                Bind();
            if (species == null)
                return;

            if (flee)
            {
                gradedFlight = true;
                float t = Mathf.InverseLerp(0.58f, 1f, Mathf.Clamp01(level));
                // A mild flush is a short bound. A hard scare is a longer, faster run.
                gradedFleeSeconds = Mathf.Lerp(3.2f, 7.5f, t);
                calmScale = Mathf.Lerp(0.65f, 1.05f, t);
                if (species.fleeSpeed > 0.1f)
                    animal.fleeSpeed = species.fleeSpeed * Mathf.Lerp(0.95f, 1.2f, t);
                alertHolds = false;
                Notice(threat, true);
                return;
            }

            alertHolds = true;
            Notice(threat, false);
        }

        public void Notice(Vector3 threat, bool urgent)
        {
            if (animal == null || animal.Dead || state == AnimalAiState.Dead)
                return;
            if (species == null)
                Bind();
            if (species == null)
                return;

            lastThreat = threat;
            hasThreat = true;
            lastNotice = Time.time;

            if (state == AnimalAiState.Flee)
            {
                if (!urgent)
                    return;

                float span = FleeSpan();
                if (timer < span)
                    timer = span;
                heading = FleeHeading();
                return;
            }

            if (urgent)
            {
                alertHolds = false;
                EnterFlee();
                return;
            }

            if (state != AnimalAiState.Alert)
                EnterAlert();
            else if (alertHolds)
                timer = species.alertSeconds > 0.05f ? species.alertSeconds : 1.15f;
        }

        public void Tick(float delta)
        {
            if (species == null)
                Bind();
            if (animal == null || species == null)
                return;

            if (animal.Dead)
            {
                state = AnimalAiState.Dead;
                return;
            }

            TickMarker.Begin();
            if (state == AnimalAiState.Idle)
                TickIdle(delta);
            else if (state == AnimalAiState.Graze)
                TickGraze(delta);
            else if (state == AnimalAiState.Alert)
                TickAlert(delta);
            else if (state == AnimalAiState.Flee)
                TickFleeState(delta);
            TickMarker.End();
        }

        void TickIdle(float delta)
        {
            if (animal.kind != AnimalKind.Bird &&
                MeatBait.TryGetTarget(animal.transform.position, animal.kind, out _))
            {
                EnterGraze();
                return;
            }

            if (animal.kind != AnimalKind.Bird)
                animal.Hold();

            timer -= delta;
            if (timer <= 0f)
                EnterGraze();
        }

        void TickGraze(float delta)
        {
            if (animal.kind == AnimalKind.Bird)
            {
                timer -= delta;
                if (timer <= 0f)
                    EnterIdle();
                return;
            }

            bool chasingBait = false;
            if (!homing && MeatBait.TryGetTarget(animal.transform.position, animal.kind, out Vector3 bait))
            {
                chasingBait = true;
                grazePoint = bait;
            }

            float speed = species.grazeSpeed > 0.1f ? species.grazeSpeed : animal.moveSpeed;
            speed *= animal.WoundMoveScale;
            animal.MoveOnGround(grazePoint, speed, false);

            if (Arrived(grazePoint))
            {
                if (chasingBait)
                    return;

                homing = false;
                EnterIdle();
                return;
            }

            if (chasingBait || homing)
                return;

            timer -= delta;
            if (timer <= 0f)
                EnterIdle();
        }

        void TickAlert(float delta)
        {
            if (animal.kind != AnimalKind.Bird && hasThreat)
                animal.Face(lastThreat);
            else if (animal.kind != AnimalKind.Bird)
                animal.Hold();

            timer -= delta;
            if (timer <= 0f)
            {
                // Fuzzy alert watches, then settles. A hard notice still bolts when the pause ends.
                if (alertHolds)
                {
                    alertHolds = false;
                    EnterIdle();
                }
                else
                    EnterFlee();
            }
        }

        void TickFleeState(float delta)
        {
            if (animal.kind == AnimalKind.Bird)
            {
                if (!animal.Fleeing)
                    EnterIdle();
                return;
            }

            float speed = animal.fleeSpeed > 0.1f ? animal.fleeSpeed : species.fleeSpeed;
            speed *= animal.WoundMoveScale;
            if (!animal.MoveHeading(animal.Steer(heading), speed))
            {
                heading = Quaternion.Euler(0f, 110f, 0f) * heading;
                animal.MoveHeading(animal.Steer(heading), speed);
            }

            timer -= delta;
            float calm = (species.calmSeconds > 0.05f ? species.calmSeconds : 0.05f) * Mathf.Max(1f, calmScale);
            bool recent = Time.time - lastNotice < calm;
            // Only a graded scare imposes a minimum run. Ordinary notices end on the calm tail.
            if (animal.kind == AnimalKind.Deer && gradedFlight)
            {
                if (recent || timer > 0f)
                    return;
            }
            else if (recent)
                return;

            animal.EndFlee();
            BeginReturn();
        }

        void EnterIdle()
        {
            state = AnimalAiState.Idle;
            homing = false;
            alertHolds = false;
            calmScale = 1f;
            RestoreFleeSpeed();
            timer = species.idleSeconds > 0.05f ? species.idleSeconds : 0.05f;
        }

        void EnterGraze()
        {
            state = AnimalAiState.Graze;
            homing = false;
            timer = species.grazeSeconds > 0.05f ? species.grazeSeconds : 4f;
            if (animal.kind != AnimalKind.Bird &&
                MeatBait.TryGetTarget(animal.transform.position, animal.kind, out Vector3 bait))
            {
                grazePoint = bait;
                return;
            }

            PickGrazePoint();
        }

        void EnterAlert()
        {
            state = AnimalAiState.Alert;
            timer = species.alertSeconds;
        }

        void EnterFlee()
        {
            state = AnimalAiState.Flee;
            homing = false;
            alertHolds = false;
            lastNotice = Time.time;
            timer = FleeSpan();
            heading = FleeHeading();
            animal.BeginFlee(heading, timer);
        }

        float FleeSpan()
        {
            float seconds = gradedFleeSeconds > 0.05f
                ? gradedFleeSeconds
                : (species.fleeSeconds > 0.05f ? species.fleeSeconds : animal.fleeDuration);
            gradedFleeSeconds = 0f;
            return seconds;
        }

        void RestoreFleeSpeed()
        {
            gradedFlight = false;
            gradedFleeSeconds = 0f;
            if (animal != null && species != null && species.fleeSpeed > 0.1f)
                animal.fleeSpeed = species.fleeSpeed;
        }

        public void UseSpecies(SpeciesData data)
        {
            speciesOverride = data;
            bound = false;
            species = null;
        }

        public void ArmIdle()
        {
            if (species == null)
                Bind();
            state = AnimalAiState.Idle;
            homing = false;
            hasThreat = false;
            alertHolds = false;
            calmScale = 1f;
            RestoreFleeSpeed();
            timer = species != null && species.idleSeconds > 0.05f ? species.idleSeconds : 2f;
            if (animal != null)
                animal.EndFlee();
        }

        /// <summary>Walk toward a world point (migration, river crossings).</summary>
        public void GoToPoint(Vector3 worldPoint, float seconds = 14f)
        {
            if (species == null)
                Bind();
            state = AnimalAiState.Graze;
            homing = false;
            hasThreat = false;
            alertHolds = false;
            calmScale = 1f;
            RestoreFleeSpeed();
            grazePoint = WorldGround.Snap(worldPoint);
            timer = seconds > 0.5f ? seconds : 14f;
            if (animal != null)
                animal.EndFlee();
        }

        void BeginReturn()
        {
            state = AnimalAiState.Graze;
            homing = true;
            alertHolds = false;
            calmScale = 1f;
            RestoreFleeSpeed();
            grazePoint = animal.HomePosition;
        }

        void PickGrazePoint()
        {
            float radius = species.grazeRadius > 0.5f ? species.grazeRadius : 6f;
            Vector3 home = animal.HomePosition;
            for (int i = 0; i < 8; i++)
            {
                Vector2 circle = Random.insideUnitCircle * radius;
                Vector3 candidate = WorldGround.Snap(home + new Vector3(circle.x, 0f, circle.y));
                if (WorldGround.IsMeadow(candidate) || i == 7)
                {
                    grazePoint = candidate;
                    return;
                }
            }
        }

        bool Arrived(Vector3 point)
        {
            float dx = animal.transform.position.x - point.x;
            float dz = animal.transform.position.z - point.z;
            return dx * dx + dz * dz < 0.36f;
        }

        Vector3 FleeHeading()
        {
            Vector3 away = animal.transform.position - lastThreat;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f)
                away = transform.forward;
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
                away = Vector3.forward;
            away.Normalize();

            if (!species.prey || species.downwindWeight <= 0f || WindSystem.Current == null)
                return away;

            Vector3 wind = WindSystem.Current.Direction;
            wind.y = 0f;
            if (wind.sqrMagnitude < 0.0001f)
                return away;
            wind.Normalize();

            // Downwind of the threat: run with the wind, and never back toward it.
            Vector3 combined = away + wind * species.downwindWeight;
            if (Vector3.Dot(combined, away) < 0.2f)
                combined = away + wind * 0.25f;
            if (combined.sqrMagnitude < 0.0001f)
                return away;
            return combined.normalized;
        }

        void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position + Vector3.up * 1.6f;
            if (state == AnimalAiState.Alert)
                Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            else if (state == AnimalAiState.Flee)
                Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.95f);
            else if (state == AnimalAiState.Graze)
                Gizmos.color = new Color(0.4f, 0.85f, 0.4f, 0.95f);
            else if (state == AnimalAiState.Dead)
                Gizmos.color = new Color(0.2f, 0.2f, 0.2f, 0.95f);
            else
                Gizmos.color = new Color(0.75f, 0.75f, 0.75f, 0.95f);

            Gizmos.DrawWireSphere(origin, 0.28f);
            if (state == AnimalAiState.Flee)
                Gizmos.DrawRay(origin, heading * 3f);
            else if (state == AnimalAiState.Graze)
                Gizmos.DrawLine(origin, grazePoint + Vector3.up * 0.4f);
        }
    }
}
