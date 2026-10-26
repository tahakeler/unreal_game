// Painted sign lettering (TextMesh glyphs) that is LIT like the board it sits on: main light + ambient SH + fog, so signs
// read in daylight and go dim with the scene at night instead of glowing like the default unlit font shader.
// _MainTex is the dynamic font atlas (alpha = glyph coverage), assigned at runtime by HarvestPanic.World.LitText.
Shader "HarvestPanic/SignText"
{
    Properties
    {
        _MainTex ("Font Texture", 2D) = "white" {}
        _Color ("Text Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
            CBUFFER_END

            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; float3 nrm : TEXCOORD1; float3 wpos : TEXCOORD2; float fog : TEXCOORD3; };

            V vert(A a)
            {
                V o;
                float3 w = TransformObjectToWorld(a.pos.xyz);
                o.wpos = w;
                o.pos = TransformWorldToHClip(w);
                o.uv = TRANSFORM_TEX(a.uv, _MainTex);
                o.col = a.col;
                o.nrm = TransformObjectToWorldDir(float3(0, 0, -1)); // TextMesh glyphs face local -Z
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.col.a * _Color.a;
                clip(a - 0.02);
                float3 n = normalize(i.nrm);
                float3 toCam = GetWorldSpaceViewDir(i.wpos);
                n = dot(n, toCam) < 0 ? -n : n;
                Light sun = GetMainLight();
                half3 lit = SampleSH(n) + sun.color * saturate(dot(n, sun.direction) * 0.6 + 0.4);
                half3 col = _Color.rgb * i.col.rgb * lit;
                col = MixFog(col, i.fog);
                return half4(col, saturate(a * 1.4));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
