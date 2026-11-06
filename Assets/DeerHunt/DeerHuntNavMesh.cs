using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace DeerHunt
{
    /// <summary>
    /// Builds a NavMesh from the colliders under this object when play starts,
    /// so the deer can path without a manual bake.
    /// </summary>
    public class DeerHuntNavMesh : MonoBehaviour
    {
        [SerializeField] Transform obstacles;
        [SerializeField] NavMeshData baked;

        public static bool Ready { get; private set; }

        NavMeshDataInstance instance;

        void Awake()
        {
            Ready = false;
            if (baked != null)
            {
                instance = NavMesh.AddNavMeshData(baked);
                Ready = instance.valid;
                return;
            }

            NavMeshData data = BuildData();
            if (data == null)
                return;

            instance = NavMesh.AddNavMeshData(data);
            Ready = instance.valid;
        }

        void Start()
        {
            AudioSource source = GetComponent<AudioSource>();
            if (source == null)
                source = gameObject.AddComponent<AudioSource>();
            source.clip = DeerHuntFx.Wind();
            source.loop = true;
            source.volume = 0.22f;
            source.spatialBlend = 0f;
            source.Play();
        }

        void OnDestroy()
        {
            if (instance.valid)
                NavMesh.RemoveNavMeshData(instance);
            Ready = false;
        }

        public NavMeshData BuildData()
        {
            List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
            List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();
            if (obstacles != null)
            {
                for (int i = 0; i < obstacles.childCount; i++)
                    MarkBlocked(obstacles.GetChild(i), markups);
            }

            NavMeshBuilder.CollectSources(
                transform,
                ~0,
                NavMeshCollectGeometry.PhysicsColliders,
                0,
                markups,
                sources);

            if (sources.Count == 0)
            {
                Debug.LogWarning("DeerHuntNavMesh found no colliders under " + name + ".");
                return null;
            }

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.45f;
            settings.agentHeight = 1.5f;
            settings.agentSlope = 40f;
            settings.agentClimb = 0.45f;

            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);
            bounds.Expand(4f);

            NavMeshData data = NavMeshBuilder.BuildNavMeshData(
                settings,
                sources,
                bounds,
                transform.position,
                transform.rotation);
            return data;
        }

        static void MarkBlocked(Transform root, List<NavMeshBuildMarkup> markups)
        {
            markups.Add(new NavMeshBuildMarkup
            {
                root = root,
                overrideArea = true,
                area = 1,
                ignoreFromBuild = false
            });
            for (int i = 0; i < root.childCount; i++)
                MarkBlocked(root.GetChild(i), markups);
        }
    }
}
