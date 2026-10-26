using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HarvestPanic.World.Editor
{
    /// <summary>
    /// Asset helpers for the Muddlebury build: the warm Region 1 palette materials, generated terrain textures,
    /// CC0 model import configuration (material remaps onto palette materials) and simple procedural meshes.
    /// Everything generated lands under Assets/Art/World so the ThirdParty folders stay unmodified source.
    /// </summary>
    public static class WorldAssets
    {
        public const string ArtRoot = "Assets/Art/World";
        public const string MatDir = ArtRoot + "/Materials";
        public const string TexDir = ArtRoot + "/Textures";
        public const string MeshDir = ArtRoot + "/Meshes";
        public const string EnvRoot = "Assets/ThirdParty/Environment";

        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        /// <summary>Region 1 palette (art bible 4.2) plus derived storybook tones. Reds/golds stay under 30% saturation except crops.</summary>
        public static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
        {
            // Kenney Nature Kit material names -> warm palette
            { "grass", Hex("8DB255") }, { "leafsGreen", Hex("7FA34A") }, { "leafsDark", Hex("5E8A3C") }, { "leafsFall", Hex("D9A04A") },
            { "woodBark", Hex("7A5236") }, { "woodBarkDark", Hex("5E3F2A") }, { "wood", Hex("A87A50") }, { "woodInner", Hex("D8B98A") },
            { "woodDark", Hex("6B4A30") }, { "woodBirch", Hex("E8DCC4") }, { "dirt", Hex("9A7448") }, { "dirtDark", Hex("6E5034") },
            { "stone", Hex("B8AE9C") }, { "stoneDark", Hex("8C8476") }, { "corn", Hex("E8C25A") }, { "colorYellow", Hex("F0D36A") },
            { "colorRed", Hex("D06A4E") }, { "colorPurple", Hex("9A7AB8") }, { "colorWhite", Hex("F6EBD3") }, { "colorTan", Hex("D9B98A") },
            { "_defaultMat", Hex("C8B8A0") },
        };

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

        /// <summary>Gets or creates a URP Lit palette material (instancing on, rough, non-metallic).</summary>
        public static Material Mat(string name, Color color, float smoothness = 0.12f, Texture2D tex = null, Color? emission = null)
        {
            string path = $"{MatDir}/M_World_{name}.mat";
            if (Cache.TryGetValue(path, out var cached) && cached != null) return cached;
            Directory.CreateDirectory(MatDir);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = m == null;
            if (create) m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_SpecularHighlights", 0f);
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            m.SetFloat("_EnvironmentReflections", 0f);
            m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.SetTexture("_BaseMap", tex);
            m.enableInstancing = true; // terrain trees and RenderMeshInstanced need it; GRD/SRP batcher ignore it
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.SetTexture("_EmissionMap", tex);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            if (create) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            Cache[path] = m;
            return m;
        }

        /// <summary>Palette material for a Kenney material name (falls back to a neutral warm tone).</summary>
        public static Material PaletteMat(string sourceName) =>
            Mat("K_" + sourceName, Palette.TryGetValue(sourceName, out var c) ? c : Hex("C8B8A0"));

        /// <summary>Writes a stylized tiling texture (flat colour, soft low-frequency blotches, sparse speckles).</summary>
        public static Texture2D NoiseTexture(string name, Color baseCol, Color blotch, Color speck, float speckDensity, int size = 128, int seed = 1, bool stripes = false)
        {
            Directory.CreateDirectory(TexDir);
            string path = $"{TexDir}/T_World_{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var rng = new System.Random(seed);
            float ox = rng.Next(0, 1000), oy = rng.Next(0, 1000);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Tileable noise: sample on a torus.
                float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
                float n = Mathf.PerlinNoise(ox + Mathf.Cos(u) * 1.3f + Mathf.Sin(v) * 0.7f, oy + Mathf.Sin(u) * 1.3f + Mathf.Cos(v) * 0.7f);
                float n2 = Mathf.PerlinNoise(ox * 2 + Mathf.Cos(u) * 4f, oy * 2 + Mathf.Sin(v) * 4f);
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((n * 0.7f + n2 * 0.3f - 0.35f) * 2.2f));
                t = Mathf.Round(t * 3f) / 3f; // stepped = painted look
                Color c = Color.Lerp(baseCol, blotch, t * 0.6f);
                if (stripes) c = Color.Lerp(c, blotch, Mathf.Sin(y / (float)size * Mathf.PI * 16f) > 0.55f ? 0.45f : 0f);
                if (rng.NextDouble() < speckDensity) c = Color.Lerp(c, speck, 0.7f);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Bilinear;
            imp.maxTextureSize = 256;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Cobblestone texture (rounded stones, warm grout).</summary>
        public static Texture2D CobbleTexture(string name, Color stone, Color grout, int size = 128)
        {
            Directory.CreateDirectory(TexDir);
            string path = $"{TexDir}/T_World_{name}.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            int cells = 6;
            var rng = new System.Random(7);
            var pts = new Vector2[cells * cells];
            var tints = new float[cells * cells];
            for (int i = 0; i < pts.Length; i++)
            {
                pts[i] = new Vector2((i % cells + 0.2f + (float)rng.NextDouble() * 0.6f) / cells, (i / cells + 0.2f + (float)rng.NextDouble() * 0.6f) / cells);
                tints[i] = (float)rng.NextDouble();
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x / (float)size, y / (float)size);
                float d1 = 9, d2 = 9; int best = 0;
                for (int i = 0; i < pts.Length; i++)
                for (int oxi = -1; oxi <= 1; oxi++)
                for (int oyi = -1; oyi <= 1; oyi++)
                {
                    float d = (p - (pts[i] + new Vector2(oxi, oyi))).magnitude;
                    if (d < d1) { d2 = d1; d1 = d; best = i; } else if (d < d2) d2 = d;
                }
                float edge = d2 - d1;
                Color c = Color.Lerp(stone * 0.88f, stone * 1.08f, tints[best]);
                c.a = 1f;
                tex.SetPixel(x, y, edge < 0.025f ? grout : c);
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat; imp.maxTextureSize = 256; imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Copies a Kenney colormap atlas into Art/World with reserved reds/golds desaturated and a warm shift, so
        /// the textured packs sit in the Region 1 palette without touching the third-party source.
        /// </summary>
        public static Texture2D WarmAtlas(string srcPath, string name)
        {
            string path = $"{TexDir}/T_World_{name}.png";
            if (!File.Exists(srcPath)) return null;
            Directory.CreateDirectory(TexDir);
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(srcPath));
            var px = src.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                Color.RGBToHSV(px[i], out float h, out float s, out float v);
                bool redGold = h < 0.14f || h > 0.94f;
                if (redGold && s > 0.3f) s = Mathf.Lerp(s, 0.42f, 0.6f);      // bricks and terracotta, not alarm red
                if (h > 0.45f && h < 0.7f) { h = Mathf.Lerp(h, 0.5f, 0.3f); s *= 0.8f; } // cool blues lean teal and soften
                var c = Color.HSVToRGB(h, s, v);
                c = Color.Lerp(c, c * new Color(1.04f, 1.0f, 0.92f), 0.6f);  // warm wash
                c.a = px[i].a;
                px[i] = c;
            }
            src.SetPixels(px);
            src.Apply();
            File.WriteAllBytes(path, src.EncodeToPNG());
            Object.DestroyImmediate(src);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.filterMode = FilterMode.Point; imp.mipmapEnabled = false; imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// Configures every FBX under ThirdParty/Environment: no cameras/lights/animation, and every embedded material
        /// remapped to a palette material (atlas packs map to their warmed colormap material). Only .meta files change.
        /// </summary>
        public static void ConfigureModelImports()
        {
            var atlasMats = new Dictionary<string, Material>();
            foreach (var pack in new[] { "KenneyFantasyTown", "KenneyFood", "KenneySurvival" })
            {
                var tex = WarmAtlas($"{EnvRoot}/{pack}/Textures/colormap.png", pack + "_Colormap");
                if (tex != null) atlasMats[pack] = Mat(pack + "_Atlas", Color.white, 0.1f, tex);
            }
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { EnvRoot }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) continue;
                string pack = p.Substring(EnvRoot.Length + 1).Split('/')[0];
                bool changed = false;
                if (imp.importCameras || imp.importLights || imp.importAnimation)
                { imp.importCameras = false; imp.importLights = false; imp.importAnimation = false; changed = true; }
                if (imp.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription)
                { imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription; changed = true; }
                var so = new SerializedObject(imp);
                foreach (var r in AssetDatabase.LoadAllAssetRepresentationsAtPath(p)) { }
                // Source material names come from the importer's material descriptions.
                var names = new List<string>();
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(p)) if (obj is Material mm) names.Add(mm.name);
                var existing = imp.GetExternalObjectMap();
                foreach (var n in names)
                {
                    Material target = atlasMats.TryGetValue(pack, out var am) ? am : PaletteMat(n);
                    var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), n);
                    if (existing.TryGetValue(id, out var cur) && cur == target) continue;
                    imp.AddRemap(id, target);
                    changed = true;
                }
                if (changed) imp.SaveAndReimport();
            }
        }

        /// <summary>Loads a model prefab from a pack folder (null if missing).</summary>
        public static GameObject Model(string pack, string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{EnvRoot}/{pack}/{name}.fbx");

        /// <summary>Saves a mesh as an asset so the scene references it instead of embedding it.</summary>
        public static Mesh SaveMesh(Mesh m, string name)
        {
            Directory.CreateDirectory(MeshDir);
            string path = $"{MeshDir}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(m, existing); Object.DestroyImmediate(m); return existing; }
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>
        /// Flat-shaded prism: a convex 2D profile (x = across, y = up) extruded along local Z by <paramref name="length"/>
        /// (centred). Used for the gambrel barn, gable houses and roofs.
        /// </summary>
        public static Mesh Prism(Vector2[] profile, float length)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            float hz = length * 0.5f;
            int count = profile.Length;
            // sides
            for (int i = 0; i < count; i++)
            {
                Vector2 a = profile[i], b = profile[(i + 1) % count];
                var p0 = new Vector3(a.x, a.y, -hz); var p1 = new Vector3(b.x, b.y, -hz);
                var p2 = new Vector3(b.x, b.y, hz); var p3 = new Vector3(a.x, a.y, hz);
                var normal = Vector3.Cross(p1 - p0, p3 - p0).normalized;
                int s = v.Count;
                v.AddRange(new[] { p0, p1, p2, p3 });
                n.AddRange(new[] { normal, normal, normal, normal });
                float len = (b - a).magnitude;
                uv.AddRange(new[] { new Vector2(0, 0), new Vector2(len, 0), new Vector2(len, length), new Vector2(0, length) });
                t.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
            }
            // caps
            for (int cap = 0; cap < 2; cap++)
            {
                float z = cap == 0 ? -hz : hz;
                var normal = cap == 0 ? Vector3.back : Vector3.forward;
                int s = v.Count;
                for (int i = 0; i < count; i++) { v.Add(new Vector3(profile[i].x, profile[i].y, z)); n.Add(normal); uv.Add(profile[i]); }
                for (int i = 1; i < count - 1; i++)
                    if (cap == 0) t.AddRange(new[] { s, s + i, s + i + 1 }); else t.AddRange(new[] { s, s + i + 1, s + i });
            }
            var m = new Mesh();
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            // Fix winding if the profile was clockwise: make normals point outward from the centroid.
            EnsureOutward(m);
            return m;
        }

        static void EnsureOutward(Mesh m)
        {
            var verts = m.vertices; var norms = m.normals; var tris = m.triangles;
            Vector3 c = m.bounds.center;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], d = verts[tris[i + 2]];
                Vector3 fn = Vector3.Cross(b - a, d - a);
                if (Vector3.Dot(fn, (a + b + d) / 3f - c) < 0f) { (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]); }
            }
            m.triangles = tris;
            // normals: recompute flat per face group by using geometry normals per vertex
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], d = verts[tris[i + 2]];
                Vector3 fn = Vector3.Cross(b - a, d - a).normalized;
                norms[tris[i]] = norms[tris[i + 1]] = norms[tris[i + 2]] = fn;
            }
            m.normals = norms;
        }

        /// <summary>Flat-shaded frustum (n-sided), base radius r0 at y=0, top radius r1 at y=h. r1 = 0 gives a cone.</summary>
        public static Mesh Frustum(int sides, float r0, float r1, float h, bool capTop = true)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * Mathf.PI * 2 / sides, a1 = (i + 1) * Mathf.PI * 2 / sides;
                var b0 = new Vector3(Mathf.Cos(a0) * r0, 0, Mathf.Sin(a0) * r0); var b1 = new Vector3(Mathf.Cos(a1) * r0, 0, Mathf.Sin(a1) * r0);
                var u0 = new Vector3(Mathf.Cos(a0) * r1, h, Mathf.Sin(a0) * r1); var u1 = new Vector3(Mathf.Cos(a1) * r1, h, Mathf.Sin(a1) * r1);
                int s = v.Count; v.AddRange(new[] { b0, b1, u1, u0 });
                t.AddRange(new[] { s, s + 2, s + 1, s, s + 3, s + 2 });
                if (capTop && r1 > 0f) { int c = v.Count; v.AddRange(new[] { new Vector3(0, h, 0), u1, u0 }); t.AddRange(new[] { c, c + 1, c + 2 }); }
            }
            var m = new Mesh(); m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            EnsureOutwardFrustum(m);
            return m;
        }

        static void EnsureOutwardFrustum(Mesh m)
        {
            var verts = m.vertices; var tris = m.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], d = verts[tris[i + 2]];
                Vector3 fn = Vector3.Cross(b - a, d - a);
                Vector3 centre = (a + b + d) / 3f;
                Vector3 outDir = new Vector3(centre.x, 0, centre.z);
                if (outDir.sqrMagnitude < 1e-4f) outDir = Vector3.up;
                if (Vector3.Dot(fn, outDir) < 0f) (tris[i + 1], tris[i + 2]) = (tris[i + 2], tris[i + 1]);
            }
            // flat shading: unweld
            var nv = new Vector3[tris.Length]; var nt = new int[tris.Length];
            for (int i = 0; i < tris.Length; i++) { nv[i] = verts[tris[i]]; nt[i] = i; }
            m.Clear(); m.vertices = nv; m.triangles = nt; m.RecalculateNormals(); m.RecalculateBounds();
        }

        /// <summary>Grid mesh for the river surface: UV.x across 0..1, UV.y downstream metres.</summary>
        public static Mesh RiverMesh(float width, float length, int across, int along)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int j = 0; j <= along; j++)
            for (int i = 0; i <= across; i++)
            {
                float x = (i / (float)across - 0.5f) * width, z = (0.5f - j / (float)along) * length;
                v.Add(new Vector3(x, 0, z)); uv.Add(new Vector2(i / (float)across, j / (float)along * length));
            }
            for (int j = 0; j < along; j++)
            for (int i = 0; i < across; i++)
            {
                int a = j * (across + 1) + i, b = a + 1, c = a + across + 1, d = c + 1;
                t.AddRange(new[] { a, b, c, b, d, c });
            }
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            // make sure it faces up
            if (m.normals.Length > 0 && m.normals[0].y < 0) { var tr = m.triangles; for (int i = 0; i < tr.Length; i += 3) (tr[i + 1], tr[i + 2]) = (tr[i + 2], tr[i + 1]); m.triangles = tr; m.RecalculateNormals(); }
            return m;
        }
    }
}
