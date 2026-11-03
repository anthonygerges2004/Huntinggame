using Unity.Profiling;
using UnityEngine;

namespace HuntingGame.Animals
{
    [DefaultExecutionOrder(-100)]
    public class AnimalAiScheduler : MonoBehaviour
    {
        static readonly ProfilerMarker TickMarker = new ProfilerMarker("HuntingGame.AnimalAi");

        static AnimalAiScheduler current;
        static IAnimalAi[] pending = new IAnimalAi[64];
        static int pendingCount;

        [SerializeField] AnimalAiSettings settings;
        [SerializeField] Transform player;

        IAnimalAi[] agents = new IAnimalAi[64];
        int count;
        int cursor;
        float fullSqr;
        float reducedSqr;
        float reducedInterval;
        int slice;

        public static void Register(IAnimalAi agent)
        {
            if (agent == null)
                return;

            if (current != null)
            {
                current.Add(agent);
                return;
            }

            for (int i = 0; i < pendingCount; i++)
            {
                if (pending[i] == agent)
                    return;
            }

            if (pendingCount >= pending.Length)
            {
                IAnimalAi[] grown = new IAnimalAi[pending.Length * 2];
                for (int i = 0; i < pending.Length; i++)
                    grown[i] = pending[i];
                pending = grown;
            }

            pending[pendingCount] = agent;
            pendingCount++;
        }

        public static void Alert(Vector3 position, float radius)
        {
            if (current == null || radius <= 0f)
                return;

            current.AlertNow(position, radius * radius);
        }

        public static void Unregister(IAnimalAi agent)
        {
            if (current != null)
                current.Remove(agent);

            for (int i = 0; i < pendingCount; i++)
            {
                if (pending[i] != agent)
                    continue;

                pendingCount--;
                pending[i] = pending[pendingCount];
                pending[pendingCount] = null;
                return;
            }
        }

        void Awake()
        {
            if (settings == null)
                settings = ScriptableObject.CreateInstance<AnimalAiSettings>();

            slice = settings.evaluationsPerSlice < 1 ? 1 : settings.evaluationsPerSlice;
            reducedInterval = settings.reducedTickInterval < 0.05f ? 0.05f : settings.reducedTickInterval;
            fullSqr = settings.fullRadius * settings.fullRadius;
            reducedSqr = settings.reducedRadius * settings.reducedRadius;
            if (reducedSqr < fullSqr)
                reducedSqr = fullSqr;

            current = this;
            for (int i = 0; i < pendingCount; i++)
            {
                Add(pending[i]);
                pending[i] = null;
            }

            pendingCount = 0;
        }

        void Start()
        {
            if (player == null && PlayerHealth.Instance != null)
                player = PlayerHealth.Instance.transform;
        }

        void OnDestroy()
        {
            if (current == this)
                current = null;
        }

        void Update()
        {
            TickMarker.Begin();
            TickSlice();
            TickMarker.End();
        }

        void TickSlice()
        {
            if (player == null || count == 0)
                return;

            int steps = slice < count ? slice : count;
            Vector3 origin = player.position;
            float now = Time.time;

            for (int step = 0; step < steps; step++)
            {
                if (count == 0)
                    return;

                if (cursor >= count)
                    cursor = 0;

                IAnimalAi agent = agents[cursor];
                Transform body = agent != null ? agent.Body : null;
                if (agent == null || body == null)
                {
                    RemoveAt(cursor);
                    continue;
                }

                Vector3 delta = body.position - origin;
                agent.Evaluate(delta.sqrMagnitude, fullSqr, reducedSqr);
                agent.ScheduledTick(now, reducedInterval);
                cursor++;
            }
        }

        void AlertNow(Vector3 position, float radiusSqr)
        {
            for (int i = 0; i < count; i++)
            {
                IAnimalAi agent = agents[i];
                if (agent != null)
                    agent.Hear(position, radiusSqr);
            }
        }

        void Add(IAnimalAi agent)
        {
            if (agent == null)
                return;

            for (int i = 0; i < count; i++)
            {
                if (agents[i] == agent)
                    return;
            }

            if (count >= agents.Length)
            {
                IAnimalAi[] grown = new IAnimalAi[agents.Length * 2];
                for (int i = 0; i < agents.Length; i++)
                    grown[i] = agents[i];
                agents = grown;
            }

            agents[count] = agent;
            count++;
        }

        void Remove(IAnimalAi agent)
        {
            for (int i = 0; i < count; i++)
            {
                if (agents[i] != agent)
                    continue;

                RemoveAt(i);
                return;
            }
        }

        void RemoveAt(int index)
        {
            int last = count - 1;
            agents[index] = agents[last];
            agents[last] = null;
            count = last;
            if (cursor > index)
                cursor--;
            if (cursor > count)
                cursor = count;
        }
    }
}
