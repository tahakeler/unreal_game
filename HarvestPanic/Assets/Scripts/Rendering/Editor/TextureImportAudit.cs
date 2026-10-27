using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HarvestPanic.Rendering.Editor
{
    /// <summary>
    /// Texture import audit for the stylized look (art bible 9: BC7 albedo, mips; first-person grazing angles need aniso).
    /// Albedo textures used by the converted materials, the terrain layers and the Tripo/third-party packs get mipmaps,
    /// trilinear filtering, 8x anisotropic (tiling surfaces: ground, walls, roofs) or 4x (props), sRGB and high-quality
    /// compression. Point-filtered palette atlases (Kenney colormaps, tree palette, bunting) are left alone: mips would
    /// bleed neighbouring palette cells into each other at distance. Sprites/UI and normal maps keep their type.
    /// Menu: Harvest Panic > Rendering > Audit Texture Imports. Batch: -executeMethod HarvestPanic.Rendering.Editor.TextureImportAudit.Run
    /// </summary>
    public static class TextureImportAudit
    {
        static readonly string[] Roots = { "Assets/Art/World", "Assets/Art/Tripo", "Assets/ThirdParty", "Assets/Prefabs/Critters", "Assets/Prefabs/Goods", "Assets/Prefabs/Player", "Assets/Art/UI/Materials" };

        [MenuItem("Harvest Panic/Rendering/Audit Texture Imports")]
        public static void Run()
        {
            var textures = new HashSet<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", Roots))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null) continue;
                foreach (var prop in new[] { "_BaseMap", "_MainTex", "_EmissionMap" })
                    if (m.HasProperty(prop) && m.GetTexture(prop) is Texture2D t) textures.Add(AssetDatabase.GetAssetPath(t));
            }
            foreach (var guid in AssetDatabase.FindAssets("t:TerrainLayer", new[] { "Assets/Art/World" }))
            {
                var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(guid));
                if (l != null && l.diffuseTexture != null) textures.Add(AssetDatabase.GetAssetPath(l.diffuseTexture));
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/ThirdParty/Tripo", "Assets/Art/World/Textures" }))
                textures.Add(AssetDatabase.GUIDToAssetPath(guid));

            var log = new StringBuilder();
            int changed = 0, kept = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in textures)
                {
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/")) continue;
                    var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (imp == null) continue;
                    if (Audit(imp, out string why)) { imp.SaveAndReimport(); changed++; log.AppendLine($"  {path}: {why}"); }
                    else kept++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Debug.Log($"[TextureAudit] changed {changed}, already fine/left alone {kept}\n{log}");
        }

        /// <summary>True when the importer was changed (call SaveAndReimport).</summary>
        public static bool Audit(TextureImporter imp, out string why)
        {
            why = "";
            if (imp.textureType != TextureImporterType.Default) return false;           // sprites, normal maps, lightmaps
            if (imp.filterMode == FilterMode.Point) return false;                         // palette atlases (crisp cells by design)
            imp.GetSourceTextureWidthAndHeight(out int w, out int h);
            if (w <= 16 || h <= 16) return false;                                          // palette strips
            bool tiling = imp.wrapMode == TextureWrapMode.Repeat;
            int aniso = tiling ? 8 : 4;
            var sb = new StringBuilder();
            if (!imp.mipmapEnabled) { imp.mipmapEnabled = true; sb.Append("mips "); }
            if (imp.filterMode != FilterMode.Trilinear) { imp.filterMode = FilterMode.Trilinear; sb.Append("trilinear "); }
            if (imp.anisoLevel < aniso) { imp.anisoLevel = aniso; sb.Append($"aniso{aniso} "); }
            if (!imp.sRGBTexture) { imp.sRGBTexture = true; sb.Append("sRGB "); }
            if (imp.textureCompression != TextureImporterCompression.CompressedHQ && imp.textureCompression != TextureImporterCompression.Uncompressed)
            { imp.textureCompression = TextureImporterCompression.CompressedHQ; sb.Append("BC7 "); }
            why = sb.ToString();
            return why.Length > 0;
        }
    }
}
