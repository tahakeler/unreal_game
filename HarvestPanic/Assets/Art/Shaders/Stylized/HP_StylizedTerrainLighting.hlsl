// HARVEST PANIC - stylized terrain lighting hook. Included by the three HarvestPanic/Stylized Terrain shaders AFTER URP's
// Lighting.hlsl and BEFORE URP's TerrainLitPasses.hlsl: the #define below renames the single UniversalFragmentPBR call
// in URP's terrain forward pass to the stylized model, so splat mixing, holes, instancing, per-pixel normals, the
// add pass and the base map all stay URP's own code (no copied terrain shader to keep in sync with package updates).
// Terrain shading is linear in albedo (no rim, no specular), so the base pass + additive pass sum stays correct.
#ifndef HP_STYLIZED_TERRAIN_LIGHTING_INCLUDED
#define HP_STYLIZED_TERRAIN_LIGHTING_INCLUDED

#include "HP_StylizedLighting.hlsl"

half4 HP_TerrainFragmentPBR(InputData inputData, half3 albedo, half metallic, half3 specular, half smoothness,
                            half occlusion, half3 emission, half alpha)
{
    HPStyle s = HP_DefaultStyle();
    s.rampThreshold = 0.0h;   // ground is mostly flat-lit: hills get a soft painted terminator
    s.rampSoftness = 0.16h;
    s.midTone = 0.80h;
    s.midOffset = 0.22h;
    s.shadowColor = half3(0.78h, 0.80h, 0.98h);
    half3 color = HP_StylizedShade(inputData, albedo, occlusion, 0.0h, emission, s);
    return half4(color, alpha);
}

#define UniversalFragmentPBR HP_TerrainFragmentPBR

#endif
