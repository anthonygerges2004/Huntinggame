// Live sky for the hunting game: atmosphere gradient driven by the sun, sun disk and halo,
// warm horizon glow at sunrise/sunset, drifting sun-lit clouds, stars and a moon at night,
// and a horizon that melts into the scene fog. Parameters come from SkyDome.cs.
Shader "HuntingGame/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.2, 0.42, 0.85, 1)
        _HorizonColor ("Horizon", Color) = (0.65, 0.78, 0.92, 1)
        _GroundColor ("Ground", Color) = (0.32, 0.33, 0.3, 1)
        _SunColor ("Sun", Color) = (1, 0.95, 0.85, 1)
        _SunDir ("Sun Direction", Vector) = (0, 0.5, 0.8, 0)
        _SunSize ("Sun Size", Range(0.0001, 0.01)) = 0.0012
        _GlowAmount ("Horizon Glow", Range(0, 2)) = 0
        _Night ("Night", Range(0, 1)) = 0
        _CloudCover ("Cloud Cover", Range(0, 1)) = 0.4
        _CloudSpeed ("Cloud Speed", Float) = 0.004
        _CloudShade ("Cloud Shadow Colour", Color) = (0.55, 0.6, 0.68, 1)
        _FogBlend ("Horizon Fog Blend", Range(0, 1)) = 0.85
        _Exposure ("Exposure", Range(0.2, 3)) = 1.1
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            float4 _ZenithColor, _HorizonColor, _GroundColor, _SunColor, _CloudShade;
            float4 _SunDir;
            float _SunSize, _GlowAmount, _Night, _CloudCover, _CloudSpeed, _FogBlend, _Exposure;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float hash31(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), u.x),
                            lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                float2x2 rot = float2x2(0.8, -0.6, 0.6, 0.8);
                for (int k = 0; k < 5; k++)
                {
                    v += a * noise(p);
                    p = mul(rot, p) * 2.03 + 11.7;
                    a *= 0.5;
                }
                return v;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float3 sun = normalize(_SunDir.xyz);
                float h = dir.y;
                float sunDot = dot(dir, sun);

                // Atmosphere: deep zenith fading to a bright, hazy horizon.
                float up = saturate(h);
                float3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, 0.42));
                sky += _HorizonColor.rgb * 0.18 * exp(-up * 9.0) * (1.0 - _Night);

                // Mie scattering around the sun.
                float mie = pow(saturate(sunDot), 6.0) * 0.28 + pow(saturate(sunDot), 64.0) * 0.55;
                sky += _SunColor.rgb * mie * (1.0 - _Night * 0.9);

                // Sunrise/sunset: a warm band on the sun's side of the horizon.
                float2 flatDir = normalize(dir.xz + 1e-5);
                float2 flatSun = normalize(sun.xz + 1e-5);
                float side = pow(saturate(dot(flatDir, flatSun) * 0.5 + 0.5), 2.5);
                sky += _SunColor.rgb * _GlowAmount * side * exp(-abs(h) * 6.0) * 0.9;

                // Below the horizon: ground haze.
                float below = saturate(-h * 6.0);
                sky = lerp(sky, _GroundColor.rgb, below);

                // Stars and moon at night.
                if (_Night > 0.01 && h > 0.0)
                {
                    float3 p = dir * 420.0;
                    float3 cell = floor(p);
                    float r = hash31(cell);
                    float star = step(0.9975, r);
                    float d = length(p - (cell + 0.5));
                    float twinkle = 0.6 + 0.4 * sin(_Time.y * (2.0 + r * 6.0) + r * 40.0);
                    float s = star * saturate(1.0 - d * 1.6) * twinkle;
                    sky += float3(0.9, 0.95, 1.0) * s * _Night * saturate(h * 4.0) * 1.6;

                    float3 moonDir = normalize(float3(-sun.x, abs(sun.y) * 0.8 + 0.25, -sun.z));
                    float md = dot(dir, moonDir);
                    float moonDisk = smoothstep(0.99955, 0.99975, md);
                    float moonGlow = pow(saturate(md), 180.0) * 0.25 + pow(saturate(md), 18.0) * 0.06;
                    sky += float3(0.85, 0.9, 1.0) * (moonDisk * 1.4 + moonGlow) * _Night;
                }

                // Clouds on a curved layer above the world, lit from the sun.
                if (h > 0.0)
                {
                    float2 uv = dir.xz / (h + 0.18) * 1.6;
                    float2 drift = float2(_Time.y, _Time.y * 0.35) * _CloudSpeed;
                    float n = fbm(uv + drift);
                    float coverage = lerp(0.72, 0.28, _CloudCover);
                    float cloud = smoothstep(coverage, coverage + 0.22, n);
                    float toward = fbm(uv + drift + sun.xz * 0.12);
                    float lit = saturate(0.55 + (n - toward) * 3.0);
                    float3 dayCloud = lerp(_CloudShade.rgb, float3(1, 1, 1), lit);
                    float3 cloudCol = lerp(dayCloud * lerp(float3(1, 1, 1), _SunColor.rgb, 0.45),
                                           _ZenithColor.rgb * 1.6 + 0.02, _Night);
                    cloudCol += _SunColor.rgb * pow(saturate(sunDot), 12.0) * (1.0 - cloud) * 0.6 * (1.0 - _Night);
                    float fade = saturate(h * 5.0);
                    sky = lerp(sky, cloudCol, cloud * fade * 0.92);
                }

                // Sun disk.
                float disk = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.6, sunDot);
                sky += _SunColor.rgb * disk * 18.0 * (1.0 - _Night) * saturate(h * 30.0 + 0.5);

                // Melt the horizon into the scene fog so the land meets the sky seamlessly.
                float fogW = exp(-max(h, 0.0) * 10.0) * _FogBlend;
                sky = lerp(sky, unity_FogColor.rgb, saturate(fogW));

                return float4(sky * _Exposure, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
