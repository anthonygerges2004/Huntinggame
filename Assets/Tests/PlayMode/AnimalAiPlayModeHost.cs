#if UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using HuntingGame.Senses;
using HuntingGame.Wind;
using UnityEngine;

namespace HuntingGame.Animals
{
    public static class AnimalAiPlayModeTests
    {
        public const string RequestName = "Temp/ai-playmode-request.txt";
        public const string ResultName = "Temp/ai-playmode-result.txt";

        public static string ProjectFile(string relative)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            string request = ProjectFile(RequestName);
            if (!File.Exists(request))
                return;

            File.Delete(request);
            GameObject host = new GameObject("AnimalAiPlayModeTests");
            host.AddComponent<AnimalAiPlayModeHost>();
        }
    }

    [DefaultExecutionOrder(-50)]
    public class AnimalAiPlayModeHost : MonoBehaviour
    {
        Animal held;
        string failure;

        void Update()
        {
            PlayerMove.SetGait(false, false, false, 1f);
            if (held != null)
                held.SetSuspendSelfTick(true);
        }

        IEnumerator Start()
        {
            string result = AnimalAiPlayModeTests.ProjectFile(AnimalAiPlayModeTests.ResultName);
            yield return Run();
            if (string.IsNullOrEmpty(failure))
            {
                File.WriteAllText(result, "passed: Idle -> Alert -> Flee, downwind flee, graze after calm, reduced LOD");
                Debug.Log("Animal AI PlayMode tests passed.");
            }
            else
            {
                File.WriteAllText(result, failure);
                Debug.LogError(failure);
            }
        }

        IEnumerator Run()
        {
            float giveUp = Time.time + 8f;
            while (PlayerHealth.Instance == null && Time.time < giveUp)
                yield return null;
            if (PlayerHealth.Instance == null)
            {
                failure = "PlayMode scene has no player.";
                yield break;
            }

            if (WindSystem.Current == null)
                new GameObject("PlayModeWind").AddComponent<WindSystem>();
            WindSystem.Current.SetFixed(Vector3.forward, 1f);
            WindSystem.Current.enabled = false;

            if (Time.timeScale < 0.01f)
                Time.timeScale = 1f;

            SpeciesData species = ScriptableObject.CreateInstance<SpeciesData>();
            species.kind = AnimalKind.Deer;
            species.prey = true;
            species.alertSeconds = 0.25f;
            species.calmSeconds = 0.5f;
            species.fleeSeconds = 20f;
            species.idleSeconds = 8f;
            species.grazeSeconds = 8f;
            species.grazeSpeed = 1f;
            species.fleeSpeed = 4f;
            species.downwindWeight = 1.2f;
            species.fullLodRadius = 45f;
            species.reducedLodRadius = 120f;
            species.senses = SenseCatalog.For(AnimalKind.Deer);

            yield return TransitionsAndFlee(species);
            if (!string.IsNullOrEmpty(failure))
                yield break;
            yield return ReducedLodSkipsFullSenses(species);
        }

        void Fail(string message)
        {
            if (string.IsNullOrEmpty(failure))
                failure = message;
        }

        IEnumerator TransitionsAndFlee(SpeciesData species)
        {
            Transform player = PlayerHealth.Instance.transform;
            GameObject body = new GameObject("PlayModeDeer");
            body.transform.position = player.position + new Vector3(0f, 0f, 6f);
            Animal animal = body.AddComponent<Animal>();
            body.AddComponent<AnimalAiAgent>();
            AnimalBrain brain = body.GetComponent<AnimalBrain>();
            brain.UseSpecies(species);
            AnimalSenses senses = body.GetComponent<AnimalSenses>();
            GameObject far = new GameObject("PlayModeFarThreat");
            far.transform.position = body.transform.position + new Vector3(0f, 0f, 400f);
            senses.SetWatch(far.transform);

            yield return null;
            senses.SetWatch(far.transform);
            brain.ArmIdle();
            body.GetComponent<AnimalAiAgent>().enabled = false;
            if (brain.State != AnimalAiState.Idle)
            {
                Fail("Deer should start Idle, was " + brain.State + ".");
                yield break;
            }

            Vector3 threat = body.transform.position + new Vector3(-8f, 0f, 0f);
            brain.Notice(threat, false);
            if (brain.State != AnimalAiState.Alert)
            {
                Fail("A soft notice should enter Alert before Flee, was " + brain.State + ".");
                yield break;
            }

            float noticedAt = Time.time;
            AnimalAiAgent transitionAgent = body.GetComponent<AnimalAiAgent>();
            while (Time.time < noticedAt + species.alertSeconds - 0.05f)
            {
                if (brain.State != AnimalAiState.Alert)
                {
                    Fail("Alert changed before the alert time, was " + brain.State
                        + " after " + (Time.time - noticedAt).ToString("0.000")
                        + "s (alert " + species.alertSeconds.ToString("0.00")
                        + "s, band " + transitionAgent.Band
                        + ", dt " + Time.deltaTime.ToString("0.000") + ").");
                    yield break;
                }
                yield return null;
            }

            float fleeBy = Time.time + 0.45f;
            while (Time.time < fleeBy && brain.State != AnimalAiState.Flee)
                yield return null;
            if (brain.State != AnimalAiState.Flee)
            {
                Fail("Alert should become Flee, was " + brain.State + ".");
                yield break;
            }

            Vector3 flat = body.transform.forward;
            flat.y = 0f;
            flat.Normalize();
            if (flat.x < 0.35f)
            {
                Fail("Flee should move away from the threat. Forward x was " + flat.x + ".");
                yield break;
            }
            if (flat.z < 0.35f)
            {
                Fail("Flee should prefer downwind. Forward z was " + flat.z + ".");
                yield break;
            }

            float presentUntil = Time.time + 0.7f;
            while (Time.time < presentUntil)
            {
                brain.Notice(body.transform.position + new Vector3(-8f, 0f, 0f), false);
                if (brain.State != AnimalAiState.Flee)
                {
                    Fail("Deer left Flee while the threat was still noticed, was " + brain.State + ".");
                    yield break;
                }
                yield return null;
            }

            float goneAt = Time.time;
            float stillFleeing = goneAt + 0.25f;
            while (Time.time < stillFleeing)
            {
                if (brain.State != AnimalAiState.Flee)
                {
                    Fail("Deer returned before the calm window, was " + brain.State + ".");
                    yield break;
                }
                yield return null;
            }

            float grazeBy = goneAt + species.calmSeconds + 0.35f;
            while (Time.time < grazeBy && brain.State != AnimalAiState.Graze)
                yield return null;
            if (brain.State != AnimalAiState.Graze)
            {
                Fail("Deer should Graze after the threat is gone for " + species.calmSeconds + "s, was " + brain.State + ".");
                yield break;
            }

            Destroy(body);
            Destroy(far);
        }

        IEnumerator ReducedLodSkipsFullSenses(SpeciesData species)
        {
            Transform player = PlayerHealth.Instance.transform;
            GameObject body = new GameObject("PlayModeDeerLod");
            body.transform.position = player.position + new Vector3(0f, 0f, 8f);
            Animal animal = body.AddComponent<Animal>();
            AnimalAiAgent agent = body.AddComponent<AnimalAiAgent>();
            AnimalBrain brain = body.GetComponent<AnimalBrain>();
            brain.UseSpecies(species);
            AnimalSenses senses = body.GetComponent<AnimalSenses>();
            GameObject threat = new GameObject("PlayModeNearThreat");
            threat.transform.position = body.transform.position + new Vector3(0f, 0f, -25f);
            senses.SetWatch(threat.transform);
            held = animal;

            yield return null;
            body.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            brain.ArmIdle();

            float fullBy = Time.time + 1.2f;
            while (Time.time < fullBy && brain.State == AnimalAiState.Idle)
                yield return null;
            if (brain.State == AnimalAiState.Idle)
            {
                Fail("Close deer should run a full sense check and leave Idle.");
                yield break;
            }

            body.transform.position = player.position + new Vector3(80f, 0f, 0f);
            threat.transform.position = body.transform.position + new Vector3(0f, 0f, -25f);
            body.transform.rotation = Quaternion.LookRotation(Vector3.forward);

            float reducedBy = Time.time + 4f;
            while (Time.time < reducedBy && agent.Band != AnimalAiBand.Reduced)
                yield return null;
            if (agent.Band != AnimalAiBand.Reduced)
            {
                Fail("Deer 80m from the player should be Reduced LOD, was " + agent.Band + ".");
                yield break;
            }

            // Remembered fear is allowed to survive an LOD change. Wait for its
            // decay before checking that reduced senses do not reacquire this cue.
            yield return new WaitForSeconds(3f);
            brain.ArmIdle();
            float quietUntil = Time.time + 0.8f;
            while (Time.time < quietUntil)
            {
                if (brain.State != AnimalAiState.Idle)
                {
                    Fail("Reduced LOD should not run the full-range sense check, was " + brain.State + ".");
                    yield break;
                }
                if (agent.Band != AnimalAiBand.Reduced)
                {
                    Fail("LOD left Reduced, was " + agent.Band + ".");
                    yield break;
                }
                yield return null;
            }

            Destroy(body);
            Destroy(threat);
            held = null;
        }
    }
}
#endif
