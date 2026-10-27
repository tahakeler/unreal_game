// HARVEST PANIC - Stylized Lit material inputs. Property names match URP Lit (_BaseMap, _BaseColor, _Cutoff, _Smoothness,
// _BumpMap, _EmissionColor, _EmissionMap, _Surface) so a converted Lit material keeps its values and per-renderer
// MaterialPropertyBlock / Material.SetColor("_BaseColor") recolours keep working. One UnityPerMaterial CBUFFER shared by
// every pass (SRP Batcher), DOTS instancing metadata like URP's own shaders (GPU Resident Drawer).
#ifndef HP_STYLIZED_LIT_INPUT_INCLUDED
#define HP_STYLIZED_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half4 _EmissionColor;
    half4 _ShadowColor;
    half4 _RimColor;
    half4 _OutlineColor;
    half _Cutoff;
    half _Smoothness;
    half _Metallic;          // unused by the stylized model; kept so converted materials round-trip
    half _BumpScale;
    half _OcclusionStrength;
    half _RampThreshold;
    half _RampSoftness;
    half _RampMidTone;
    half _RampMidOffset;
    half _SpecularStrength;
    half _RimStrength;
    half _RimPower;
    half _ColorVariation;
    half _OutlineWidth;
    half _OutlineFadeDistance;
    half _Surface;
CBUFFER_END

#ifdef UNITY_DOTS_INSTANCING_ENABLED
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
    UNITY_DOTS_INSTANCED_PROP(float , _Surface)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

static float4 unity_DOTS_Sampled_BaseColor;
static float4 unity_DOTS_Sampled_EmissionColor;
static float  unity_DOTS_Sampled_Cutoff;
static float  unity_DOTS_Sampled_Surface;

void SetupDOTSStylizedLitMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor     = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_EmissionColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _EmissionColor);
    unity_DOTS_Sampled_Cutoff        = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Cutoff);
    unity_DOTS_Sampled_Surface       = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Surface);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSStylizedLitMaterialPropertyCaches()

#define _BaseColor      unity_DOTS_Sampled_BaseColor
#define _EmissionColor  unity_DOTS_Sampled_EmissionColor
#define _Cutoff         unity_DOTS_Sampled_Cutoff
#define _Surface        unity_DOTS_Sampled_Surface
#endif

// Per-object colour variation (forests, fences, crates): a stable 0..1 hash of the object's world position.
half HP_ObjectHash()
{
    float3 p = UNITY_MATRIX_M._m03_m13_m23;
    return (half)frac(sin(dot(floor(p.xz * 2.0) + p.y, float2(12.9898, 78.233))) * 43758.5453);
}

// Darker/cooler <-> brighter/warmer shift scaled by _ColorVariation.
half3 HP_ApplyVariation(half3 albedo, half hash)
{
    half3 shift = lerp(half3(-0.9h, -0.6h, -0.1h), half3(0.8h, 0.65h, 0.15h), hash) * _ColorVariation;
    return saturate(albedo * (1.0h + shift));
}

#endif
