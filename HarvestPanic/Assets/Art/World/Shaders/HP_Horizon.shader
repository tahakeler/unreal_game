// Distant hill silhouettes ring (beyond the scenery terrain). Plain distance fog would turn them into flat fog-coloured
// "dunes" (they sit past the afternoon fog end), so this blends a fixed share of the fog colour instead (aerial
// perspective) and only lets real fog win when it is denser (fog weather, rain, night), evaluated at half the distance.
Shader "HarvestPanic/Horizon"
{
    Properties
    {
        _Color ("Color", Color) = (0.56, 0.66, 0.62, 1)
        _FogMix ("Fog share", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _FogMix;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 n : NORMAL; };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float fog : TEXCOORD1; };

            V vert(A a)
            {
                V o;
                float3 w = TransformObjectToWorld(a.pos.xyz);
                o.pos = TransformWorldToHClip(w);
                o.n = TransformObjectToWorldNormal(a.n);
                float eye = -TransformWorldToView(w).z;
                o.fog = ComputeFogFactorZ0ToFar(eye * 0.5);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float3 n = normalize(i.n);
                Light sun = GetMainLight();
                half3 lit = _Color.rgb * (SampleSH(n) + sun.color * saturate(dot(n, sun.direction) * 0.5 + 0.25));
                half fogAmount = 1.0h - ComputeFogIntensity(i.fog);
                half mixAmt = max(_FogMix, fogAmount);
                return half4(lerp(lit, unity_FogColor.rgb, mixAmt), 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
