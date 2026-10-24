using System.IO;
using UnityEditor;
using UnityEngine;

namespace HarvestPanic.World.Editor
{
    /// <summary>
    /// Seeded procedural surface textures for the Muddlebury architecture (art bible 3: painted, low-detail, no photo
    /// textures). Most are near-white "value" maps so the palette material colour carries the hue; a few (hay, stripes,
    /// clock face, bunting palette) are coloured. All tile, all are tiny (64-256 px), all land in Art/World/Textures.
    /// </summary>
    public static class WorldTextures
    {
        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + s * 982451653;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        /// <summary>Tileable smooth noise on a <paramref name="period"/>-cell lattice (u, v in 0..1).</summary>
        static float TNoise(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int M(int a) => ((a % period) + period) % period;
            float a00 = Hash(M(x0), M(y0), seed), a10 = Hash(M(x0 + 1), M(y0), seed), a01 = Hash(M(x0), M(y0 + 1), seed), a11 = Hash(M(x0 + 1), M(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a00, a10, fx), Mathf.Lerp(a01, a11, fx), fy);
        }

        static Texture2D Save(string name, Texture2D tex, bool repeat = true, bool point = false, int maxSize = 256)
        {
            Directory.CreateDirectory(WorldAssets.TexDir);
            string path = $"{WorldAssets.TexDir}/T_World_{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
            imp.mipmapEnabled = !point;
            imp.anisoLevel = point ? 0 : 4;
            imp.maxTextureSize = maxSize;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Wood boards: <paramref name="boards"/> per tile, seams along V (vertical boards) or U (horizontal).</summary>
        public static Texture2D Boards(string name, int boards, bool vertical, int seed)
        {
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;
                float across = vertical ? u : v, along = vertical ? v : u;
                int b = Mathf.FloorToInt(across * boards);
                float inB = across * boards - b;
                float tone = 0.86f + Hash(b, 7, seed) * 0.14f;
                float grain = TNoise(vertical ? across * 6f : along, vertical ? along : across * 6f, 32, seed + b) * 0.08f;
                float streak = Mathf.PerlinNoise(across * boards * 9f + b * 3.1f, along * 2f) * 0.07f;
                float c = tone - grain - streak;
                float seam = Mathf.Min(inB, 1f - inB) * S / boards;
                if (seam < 1.6f) c *= 0.55f; else if (seam < 3f) c *= 0.82f;
                // occasional plank end joint
                float joint = Mathf.Repeat(along + Hash(b, 3, seed), 1f);
                if (Hash(b, 5, seed) > 0.55f && Mathf.Abs(joint - 0.5f) * S < 1.2f) c *= 0.6f;
                // knot
                var kp = new Vector2(b + 0.5f, Hash(b, 9, seed));
                float kd = new Vector2((across * boards - kp.x) * 1.0f, (along - kp.y) * boards * 1.6f).magnitude;
                if (Hash(b, 11, seed) > 0.6f && kd < 0.12f) c *= 0.78f;
                tex.SetPixel(x, y, new Color(c, c * 0.985f, c * 0.96f, 1f));
            }
            tex.Apply();
            return Save(name, tex);
        }

        /// <summary>Roof shingles: offset rows of rounded tiles with a shadow lip (value map).</summary>
        public static Texture2D Shingles(string name, int rows, int perRow, int seed)
        {
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;
                int r = Mathf.FloorToInt(v * rows);
                float inR = v * rows - r;                       // 0 = bottom lip of the row
                float uu = u * perRow + (r % 2) * 0.5f;
                int col = Mathf.FloorToInt(uu);
                float inC = uu - col;
                float tone = 0.8f + Hash(col % perRow, r, seed) * 0.2f;
                // rounded bottom corners
                float edgeX = Mathf.Min(inC, 1f - inC);
                float round = inR < 0.25f && edgeX < 0.18f ? (0.18f - edgeX) * (0.25f - inR) * 60f : 0f;
                float c = tone * Mathf.Lerp(0.72f, 1f, Mathf.SmoothStep(0f, 0.35f, inR));   // shadow under the next row's lip
                if (edgeX * S / perRow < 1.2f) c *= 0.6f;
                if (inR * S / rows < 1.5f || round > 0.5f) c *= 0.55f;
                c -= TNoise(u, v, 16, seed) * 0.06f;
                tex.SetPixel(x, y, new Color(c, c, c * 0.98f, 1f));
            }
            tex.Apply();
            return Save(name, tex);
        }

        /// <summary>Soft plaster (near-white value map with faint patches).</summary>
        public static Texture2D Plaster(string name, int seed)
        {
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;
                float n = TNoise(u, v, 4, seed) * 0.6f + TNoise(u, v, 16, seed + 1) * 0.4f;
                float c = 0.9f + Mathf.Round(n * 4f) / 4f * 0.1f - (Hash(x, y, seed) > 0.985f ? 0.06f : 0f);
                tex.SetPixel(x, y, new Color(c, c, c, 1f));
            }
            tex.Apply();
            return Save(name, tex);
        }

        /// <summary>Coursed stone blocks (rows of jittered rectangles, rounded, mortar lines).</summary>
        public static Texture2D StoneBlocks(string name, int rows, int perRow, int seed)
        {
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;
                int r = Mathf.FloorToInt(v * rows);
                float inR = v * rows - r;
                float shift = Hash(r, 1, seed) * 0.6f;
                float uu = u * perRow + shift;
                int col = Mathf.FloorToInt(uu);
                float inC = uu - col;
                float tone = 0.74f + Hash(col % perRow, r, seed) * 0.26f;
                float ex = Mathf.Min(inC, 1f - inC) * S / perRow, ey = Mathf.Min(inR, 1f - inR) * S / rows;
                float e = Mathf.Min(ex, ey);
                float c = e < 1.5f ? 0.5f : tone * Mathf.Lerp(0.8f, 1f, Mathf.Clamp01((e - 1.5f) / 4f));
                c -= TNoise(u, v, 32, seed) * 0.08f;
                tex.SetPixel(x, y, new Color(c, c * 0.98f, c * 0.95f, 1f));
            }
            tex.Apply();
            return Save(name, tex);
        }

        /// <summary>Straw: many short golden strands (coloured).</summary>
        public static Texture2D Hay(string name, Color baseCol, Color dark, Color light, int seed)
        {
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
            var px = new Color[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = baseCol;
            var rng = new System.Random(seed);
            for (int k = 0; k < 900; k++)
            {
                int x0 = rng.Next(S), y0 = rng.Next(S), len = 6 + rng.Next(14);
                float ang = (float)(rng.NextDouble() - 0.5) * 0.7f;
                var col = rng.NextDouble() < 0.5 ? dark : light;
                for (int t = 0; t < len; t++)
                {
                    int x = ((int)(x0 + Mathf.Cos(ang) * t) % S + S) % S, y = ((int)(y0 + Mathf.Sin(ang) * t) % S + S) % S;
                    px[y * S + x] = Color.Lerp(px[y * S + x], col, 0.6f);
                }
            }
            tex.SetPixels(px); tex.Apply();
            return Save(name, tex);
        }

        /// <summary>Round-bale end: spiral rings (coloured, clamped).</summary>
        public static Texture2D HayRings(string name, Color a, Color b)
        {
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                var p = new Vector2(x / (float)S - 0.5f, y / (float)S - 0.5f);
                float r = p.magnitude, ang = Mathf.Atan2(p.y, p.x);
                float s = Mathf.Repeat(r * 22f + ang / (2f * Mathf.PI), 1f);
                var c = Color.Lerp(a, b, s < 0.35f ? 1f : 0f);
                c = Color.Lerp(c, b * 0.8f, Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.25f);
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Save(name, tex, repeat: false);
        }

        /// <summary>Awning/canvas stripes along U (coloured).</summary>
        public static Texture2D Stripes(string name, Color a, Color b, int stripes)
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int s = Mathf.FloorToInt(x / (float)S * stripes * 2f);
                var c = s % 2 == 0 ? a : b;
                c *= 0.96f + Hash(x / 3, y / 3, 3) * 0.04f;
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Save(name, tex);
        }

        /// <summary>Palette strip (one texel per colour, point-filtered) for bunting flags and similar.</summary>
        public static Texture2D Palette(string name, Color[] colors)
        {
            var tex = new Texture2D(colors.Length, 1, TextureFormat.RGBA32, false);
            for (int i = 0; i < colors.Length; i++) tex.SetPixel(i, 0, colors[i]);
            tex.Apply();
            return Save(name, tex, repeat: false, point: true, maxSize: 64);
        }

        /// <summary>Clock face (cream disc, ink ticks and rim) on a transparent-free square.</summary>
        public static Texture2D ClockFace(string name, Color face, Color ink, Color rim)
        {
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                var p = new Vector2(x / (float)S - 0.5f, y / (float)S - 0.5f) * 2f;
                float r = p.magnitude, ang = Mathf.Atan2(p.y, p.x) / (2f * Mathf.PI) * 12f;
                Color c = r > 0.86f ? rim : face;
                float tick = Mathf.Abs(ang - Mathf.Round(ang));
                bool major = Mathf.Abs(Mathf.Repeat(Mathf.Round(ang), 3f)) < 0.01f;
                if (r > 0.62f && r < 0.8f && tick < (major ? 0.06f : 0.025f)) c = ink;
                if (r > 0.84f && r < 0.88f) c = ink;
                if (r < 0.06f) c = ink;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Save(name, tex, repeat: false);
        }

        /// <summary>Four-pane window (glass with cream mullions and frame), coloured.</summary>
        public static Texture2D Window(string name, Color glass, Color frame, Color shine)
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;
                bool bar = Mathf.Abs(u - 0.5f) < 0.035f || Mathf.Abs(v - 0.5f) < 0.035f || u < 0.06f || u > 0.94f || v < 0.06f || v > 0.94f;
                Color c = bar ? frame : Color.Lerp(glass, glass * 1.25f, v * 0.6f);
                // diagonal sheen in each pane
                float pu = Mathf.Repeat(u * 2f, 1f), pv = Mathf.Repeat(v * 2f, 1f);
                if (!bar && Mathf.Abs(pu + pv - 1.1f) < 0.07f) c = Color.Lerp(c, shine, 0.55f);
                c.a = 1f;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return Save(name, tex, repeat: false);
        }

        /// <summary>Grass with painted blade strokes and two-tone patches (replaces the flat speckle look).</summary>
        public static Texture2D GrassStrokes(string name, Color baseCol, Color dark, Color light, int seed)
        {
            const int S = 256;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, true);
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;
                float n = TNoise(u, v, 4, seed) * 0.65f + TNoise(u, v, 12, seed + 3) * 0.35f;
                float t = Mathf.Round(Mathf.Clamp01((n - 0.3f) * 2f) * 3f) / 3f;
                px[y * S + x] = Color.Lerp(baseCol, dark, t * 0.45f);
            }
            var rng = new System.Random(seed);
            for (int k = 0; k < 2600; k++)
            {
                int x0 = rng.Next(S), y0 = rng.Next(S), len = 3 + rng.Next(5);
                var col = rng.NextDouble() < 0.6 ? light : dark;
                float lean = (float)(rng.NextDouble() - 0.5) * 0.8f;
                for (int t = 0; t < len; t++)
                {
                    int x = ((int)(x0 + lean * t) % S + S) % S, y = ((y0 + t) % S + S) % S;
                    px[y * S + x] = Color.Lerp(px[y * S + x], col, 0.35f + 0.1f * t / len);
                }
            }
            tex.SetPixels(px); tex.Apply();
            return Save(name, tex);
        }
    }
}
