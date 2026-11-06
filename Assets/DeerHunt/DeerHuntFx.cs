using UnityEngine;

namespace DeerHunt
{
    /// <summary>
    /// Short-lived particles and one-shot noises so the prototype needs no imported effects.
    /// </summary>
    public static class DeerHuntFx
    {
        static AudioClip shotClip;
        static AudioClip hitClip;
        static AudioClip windClip;

        public static void Muzzle(Vector3 position, Vector3 forward)
        {
            Burst(position, forward, new Color(1f, 0.75f, 0.35f), 10, 4f, 0.08f);
            Light flash = new GameObject("MuzzleFlash").AddComponent<Light>();
            flash.transform.position = position;
            flash.type = LightType.Point;
            flash.color = new Color(1f, 0.7f, 0.35f);
            flash.intensity = 8f;
            flash.range = 6f;
            Object.Destroy(flash.gameObject, 0.05f);
        }

        public static void Dust(Vector3 position, Vector3 normal)
        {
            Burst(position, normal, new Color(0.55f, 0.48f, 0.32f), 14, 2.5f, 0.45f);
        }

        public static void Blood(Vector3 position, Vector3 normal)
        {
            Blood(position, normal, null);
        }

        public static void Blood(Vector3 position, Vector3 normal, Transform stickTo)
        {
            ShotBlood.Play(position, normal, stickTo);
        }

        public static void PlayShot(Vector3 position)
        {
            Play(position, Shot(), 0.9f);
        }

        public static void PlayHit(Vector3 position)
        {
            Play(position, Hit(), 0.7f);
        }

        public static AudioClip Wind()
        {
            if (windClip == null)
                windClip = Tone(1.6f, 90f, 0.08f, true);
            return windClip;
        }

        static AudioClip Shot()
        {
            if (shotClip == null)
                shotClip = Tone(0.28f, 140f, 0.85f, false);
            return shotClip;
        }

        static AudioClip Hit()
        {
            if (hitClip == null)
                hitClip = Tone(0.16f, 90f, 0.45f, false);
            return hitClip;
        }

        static void Play(Vector3 position, AudioClip clip, float volume)
        {
            if (clip == null)
                return;

            GameObject go = new GameObject("OneShot");
            go.transform.position = position;
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 0.4f;
            source.Play();
            Object.Destroy(go, clip.length + 0.05f);
        }

        static AudioClip Tone(float seconds, float frequency, float volume, bool loop)
        {
            const int rate = 22050;
            int count = Mathf.Max(1, (int)(rate * seconds));
            float[] data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)rate;
                float envelope = loop ? 0.6f + Mathf.Sin(t * 3f) * 0.4f : Mathf.Exp(-t * (loop ? 1f : 12f));
                float noise = ((i * 37) % 100) / 100f - 0.5f;
                float wave = Mathf.Sin(t * frequency * Mathf.PI * 2f);
                data[i] = (wave * 0.35f + noise * 0.65f) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create(loop ? "wind" : "tone", count, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void Burst(Vector3 position, Vector3 direction, Color color, int count, float speed, float life)
        {
            GameObject go = new GameObject("Burst");
            go.transform.position = position;
            if (direction.sqrMagnitude > 0.001f)
                go.transform.rotation = Quaternion.LookRotation(direction.normalized);

            ParticleSystem particles = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.duration = life;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = 0.08f;
            main.startColor = color;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28f;
            shape.radius = 0.02f;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (renderer.material.shader == null || renderer.material.shader.name == "Hidden/InternalErrorShader")
                renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material.color = color;

            particles.Play();
            Object.Destroy(go, life + 0.2f);
        }
    }
}
