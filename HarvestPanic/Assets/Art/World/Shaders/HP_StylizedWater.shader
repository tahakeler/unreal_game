// Stylized animated river (art bible: storybook, flat colour bands, cream foam). Cheap: one pass, no depth texture,
// no reflections. UV.x = across the river (0..1, banks at the ends), UV.y = downstream metres.
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _FoamColor;
                float _FlowSpeed, _StripeScale, _BankFoam, _Alpha;
            CBUFFER_END

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; float3 n : NORMAL; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wpos : TEXCOORD1; float fog : TEXCOORD2; };

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
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float across = i.uv.x;
                float down = i.uv.y + _Time.y * _FlowSpeed;
                float centre = 1 - abs(across * 2 - 1);                 // 0 at banks, 1 mid-river
                half3 col = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.05, 0.7, centre));
                // Flowing streaks: stretched noise, stepped for a painted look.
                float n = vnoise(float2(across * 18, down * _StripeScale));
                float n2 = vnoise(float2(across * 7 + 3, down * _StripeScale * 0.5 + 11));
                float streak = step(0.78, n * 0.6 + n2 * 0.4);
                col = lerp(col, _FoamColor.rgb, streak * 0.55);
                // Bank foam with a wobbly edge.
                float edge = min(across, 1 - across);
                float wob = vnoise(float2(down * 0.8, across * 3)) * 0.04;
                col = lerp(col, _FoamColor.rgb, step(edge, _BankFoam + wob));
                // Soft sun term so the river reacts to the golden light without full PBR.
                Light sun = GetMainLight();
                col *= lerp(0.85, 1.1, saturate(sun.direction.y)) * lerp(half3(1, 1, 1), sun.color, 0.35);
                col = MixFog(col, i.fog);
                return half4(col, _Alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
