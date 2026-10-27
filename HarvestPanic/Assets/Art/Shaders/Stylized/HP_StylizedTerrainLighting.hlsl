// HARVEST PANIC - stylized terrain lighting hook. Included by the three HarvestPanic/Stylized Terrain shaders AFTER URP's
// Lighting.hlsl and BEFORE URP's TerrainLitPasses.hlsl: the #define below renames the single UniversalFragmentPBR call
// in URP's terrain forward pass to the stylized model, so splat mixing, holes, instancing, per-pixel normals, the
// add pass and the base map all stay URP's own code (no copied terrain shader to keep in sync with package updates).
// Terrain shading is linear in albedo (no rim, no specular), so the base pass + additive pass sum stays correct.
#ifndef HP_STYLIZED_TERRAIN_LIGHTING_INCLUDED
#define HP_STYLIZED_TERRAIN_LIGHTING_INCLUDED

#include "HP_StylizedLighting.hlsl"

float HP_TerrainHash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
float HP_TerrainNoise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(HP_TerrainHash(i), HP_TerrainHash(i + float2(1, 0)), f.x), lerp(HP_TerrainHash(i + float2(0, 1)), HP_TerrainHash(i + 1), f.x), f.y);
}

half4 HP_TerrainFragmentPBR(InputData inputData, half3 albedo, half metallic, half3 specular, half smoothness,
                            half occlusion, half3 emission, half alpha)
{
    HPStyle s = HP_DefaultStyle();
    s.rampThreshold = 0.0h;   // ground is mostly flat-lit: hills get a soft painted terminator
    s.rampSoftness = 0.16h;
    s.midTone = 0.80h;
    s.midOffset = 0.22h;
    s.shadowColor = half3(0.86h, 0.88h, 1.0h);
    // Macro variation: two octaves of world-space value noise (~30 m and ~9 m) break the big flat fields and the
    // texture tiling into painted patches - darker/cooler and lighter/warmer - without another texture fetch.
    float2 wp = inputData.positionWS.xz;
    half macro = (half)(HP_TerrainNoise(wp * 0.033) * 0.65 + HP_TerrainNoise(wp * 0.11 + 17.0) * 0.35);
    albedo *= lerp(half3(0.84h, 0.88h, 0.92h), half3(1.08h, 1.06h, 0.96h), smoothstep(0.2h, 0.8h, macro));
    half3 color = HP_StylizedShade(inputData, albedo, occlusion, 0.0h, emission, s);
    return half4(color, alpha);
}

#define UniversalFragmentPBR HP_TerrainFragmentPBR

#endif
