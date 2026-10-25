// HARVEST PANIC - player farmer recolour (hero Tripo farmer, tools/tripo/farmer_hero.py).
// _MaskMap RGBA: R overalls (primary), G shirt (secondary), B boots + gloves (accent), A skin. 0 = keep the painted colour
// (eyes, brows, mouth line and the rest of the face features are 0 by construction).
// _RegionMap: R straw hat, G hat band, B hair (+ painted chest hair).
// Colour transfer per zone: c = texel + mask * s * (tint - ref), s = texel luminance / zone mean luminance. A texel is
// s * ref + detail, so the result is s * tint + detail: folds, AO, stitching and painted detail stay, the swatch reads
// true at average brightness, and tint == ref (the zone's mean painted colour, _Ref*) reproduces the texture exactly -
// FarmerVisual passes ref for a zone whose swatch is that zone's "home" swatch, so the default farmer is the painting.
// Outfit patterns multiply the overalls / shirt tints, sampled from UV1 (box projection in metres).
// _HideHat clips the baked straw hat (another hat or none is worn); the farmer adds a scalp cap.
// Lighting is URP's own PBR path (main + additional lights, Forward+, shadows, fog), like every Lit prop around it.
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
        _RefOveralls("Overalls ref (linear rgb, lum)", Vector) = (0.0714, 0.1356, 0.2332, 0.1274)
        _RefShirt("Shirt ref (linear rgb, lum)", Vector) = (0.7529, 0.6362, 0.4356, 0.6483)
        _RefAccent("Boots/gloves ref (linear rgb, lum)", Vector) = (0.3206, 0.1620, 0.0405, 0.1955)
        _RefSkin("Skin ref (linear rgb, lum)", Vector) = (0.5120, 0.1654, 0.0557, 0.2567)
        _RefHair("Hair ref (linear rgb, lum)", Vector) = (0.1281, 0.0360, 0.0145, 0.0611)
        _RefBand("Band ref (linear rgb, lum)", Vector) = (0.5239, 0.0162, 0.0125, 0.1676)
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
            float4 _RefOveralls;
            float4 _RefShirt;
            float4 _RefAccent;
            float4 _RefSkin;
            float4 _RefHair;
            float4 _RefBand;
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

        float3 Zone(float3 c, float lum, float4 refColour, float3 tint, float m)
        {
            float shade = clamp(lum / max(refColour.a, 0.02), 0.0, 1.6);
            return c + saturate(m) * shade * (tint - refColour.rgb);
        }

        float3 Recolour(float2 uv, float2 uv1)
        {
            float3 a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
            float4 m = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, uv);
            float4 reg = SAMPLE_TEXTURE2D(_RegionMap, sampler_RegionMap, uv);
            float lum = dot(a, float3(0.299, 0.587, 0.114));
            float3 overalls = _PrimaryColor.rgb * SAMPLE_TEXTURE2D(_PatternMap, sampler_PatternMap, uv1 * _PatternTiling.xy).rgb;
            float3 shirt = _SecondaryColor.rgb * SAMPLE_TEXTURE2D(_ShirtPatternMap, sampler_ShirtPatternMap, uv1 * _ShirtPatternTiling.xy).rgb;
            float3 c = a;
            c = Zone(c, lum, _RefOveralls, overalls, m.r);
            c = Zone(c, lum, _RefShirt, shirt, m.g);
            c = Zone(c, lum, _RefAccent, _AccentColor.rgb, m.b);
            c = Zone(c, lum, _RefSkin, _SkinColor.rgb, m.a);
            c = Zone(c, lum, _RefBand, _BandColor.rgb, reg.g);
            c = Zone(c, lum, _RefHair, _HairColor.rgb, reg.b);
            return saturate(c);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
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
                half3 vertexLight : TEXCOORD5;
                half3 vertexSH : TEXCOORD6;
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
                o.vertexLight = VertexLighting(p.positionWS, o.normalWS);
                o.vertexSH = SampleSHVertex(o.normalWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                ClipHat(i.uv);
                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = normalize(i.normalWS);
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fog;
                d.vertexLighting = i.vertexLight;
                d.bakedGI = SampleSHPixel(i.vertexSH, d.normalWS); // the same ambient path as URP/Lit
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);
                SurfaceData s = (SurfaceData)0;
                s.albedo = Recolour(i.uv, i.uv1);
                s.smoothness = _Smoothness;
                s.occlusion = 1;
                s.alpha = 1;
                s.normalTS = half3(0, 0, 1);
                half4 color = UniversalFragmentPBR(d, s);
                color.rgb = MixFog(color.rgb, i.fog);
                return half4(color.rgb, 1.0h);
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
