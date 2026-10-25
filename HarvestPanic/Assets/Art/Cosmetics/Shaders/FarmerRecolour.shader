// HARVEST PANIC - player farmer recolour (hero Tripo farmer, tools/tripo/farmer_hero.py).
// _MaskMap RGBA: R overalls (primary), G shirt (secondary), B boots + gloves (accent), A skin. 0 = keep the painted colour.
// _RegionMap: R straw hat, G hat band, B hair + brows (+ painted chest hair).
// Luminance-preserving: each zone becomes tint * (texel luminance / zone mean luminance), so folds, AO and stitching stay
// while the swatch reads true at average brightness (works for dark denim -> white overalls and back).
// Outfit patterns multiply the overalls / shirt zones, sampled from UV1 (box projection in metres).
// _HideHat clips the baked straw hat (another hat or none is worn); the farmer adds a scalp cap.
// All colours come from MaterialPropertyBlocks (FarmerVisual), so one material serves every player.
Shader "HarvestPanic/Characters/FarmerRecolour"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Base Colour", Color) = (1,1,1,1)
        _MaskMap("Recolour Mask (R overalls, G shirt, B boots/gloves, A skin)", 2D) = "black" {}
        _RegionMap("Regions (R hat, G band, B hair)", 2D) = "black" {}
        _PrimaryColor("Overalls", Color) = (0.23,0.43,0.82,1)
        _SecondaryColor("Shirt", Color) = (0.95,0.9,0.78,1)
        _AccentColor("Boots / gloves", Color) = (0.42,0.29,0.18,1)
        _SkinColor("Skin", Color) = (0.79,0.58,0.42,1)
        _HairColor("Hair", Color) = (0.42,0.29,0.18,1)
        _BandColor("Hat band", Color) = (0.75,0.27,0.23,1)
        _MaskRef("Zone mean luminance (R,G,B,A)", Vector) = (0.127, 0.648, 0.196, 0.257)
        _RegionRef("Region mean luminance (hat, band, hair)", Vector) = (0.375, 0.168, 0.062, 0)
        _HideHat("Hide baked hat", Float) = 0
        _PatternMap("Overalls pattern", 2D) = "white" {}
        _PatternTiling("Overalls pattern tiling", Vector) = (1,1,0,0)
        _ShirtPatternMap("Shirt pattern", 2D) = "white" {}
        _ShirtPatternTiling("Shirt pattern tiling", Vector) = (1,1,0,0)
        _Smoothness("Smoothness", Range(0,1)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
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
            half4 _HairColor;
            half4 _BandColor;
            half4 _MaskRef;
            half4 _RegionRef;
            half _HideHat;
            float4 _PatternTiling;
            float4 _ShirtPatternTiling;
            half _Smoothness;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
        TEXTURE2D(_RegionMap); SAMPLER(sampler_RegionMap);
        TEXTURE2D(_PatternMap); SAMPLER(sampler_PatternMap);
        TEXTURE2D(_ShirtPatternMap); SAMPLER(sampler_ShirtPatternMap);

        void ClipHat(float2 uv)
        {
            if (_HideHat > 0.5h)
            {
                half4 reg = SAMPLE_TEXTURE2D(_RegionMap, sampler_RegionMap, uv);
                clip(0.5h - (reg.r + reg.g));
            }
        }

        half3 Zone(half3 c, half lum, half refLum, half3 tint, half m)
        {
            return lerp(c, tint * clamp(lum / max(refLum, 0.02h), 0.0h, 1.6h), saturate(m));
        }

        half3 Recolour(float2 uv, float2 uv1)
        {
            half3 a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
            half4 m = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
            half4 reg = SAMPLE_TEXTURE2D(_RegionMap, sampler_RegionMap, uv);
            half lum = dot(a, half3(0.299h, 0.587h, 0.114h));
            half3 overalls = _PrimaryColor.rgb * SAMPLE_TEXTURE2D(_PatternMap, sampler_PatternMap, uv1 * _PatternTiling.xy).rgb;
            half3 shirt = _SecondaryColor.rgb * SAMPLE_TEXTURE2D(_ShirtPatternMap, sampler_ShirtPatternMap, uv1 * _ShirtPatternTiling.xy).rgb;
            half3 c = a;
            c = Zone(c, lum, _MaskRef.x, overalls, m.r);
            c = Zone(c, lum, _MaskRef.y, shirt, m.g);
            c = Zone(c, lum, _MaskRef.z, _AccentColor.rgb, m.b);
            c = Zone(c, lum, _MaskRef.w, _SkinColor.rgb, m.a);
            c = Zone(c, lum, _RegionRef.y, _BandColor.rgb, reg.g);
            c = Zone(c, lum, _RegionRef.z, _HairColor.rgb, reg.b);
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
                float2 uv1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                half fog : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes i)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = GetVertexNormalInputs(i.normalOS).normalWS;
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.uv1 = i.uv1;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                ClipHat(i.uv);
                half3 albedo = Recolour(i.uv, i.uv1);
                float3 n = normalize(i.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half ndl = saturate(dot(n, mainLight.direction));
                half wrap = saturate((dot(n, mainLight.direction) + 0.35h) / 1.35h); // soft toy wrap lighting
                half3 color = albedo * SampleSH(n);
                color += albedo * mainLight.color * (mainLight.distanceAttenuation * lerp(wrap, ndl, 0.5h) * mainLight.shadowAttenuation);
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = pow(1.0h - saturate(dot(n, v)), 4.0h) * 0.12h;
                color += rim * mainLight.color;
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

            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct ShadowVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            ShadowVaryings ShadowVert(ShadowAttributes i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                ShadowVaryings o;
                float3 positionWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(i.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                o.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                o.positionCS.z = min(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                o.positionCS.z = max(o.positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 ShadowFrag(ShadowVaryings i) : SV_Target { ClipHat(i.uv); return 0; }
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

            struct DepthAttributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct DepthVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            DepthVaryings DepthVert(DepthAttributes i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                DepthVaryings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half DepthFrag(DepthVaryings i) : SV_Target { ClipHat(i.uv); return 0; }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
