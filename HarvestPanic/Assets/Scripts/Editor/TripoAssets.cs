using System.IO;
using UnityEditor;
using UnityEngine;

namespace HarvestPanic.Editor
{
    /// <summary>
    /// Import settings + URP Lit materials for the owner's Tripo3D models (<c>Assets/ThirdParty/Tripo/&lt;Name&gt;</c>,
    /// produced by <c>tools/tripo/process_tripo.py</c>). Each FBX's embedded material is remapped to
    /// <c>Assets/Art/Tripo/Materials/M_Tripo_&lt;Name&gt;.mat</c>, so builders can instantiate the FBX directly.
    /// Run before the team builders: <c>-executeMethod HarvestPanic.Editor.TripoAssets.Setup</c>.
    /// </summary>
    public static class TripoAssets
    {
        public const string Root = "Assets/ThirdParty/Tripo";
        public const string MatDir = "Assets/Art/Tripo/Materials";

        /// <summary>(name, max texture size). Sizes follow art-bible §9 (critters 1024, hero/large 2048).</summary>
        public static readonly (string name, int tex)[] Models =
        {
            ("Pig_Hamlet", 1024), ("Chicken", 1024), ("PrizePumpkin", 2048), ("EggBasket", 1024),
            ("FeedBucket", 1024), ("Net", 1024), ("MarketStall", 2048), ("PenGate", 1024),
        };

        public static string FbxPath(string name) => $"{Root}/{name}/{name}.fbx";
        public static string MatPath(string name) => $"{MatDir}/M_Tripo_{name}.mat";

        /// <summary>Model prefab for a Tripo asset, or null when the folder is missing (callers fall back).</summary>
        public static GameObject Model(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath(name));

        [MenuItem("Harvest Panic/Art/Setup Tripo Models")]
        public static void Setup()
        {
            Directory.CreateDirectory(MatDir);
            AssetDatabase.Refresh();
            foreach (var (name, texSize) in Models)
            {
                string fbx = FbxPath(name), texPath = $"{Root}/{name}/{name}_BaseColor.jpg";
                if (!File.Exists(fbx)) { Debug.LogWarning("[TripoAssets] missing " + fbx); continue; }

                if (AssetImporter.GetAtPath(texPath) is TextureImporter ti)
                {
                    bool d = false;
                    if (!ti.sRGBTexture) { ti.sRGBTexture = true; d = true; }
                    if (ti.maxTextureSize != texSize) { ti.maxTextureSize = texSize; d = true; }
                    if (ti.textureCompression != TextureImporterCompression.CompressedHQ) { ti.textureCompression = TextureImporterCompression.CompressedHQ; d = true; }
                    if (ti.mipmapEnabled == false) { ti.mipmapEnabled = true; d = true; }
                    if (ti.anisoLevel != 4) { ti.anisoLevel = 4; d = true; }
                    if (d) ti.SaveAndReimport();
                }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

                var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath(name));
                if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, MatPath(name)); }
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Smoothness", 0.12f);   // matte painted look; art bible: no glossy toys
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_SpecularHighlights", 0f); mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
                EditorUtility.SetDirty(mat);

                var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
                bool dirty = false;
                if (imp.importAnimation || imp.animationType != ModelImporterAnimationType.None || imp.importCameras || imp.importLights)
                { imp.importAnimation = false; imp.animationType = ModelImporterAnimationType.None; imp.importCameras = false; imp.importLights = false; dirty = true; }
                if (imp.importBlendShapes) { imp.importBlendShapes = false; dirty = true; }
                if (imp.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
                { imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription; dirty = true; }
                if (dirty) imp.SaveAndReimport();
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbx))
                {
                    if (!(o is Material m) || m == mat) continue;
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name);
                    imp.AddRemap(id, mat); dirty = true;
                }
                if (dirty) imp.SaveAndReimport();

                var model = Model(name);
                var mf = model != null ? model.GetComponentInChildren<MeshFilter>() : null;
                if (mf != null)
                {
                    var mesh = mf.sharedMesh; var t = mf.transform;
                    // Facing check: the highest vertex of the chicken (comb) should sit at +Z (forward).
                    Vector3 top = Vector3.zero; float best = float.MinValue;
                    foreach (var v in mesh.vertices) { var w = model.transform.InverseTransformPoint(t.TransformPoint(v)); if (w.y > best) { best = w.y; top = w; } }
                    Debug.Log($"[TripoAssets] {name}: tris {mesh.triangles.Length / 3}, bounds {mesh.bounds.size} (node scale {t.lossyScale}, rot {t.localEulerAngles}), top vertex {top}");
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[TripoAssets] Done.");
        }
    }
}
