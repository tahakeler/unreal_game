// Stylized animated river (art bible: storybook, flat colour bands, cream foam). UV.x = across the river (0..1, banks
// at the ends), UV.y = downstream metres. Visual overhaul: depth tint from the camera depth texture (shallow teal over
// sand and rocks -> deep blue in the channel), broken cream shoreline foam wherever the surface meets geometry (banks,
// rocks, bridge footings), flow streaks, sky-coloured fresnel reflection from the ambient probe, a stepped sun glint,
// main-light shadows (trees and the bridge darken the water) and lantern light at night. One transparent pass.
// Needs the URP depth texture (HP_URP "Depth Texture" on); without it the water falls back to the UV-based banding.
Shader "HarvestPanic/StylizedWater"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.42, 0.78, 0.74, 1)
        _DeepColor ("Deep", Color) = (0.16, 0.46, 0.55, 1)
        _FoamColor ("Foam", Color) = (1, 0.96, 0.86, 1)
        _FlowSpeed ("Flow speed (m/s)", Float) = 2.5
        _StripeScale ("Stripe scale", Float) = 0.35
        _BankFoam ("Bank foam width (0..0.5)", Range(0, 0.5)) = 0.06
        _Alpha ("Alpha", Range(0, 1)) = 0.9
        _DepthFade ("Depth for full deep colour (m)", Float) = 2.2
        _ShoreFoamDepth ("Shore foam depth (m)", Float) = 0.45
        _ReflectStrength ("Sky reflection", Range(0, 1)) = 0.3
        _GlintStrength ("Sun glint", Range(0, 4)) = 1.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Art/Shaders/Stylized/HP_StylizedLighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FoamColor;
                float _FlowSpeed, _StripeScale, _BankFoam, _Alpha;
                float _DepthFade, _ShoreFoamDepth, _ReflectStrength, _GlintStrength;
            CBUFFER_END

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float3 n : NORMAL; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; float fog : TEXCOORD2; float4 screen : TEXCOORD3; };

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + 1), f.x), f.y);
            }

            V vert(A a)
            {
                V o;
                float3 w = TransformObjectToWorld(a.pos.xyz);
                w.y += sin(_Time.y * 1.3 + w.x * 0.35 + w.z * 0.2) * 0.05;
                o.wpos = w;
                o.pos = TransformWorldToHClip(w);
                o.uv = a.uv;
                o.fog = ComputeFogFactor(o.pos.z);
                o.screen = ComputeScreenPos(o.pos);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float across = i.uv.x;
                float down = i.uv.y + _Time.y * _FlowSpeed;
                float centre = 1 - abs(across * 2 - 1);                 // 0 at banks, 1 mid-river

                // Water depth along the view ray from the depth texture (missing texture -> UV banding only).
                float2 suv = i.screen.xy / i.screen.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float thick = sceneEye - i.screen.w;
                bool hasDepth = thick > -0.01 && sceneEye < _ProjectionParams.z * 0.98;
                float depthT = hasDepth ? saturate(thick / _DepthFade) : smoothstep(0.05, 0.7, centre);
                depthT = max(depthT, smoothstep(0.15, 0.9, centre) * 0.6);
                half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.0, 1.0, depthT));

                // Ripple normal from drifting noise (cheap finite difference).
                float2 rp = i.wpos.xz * 0.9 + float2(0, -_Time.y * _FlowSpeed * 0.35);
                float h0 = vnoise(rp), hx = vnoise(rp + float2(0.15, 0)), hz = vnoise(rp + float2(0, 0.15));
                half3 n = normalize(half3((h0 - hx) * 0.9, 1.0, (h0 - hz) * 0.9));

                // Flowing streaks: stretched noise, stepped for a painted look.
                float sn = vnoise(float2(across * 38, down * _StripeScale));
                float sn2 = vnoise(float2(across * 13 + 3, down * _StripeScale * 0.5 + 11));
                float streak = step(0.82, sn * 0.6 + sn2 * 0.4);

                // Lighting: ambient sky + sun with shadows (trees/bridge darken the water), lanterns at night.
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.wpos));
                half sh = smoothstep(0.0, 1.0, sun.shadowAttenuation);
                half3 ambient = SampleSH(half3(0, 1, 0)) + _HP_LookParams.z;
                half3 light = ambient * lerp(half3(0.8, 0.82, 0.95), 1.0, sh) + sun.color * (0.35 + 0.4 * sh) * saturate(sun.direction.y + 0.2);
                #if defined(_ADDITIONAL_LIGHTS)
                {
                    InputData inputData = (InputData)0;
                    inputData.positionWS = i.wpos;
                    inputData.normalizedScreenSpaceUV = suv;
                    uint pixelLightCount = GetAdditionalLightsCount();
                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint li = 0; li < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li++)
                        light += HP_AdditionalLight(GetAdditionalLight(li, i.wpos), half3(0, 1, 0));
                    #endif
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        light += HP_AdditionalLight(GetAdditionalLight(lightIndex, i.wpos), half3(0, 1, 0));
                    LIGHT_LOOP_END
                }
                #endif
                col *= light;

                // Sky reflection (fresnel) from the ambient probe: the river picks up the sky colour at grazing angles.
                half3 viewDir = normalize(GetWorldSpaceViewDir(i.wpos));
                half fres = pow(1.0 - saturate(dot(n, viewDir)), 5.0);
                half3 skyRefl = SampleSH(reflect(-viewDir, n));
                col = lerp(col, skyRefl, saturate(fres * _ReflectStrength));

                // Foam: flow streaks, bank band (UV) and shoreline/contact foam (depth), broken by noise.
                half3 foam = _FoamColor.rgb * light;
                col = lerp(col, foam, streak * 0.22);
                float edge = min(across, 1 - across);
                float wob = vnoise(float2(down * 0.8, across * 3)) * 0.04;
                float bank = step(edge, _BankFoam + wob);
                float shore = hasDepth ? 1.0 - saturate(thick / _ShoreFoamDepth) : 0.0;
                float broken = vnoise(i.wpos.xz * 2.2 + float2(_Time.y * 0.4, -_Time.y * _FlowSpeed * 0.2));
                float shoreFoam = step(0.35 + broken * 0.5, shore);
                col = lerp(col, foam, saturate(bank + shoreFoam));

                // Stepped sun glint on the ripples (moon glint at night), hidden in shadow.
                half3 R = reflect(-sun.direction, n);
                half g = pow(saturate(dot(R, viewDir)), 220.0);
                col += sun.color * step(0.35, g) * _GlintStrength * sh;

                col = MixFog(col, i.fog);
                half alpha = lerp(_Alpha * 0.55, _Alpha, smoothstep(0.0, 0.5, depthT));
                alpha = max(alpha, saturate(bank + shoreFoam));
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
