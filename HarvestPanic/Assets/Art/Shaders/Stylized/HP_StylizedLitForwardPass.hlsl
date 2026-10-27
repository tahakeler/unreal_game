// HARVEST PANIC - Stylized Lit forward pass (vertex/fragment). Structure follows URP SimpleLit's forward pass;
// lighting is HP_StylizedShade (HP_StylizedLighting.hlsl).
#ifndef HP_STYLIZED_LIT_FORWARD_PASS_INCLUDED
#define HP_STYLIZED_LIT_FORWARD_PASS_INCLUDED

#include "HP_StylizedLighting.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float4 tangentOS  : TANGENT;
    float2 texcoord   : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
#ifdef _NORMALMAP
    half4 normalWS    : TEXCOORD2; // w: object hash
    half4 tangentWS   : TEXCOORD3;
#else
    half4 normalWS    : TEXCOORD2; // w: object hash
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4 fogFactorAndVertexLight : TEXCOORD4;
#else
    half fogFactor : TEXCOORD4;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD5;
#endif
    half3 vertexSH : TEXCOORD6;
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

HPStyle HP_MaterialStyle()
{
    HPStyle s;
    s.rampThreshold = _RampThreshold;
    s.rampSoftness = _RampSoftness;
    s.midTone = _RampMidTone;
    s.midOffset = _RampMidOffset;
    s.shadowColor = _ShadowColor.rgb;
    s.specular = _SpecularStrength;
    s.rimColor = _RimColor.rgb;
    s.rimStrength = _RimStrength;
    s.rimPower = _RimPower;
    return s;
}

Varyings StylizedLitVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionWS = vertexInput.positionWS;
    output.positionCS = vertexInput.positionCS;
    output.normalWS = half4(NormalizeNormalPerVertex(normalInput.normalWS), HP_ObjectHash());
#ifdef _NORMALMAP
    real sign = input.tangentOS.w * GetOddNegativeScale();
    output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
#endif
    output.vertexSH = SampleSHVertex(output.normalWS.xyz);

    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    output.fogFactorAndVertexLight = half4(fogFactor, VertexLighting(vertexInput.positionWS, normalInput.normalWS));
#else
    output.fogFactor = fogFactor;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif
    return output;
}

void StylizedLitFragment(Varyings input, out half4 outColor : SV_Target0)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 albedoAlpha = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    half alpha = AlphaDiscard(albedoAlpha.a * _BaseColor.a, _Cutoff);
    half3 albedo = HP_ApplyVariation(albedoAlpha.rgb * _BaseColor.rgb, input.normalWS.w);
#ifdef _ALPHAPREMULTIPLY_ON
    albedo *= alpha;
#endif

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
#ifdef _NORMALMAP
    half3 normalTS = SampleNormal(input.uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    float sgn = input.tangentWS.w;
    float3 bitangent = sgn * cross(input.normalWS.xyz, input.tangentWS.xyz);
    inputData.tangentToWorld = half3x3(input.tangentWS.xyz, bitangent.xyz, input.normalWS.xyz);
    inputData.normalWS = TransformTangentToWorld(normalTS, inputData.tangentToWorld);
#else
    inputData.normalWS = input.normalWS.xyz;
#endif
    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.bakedGI = SampleSHPixel(input.vertexSH, inputData.normalWS);
    inputData.shadowMask = half4(1, 1, 1, 1);

#ifdef _EMISSION
    half3 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
#else
    half3 emission = 0;
#endif

    half3 color = HP_StylizedShade(inputData, albedo, 1.0h, _Smoothness, emission, HP_MaterialStyle());
    color = MixFog(color, inputData.fogCoord);
    outColor = half4(color, OutputAlpha(alpha, IsSurfaceTypeTransparent(_Surface)));
}

#endif
