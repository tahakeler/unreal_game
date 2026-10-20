using System.IO;
using System.Text.RegularExpressions;
using HarvestPanic.Weather.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Weather
{
    /// <summary>Polish-2 item 7: every shader looked up by name at runtime is kept alive for player builds.</summary>
    public class ShaderKeepAliveTests
    {
        [Test]
        public void EveryShaderFind_IsKeptAliveByAResourcesMaterial()
        {
            var rx = new Regex("Shader\\.Find\\(\"([^\"]+)\"\\)");
            foreach (var file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/") || file.Replace('\\', '/').Contains("/Editor/")) continue;
                foreach (Match m in rx.Matches(File.ReadAllText(file)))
                {
                    string name = m.Groups[1].Value;
                    if (name == "Standard") continue; // built-in fallback only, never reached under URP
                    CollectionAssert.Contains(ShaderKeepAliveBuilder.Shaders, name, $"{file} uses Shader.Find(\"{name}\")");
                }
            }
            var mats = AssetDatabase.FindAssets("t:Material", new[] { ShaderKeepAliveBuilder.Dir });
            foreach (var name in ShaderKeepAliveBuilder.Shaders)
            {
                bool found = false;
                foreach (var g in mats)
                {
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g));
                    if (mat != null && mat.shader != null && mat.shader.name == name) { found = true; break; }
                }
                Assert.IsTrue(found, "no keep-alive material for " + name);
            }
        }
    }
}
