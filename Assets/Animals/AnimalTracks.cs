using UnityEngine;

namespace HuntingGame.Tracking
{
    [RequireComponent(typeof(Animal))]
    public class AnimalTracks : MonoBehaviour, IAnimalTracks
    {
        const int Trail = 6;

        Animal animal;
        Vector3 last;
        Vector3[] trail;
        float[] trailYaw;
        bool[] trailOn;
        int trailWrite;
        float nextStep;
        float nextBranch;
        float stepInterval = 0.55f;
        float minStepSqr = 0.72f;
        float branchInterval = 1.4f;
        float printSize = 0.42f;
        float printLife = 75f;
        float bloodSize = 0.2f;
        float branchLength = 0.48f;
        bool armed;

        void Awake()
        {
            animal = GetComponent<Animal>();
            trail = new Vector3[Trail];
            trailYaw = new float[Trail];
            trailOn = new bool[Trail];
        }

        void Start()
        {
            PullSettings();
            last = transform.position;
            nextStep = Time.time + Random.Range(0f, stepInterval);
            nextBranch = Time.time + Random.Range(0f, branchInterval);
            armed = true;
        }

        public void ArmAt(float now)
        {
            PullSettings();
            last = transform.position;
            nextStep = now;
            nextBranch = now + branchInterval;
            armed = true;
        }

        void PullSettings()
        {
            TrackPool pool = TrackPool.Current;
            if (pool == null)
                return;

            stepInterval = pool.StepInterval;
            minStepSqr = pool.MinStepSqr;
            branchInterval = pool.BranchInterval;
            bloodSize = pool.BloodSize;
            branchLength = pool.BranchLength;
            pool.PrintFor(animal != null ? animal.kind : AnimalKind.Deer, out printSize, out printLife);
            if (animal != null && animal.kind == AnimalKind.Game)
                printSize *= QuarryProfile.Of(animal.quarry).trackScale;
        }

        public void Sample(Vector3 position, float yawDegrees)
        {
            SampleAt(position, yawDegrees, Time.time);
        }

        public void SampleAt(Vector3 position, float yawDegrees, float now)
        {
            if (!armed || animal == null || animal.Dead || !animal.IsPresent())
                return;

            TrackPool pool = TrackPool.Current;
            if (pool == null)
                return;

            Vector3 offset = position - last;
            offset.y = 0f;
            if (offset.sqrMagnitude < minStepSqr)
                return;

            // One print per step. Standing still and mid-stride frames do not spawn.
            if (now < nextStep)
                return;

            Vector3 ground = WorldGround.Snap(position);
            if (WorldGround.InsideWater(ground))
            {
                last = position;
                return;
            }

            ground.y += 0.045f;
            last = position;
            nextStep = now + stepInterval;
            pool.Place(TrackKind.Footprint, ground, yawDegrees, printSize, printLife, now);
            Remember(ground, yawDegrees);

            bool throughCover = animal.Fleeing || animal.kind != AnimalKind.Deer;
            if (!throughCover || now < nextBranch || WorldGround.IsMeadow(position))
                return;

            nextBranch = now + branchInterval;
            Vector3 twig = ground;
            twig.y += 0.02f;
            pool.Place(TrackKind.Branch, twig, yawDegrees + 48f, branchLength, 0f, now);
        }

        public void SpillBlood(Vector3 position)
        {
            TrackPool pool = TrackPool.Current;
            if (pool == null)
                return;

            int drops = pool.BloodTrail;
            if (drops < 1)
                drops = 1;
            if (drops > Trail)
                drops = Trail;

            Vector3 kill = WorldGround.Snap(position);
            kill.y += 0.06f;
            float size = bloodSize;
            pool.Place(TrackKind.Blood, kill, position.x + position.z, size, 0f);

            int index = trailWrite - 1;
            if (index < 0)
                index = Trail - 1;

            for (int i = 0; i < drops; i++)
            {
                if (!trailOn[index])
                    break;

                size *= 0.86f;
                Vector3 spot = trail[index];
                spot.y += 0.055f;
                pool.Place(TrackKind.Blood, spot, trailYaw[index], size, 0f);
                index--;
                if (index < 0)
                    index = Trail - 1;
            }
        }

        void Remember(Vector3 ground, float yaw)
        {
            trail[trailWrite] = ground;
            trailYaw[trailWrite] = yaw;
            trailOn[trailWrite] = true;
            trailWrite++;
            if (trailWrite >= Trail)
                trailWrite = 0;
        }
    }
}
