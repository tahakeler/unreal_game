// HARVEST PANIC - "HarvestPanic/Stylized Lit (Outlined)": Stylized Lit plus an inverted-hull outline pass for characters,
// critters and hero props. A separate shader (not a toggle) so the thousands of non-outlined world objects never pay for
// a second pass. The outline pass uses LightMode SRPDefaultUnlit, which URP's opaque pass draws without a renderer feature.
Shader "HarvestPanic/Stylized Lit (Outlined)"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Colour", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Smoothness("Smoothness (highlight size)", Range(0.0, 1.0)) = 0.15
        [HideInInspector] _Metallic("Metallic (unused)", Range(0.0, 1.0)) = 0.0
        [NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0)
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}

        [Header(Stylized Ramp)]
        _RampThreshold("Terminator (N.L)", Range(-0.5, 0.5)) = 0.02
        _RampSoftness("Terminator Softness", Range(0.002, 0.5)) = 0.10
        _RampMidTone("Middle Band Level (1 = two bands)", Range(0.0, 1.0)) = 0.72
        _RampMidOffset("Middle Band Width", Range(0.0, 1.0)) = 0.30
        _ShadowColor("Shadow Tint", Color) = (0.80, 0.80, 0.95, 1)
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0

        [Header(Highlights)]
        _SpecularStrength("Toon Specular", Range(0.0, 1.0)) = 0.25
        _RimColor("Rim Colour", Color) = (1.0, 0.96, 0.84, 1)
        _RimStrength("Rim Strength", Range(0.0, 2.0)) = 0.35
        _RimPower("Rim Width (higher = thinner)", Range(0.5, 8.0)) = 3.0
        _ColorVariation("Per-object Colour Variation", Range(0.0, 0.4)) = 0.0

        [Header(Outline)]
        _OutlineColor("Outline Shade (x albedo)", Color) = (0.32, 0.26, 0.28, 1)
        _OutlineWidth("Outline Width (px at 1080p)", Range(0.0, 6.0)) = 1.6
        _OutlineFadeDistance("Outline Full Width Until (m)", Float) = 18.0

        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
        [HideInInspector] [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "SimpleLit" "IgnoreProjector" = "True" }
        LOD 300

        UsePass "HarvestPanic/Stylized Lit/FORWARDLIT"
        UsePass "HarvestPanic/Stylized Lit/SHADOWCASTER"
        UsePass "HarvestPanic/Stylized Lit/DEPTHONLY"
        UsePass "HarvestPanic/Stylized Lit/DEPTHNORMALS"
        UsePass "HarvestPanic/Stylized Lit/META"

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "HP_StylizedLitInput.hlsl"
            #include "HP_StylizedOutlinePass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "HarvestPanic.Rendering.Editor.StylizedLitShaderGUI"
}
