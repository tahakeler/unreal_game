// HARVEST PANIC - shared stylized lighting (technical-artist). One lighting model for every opaque surface so the world,
// props, critters, terrain and (later) the hero farmer's recolour shader all shade alike:
//   * 2-3 band ramp on N.L with a soft terminator (painted, not cel-hard),
//   * the unlit side keeps the ambient (sky SH) but is multiplied by a shadow tint (cool violet by day, teal at night),
//   * SSAO and material occlusion on the ambient, a little on the key light,
//   * toon specular (only on smooth surfaces), fresnel rim that follows the key light colour,
//   * Forward+ / clustered additional lights with wrapped lighting so lantern pools wrap round shapes.
//
// HOW ANOTHER SHADER ADOPTS IT (e.g. HarvestPanic/Characters/FarmerRecolour on feature/character-creator):
//   #include "Assets/Art/Shaders/Stylized/HP_StylizedLighting.hlsl"
//   HPStyle s = HP_DefaultStyle();               // or fill from the material (same property names as Stylized Lit)
//   half3 c = HP_StylizedShade(inputData, albedo, occlusion, smoothness, emission, s);
//   c = MixFog(c, inputData.fogCoord);
// i.e. replace `UniversalFragmentPBR(inputData, surfaceData)` - the InputData setup stays exactly as it is. The function
// needs the same keywords as URP Lit's forward pass (shadows, additional lights, _CLUSTER_LIGHT_LOOP, SSAO).
//
// Globals (set every frame by HarvestPanic.Weather.WeatherLookApplier; all-zero = neutral, so scenes without the weather
// presenter - menus, tests, edit mode - shade with the material values alone):
//   _HP_ShadowShift.rgb : 1 - shadow tint multiplier (0 = no extra tint)
//   _HP_LookParams      : x rim boost (rim *= 1 + x), y ramp softness add, z ambient floor (linear, night readability),
//                         w specular boost (spec *= 1 + w)
#ifndef HP_STYLIZED_LIGHTING_INCLUDED
#define HP_STYLIZED_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

half4 _HP_ShadowShift;
half4 _HP_LookParams;

struct HPStyle
{
    half rampThreshold;  // N.L where the lit side starts (0 = terminator at 90 degrees)
    half rampSoftness;   // half width of the terminator blend
    half midTone;        // brightness of the middle band (1 = two bands only)
    half midOffset;      // N.L above the threshold where the middle band turns fully lit
    half3 shadowColor;   // multiplies the ambient on the unlit side
    half specular;       // toon highlight strength (0 = off)
    half3 rimColor;
    half rimStrength;
    half rimPower;       // higher = thinner rim
};

HPStyle HP_DefaultStyle()
{
    HPStyle s;
    s.rampThreshold = 0.02h;
    s.rampSoftness = 0.10h;
    s.midTone = 0.72h;
    s.midOffset = 0.30h;
    s.shadowColor = half3(0.86h, 0.86h, 0.98h);
    s.specular = 0.0h;
    s.rimColor = half3(1.0h, 0.96h, 0.84h);
    s.rimStrength = 0.0h;
    s.rimPower = 3.0h;
    return s;
}

// 0..1 light amount from N.L: soft step at the terminator, a second soft step to full light.
half HP_Ramp(half ndl, HPStyle s)
{
    half soft = max(s.rampSoftness + _HP_LookParams.y, 0.002h);
    half lower = smoothstep(s.rampThreshold - soft, s.rampThreshold + soft, ndl);
    half upper = smoothstep(s.rampThreshold + s.midOffset - soft, s.rampThreshold + s.midOffset + soft, ndl);
    return lower * lerp(s.midTone, 1.0h, upper);
}

// Lanterns and other punctual lights: wrapped so a pool lights the whole silhouette softly (readable at night),
// still stepped a little so the falloff reads painted rather than photographic.
half3 HP_AdditionalLight(Light light, half3 normalWS)
{
    half ndl = dot(normalWS, light.direction);
    half wrap = saturate(ndl * 0.55h + 0.45h);
    half ramp = smoothstep(0.05h, 0.95h, wrap);
    half atten = (half)light.distanceAttenuation * light.shadowAttenuation;
    return light.color * (ramp * atten);
}

// Full stylized shading for an opaque or transparent surface. `inputData` must be named so (URP's Forward+ light loop
// macro reads it). Returns linear colour before fog.
half3 HP_StylizedShade(InputData inputData, half3 albedo, half occlusion, half smoothness, half3 emission, HPStyle s)
{
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, occlusion);
    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor); // colour already carries SSAO's direct share
    half3 N = inputData.normalWS;
    half3 V = inputData.viewDirectionWS;

    half ndl = dot(N, mainLight.direction);
    // Shadow maps are PCF-soft; a gentle S-curve keeps them soft but gives the edge a painted shape.
    half shadow = smoothstep(0.0h, 1.0h, mainLight.shadowAttenuation);
    half lit = HP_Ramp(ndl, s) * shadow * (half)mainLight.distanceAttenuation;

    half3 ambient = inputData.bakedGI * aoFactor.indirectAmbientOcclusion + _HP_LookParams.z * aoFactor.indirectAmbientOcclusion;
    half3 tint = s.shadowColor * (1.0h - _HP_ShadowShift.rgb);
    half3 diffuse = ambient * lerp(tint, 1.0h, lit) + mainLight.color * lit;
    half3 color = albedo * diffuse;

    // Toon highlight: only smooth materials (smoothness > ~0.3) get one; size follows smoothness.
    half3 H = SafeNormalize(mainLight.direction + V);
    half ndh = saturate(dot(N, H));
    half specPow = exp2(10.0h * smoothness + 1.0h);
    half specMask = saturate((smoothness - 0.25h) * 3.0h);
    half spec = smoothstep(0.42h, 0.58h, pow(ndh, specPow)) * s.specular * specMask * (1.0h + _HP_LookParams.w) * lit;
    color += mainLight.color * spec;

    #if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    #if USE_CLUSTER_LIGHT_LOOP
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        color += albedo * HP_AdditionalLight(light, N);
    }
    #endif
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
        color += albedo * HP_AdditionalLight(light, N);
    LIGHT_LOOP_END
    #endif
    #if defined(_ADDITIONAL_LIGHTS_VERTEX)
    color += albedo * inputData.vertexLighting;
    #endif

    // Rim: lit by whatever lights the scene (warm sun by day, moon blue at night), never a flat emissive glow.
    half fres = 1.0h - saturate(dot(N, V));
    half rim = smoothstep(0.25h, 0.75h, pow(fres, s.rimPower));
    half3 rimLight = mainLight.color * (0.35h + 0.65h * lit) + ambient;
    color += s.rimColor * rimLight * (rim * s.rimStrength * (1.0h + _HP_LookParams.x));

    return color + emission;
}

#endif
