using UnityEngine;

/// <summary>Procedural fire look: log ring, flames, smoke, embers, flickering light.</summary>
public class CampfireFireEffects : MonoBehaviour
{
    Light fireLight;
    float baseIntensity = 2.4f;
    float baseRange = 8f;
    float noiseSeed;
    static Texture2D softParticleTex;
    static Texture2D softSmokeTex;

    public static void Setup(GameObject root, Light light, float safeRadius)
    {
        if (root == null || light == null)
            return;

        CampfireFireEffects fx = root.GetComponent<CampfireFireEffects>();
        if (fx == null)
            fx = root.AddComponent<CampfireFireEffects>();
        fx.Build(light, safeRadius);
    }

    void Build(Light light, float safeRadius)
    {
        fireLight = light;
        baseRange = safeRadius;
        baseIntensity = 2.6f;
        noiseSeed = Random.Range(0f, 100f);

        fireLight.color = new Color(1f, 0.52f, 0.18f);
        fireLight.intensity = baseIntensity;
        fireLight.range = baseRange;
        fireLight.shadows = LightShadows.None;

        BuildLogRing();
        CreateFlames(transform);
        CreateSmoke(transform);
        CreateEmbers(transform);
    }

    void Update()
    {
        if (fireLight == null)
            return;

        float t = Time.time;
        float slow = Mathf.PerlinNoise(t * 4f, noiseSeed);
        float fast = Mathf.PerlinNoise(t * 11f, noiseSeed + 2.7f);
        float flicker = 0.82f + slow * 0.28f + fast * 0.12f;
        fireLight.intensity = baseIntensity * flicker;
        fireLight.range = baseRange * (0.94f + slow * 0.08f);

        float hueShift = (fast - 0.5f) * 0.06f;
        fireLight.color = new Color(1f, 0.48f + hueShift, 0.14f + hueShift * 0.5f);
    }

    void BuildLogRing()
    {
        Material logMat = Resources.Load<Material>("Building/LogMat");
        if (logMat == null)
            return;

        const int count = 7;
        const float radius = 0.42f;
        for (int i = 0; i < count; i++)
        {
            float angle = i * (360f / count) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0.04f, Mathf.Sin(angle) * radius);

            GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = "CampLog";
            log.transform.SetParent(transform, false);
            log.transform.localPosition = offset;
            log.transform.localScale = new Vector3(0.11f, 0.05f, 0.11f);
            log.transform.localRotation = Quaternion.Euler(90f, i * (360f / count), 0f);

            Collider col = log.GetComponent<Collider>();
            if (col != null)
                Destroy(col);

            MeshRenderer renderer = log.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = logMat;
        }
    }

    static Texture2D SoftParticleTexture()
    {
        if (softParticleTex != null)
            return softParticleTex;

        const int size = 64;
        softParticleTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Campfire_SoftParticle",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - dist);
                alpha *= alpha;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        softParticleTex.SetPixels(pixels);
        softParticleTex.Apply();
        return softParticleTex;
    }

    static Texture2D SoftSmokeTexture()
    {
        if (softSmokeTex != null)
            return softSmokeTex;

        const int size = 128;
        softSmokeTex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Campfire_SoftSmoke",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = alpha * alpha * alpha;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        softSmokeTex.SetPixels(pixels);
        softSmokeTex.Apply();
        return softSmokeTex;
    }

    static Material ParticleMaterial(Color tint, bool additive, bool softSmoke = false)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return null;

        Material mat = new Material(shader);
        mat.name = additive ? "Campfire_Flame" : "Campfire_Smoke";
        Texture2D tex = softSmoke ? SoftSmokeTexture() : SoftParticleTexture();
        if (mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", tint);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", tint);

        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f);
        if (additive)
        {
            if (mat.HasProperty("_Blend"))
                mat.SetFloat("_Blend", 1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ADD");
        }
        else
        {
            if (mat.HasProperty("_Blend"))
                mat.SetFloat("_Blend", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ALPHA");
        }

        mat.renderQueue = 3000;
        return mat;
    }

    static void CreateFlames(Transform parent)
    {
        Material mat = ParticleMaterial(new Color(1f, 0.55f, 0.12f, 0.85f), additive: true);
        if (mat == null)
            return;

        GameObject go = new GameObject("Flames");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.12f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 2f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.85f, 0.2f, 0.9f),
            new Color(1f, 0.35f, 0.05f, 0.75f));
        main.maxParticles = 48;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = -0.15f;

        var emission = ps.emission;
        emission.rateOverTime = 28f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.22f;
        shape.arc = 360f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.35f), 0f),
                new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.55f),
                new GradientColorKey(new Color(0.25f, 0.25f, 0.25f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.95f, 0.12f),
                new GradientAlphaKey(0.55f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.25f, 1f),
            new Keyframe(1f, 0.15f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    static void CreateSmoke(Transform parent)
    {
        CreateSmokeLayer(
            parent,
            "SmokePlume",
            localY: 0.22f,
            rate: 14f,
            maxParticles: 48,
            lifetimeMin: 2.4f,
            lifetimeMax: 4.2f,
            speedMin: 0.55f,
            speedMax: 1.05f,
            sizeMin: 0.12f,
            sizeMax: 0.28f,
            shapeRadius: 0.14f,
            alphaPeak: 0.38f,
            warmCore: true);

        CreateSmokeLayer(
            parent,
            "SmokeHaze",
            localY: 0.45f,
            rate: 4f,
            maxParticles: 16,
            lifetimeMin: 4f,
            lifetimeMax: 6.5f,
            speedMin: 0.18f,
            speedMax: 0.42f,
            sizeMin: 0.55f,
            sizeMax: 1.05f,
            shapeRadius: 0.22f,
            alphaPeak: 0.16f,
            warmCore: false);
    }

    static void CreateSmokeLayer(
        Transform parent,
        string objectName,
        float localY,
        float rate,
        int maxParticles,
        float lifetimeMin,
        float lifetimeMax,
        float speedMin,
        float speedMax,
        float sizeMin,
        float sizeMax,
        float shapeRadius,
        float alphaPeak,
        bool warmCore)
    {
        Material mat = ParticleMaterial(new Color(1f, 1f, 1f, alphaPeak), additive: false, softSmoke: true);
        if (mat == null)
            return;

        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, localY, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = warmCore
            ? new Color(0.98f, 0.96f, 0.93f, alphaPeak * 0.85f)
            : new Color(0.94f, 0.94f, 0.96f, alphaPeak);
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.gravityModifier = warmCore ? -0.03f : -0.015f;

        var emission = ps.emission;
        emission.rateOverTime = rate;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = shapeRadius;
        shape.radiusThickness = 0.65f;

        ApplySmokeColorOverLifetime(ps, alphaPeak, warmCore);
        ApplySmokeSizeOverLifetime(ps, warmCore);
        ApplySmokeTurbulence(ps, warmCore);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.35f, 0.75f),
            new Keyframe(1f, 0.25f)));
        velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.22f;
        limit.drag = 0.15f;
        limit.separateAxes = false;
        limit.limit = new ParticleSystem.MinMaxCurve(warmCore ? 1.4f : 0.75f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingFudge = -4f;
    }

    static void ApplySmokeColorOverLifetime(ParticleSystem ps, float alphaPeak, bool warmCore)
    {
        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;

        Gradient gradient = new Gradient();
        if (warmCore)
        {
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.98f, 0.95f, 0.9f), 0f),
                    new GradientColorKey(new Color(0.88f, 0.88f, 0.9f), 0.45f),
                    new GradientColorKey(new Color(0.72f, 0.74f, 0.78f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(alphaPeak, 0.08f),
                    new GradientAlphaKey(alphaPeak * 0.75f, 0.35f),
                    new GradientAlphaKey(alphaPeak * 0.35f, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                });
        }
        else
        {
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.92f, 0.92f, 0.94f), 0f),
                    new GradientColorKey(new Color(0.78f, 0.8f, 0.84f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(alphaPeak * 0.6f, 0.15f),
                    new GradientAlphaKey(alphaPeak * 0.45f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
        }

        colorOverLifetime.color = gradient;
    }

    static void ApplySmokeSizeOverLifetime(ParticleSystem ps, bool warmCore)
    {
        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = warmCore
            ? new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.2f, 0.75f),
                new Keyframe(0.55f, 1.35f),
                new Keyframe(1f, 2.1f))
            : new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.25f, 1f),
                new Keyframe(0.65f, 1.65f),
                new Keyframe(1f, 2.4f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
    }

    static void ApplySmokeTurbulence(ParticleSystem ps, bool warmCore)
    {
        var noise = ps.noise;
        noise.enabled = true;
        noise.separateAxes = true;
        noise.strength = warmCore ? 0.28f : 0.42f;
        noise.strengthX = warmCore ? 0.22f : 0.35f;
        noise.strengthY = warmCore ? 0.12f : 0.18f;
        noise.strengthZ = warmCore ? 0.22f : 0.35f;
        noise.frequency = warmCore ? 0.55f : 0.35f;
        noise.scrollSpeed = warmCore ? 0.35f : 0.22f;
        noise.damping = true;
        noise.octaveCount = warmCore ? 2 : 3;
        noise.quality = ParticleSystemNoiseQuality.Medium;
    }

    static void CreateEmbers(Transform parent)
    {
        Material mat = ParticleMaterial(new Color(1f, 0.65f, 0.2f, 1f), additive: true);
        if (mat == null)
            return;

        GameObject go = new GameObject("Embers");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.2f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new Color(1f, 0.7f, 0.25f, 1f);
        main.maxParticles = 18;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.12f;

        var emission = ps.emission;
        emission.rateOverTime = 6f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }
}
