// HARVEST PANIC - "HarvestPanic/Stylized Sky": storybook gradient sky (art bible 3: "stylized gradient sky") with a
// soft sun/moon disc and glow that follow URP's main light, painted two-tone procedural clouds that drift, and stars
// for the night. No textures, one pass, drawn only where no geometry covers the sky.
// Driven per phase/weather by HarvestPanic.Weather.WeatherLookApplier (_ZenithColor, _HorizonColor, _CloudCover, ...).
// The URP procedural-sky names (_SkyTint, _Exposure, _GroundColor, _SunSize, ...) are accepted so older code paths and
// the scene builder can still set them; _SkyTint multiplies the gradient.
Shader "HarvestPanic/Stylized Sky"
{
    Properties
    {
        _ZenithColor("Zenith", Color) = (0.36, 0.58, 0.86, 1)
        _HorizonColor("Horizon", Color) = (0.86, 0.90, 0.92, 1)
        _GroundColor("Below Horizon", Color) = (0.62, 0.66, 0.60, 1)
        _HorizonExponent("Gradient Curve", Range(0.1, 2.0)) = 0.55
        [HDR] _SunDiscColor("Sun / Moon Disc", Color) = (6.0, 5.4, 4.4, 1)
        _SunDiscSize("Disc Size (degrees)", Range(0.2, 6.0)) = 1.6
        _SunGlowColor("Glow Colour", Color) = (1.0, 0.86, 0.62, 1)
        _SunGlow("Glow Strength", Range(0.0, 2.0)) = 0.45
        _SunGlowPower("Glow Tightness", Range(1.0, 64.0)) = 8.0
        _CloudColor("Cloud Lit", Color) = (1.0, 0.98, 0.94, 1)
        _CloudShadeColor("Cloud Shade", Color) = (0.72, 0.78, 0.88, 1)
        _CloudCover("Cloud Cover", Range(0.0, 1.0)) = 0.38
        _CloudSoftness("Cloud Edge Softness", Range(0.005, 0.4)) = 0.035
        _CloudScale("Cloud Scale", Float) = 2.2
        _CloudSpeed("Cloud Drift (xy)", Vector) = (0.006, 0.002, 0, 0)
        _CloudOpacity("Cloud Opacity", Range(0.0, 1.0)) = 0.95
        _StarIntensity("Stars", Range(0.0, 2.0)) = 0.0
        _Exposure("Exposure", Range(0.0, 4.0)) = 1.0
        _SkyTint("Tint (legacy)", Color) = (1, 1, 1, 1)
        [HideInInspector] _AtmosphereThickness("unused", Float) = 1.0
        [HideInInspector] _SunSize("unused", Float) = 0.04
        [HideInInspector] _SunSizeConvergence("unused", Float) = 5.0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ZenithColor, _HorizonColor, _GroundColor, _SunDiscColor, _SunGlowColor, _CloudColor, _CloudShadeColor, _SkyTint;
                float4 _CloudSpeed;
                half _HorizonExponent, _SunDiscSize, _SunGlow, _SunGlowPower, _CloudCover, _CloudSoftness, _CloudScale, _CloudOpacity;
                half _StarIntensity, _Exposure, _AtmosphereThickness, _SunSize, _SunSizeConvergence;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            V vert(A a)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(a);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(a.positionOS.xyz);
                o.dir = a.positionOS.xyz;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float Hash3(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + 1), f.x), f.y);
            }
            float Fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                [unroll] for (int k = 0; k < 5; k++) { v += a * Noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v / 0.96875;
            }

            half4 frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                float3 L = normalize(_MainLightPosition.xyz);

                // Gradient: horizon band -> zenith, quick fade to the ground colour just below the horizon.
                float t = pow(saturate(y), _HorizonExponent);
                half3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, t);
                sky = lerp(sky, _GroundColor.rgb, smoothstep(0.0, -0.06, y));
                sky *= _SkyTint.rgb;

                // Sun / moon glow, stronger near the horizon (golden hour, coral dusk).
                float cosA = dot(d, L);
                half glow = pow(saturate(cosA), _SunGlowPower) * _SunGlow * (1.0 + 0.8 * (1.0 - saturate(y * 3.0)));
                sky += _SunGlowColor.rgb * glow;

                // Stars (night): round points in direction cells, gently twinkling, faded near the horizon.
                if (_StarIntensity > 0.001)
                {
                    float3 sd = d * 220.0;
                    float3 cell = floor(sd);
                    float h = Hash3(cell);
                    float r = length(frac(sd) - 0.5);
                    float star = step(0.9955, h) * smoothstep(0.32, 0.05, r);
                    star *= 0.6 + 0.4 * sin(_Time.y * (1.5 + h * 3.0) + h * 40.0);
                    sky += star * _StarIntensity * smoothstep(0.02, 0.25, y);
                }

                // Disc: hard-ish edge, HDR so bloom gives it a soft halo.
                float discCos = cos(radians(_SunDiscSize * 0.5));
                half disc = smoothstep(discCos - 0.00012, discCos + 0.00012, cosA) * smoothstep(-0.02, 0.02, y);
                sky = lerp(sky, _SunDiscColor.rgb, disc);

                // Painted cumulus on a high flat cloud layer: a slow large-scale "weather" mask clusters them into
                // islands, a domain-warped fbm gives puffy edges, the threshold is crisp (storybook, not photo), and a
                // second sample offset toward the light splits each cloud into a bright sun side and a cool shade side.
                if (y > 0.0 && _CloudCover > 0.001)
                {
                    float2 uv = d.xz / (y + 0.32) * _CloudScale + _Time.y * _CloudSpeed.xy;
                    float2 warp = float2(Noise(uv * 0.7 + 3.1), Noise(uv * 0.7 + 9.7)) - 0.5;
                    float2 cuv = uv + warp * 0.55;
                    float cluster = Noise(uv * 0.23 + 41.0);
                    float n = Fbm(cuv) * 0.75 + cluster * 0.45 - 0.1;
                    float edge = 1.0 - _CloudCover * 1.05;   // cover 0.4 ~ a third of the sky, 0.9+ overcast
                    float c = smoothstep(edge, edge + _CloudSoftness, n);
                    float nSun = Fbm(cuv + L.xz * 0.18) * 0.75 + cluster * 0.45 - 0.1;
                    half shade = smoothstep(-0.02, 0.05, n - nSun); // density falling toward the light = lit side
                    half core = smoothstep(edge + 0.05, edge + 0.3, n);  // thick cores slightly darker (volume)
                    half3 cloud = lerp(_CloudShadeColor.rgb, _CloudColor.rgb, saturate(shade + (1.0 - core) * 0.35));
                    cloud += _SunGlowColor.rgb * glow * 0.6;
                    half fade = smoothstep(0.02, 0.2, y);
                    sky = lerp(sky, cloud, c * fade * _CloudOpacity * (1.0 - disc * 0.6));
                }

                return half4(sky * _Exposure, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
