// HARVEST PANIC - inverted-hull outline for characters, critters and hero props (never terrain or buildings).
// Extrudes back faces along the normal in screen space: _OutlineWidth is pixels at 1080p, fading to nothing between
// _OutlineFadeDistance and 2x that distance so far critters never wear thick black borders. The line colour is the
// surface's own albedo darkened by _OutlineColor and lit by ambient + a share of the key light, so it reads as a
// darker shade of the object (painted), not a black marker, and it darkens with the night like everything else.
#ifndef HP_STYLIZED_OUTLINE_PASS_INCLUDED
#define HP_STYLIZED_OUTLINE_PASS_INCLUDED

#include "HP_StylizedLighting.hlsl"

struct OutlineAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS   : NORMAL;
    float2 texcoord   : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half4 lightAndFog : TEXCOORD1; // rgb light, a fog factor
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

OutlineVaryings OutlineVertex(OutlineAttributes input)
{
    OutlineVaryings output = (OutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
    float4 positionCS = TransformWorldToHClip(positionWS);

    float3 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS);
    float2 dirPx = normalCS.xy * _ScreenParams.xy;
    float len = length(dirPx);
    dirPx = len > 1e-5 ? dirPx / len : float2(0, 0);
    float dist = positionCS.w;
    float fade = saturate(2.0 - dist / max(_OutlineFadeDistance, 0.01));
    float widthPx = _OutlineWidth * (_ScreenParams.y / 1080.0) * fade;
    positionCS.xy += dirPx * (widthPx * 2.0) / _ScreenParams.xy * positionCS.w;
    output.positionCS = positionCS;
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);

    Light mainLight = GetMainLight();
    half3 light = SampleSH(normalWS) + mainLight.color * 0.45h + _HP_LookParams.z;
    output.lightAndFog = half4(light, ComputeFogFactor(positionCS.z));
    return output;
}

half4 OutlineFragment(OutlineVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    half4 albedo = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)) * _BaseColor;
    clip(_OutlineWidth - 0.001);
    half3 color = albedo.rgb * _OutlineColor.rgb * input.lightAndFog.rgb;
    color = MixFog(color, input.lightAndFog.a);
    return half4(color, 1);
}

#endif
