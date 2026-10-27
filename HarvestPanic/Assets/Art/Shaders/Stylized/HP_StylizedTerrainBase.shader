// HARVEST PANIC - base map pass of HarvestPanic/Stylized Terrain (terrain beyond the basemap distance).
Shader "Hidden/HarvestPanic/Stylized Terrain (Base Pass)"
{
    Properties
    {
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _MainTex("Albedo(RGB), Smoothness(A)", 2D) = "white" {}
        _MetallicTex ("Metallic (R)", 2D) = "black" {}
        [HideInInspector] _TerrainHolesTexture("Holes Map (RGB)", 2D) = "white" {}
    }

    HLSLINCLUDE
    #pragma multi_compile_fragment __ _ALPHATEST_ON
    ENDHLSL

    SubShader
    {
        Tags { "Queue" = "Geometry-100" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" "IgnoreProjector" = "True"}
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 2.0
            #define _METALLICSPECGLOSSMAP 1
            #define _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A 1

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap

            #pragma vertex SplatmapVert
            #pragma fragment SplatmapFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _TERRAIN_INSTANCED_PERPIXEL_NORMAL
            #define TERRAIN_SPLAT_BASEPASS 1

            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitInput.hlsl"
            #include "HP_StylizedTerrainLighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Terrain/TerrainLitPasses.hlsl"
            ENDHLSL
        }

        UsePass "Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)/SHADOWCASTER"
        UsePass "Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)/DEPTHONLY"
        UsePass "Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)/DEPTHNORMALS"
        UsePass "Hidden/Universal Render Pipeline/Terrain/Lit (Base Pass)/META"
        UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
        UsePass "Universal Render Pipeline/Terrain/Lit/SCENESELECTIONPASS"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
