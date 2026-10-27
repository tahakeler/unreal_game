using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarvestPanic.Rendering.Editor
{
    /// <summary>
    /// Converts the project's URP Lit / Simple Lit materials to "HarvestPanic/Stylized Lit" (or the Outlined variant for
    /// critters and hero props), keeping albedo, colour, maps, emission, cutout/transparency and render queue. Idempotent:
    /// re-run after any asset builder recreates Lit materials (an EditMode test fails while unconverted ones remain).
    /// Menu: Harvest Panic > Rendering > Convert Materials to Stylized.
    /// Batch: Unity -batchmode -nographics -quit -projectPath HarvestPanic
    ///        -executeMethod HarvestPanic.Rendering.Editor.StylizedMaterialConverter.ConvertAll -logFile /abs/log
    /// Excluded on purpose: Art/Cosmetics + Prefabs/Farmer (the character branch owns the farmer recolour shader and adopts
    /// HP_StylizedLighting.hlsl itself), Resources/ShaderKeepAlive (runtime Shader.Find keep-alives), Scenes (dev/net tests).
    /// Per-renderer MaterialPropertyBlock / SetColor("_BaseColor") recolours keep working: the property names are URP Lit's.
    /// </summary>
    public static class StylizedMaterialConverter
    {
        public const string LitShader = "HarvestPanic/Stylized Lit";
        public const string OutlinedShader = "HarvestPanic/Stylized Lit (Outlined)";

        /// <summary>Folders whose Lit materials are converted, most specific first.</summary>
        public static readonly string[] Roots =
        {
            "Assets/Art/World", "Assets/Art/Tripo", "Assets/Art/UI", "Assets/Prefabs/Critters", "Assets/Prefabs/Goods",
            "Assets/Prefabs/Player", "Assets/Prefabs/World", "Assets/Prefabs/Tools", "Assets/Prefabs/Vehicles", "Assets/Prefabs/Night",
            "Assets/ThirdParty",
        };

        static readonly string[] SourceShaders = { "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Complex Lit", "Standard" };

        /// <summary>Look profile applied on top of the copied surface values.</summary>
        public struct Profile
        {
            public string Name;
            public bool Outline;
            public float OutlineWidth, Rim, Specular, Variation, MidTone, Softness;
            public Color Shadow;
        }

        static readonly Profile World = new Profile { Name = "world", Outline = false, Rim = 0f, Specular = 0.15f, Variation = 0.06f, MidTone = 0.72f, Softness = 0.10f, Shadow = new Color(0.86f, 0.86f, 0.98f) };
        static readonly Profile Foliage = new Profile { Name = "foliage", Outline = false, Rim = 0.12f, Specular = 0f, Variation = 0.16f, MidTone = 0.78f, Softness = 0.16f, Shadow = new Color(0.84f, 0.88f, 0.98f) };
        static readonly Profile Prop = new Profile { Name = "prop", Outline = false, Rim = 0.18f, Specular = 0.25f, Variation = 0f, MidTone = 0.72f, Softness = 0.08f, Shadow = new Color(0.86f, 0.86f, 0.98f) };
        static readonly Profile Hero = new Profile { Name = "hero", Outline = true, OutlineWidth = 1.3f, Rim = 0.3f, Specular = 0.25f, Variation = 0f, MidTone = 0.74f, Softness = 0.07f, Shadow = new Color(0.88f, 0.86f, 0.98f) };
        static readonly Profile Critter = new Profile { Name = "critter", Outline = true, OutlineWidth = 1.7f, Rim = 0.4f, Specular = 0.2f, Variation = 0f, MidTone = 0.76f, Softness = 0.08f, Shadow = new Color(0.9f, 0.86f, 0.98f) };

        static readonly string[] FoliageNames = { "leaf", "leaves", "hedge", "treeatlas", "grass", "corn", "wheat", "bush", "crop" };

        /// <summary>Profile for a material path (null = leave alone).</summary>
        public static Profile? ProfileFor(string path)
        {
            string p = path.Replace('\\', '/');
            if (p.StartsWith("Assets/Art/Cosmetics/") || p.StartsWith("Assets/Prefabs/Farmer/") || p.Contains("/ShaderKeepAlive/") || p.StartsWith("Assets/Scenes/")) return null;
            string file = System.IO.Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
            if (p.StartsWith("Assets/Prefabs/Critters/")) return file.StartsWith("m_crop_") || file.Contains("eye") ? Prop : Critter;
            if (p.StartsWith("Assets/Art/Tripo/") || p.StartsWith("Assets/Prefabs/Goods/") || p.StartsWith("Assets/ThirdParty/Tripo/"))
                return file.Contains("marketstall") || file.Contains("pengate") ? Prop : Hero;
            if (p.StartsWith("Assets/Prefabs/") || p.StartsWith("Assets/ThirdParty/")) return Prop;
            foreach (var f in FoliageNames) if (file.Contains(f)) return Foliage;
            return World;
        }

        [MenuItem("Harvest Panic/Rendering/Convert Materials to Stylized")]
        public static void ConvertAllMenu() => Run(false);

        /// <summary>Batch entry point (exit code 1 when a shader is missing or a material fails).</summary>
        public static void ConvertAll() => Run(Application.isBatchMode);

        static void Run(bool exitOnFail)
        {
            var lit = Shader.Find(LitShader);
            var outlined = Shader.Find(OutlinedShader);
            if (lit == null || outlined == null)
            {
                Debug.LogError("[StylizedConvert] stylized shaders not found (compile errors?)");
                if (exitOnFail) EditorApplication.Exit(1);
                return;
            }
            var log = new StringBuilder();
            int converted = 0, refreshed = 0, skipped = 0;
            var counts = new Dictionary<string, int>();
            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var guid in AssetDatabase.FindAssets("t:Material", Roots))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue; // embedded FBX materials are read-only
                    var prof = ProfileFor(path);
                    if (prof == null) { skipped++; continue; }
                    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (m == null || m.shader == null) continue;
                    string sn = m.shader.name;
                    bool fromLit = Array.IndexOf(SourceShaders, sn) >= 0;
                    bool already = sn == LitShader || sn == OutlinedShader;
                    if (!fromLit && !already) { skipped++; continue; }
                    Convert(m, prof.Value, prof.Value.Outline ? outlined : lit, fromLit);
                    EditorUtility.SetDirty(m);
                    if (fromLit) { converted++; log.AppendLine($"  {prof.Value.Name,-8} {path}"); } else refreshed++;
                    counts.TryGetValue(prof.Value.Name, out int c); counts[prof.Value.Name] = c + 1;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }
            var summary = new StringBuilder();
            foreach (var kv in counts) summary.Append($"{kv.Key}={kv.Value} ");
            Debug.Log($"[StylizedConvert] converted {converted}, refreshed {refreshed}, skipped {skipped} ({summary})\n{log}");
        }

        /// <summary>Converts one material in place (keeps the asset GUID, so every renderer reference survives).</summary>
        public static void Convert(Material m, Profile prof, Shader target, bool fromLit)
        {
            // Read the Lit surface state before the shader swap (Unity keeps same-named properties, but be explicit).
            var baseColor = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
            var baseMap = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : (m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null);
            var st = m.HasProperty("_BaseMap") ? new Vector4(m.GetTextureScale("_BaseMap").x, m.GetTextureScale("_BaseMap").y, m.GetTextureOffset("_BaseMap").x, m.GetTextureOffset("_BaseMap").y) : new Vector4(1, 1, 0, 0);
            float smooth = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.15f;
            var bump = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            bool emissive = m.IsKeywordEnabled("_EMISSION") || (m.globalIlluminationFlags & MaterialGlobalIlluminationFlags.EmissiveIsBlack) == 0 && m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0.001f;
            var emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            var emissionMap = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
            float surface = m.HasProperty("_Surface") ? m.GetFloat("_Surface") : 0f;
            float blend = m.HasProperty("_Blend") ? m.GetFloat("_Blend") : 0f;
            float cull = m.HasProperty("_Cull") ? m.GetFloat("_Cull") : 2f;
            bool clip = (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f) || m.IsKeywordEnabled("_ALPHATEST_ON");
            float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
            bool receive = !m.HasProperty("_ReceiveShadows") || m.GetFloat("_ReceiveShadows") > 0.5f;
            float queueOffset = m.HasProperty("_QueueOffset") ? m.GetFloat("_QueueOffset") : 0f;
            bool instancing = m.enableInstancing;

            if (m.shader != target) m.shader = target;

            m.SetColor("_BaseColor", baseColor);
            m.SetColor("_Color", baseColor);
            m.SetTexture("_BaseMap", baseMap);
            m.SetTexture("_MainTex", baseMap);
            m.SetTextureScale("_BaseMap", new Vector2(st.x, st.y)); m.SetTextureOffset("_BaseMap", new Vector2(st.z, st.w));
            m.SetFloat("_Smoothness", smooth);
            m.SetTexture("_BumpMap", bump);
            SetKeyword(m, "_NORMALMAP", bump != null);
            m.SetColor("_EmissionColor", emissive ? emission : Color.black);
            m.SetTexture("_EmissionMap", emissionMap);
            SetKeyword(m, "_EMISSION", emissive);
            m.globalIlluminationFlags = emissive ? MaterialGlobalIlluminationFlags.None : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            m.SetFloat("_Cutoff", cutoff);
            m.SetFloat("_AlphaClip", clip ? 1f : 0f);
            m.SetFloat("_Cull", cull);
            m.SetFloat("_ReceiveShadows", receive ? 1f : 0f);
            SetKeyword(m, "_RECEIVE_SHADOWS_OFF", !receive);
            m.SetFloat("_QueueOffset", queueOffset);
            m.SetFloat("_Surface", surface);
            m.SetFloat("_Blend", blend);
            m.enableInstancing = instancing || fromLit;

            // Stylized look profile. On a refresh run only fill values the artist has not touched since conversion is not
            // knowable, so the profile is re-applied wholesale: hand tweaks belong in the profile table above.
            m.SetFloat("_RampThreshold", 0.02f);
            m.SetFloat("_RampSoftness", prof.Softness);
            m.SetFloat("_RampMidTone", prof.MidTone);
            m.SetFloat("_RampMidOffset", 0.30f);
            m.SetColor("_ShadowColor", prof.Shadow);
            m.SetFloat("_SpecularStrength", prof.Specular);
            m.SetColor("_RimColor", new Color(1f, 0.96f, 0.84f));
            m.SetFloat("_RimStrength", prof.Rim);
            m.SetFloat("_RimPower", 3f);
            m.SetFloat("_ColorVariation", prof.Variation);
            m.SetFloat("_OutlineWidth", prof.Outline ? prof.OutlineWidth : 0f);
            m.SetColor("_OutlineColor", new Color(0.32f, 0.26f, 0.28f));
            m.SetFloat("_OutlineFadeDistance", 18f);
            ApplySurface(m);
        }

        /// <summary>Blend/ZWrite/queue/keywords from _Surface, _Blend, _AlphaClip (URP Lit semantics).</summary>
        public static void ApplySurface(Material m)
        {
            bool transparent = m.GetFloat("_Surface") > 0.5f;
            bool clip = m.GetFloat("_AlphaClip") > 0.5f;
            int blend = Mathf.RoundToInt(m.GetFloat("_Blend"));
            BlendMode src = BlendMode.One, dst = BlendMode.Zero;
            bool premul = false;
            if (transparent)
            {
                switch (blend)
                {
                    case 1: src = BlendMode.One; dst = BlendMode.OneMinusSrcAlpha; premul = true; break;      // premultiply
                    case 2: src = BlendMode.SrcAlpha; dst = BlendMode.One; break;                             // additive
                    case 3: src = BlendMode.DstColor; dst = BlendMode.Zero; break;                            // multiply
                    default: src = BlendMode.SrcAlpha; dst = BlendMode.OneMinusSrcAlpha; break;               // alpha
                }
            }
            m.SetFloat("_SrcBlend", (float)src); m.SetFloat("_DstBlend", (float)dst);
            m.SetFloat("_SrcBlendAlpha", transparent ? (float)BlendMode.One : (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", transparent ? 0f : 1f);
            m.SetFloat("_AlphaToMask", 0f);
            SetKeyword(m, "_SURFACE_TYPE_TRANSPARENT", transparent);
            SetKeyword(m, "_ALPHATEST_ON", clip);
            SetKeyword(m, "_ALPHAPREMULTIPLY_ON", premul);
            int queue = transparent ? (int)RenderQueue.Transparent : clip ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            m.renderQueue = queue + Mathf.RoundToInt(m.GetFloat("_QueueOffset"));
            m.SetOverrideTag("RenderType", transparent ? "Transparent" : clip ? "TransparentCutout" : "Opaque");
            m.SetShaderPassEnabled("DepthOnly", !transparent);
            m.SetShaderPassEnabled("ShadowCaster", !transparent || clip);
        }

        static void SetKeyword(Material m, string k, bool on) { if (on) m.EnableKeyword(k); else m.DisableKeyword(k); }

        /// <summary>Lit/Simple Lit .mat assets still left under the converted roots (audit; tests assert this is empty).</summary>
        public static List<string> FindUnconverted()
        {
            var left = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", Roots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase) || ProfileFor(path) == null) continue;
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null && m.shader != null && Array.IndexOf(SourceShaders, m.shader.name) >= 0) left.Add(path);
            }
            return left;
        }
    }
}
