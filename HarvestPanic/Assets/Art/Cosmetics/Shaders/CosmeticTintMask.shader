// HARVEST PANIC - cosmetic tint mask (design/art/asset-specs/tripo-batch-02-character.md section 2).
// Albedo is painted in neutral zone greys; the RGBA mask says which loadout colour recolours each zone:
// R = primary (overalls; hair colour for hair items), G = secondary (shirt), B = accent (boots/gloves), A = skin.
// Each zone keeps its baked shading: tint * (albedo luminance / zone reference grey). Main light + shadows + SH ambient
// + fog; colours come from MaterialPropertyBlocks set by AccessoryBuilder (materials stay shared).
Shader "HarvestPanic/Cosmetics/TintMask"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo (BC)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1,1,1,1)
        _MaskMap("Tint Mask (R primary, G secondary, B accent, A skin)", 2D) = "black" {}
        _PrimaryColor("Primary", Color) = (1,1,1,1)
        _SecondaryColor("Secondary", Color) = (1,1,1,1)
        _AccentColor("Accent", Color) = (1,1,1,1)
        _SkinColor("Skin", Color) = (1,1,1,1)
        _MaskRef("Zone reference greys (linear luminance R,G,B,A)", Vector) = (0.32, 0.78, 0.07, 0.48)
        _Smoothness("Smoothness", Range(0,1)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _PrimaryColor;
            half4 _SecondaryColor;
            half4 _AccentColor;
            half4 _SkinColor;
            half4 _MaskRef;
            half _Smoothness;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);

        half3 Zone(half3 albedo, half lum, half ref, half3 tint, half mask)
        {
            half3 tinted = tint * clamp(lum / max(ref, 0.02h), 0.0h, 1.3h);
            return lerp(albedo, tinted, saturate(mask));
        }

        half3 TintedAlbedo(float2 uv)
        {
            half3 a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
            half4 m = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
            half lum = dot(a, half3(0.299h, 0.587h, 0.114h));
            half3 c = a;
            c = Zone(c, lum, _MaskRef.x, _PrimaryColor.rgb, m.r);
            c = Zone(c, lum, _MaskRef.y, _SecondaryColor.rgb, m.g);
            c = Zone(c, lum, _MaskRef.z, _AccentColor.rgb, m.b);
            c = Zone(c, lum, _MaskRef.w, _SkinColor.rgb, m.a);
            return c;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes i)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(i.normalOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 albedo = TintedAlbedo(i.uv);
                float3 n = normalize(i.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = saturate(dot(n, mainLight.direction));
                half3 color = albedo * SampleSH(n);
                color += albedo * mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation * ndl);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half spec = pow(saturate(dot(n, normalize(mainLight.direction + v))), 8.0h + _Smoothness * 120.0h) * _Smoothness;
                color += mainLight.color * spec * mainLight.shadowAttenuation;
                color = MixFog(color, i.fog);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(ShadowAttributes i) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 positionWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(i.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthAttributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };

            float4 DepthVert(DepthAttributes i) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(i);
                return TransformObjectToHClip(i.positionOS.xyz);
            }

            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
