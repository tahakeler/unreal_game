using System.IO;
using UnityEditor;
using UnityEngine;

namespace HarvestPanic.Weather.Editor
{
    /// <summary>
    /// Polish-2: runtime code creates materials with Shader.Find (URP Lit/Unlit/Particles, stylized water, procedural sky).
    /// A shader reached only by name is stripped from player builds (pink objects). Instead of editing ProjectSettings
    /// (Always Included Shaders, Networking-owned), one material per shader lives in Resources/ShaderKeepAlive, which
    /// forces each shader into every build. Run: -executeMethod HarvestPanic.Weather.Editor.ShaderKeepAliveBuilder.Build
    /// Keep <see cref="Shaders"/> in sync with Shader.Find calls (EditMode test ShaderKeepAliveTests checks it).
    /// </summary>
    public static class ShaderKeepAliveBuilder
    {
        public const string Dir = "Assets/Resources/ShaderKeepAlive";

        public static readonly string[] Shaders =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Particles/Unlit",
            "HarvestPanic/StylizedWater",
            "Skybox/Procedural",
            "Sprites/Default",
        };

        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) { Directory.CreateDirectory(Dir); AssetDatabase.Refresh(); }
            foreach (var name in Shaders)
            {
                var sh = Shader.Find(name);
                if (sh == null) { Debug.LogError("[ShaderKeepAlive] missing shader " + name); continue; }
                string path = $"{Dir}/KeepAlive_{name.Replace('/', '_').Replace(' ', '_')}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) AssetDatabase.CreateAsset(new Material(sh), path);
                else if (m.shader != sh) { m.shader = sh; EditorUtility.SetDirty(m); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[ShaderKeepAlive] built " + Shaders.Length);
        }
    }
}
