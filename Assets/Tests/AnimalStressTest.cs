using System.Text;
using HuntingGame.Tracking;
using HuntingGame.Wounds;
using Unity.Profiling;
using UnityEngine;

namespace HuntingGame.Animals
{
    public enum StressHerdSize
    {
        Fifty = 50,
        TwoHundred = 200,
        FiveHundred = 500
    }

    /// <summary>
    /// Add this to an object in the hunting scene and press Play.
    /// After the herd is up, one Console report lists CPU frame time, GC, and each HuntingGame marker.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    [AddComponentMenu("Hunting Game/Animal Stress Test")]
    public class AnimalStressTest : MonoBehaviour
    {
        const double FrameBudgetMs = 1000.0 / 60.0;
        const double HotMarkerMs = 2.0;
        const double ScriptBudgetMs = 4.0;
        const int AiIndex = 0;

        static readonly string[] MarkerNames =
        {
            "HuntingGame.AnimalAi",
            "HuntingGame.AnimalBrain",
            "HuntingGame.Senses",
            "HuntingGame.Tracking",
            "HuntingGame.Wounds",
            "HuntingGame.Weather",
            "HuntingGame.Survival",
            "HuntingGame.Ballistics",
            "HuntingGame.Crafting"
        };

        [SerializeField] StressHerdSize herd = StressHerdSize.Fifty;
        [SerializeField] int seed = 40501;
        [SerializeField] int warmupFrames = 240;
        [SerializeField] int sampleFrames = 300;
        [SerializeField] bool runOnPlay = true;

        enum Phase
        {
            Idle,
            Warmup,
            Sample,
            Done
        }

        Phase phase;
        Transform herdRoot;
        ProfilerRecorder frameTime;
        ProfilerRecorder gcAlloc;
        ProfilerRecorder[] recorders = new ProfilerRecorder[MarkerNames.Length];
        long[] sums = new long[MarkerNames.Length];
        bool[] live = new bool[MarkerNames.Length];
        long frameSum;
        long gcSum;
        double wallSum;
        int warmed;
        int sampled;
        int spawned;
        int wet;
        int deer;
        int birds;
        int large;
        int legendary;
        bool cpuLive;
        bool gcLive;
        bool playerPresent;

        void Start()
        {
            if (runOnPlay)
                StartCoroutine(Run());
        }

        void OnDestroy()
        {
            StopRecorders();
            if (herdRoot != null)
                Destroy(herdRoot.gameObject);
        }

        System.Collections.IEnumerator Run()
        {
            yield return null;

            float readyAt = Time.unscaledTime + 1f;
            float giveUp = Time.unscaledTime + 8f;
            Terrain[] terrains = Terrain.activeTerrains;
            while (Time.unscaledTime < giveUp)
            {
                terrains = Terrain.activeTerrains;
                if (terrains != null && terrains.Length > 0 && Time.unscaledTime >= readyAt)
                    break;
                yield return null;
            }

            if (!TryBounds(terrains, out Vector3 min, out Vector3 max))
            {
                Debug.LogError("Animal stress test found no terrain.");
                yield break;
            }

            EnsureSystems();
            playerPresent = PlayerHealth.Instance != null;
            herdRoot = new GameObject("StressHerd").transform;

            int target = (int)herd;
            System.Random random = new System.Random(seed);
            Debug.Log("Animal stress test spawning " + target + " animals.");
            for (int i = 0; i < target; i++)
            {
                SpawnOne(i, random, min, max);
                if ((i & 15) == 15)
                    yield return null;
            }

            BeginRecording();
            phase = Phase.Warmup;
        }

        void LateUpdate()
        {
            if (phase == Phase.Warmup)
            {
                warmed++;
                if (warmed >= warmupFrames)
                    phase = Phase.Sample;
                return;
            }

            if (phase != Phase.Sample)
                return;

            if (cpuLive)
                frameSum += frameTime.LastValue;
            if (gcLive)
                gcSum += gcAlloc.LastValue;
            wallSum += Time.unscaledDeltaTime;

            for (int i = 0; i < recorders.Length; i++)
            {
                if (live[i])
                    sums[i] += recorders[i].LastValue;
            }

            sampled++;
            if (sampled < sampleFrames)
                return;

            phase = Phase.Done;
            Report();
            StopRecorders();
        }

        void SpawnOne(int index, System.Random random, Vector3 min, Vector3 max)
        {
            AnimalKind kind = KindFor(index);
            Vector3 point = PickPoint(random, min, max);
            if (kind == AnimalKind.Bird)
                point.y += 4f;

            GameObject body = new GameObject(NameFor(kind, index));
            body.SetActive(false);
            body.transform.SetParent(herdRoot, false);
            body.transform.position = point;
            Animal animal = body.AddComponent<Animal>();
            animal.kind = kind;
            body.SetActive(true);

            spawned++;
            if (kind == AnimalKind.Bird)
                birds++;
            else if (kind == AnimalKind.Large)
                large++;
            else if (kind == AnimalKind.Legendary)
                legendary++;
            else
                deer++;
        }

        Vector3 PickPoint(System.Random random, Vector3 min, Vector3 max)
        {
            Vector3 point = new Vector3(min.x, 0f, min.z);
            float spanX = max.x - min.x;
            float spanZ = max.z - min.z;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                float x = min.x + (float)random.NextDouble() * spanX;
                float z = min.z + (float)random.NextDouble() * spanZ;
                point = WorldGround.Snap(new Vector3(x, 0f, z));
                if (!WorldGround.InsideWater(point))
                    return point;
            }

            wet++;
            return point;
        }

        static AnimalKind KindFor(int index)
        {
            int slot = index % 20;
            if (slot == 0)
                return AnimalKind.Legendary;
            if (slot < 3)
                return AnimalKind.Large;
            if (slot < 6)
                return AnimalKind.Bird;
            return AnimalKind.Deer;
        }

        static string NameFor(AnimalKind kind, int index)
        {
            if (kind == AnimalKind.Bird)
                return "Stress Bird " + index;
            if (kind == AnimalKind.Large)
                return "Stress Large " + index;
            if (kind == AnimalKind.Legendary)
                return "Stress Legendary " + index;
            return "Stress Deer " + index;
        }

        static bool TryBounds(Terrain[] terrains, out Vector3 min, out Vector3 max)
        {
            min = new Vector3(float.MaxValue, 0f, float.MaxValue);
            max = new Vector3(float.MinValue, 0f, float.MinValue);
            bool any = false;
            if (terrains == null)
                return false;

            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null)
                    continue;

                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (size.x < 1f || size.z < 1f)
                    continue;

                any = true;
                if (origin.x < min.x)
                    min.x = origin.x;
                if (origin.z < min.z)
                    min.z = origin.z;
                float endX = origin.x + size.x;
                float endZ = origin.z + size.z;
                if (endX > max.x)
                    max.x = endX;
                if (endZ > max.z)
                    max.z = endZ;
            }

            if (!any)
                return false;

            const float edge = 8f;
            if (max.x - min.x > edge * 4f && max.z - min.z > edge * 4f)
            {
                min.x += edge;
                max.x -= edge;
                min.z += edge;
                max.z -= edge;
            }

            return true;
        }

        void EnsureSystems()
        {
            if (FindAnyObjectByType<AnimalAiScheduler>() == null)
                gameObject.AddComponent<AnimalAiScheduler>();
            if (FindAnyObjectByType<TrackPool>() == null)
                gameObject.AddComponent<TrackPool>();
            if (FindAnyObjectByType<WoundPool>() == null)
                gameObject.AddComponent<WoundPool>();
        }

        void BeginRecording()
        {
            frameTime = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            gcAlloc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            cpuLive = frameTime.Valid;
            gcLive = gcAlloc.Valid;

            ProfilerRecorderOptions options =
                ProfilerRecorderOptions.StartImmediately |
                ProfilerRecorderOptions.SumAllSamplesInFrame |
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread;

            for (int i = 0; i < MarkerNames.Length; i++)
            {
                recorders[i] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, MarkerNames[i], 1, options);
                live[i] = recorders[i].Valid;
            }
        }

        void StopRecorders()
        {
            DisposeRecorder(ref frameTime);
            DisposeRecorder(ref gcAlloc);
            for (int i = 0; i < recorders.Length; i++)
                DisposeRecorder(ref recorders[i]);
        }

        static void DisposeRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid)
                recorder.Dispose();
        }

        void Report()
        {
            double cpuMs = cpuLive ? Ms(frameSum, sampled) : wallSum / sampled * 1000.0;
            double wallMs = wallSum / sampled * 1000.0;
            double gcBytes = gcLive ? gcSum / (double)sampled : 0.0;
            int leaf = HottestLeaf();
            double leafMs = leaf >= 0 ? Ms(sums[leaf], sampled) : 0.0;
            double leafSumMs = Ms(LeafNanoseconds(), sampled);
            bool framePass = cpuMs <= FrameBudgetMs;
            bool gcPass = !gcLive || gcBytes <= 0.0;

            StringBuilder text = new StringBuilder(1024);
            text.Append("Animal stress test\n");
            text.Append("Herd ");
            text.Append(spawned);
            text.Append(" (deer ");
            text.Append(deer);
            text.Append(", bird ");
            text.Append(birds);
            text.Append(", large ");
            text.Append(large);
            text.Append(", legendary ");
            text.Append(legendary);
            text.Append(")  sample ");
            text.Append(sampled);
            text.Append(" frames  seed ");
            text.Append(seed);
            text.Append('\n');
            if (wet > 0)
            {
                text.Append(wet);
                text.Append(" spawned in water after the ground retries.\n");
            }

            text.Append(playerPresent
                ? "Player is in the scene, so distant animals drop to reduced and dormant LOD.\n"
                : "No player is in the scene, so every animal stays in the full band and simulates every frame.\n");

            text.Append("CPU frame ");
            text.Append(cpuMs.ToString("0.000"));
            text.Append(" ms  (budget ");
            text.Append(FrameBudgetMs.ToString("0.00"));
            text.Append(" ms)  ");
            text.Append(framePass ? "PASS" : "FAIL");
            if (!cpuLive)
                text.Append("  (Main Thread counter unavailable, wall time used)");
            text.Append('\n');

            text.Append("Wall frame ");
            text.Append(wallMs.ToString("0.000"));
            text.Append(" ms. Judge the 60 FPS budget by CPU frame. Wall time includes vsync wait.\n");

            text.Append("GC ");
            if (gcLive)
            {
                text.Append(gcBytes.ToString("0.0"));
                text.Append(" B/frame  (budget 0)  ");
                text.Append(gcPass ? "PASS" : "FAIL");
            }
            else
            {
                text.Append("unavailable. Run in the Editor or a Development Build.");
            }

            text.Append('\n');
            for (int i = 0; i < MarkerNames.Length; i++)
            {
                AppendLabel(text, MarkerNames[i]);
                if (!live[i])
                {
                    text.Append("unavailable\n");
                    continue;
                }

                text.Append(Ms(sums[i], sampled).ToString("0.000"));
                text.Append(" ms\n");
            }

            text.Append("Script markers except AnimalAi add to ");
            text.Append(leafSumMs.ToString("0.000"));
            text.Append(" ms. AnimalAi includes the brain, senses, and tracks that slice calls.\n");
            text.Append("Optimize first: ");
            text.Append(FirstFix(cpuMs, gcBytes, gcLive, leaf, leafMs, leafSumMs));
            text.Append('\n');

            if (framePass && gcPass)
                Debug.Log(text.ToString());
            else
                Debug.LogWarning(text.ToString());
        }

        string FirstFix(double cpuMs, double gcBytes, bool gcKnown, int leaf, double leafMs, double leafSumMs)
        {
            if (gcKnown && gcBytes > 0.0)
                return "allocations. GC is above 0 bytes per frame. A collection costs more than these scripts and will miss 60 FPS.";

            if (cpuMs > FrameBudgetMs && leafMs < HotMarkerMs && leafSumMs < ScriptBudgetMs)
            {
                return "animal rendering and animators. The script markers together stay under 4 ms, so the frame is spent outside them. AnimalVisuals enables updateWhenOffscreen on every skinned mesh, so all " +
                       spawned + " animals keep updating off screen.";
            }

            if (leaf >= 0 && (cpuMs > FrameBudgetMs || leafMs >= HotMarkerMs))
                return MarkerNames[leaf] + " at " + leafMs.ToString("0.000") + " ms. It is the slowest gameplay system on the markers.";

            if (leaf >= 0)
                return "none. The frame is inside the 60 FPS budget. If the herd grows, watch " + MarkerNames[leaf] + " first (" + leafMs.ToString("0.000") + " ms).";

            return "none. The frame is inside the 60 FPS budget.";
        }

        int HottestLeaf()
        {
            int best = -1;
            long bestSum = -1;
            for (int i = 0; i < sums.Length; i++)
            {
                if (i == AiIndex || !live[i])
                    continue;
                if (sums[i] <= bestSum)
                    continue;
                bestSum = sums[i];
                best = i;
            }

            return best;
        }

        long LeafNanoseconds()
        {
            long total = 0;
            for (int i = 0; i < sums.Length; i++)
            {
                if (i == AiIndex || !live[i])
                    continue;
                total += sums[i];
            }

            return total;
        }

        static double Ms(long nanoseconds, int frames)
        {
            if (frames < 1)
                return 0.0;
            return nanoseconds / (double)frames * 1e-6;
        }

        static void AppendLabel(StringBuilder text, string name)
        {
            text.Append(name);
            for (int i = name.Length; i < 26; i++)
                text.Append(' ');
        }
    }
}
