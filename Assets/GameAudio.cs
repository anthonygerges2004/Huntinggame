using System.Collections.Generic;
using HuntingGame.Wind;
using UnityEngine;

/// <summary>
/// The game's whole soundscape, synthesized at start-up (no audio files needed):
///   guns       shotgun boom, rifle crack, sniper report, each with a rolling valley echo
///   reloads    break-open clunk and seated shells, bolt cycling
///   footsteps  by pace (sneak / walk / sprint) and ground (grass is soft, rock is gritty)
///   ambience   wind that follows the wind system, birdsong by day, crickets at night,
///              distant wolf howls after dark, crackle at a lit campfire
///   feedback   a thud when a shot connects
/// Volume follows the pause-menu slider (AudioListener.volume) and pauses with the game.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    const int Rate = 22050;

    AudioClip shotgunShot, rifleShot, sniperShot, breakOpen, shellIn, boltCycle, hitThud;
    AudioClip[] steps;
    AudioClip windLoop, cricketLoop, crackleLoop, howl;
    AudioClip axeBite, axeWhoosh;
    AudioClip[] chirps;

    AudioSource player2D;      // shots, reloads, footsteps: heard from the hunter's own position
    AudioSource wind, crickets;
    AudioSource crackle;
    readonly List<AudioSource> pool = new List<AudioSource>();

    float stepTimer;
    float nextBird = 4f;
    float nextHowl = 25f;
    bool ready;

    public static void Ensure(GameObject host)
    {
        if (host.GetComponent<GameAudio>() == null)
            host.AddComponent<GameAudio>();
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        System.Random rng = new System.Random(1234);
        shotgunShot = Clip("Shotgun", Gunshot(rng, 1.9f, 55f, 70f, 9f, 0.42f, 0.32f));
        rifleShot = Clip("Rifle", Gunshot(rng, 2.0f, 85f, 120f, 14f, 0.28f, 0.38f));
        sniperShot = Clip("Sniper", Gunshot(rng, 2.6f, 70f, 140f, 11f, 0.36f, 0.5f));
        breakOpen = Clip("BreakOpen", Mechanical(rng, new[] { 0f, 0.09f }, new[] { 900f, 1500f }, 0.35f));
        shellIn = Clip("ShellIn", Mechanical(rng, new[] { 0f }, new[] { 2200f }, 0.18f));
        boltCycle = Clip("Bolt", Mechanical(rng, new[] { 0f, 0.12f, 0.3f, 0.38f }, new[] { 1700f, 2400f, 2100f, 1300f }, 0.55f));
        hitThud = Clip("Hit", Thud(rng));
        axeBite = Clip("Axe wood impact", WoodImpact(rng));
        axeWhoosh = Clip("Axe swing", SwingAir(rng));
        steps = new AudioClip[4];
        for (int i = 0; i < steps.Length; i++)
            steps[i] = Clip("Step" + i, Footstep(rng));
        chirps = new AudioClip[5];
        for (int i = 0; i < chirps.Length; i++)
            chirps[i] = Clip("Bird" + i, Birdsong(rng, i));
        windLoop = Clip("Wind", Loopable(WindNoise(rng, 8f), 0.6f));
        cricketLoop = Clip("Crickets", Loopable(Crickets(rng, 4f), 0.3f));
        crackleLoop = Clip("Crackle", Loopable(Crackle(rng, 4f), 0.3f));
        howl = Clip("Howl", Howl(rng));

        player2D = MakeSource("HunterAudio", false);
        wind = MakeSource("WindAudio", false, windLoop, true);
        crickets = MakeSource("CricketAudio", false, cricketLoop, true);
        crackle = MakeSource("CampfireAudio", true, crackleLoop, true);
        crackle.maxDistance = 22f;
        for (int i = 0; i < 8; i++)
            pool.Add(MakeSource("WorldAudio" + i, true));
        ready = true;
    }

    // ---------------------------------------------------------------- calls from gameplay

    public static void Gunshot(HuntWeapon weapon)
    {
        if (Instance == null || !Instance.ready)
            return;
        AudioClip clip = weapon == HuntWeapon.Sniper ? Instance.sniperShot : weapon == HuntWeapon.Shotgun ? Instance.shotgunShot : Instance.rifleShot;
        Instance.player2D.pitch = Random.Range(0.96f, 1.04f) * (weapon == HuntWeapon.Pistol ? 1.3f : weapon == HuntWeapon.SMG ? 1.18f : 1f);
        Instance.player2D.PlayOneShot(clip, 1f);
    }

    public static void Reload(HuntWeapon weapon)
    {
        if (Instance == null || !Instance.ready)
            return;
        if (weapon == HuntWeapon.Shotgun)
        {
            Instance.player2D.PlayOneShot(Instance.breakOpen, 0.7f);
            Instance.StartCoroutine(Instance.Later(0.55f, Instance.shellIn, 0.6f));
            Instance.StartCoroutine(Instance.Later(0.78f, Instance.shellIn, 0.6f));
        }
        else
            Instance.player2D.PlayOneShot(Instance.boltCycle, 0.7f);
    }

    public static void Hit()
    {
        if (Instance != null && Instance.ready)
            Instance.player2D.PlayOneShot(Instance.hitThud, 0.55f);
    }

    public static void WoodChop(Vector3 point)
    {
        if (Instance == null || !Instance.ready) return;
        Instance.PlayAt(Instance.axeBite, point, 0.8f, Random.Range(0.94f, 1.06f), 28f);
    }

    public static void AxeWhoosh()
    {
        if (Instance == null || !Instance.ready) return;
        Instance.player2D.PlayOneShot(Instance.axeWhoosh, 0.23f);
    }

    static float[] WoodImpact(System.Random rng)
    {
        float[] data = new float[(int)(Rate * 0.3f)];
        float low = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)Rate;
            float noise = Noise(rng);
            low += 0.16f * (noise - low);
            data[i] = noise * Mathf.Exp(-t * 180f) * 0.5f
                + low * Mathf.Exp(-t * 28f) * 0.8f
                + Mathf.Sin(t * 2f * Mathf.PI * 165f) * Mathf.Exp(-t * 35f) * 0.6f
                + Mathf.Sin(t * 2f * Mathf.PI * 410f) * Mathf.Exp(-t * 55f) * 0.2f;
        }
        return Normalize(data, 0.85f);
    }

    static float[] SwingAir(System.Random rng)
    {
        float[] data = new float[(int)(Rate * 0.17f)];
        float low = 0f;
        for (int i = 0; i < data.Length; i++)
        {
            float t = i / (float)data.Length;
            low += 0.1f * (Noise(rng) - low);
            data[i] = low * Mathf.Sin(t * Mathf.PI);
        }
        return Normalize(data, 0.5f);
    }

    System.Collections.IEnumerator Later(float seconds, AudioClip clip, float volume)
    {
        yield return new WaitForSeconds(seconds);
        player2D.PlayOneShot(clip, volume);
    }

    // ---------------------------------------------------------------- per frame

    void Update()
    {
        if (!ready)
            return;

        Transform listener = PlayerView.Body();
        Vector3 me = listener != null ? listener.position : transform.position;
        bool dead = PlayerHealth.Instance != null && PlayerHealth.Instance.isDead;
        float dt = Time.deltaTime;

        // Footsteps.
        if (!dead && PlayerMove.IsMoving && !GamePause.Active)
        {
            float interval = PlayerMove.IsSprinting ? 0.3f : PlayerMove.IsCrouching ? 0.7f : 0.48f;
            stepTimer -= dt;
            if (stepTimer <= 0f)
            {
                stepTimer = interval;
                float surface = PlayerMove.SurfaceNoise; // 0.75 grass, 1 dirt, 1.3 rock
                float vol = (PlayerMove.IsSprinting ? 0.42f : PlayerMove.IsCrouching ? 0.12f : 0.26f) * Mathf.Clamp(surface, 0.6f, 1.4f);
                player2D.pitch = Random.Range(0.9f, 1.1f) * (surface > 1.2f ? 1.25f : surface < 0.9f ? 0.85f : 1f);
                player2D.PlayOneShot(steps[Random.Range(0, steps.Length)], vol);
                player2D.pitch = 1f;
            }
        }
        else
            stepTimer = 0.05f;

        // Wind follows the wind system.
        float strength = WindSystem.Current != null ? Mathf.Clamp01(WindSystem.Current.Strength) : 0.4f;
        wind.volume = Mathf.Lerp(wind.volume, 0.08f + 0.32f * strength, dt * 0.8f);
        wind.pitch = Mathf.Lerp(wind.pitch, 0.85f + 0.35f * strength, dt * 0.5f);

        // Day birds, night crickets and wolves.
        bool night = DayNightCycle.Instance != null && DayNightCycle.Instance.IsNight;
        crickets.volume = Mathf.Lerp(crickets.volume, night ? 0.16f : 0f, dt * 0.5f);
        if (!night)
        {
            nextBird -= dt;
            if (nextBird <= 0f)
            {
                nextBird = Random.Range(2.5f, 8f);
                Vector2 c = Random.insideUnitCircle.normalized * Random.Range(12f, 40f);
                PlayAt(chirps[Random.Range(0, chirps.Length)], me + new Vector3(c.x, Random.Range(3f, 9f), c.y), Random.Range(0.25f, 0.5f),
                       Random.Range(0.92f, 1.12f), 60f);
            }
        }
        else
        {
            nextHowl -= dt;
            if (nextHowl <= 0f)
            {
                nextHowl = Random.Range(35f, 80f);
                Vector2 c = Random.insideUnitCircle.normalized * Random.Range(90f, 160f);
                PlayAt(howl, me + new Vector3(c.x, 5f, c.y), 0.9f, Random.Range(0.9f, 1.05f), 300f);
            }
        }

        // Campfire crackle at the fire.
        Campfire fire = Campfire.Instance;
        bool lit = fire != null && fire.isLit;
        if (lit)
        {
            crackle.transform.position = fire.FireWorldPosition();
            if (!crackle.isPlaying)
                crackle.Play();
        }
        else if (crackle.isPlaying)
            crackle.Stop();
    }

    void PlayAt(AudioClip clip, Vector3 position, float volume, float pitch, float maxDistance)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            AudioSource s = pool[i];
            if (s.isPlaying)
                continue;
            s.transform.position = position;
            s.clip = clip;
            s.volume = volume;
            s.pitch = pitch;
            s.maxDistance = maxDistance;
            s.Play();
            return;
        }
    }

    AudioSource MakeSource(string name, bool spatial, AudioClip loop = null, bool playLoop = false)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = spatial ? 1f : 0f;
        s.rolloffMode = AudioRolloffMode.Linear;
        s.minDistance = 2f;
        s.maxDistance = 60f;
        s.dopplerLevel = 0f;
        if (loop != null)
        {
            s.clip = loop;
            s.loop = true;
            s.volume = 0f;
            if (playLoop && !spatial)
                s.Play();
        }
        return s;
    }

    static AudioClip Clip(string name, float[] data)
    {
        AudioClip c = AudioClip.Create(name, data.Length, 1, Rate, false);
        c.SetData(data, 0);
        return c;
    }

    // ---------------------------------------------------------------- synthesis

    static float Noise(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

    /// <summary>Crack, body, low thump, then a reverberant tail with two valley echoes.</summary>
    static float[] Gunshot(System.Random r, float seconds, float thumpHz, float crackDecay, float bodyDecay, float tail, float echo)
    {
        int n = (int)(Rate * seconds);
        float[] dry = new float[n];
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = Noise(r);
            lp += 0.35f * (noise - lp);
            lp2 += 0.06f * (noise - lp2);
            float crack = noise * Mathf.Exp(-t * crackDecay);
            float body = lp * Mathf.Exp(-t * bodyDecay) * 1.4f;
            float thump = Mathf.Sin(2f * Mathf.PI * thumpHz * t * (1f - t * 0.6f)) * Mathf.Exp(-t * 16f) * 0.9f;
            float rumble = lp2 * Mathf.Exp(-t * 2.2f) * tail * 3f;
            dry[i] = crack * 0.8f + body + thump + rumble;
        }

        float[] wet = (float[])dry.Clone();
        AddEcho(wet, dry, 0.38f, echo, 0.05f);
        AddEcho(wet, dry, 0.86f, echo * 0.5f, 0.03f);
        return Normalize(wet, 0.95f);
    }

    static void AddEcho(float[] wet, float[] dry, float delay, float gain, float smoothing)
    {
        int d = (int)(delay * Rate);
        float lp = 0f;
        for (int i = d; i < wet.Length; i++)
        {
            lp += smoothing * (dry[i - d] - lp); // echoes come back dull
            wet[i] += lp * gain * 4f;
        }
    }

    /// <summary>Metal-on-metal clicks at the given times, each a short ring at its own pitch.</summary>
    static float[] Mechanical(System.Random r, float[] times, float[] pitches, float seconds)
    {
        int n = (int)(Rate * seconds);
        float[] d = new float[n];
        for (int k = 0; k < times.Length; k++)
        {
            int start = (int)(times[k] * Rate);
            for (int i = start; i < n; i++)
            {
                float t = (i - start) / (float)Rate;
                float env = Mathf.Exp(-t * 70f);
                if (env < 0.001f) break;
                d[i] += (Mathf.Sin(2f * Mathf.PI * pitches[k] * t) * 0.6f + Noise(r) * 0.5f) * env;
            }
        }
        return Normalize(d, 0.8f);
    }

    static float[] Thud(System.Random r)
    {
        int n = (int)(Rate * 0.22f);
        float[] d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            lp += 0.15f * (Noise(r) - lp);
            d[i] = (Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.8f + lp * 1.5f) * Mathf.Exp(-t * 22f);
        }
        return Normalize(d, 0.8f);
    }

    static float[] Footstep(System.Random r)
    {
        int n = (int)(Rate * 0.16f);
        float[] d = new float[n];
        float lp = 0f, hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float x = Noise(r);
            lp += 0.12f * (x - lp);
            hp = 0.9f * (hp + x - prev);
            prev = x;
            float heel = lp * Mathf.Exp(-t * 35f) * 2f;
            float brush = hp * Mathf.Exp(-t * 18f) * 0.25f; // grass and grit under the boot
            d[i] = heel + brush;
        }
        return Normalize(d, 0.7f);
    }

    static float[] Birdsong(System.Random r, int variant)
    {
        int notes = 3 + variant % 4;
        float noteLen = 0.07f + (variant % 3) * 0.02f;
        float gap = 0.05f;
        int n = (int)(Rate * (notes * (noteLen + gap) + 0.1f));
        float[] d = new float[n];
        float baseHz = 2600f + variant * 350f;
        float phase = 0f;
        for (int k = 0; k < notes; k++)
        {
            int start = (int)(k * (noteLen + gap) * Rate);
            float from = baseHz * (1f + 0.25f * (float)r.NextDouble());
            float to = from * (k % 2 == 0 ? 1.35f : 0.8f);
            int len = (int)(noteLen * Rate);
            for (int i = 0; i < len && start + i < n; i++)
            {
                float u = i / (float)len;
                float hz = Mathf.Lerp(from, to, u) * (1f + 0.03f * Mathf.Sin(u * 60f));
                phase += 2f * Mathf.PI * hz / Rate;
                float env = Mathf.Sin(u * Mathf.PI);
                d[start + i] += Mathf.Sin(phase) * env * env;
            }
        }
        return Normalize(d, 0.6f);
    }

    static float[] WindNoise(System.Random r, float seconds)
    {
        int n = (int)(Rate * seconds);
        float[] d = new float[n];
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float gust = 0.55f + 0.45f * Mathf.Sin(t * 0.9f) * Mathf.Sin(t * 0.37f + 1f);
            float cutoff = 0.01f + 0.03f * gust;
            float x = Noise(r);
            lp += cutoff * (x - lp);
            lp2 += cutoff * (lp - lp2);
            d[i] = lp2 * gust * 6f;
        }
        return Normalize(d, 0.6f);
    }

    static float[] Crickets(System.Random r, float seconds)
    {
        int n = (int)(Rate * seconds);
        float[] d = new float[n];
        for (int c = 0; c < 3; c++)
        {
            float hz = 4200f + c * 380f;
            float chirpRate = 2.2f + c * 0.7f;
            float offset = (float)r.NextDouble();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float cycle = Mathf.Repeat(t * chirpRate + offset, 1f);
                if (cycle > 0.35f) continue;
                float pulse = Mathf.Sin(cycle / 0.35f * Mathf.PI) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 45f * t));
                d[i] += Mathf.Sin(2f * Mathf.PI * hz * t) * pulse * 0.33f;
            }
        }
        return Normalize(d, 0.5f);
    }

    static float[] Crackle(System.Random r, float seconds)
    {
        int n = (int)(Rate * seconds);
        float[] d = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            lp += 0.02f * (Noise(r) - lp);
            d[i] = lp * 0.5f; // low roar of the flames
        }
        for (int k = 0; k < (int)(seconds * 14f); k++)
        {
            int at = r.Next(n - Rate / 20);
            float pitch = 1500f + (float)r.NextDouble() * 3000f;
            for (int i = 0; i < Rate / 20; i++)
            {
                float t = i / (float)Rate;
                d[at + i] += (Noise(r) * 0.7f + Mathf.Sin(2f * Mathf.PI * pitch * t) * 0.3f) * Mathf.Exp(-t * 160f);
            }
        }
        return Normalize(d, 0.6f);
    }

    static float[] Howl(System.Random r)
    {
        int n = (int)(Rate * 3.2f);
        float[] d = new float[n];
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float u = t / 3.2f;
            float hz = Mathf.Lerp(380f, 640f, Mathf.SmoothStep(0f, 1f, u * 3f)) - Mathf.Max(0f, u - 0.6f) * 520f;
            hz *= 1f + 0.012f * Mathf.Sin(t * 33f);
            phase += 2f * Mathf.PI * hz / Rate;
            float env = Mathf.SmoothStep(0f, 1f, u * 6f) * Mathf.SmoothStep(1f, 0f, (u - 0.75f) * 4f);
            lp += 0.08f * (Noise(r) - lp);
            float voice = Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 2f) + 0.12f * Mathf.Sin(phase * 3f);
            d[i] = (voice + lp * 0.4f) * env;
        }
        return Normalize(d, 0.7f);
    }

    /// <summary>Crossfades the end into the start so the clip loops without a click.</summary>
    static float[] Loopable(float[] d, float fadeSeconds)
    {
        int f = Mathf.Min(d.Length / 2, (int)(fadeSeconds * Rate));
        int n = d.Length - f;
        float[] o = new float[n];
        System.Array.Copy(d, o, n);
        for (int i = 0; i < f; i++)
        {
            float a = i / (float)f;
            o[i] = d[i] * a + d[n + i] * (1f - a);
        }
        return o;
    }

    static float[] Normalize(float[] d, float peak)
    {
        float max = 0.0001f;
        for (int i = 0; i < d.Length; i++)
            max = Mathf.Max(max, Mathf.Abs(d[i]));
        float k = peak / max;
        for (int i = 0; i < d.Length; i++)
            d[i] *= k;
        return d;
    }
}
