using System.Collections.Generic;
using UnityEngine;
using HuntingGame.Animals;

/// <summary>
/// Deer herds use terrain-gap rivers as dawn/dusk crossing funnels.
/// </summary>
public class GapRiverCrossings : MonoBehaviour
{
    public const string GapRiverRoot = "_GapRivers";

    public static GapRiverCrossings Instance { get; private set; }

    public float herdRadius = 95f;
    public int maxDeerPerEvent = 2;

    struct Funnel
    {
        public Vector3 center;
        public float halfWidth;
        public float zMin;
        public float zMax;
        public float crossAxisX;
    }

    readonly List<Funnel> funnels = new List<Funnel>();
    float lastDayPhase = -1f;
    bool dawnTriggered;
    bool duskTriggered;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        CacheFunnels();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void CacheFunnels()
    {
        funnels.Clear();
        GameObject root = GameObject.Find(GapRiverRoot);
        if (root == null)
            return;

        for (int i = 0; i < root.transform.childCount; i++)
        {
            Transform child = root.transform.GetChild(i);
            Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                continue;

            Bounds bounds = renderers[0].bounds;
            for (int r = 1; r < renderers.Length; r++)
                bounds.Encapsulate(renderers[r].bounds);

            funnels.Add(new Funnel
            {
                center = bounds.center,
                halfWidth = bounds.extents.x,
                zMin = bounds.min.z,
                zMax = bounds.max.z,
                crossAxisX = bounds.center.x
            });
        }
    }

    void Update()
    {
        if (DayNightCycle.Instance == null || funnels.Count == 0)
            return;

        float phase = DayNightCycle.Instance.TimeOfDayNormalized;
        if (phase < lastDayPhase - 0.3f)
        {
            dawnTriggered = false;
            duskTriggered = false;
        }

        lastDayPhase = phase;

        if (!dawnTriggered && phase >= 0.20f && phase < 0.21f)
        {
            dawnTriggered = true;
            RunCrossing();
        }

        if (!duskTriggered && phase >= 0.49f && phase < 0.50f)
        {
            duskTriggered = true;
            RunCrossing();
        }
    }

    void RunCrossing()
    {
        Animal[] animals = Object.FindObjectsByType<Animal>();
        int sent = 0;

        for (int f = 0; f < funnels.Count && sent < maxDeerPerEvent; f++)
        {
            Funnel funnel = funnels[f];
            float z = Random.Range(funnel.zMin + 6f, funnel.zMax - 6f);
            Vector3 bankWest = SnapDry(new Vector3(funnel.crossAxisX - funnel.halfWidth - 10f, 0f, z));
            Vector3 bankEast = SnapDry(new Vector3(funnel.crossAxisX + funnel.halfWidth + 10f, 0f, z));

            for (int i = 0; i < animals.Length && sent < maxDeerPerEvent; i++)
            {
                Animal animal = animals[i];
                if (animal == null || animal.Dead || animal.kind != AnimalKind.Deer || !animal.IsPresent())
                    continue;
                if (animal.Fleeing)
                    continue;

                AnimalBrain brain = animal.GetComponent<AnimalBrain>();
                if (brain == null || brain.State == AnimalAiState.Flee || brain.State == AnimalAiState.Alert)
                    continue;

                Vector3 offset = animal.transform.position - funnel.center;
                offset.y = 0f;
                if (offset.magnitude > herdRadius)
                    continue;

                bool fromWest = animal.transform.position.x < funnel.crossAxisX;
                brain.GoToPoint(fromWest ? bankEast : bankWest, 22f);
                sent++;
            }
        }

        if (sent > 0 && Inventory.Instance != null)
            Inventory.Instance.ShowNotice("Deer are crossing a river gap.");
    }

    static Vector3 SnapDry(Vector3 point)
    {
        point = WorldGround.Snap(point);
        if (WaterBodies.InsideWater(point))
            point.y += 0.5f;
        return point;
    }
}
