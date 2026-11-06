using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Leaves a fading scent path the wind can carry. Animals may investigate old spots upwind.
/// </summary>
public class PlayerScentTrail : MonoBehaviour
{
    public static PlayerScentTrail Instance { get; private set; }

    public float sampleInterval = 1.1f;
    public float maxMarkAge = 90f;
    public int maxMarks = 24;
    public float minMove = 0.35f;

    struct Mark
    {
        public Vector3 position;
        public float time;
    }

    readonly List<Mark> marks = new List<Mark>();
    float nextSample;
    Vector3 lastSample;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        lastSample = transform.position;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (PlayerHealth.Instance != null && PlayerHealth.Instance.isDead)
            return;

        float now = Time.time;
        while (marks.Count > 0 && now - marks[0].time > maxMarkAge)
            marks.RemoveAt(0);

        nextSample -= Time.deltaTime;
        if (nextSample > 0f)
            return;

        nextSample = sampleInterval;
        Vector3 p = transform.position;
        if (!PlayerMove.IsMoving && marks.Count > 0)
            return;

        if ((p - lastSample).sqrMagnitude < minMove * minMove && PlayerMove.IsMoving)
            return;

        lastSample = p;
        marks.Add(new Mark { position = p, time = now });
        while (marks.Count > maxMarks)
            marks.RemoveAt(0);
    }

    /// <summary>
    /// Finds an old scent mark upwind of the animal worth investigating.
    /// </summary>
    public static bool TryFindInvestigationPoint(
        Vector3 animalPosition,
        Vector3 windNormalized,
        float smellDot,
        float maxRange,
        float minAge,
        float maxAge,
        out Vector3 scentPoint)
    {
        scentPoint = default;
        if (Instance == null || windNormalized.sqrMagnitude < 0.0001f)
            return false;

        windNormalized.y = 0f;
        windNormalized.Normalize();
        float limitSqr = maxRange * maxRange;
        float now = Time.time;
        Mark best = default;
        float bestScore = float.MinValue;

        for (int i = 0; i < Instance.marks.Count; i++)
        {
            Mark mark = Instance.marks[i];
            float age = now - mark.time;
            if (age < minAge || age > maxAge)
                continue;

            Vector3 toAnimal = animalPosition - mark.position;
            toAnimal.y = 0f;
            float distSqr = toAnimal.sqrMagnitude;
            if (distSqr < 0.25f || distSqr > limitSqr)
                continue;

            float dist = Mathf.Sqrt(distSqr);
            if (Vector3.Dot(toAnimal / dist, windNormalized) < smellDot)
                continue;

            float score = age * 0.02f - dist * 0.01f;
            if (score > bestScore)
            {
                bestScore = score;
                best = mark;
            }
        }

        if (bestScore <= float.MinValue + 1f)
            return false;

        scentPoint = best.position;
        return true;
    }
}
