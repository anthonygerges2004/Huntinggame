using UnityEngine;
using UnityEngine.AI;

namespace DeerHunt
{
    /// <summary>
    /// Places deer on the NavMesh around the player. Default count is inside the 5 to 10 range.
    /// </summary>
    public class DeerSpawner : MonoBehaviour
    {
        [SerializeField] GameObject deerPrefab;
        [SerializeField] RuntimeAnimatorController deerAnimator;
        [SerializeField] int count = 7;
        [SerializeField] float radius = 48f;
        [SerializeField] float minDistance = 16f;
        [SerializeField] Transform center;

        bool spawned;

        void Start()
        {
            if (center == null)
            {
                PlayerController player = FindAnyObjectByType<PlayerController>();
                if (player != null)
                    center = player.transform;
            }

            TrySpawn();
        }

        void Update()
        {
            if (!spawned && DeerHuntNavMesh.Ready)
                TrySpawn();
        }

        void TrySpawn()
        {
            if (spawned || deerPrefab == null || !DeerHuntNavMesh.Ready)
                return;

            Vector3 origin = center != null ? center.position : transform.position;
            int placed = 0;
            for (int attempt = 0; attempt < count * 12 && placed < count; attempt++)
            {
                Vector2 circle = Random.insideUnitCircle * radius;
                if (circle.magnitude < minDistance)
                    continue;

                Vector3 guess = origin + new Vector3(circle.x, 0f, circle.y);
                if (!NavMesh.SamplePosition(guess, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                    continue;

                GameObject deer = Instantiate(deerPrefab, hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                deer.name = "Deer " + (placed + 1);
                DeerController brain = deer.GetComponent<DeerController>();
                if (brain == null)
                    brain = deer.AddComponent<DeerController>();
                brain.UseAnimator(deerAnimator);
                if (deer.GetComponent<Health>() == null)
                    deer.AddComponent<Health>();
                if (deer.GetComponent<NavMeshAgent>() == null)
                    deer.AddComponent<NavMeshAgent>();
                placed++;
            }

            spawned = true;
            if (placed == 0)
                Debug.LogWarning("DeerSpawner found no NavMesh points. Bake or enable DeerHuntNavMesh.");
        }
    }
}
