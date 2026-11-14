using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Bounded, reusable impact bursts. Nearby shotgun pellets share an effect.</summary>
public sealed class BulletImpactPool : MonoBehaviour
{
    sealed class Impact
    {
        public Transform root;
        public ParticleSystem spray;
        public LineRenderer ring;
        public float age;
        public bool water;
    }
    readonly Impact[] pool = new Impact[16];
    Transform root;
    Material material;
    int cursor;
    Vector3 lastPoint;
    float lastTime = -10f;

    public void Play(Vector3 point, Vector3 normal, bool water)
    {
        if (Time.time - lastTime < 0.045f && (point - lastPoint).sqrMagnitude < 0.25f) return;
        lastPoint = point;
        lastTime = Time.time;
        if (root == null) root = new GameObject("Bullet impact pool").transform;
        Impact impact = pool[cursor] ?? (pool[cursor] = Build());
        cursor = (cursor + 1) % pool.Length;
        impact.age = 0f;
        impact.water = water;
        impact.root.gameObject.SetActive(true);
        impact.root.SetPositionAndRotation(point + normal * 0.025f, Quaternion.FromToRotation(Vector3.up, normal));
        impact.spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = impact.spray.main;
        main.startColor = water ? new Color(0.72f, 0.9f, 1f, 0.8f) : new Color(0.42f, 0.32f, 0.19f, 0.75f);
        main.startSpeed = water ? new ParticleSystem.MinMaxCurve(1.5f, 3.8f) : new ParticleSystem.MinMaxCurve(0.3f, 1.4f);
        main.startSize = water ? new ParticleSystem.MinMaxCurve(0.025f, 0.07f) : new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.gravityModifier = water ? 0.8f : 0.25f;
        impact.spray.Emit(water ? 14 : 9);
        PoseRing(impact);
        GameAudio.BulletImpact(point, water);
    }

    Impact Build()
    {
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            material = new Material(shader);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
        }
        var impact = new Impact();
        impact.root = new GameObject("Impact").transform;
        impact.root.SetParent(root, false);
        GameObject spray = new GameObject("Dust or splash");
        spray.transform.SetParent(impact.root, false);
        spray.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        impact.spray = spray.AddComponent<ParticleSystem>();
        impact.spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = impact.spray.main;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = 24;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = impact.spray.emission;
        emission.enabled = false;
        var shape = impact.spray.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = 0.035f;
        var fade = impact.spray.colorOverLifetime;
        fade.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        var renderer = spray.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        GameObject ring = new GameObject("Ripple or impact mark");
        ring.transform.SetParent(impact.root, false);
        impact.ring = ring.AddComponent<LineRenderer>();
        impact.ring.sharedMaterial = material;
        impact.ring.useWorldSpace = false;
        impact.ring.loop = true;
        impact.ring.positionCount = 24;
        impact.ring.shadowCastingMode = ShadowCastingMode.Off;
        impact.ring.receiveShadows = false;
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI * 2f / 24;
            impact.ring.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
        }
        return impact;
    }

    void Update()
    {
        foreach (Impact impact in pool)
        {
            if (impact == null || !impact.root.gameObject.activeSelf) continue;
            impact.age += Time.deltaTime;
            if (impact.age >= (impact.water ? 1.2f : 2f)) impact.root.gameObject.SetActive(false);
            else PoseRing(impact);
        }
    }

    static void PoseRing(Impact impact)
    {
        float radius = impact.water ? Mathf.Lerp(0.06f, 0.8f, impact.age / 1.2f) : 0.045f;
        impact.ring.transform.localScale = Vector3.one * radius;
        impact.ring.widthMultiplier = impact.water ? 0.018f : 0.025f;
        Color color = impact.water ? new Color(0.75f, 0.92f, 1f, 0.7f) : new Color(0.16f, 0.11f, 0.07f, 0.85f);
        color.a *= 1f - impact.age / (impact.water ? 1.2f : 2f);
        impact.ring.startColor = impact.ring.endColor = color;
    }

    void OnDestroy()
    {
        if (root != null) { if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject); }
        if (material != null) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
    }
}
