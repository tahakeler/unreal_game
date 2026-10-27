using UnityEditor;
using UnityEngine;

namespace HarvestPanic.Rendering.Editor
{
    /// <summary>
    /// Inspector for HarvestPanic/Stylized Lit (+ Outlined): surface type / blend / alpha clip like URP Lit (kept in sync
    /// with keywords, blend states and queue via <see cref="StylizedMaterialConverter.ApplySurface"/>), then the stylized
    /// properties. Normal map and emission keywords follow their slots.
    /// </summary>
    public sealed class StylizedLitShaderGUI : ShaderGUI
    {
        static readonly string[] SurfaceNames = { "Opaque", "Transparent" };
        static readonly string[] BlendNames = { "Alpha", "Premultiply", "Additive", "Multiply" };

        public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
        {
            var m = (Material)editor.target;
            EditorGUI.BeginChangeCheck();
            int surface = EditorGUILayout.Popup("Surface Type", Mathf.RoundToInt(m.GetFloat("_Surface")), SurfaceNames);
            int blend = Mathf.RoundToInt(m.GetFloat("_Blend"));
            if (surface == 1) blend = EditorGUILayout.Popup("Blending Mode", blend, BlendNames);
            bool clip = EditorGUILayout.Toggle("Alpha Clipping", m.GetFloat("_AlphaClip") > 0.5f);
            int cull = EditorGUILayout.Popup("Render Face", Mathf.RoundToInt(m.GetFloat("_Cull")), new[] { "Both", "Back", "Front" });
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var o in editor.targets)
                {
                    var mm = (Material)o;
                    Undo.RecordObject(mm, "Surface");
                    mm.SetFloat("_Surface", surface); mm.SetFloat("_Blend", blend); mm.SetFloat("_AlphaClip", clip ? 1f : 0f); mm.SetFloat("_Cull", cull);
                    StylizedMaterialConverter.ApplySurface(mm);
                }
            }
            EditorGUILayout.Space();
            EditorGUI.BeginChangeCheck();
            foreach (var p in props)
            {
                if ((p.propertyFlags & UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) != 0) continue;
                if (p.name == "_Cutoff" && !clip) continue;
                if ((p.name == "_OutlineColor" || p.name == "_OutlineWidth" || p.name == "_OutlineFadeDistance") && !m.shader.name.Contains("Outlined")) continue;
                editor.ShaderProperty(p, p.displayName);
            }
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var o in editor.targets)
                {
                    var mm = (Material)o;
                    SetKeyword(mm, "_NORMALMAP", mm.GetTexture("_BumpMap") != null);
                    bool em = mm.GetColor("_EmissionColor").maxColorComponent > 0.001f;
                    SetKeyword(mm, "_EMISSION", em);
                    mm.globalIlluminationFlags = em ? MaterialGlobalIlluminationFlags.None : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    SetKeyword(mm, "_RECEIVE_SHADOWS_OFF", mm.GetFloat("_ReceiveShadows") < 0.5f);
                }
            }
            EditorGUILayout.Space();
            editor.RenderQueueField();
            editor.EnableInstancingField();
        }

        static void SetKeyword(Material m, string k, bool on) { if (on) m.EnableKeyword(k); else m.DisableKeyword(k); }
    }
}
