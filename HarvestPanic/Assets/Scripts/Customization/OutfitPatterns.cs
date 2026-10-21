using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Customization
{
    /// <summary>
    /// Procedural cloth patterns for outfits (character-style "Colour Palettes": Gingham Gamble, Patched Up, Mud Season,
    /// Debt Collector Pinstripe, plus flannel/denim/polka/sailor stripes). Small greyscale-ish multiply textures generated
    /// once at runtime and tinted by the zone colour, so the team colour stays dominant (art rule: >= 60%).
    /// The farmer meshes carry box-projected UVs in metres (FarmerAssetBuilder), so <see cref="Tiling"/> is "repeats per metre".
    /// </summary>
    public static class OutfitPatterns
    {
        const int Size = 128;
        static readonly Dictionary<OutfitPattern, Texture2D> Cache = new Dictionary<OutfitPattern, Texture2D>();

        /// <summary>Repeats per metre (u = across the body, v = up the body).</summary>
        public static Vector2 Tiling(OutfitPattern p)
        {
            switch (p)
            {
                case OutfitPattern.Gingham: return new Vector2(6f, 6f);
                case OutfitPattern.Pinstripe: return new Vector2(9f, 9f);
                case OutfitPattern.Patches: return new Vector2(1.7f, 1.7f);
                case OutfitPattern.Mud: return new Vector2(1.5f, 0.75f);
                case OutfitPattern.Flannel: return new Vector2(4.5f, 4.5f);
                case OutfitPattern.Denim: return new Vector2(7f, 7f);
                case OutfitPattern.Polka: return new Vector2(7f, 7f);
                case OutfitPattern.Stripes: return new Vector2(9f, 9f);
                default: return Vector2.one;
            }
        }

        /// <summary>Cached pattern texture (white = zone colour, darker = pattern).</summary>
        public static Texture2D Texture(OutfitPattern p)
        {
            if (Cache.TryGetValue(p, out var t) && t != null) return t;
            t = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "HP_Pattern_" + p, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            if (p == OutfitPattern.Mud) t.wrapModeV = TextureWrapMode.Clamp;
            var px = new Color32[Size * Size];
            var rng = new System.Random(1234 + (int)p);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    px[y * Size + x] = Sample(p, x, y);
            if (p == OutfitPattern.Patches) PaintPatches(px, rng);
            if (p == OutfitPattern.Mud) PaintMud(px, rng);
            if (p == OutfitPattern.Denim) PaintDenim(px, rng);
            t.SetPixels32(px);
            t.Apply(true, true);
            Cache[p] = t;
            return t;
        }

        static Color32 G(float v) { byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255); return new Color32(b, b, b, 255); }

        static Color32 Sample(OutfitPattern p, int x, int y)
        {
            float u = (x + 0.5f) / Size, v = (y + 0.5f) / Size;
            switch (p)
            {
                case OutfitPattern.Gingham:
                {
                    bool a = u < 0.5f, b = v < 0.5f;
                    return G(a && b ? 0.5f : a || b ? 0.74f : 1f);
                }
                case OutfitPattern.Pinstripe:
                    return G(Mathf.Abs(u - 0.5f) < 0.035f ? 0.55f : 0.93f);
                case OutfitPattern.Flannel:
                {
                    float k = 1f;
                    if (u < 0.32f) k *= 0.62f; else if (u > 0.58f && u < 0.68f) k *= 0.82f;
                    if (v < 0.32f) k *= 0.62f; else if (v > 0.58f && v < 0.68f) k *= 0.82f;
                    if (Mathf.Abs(u - 0.8f) < 0.012f || Mathf.Abs(v - 0.8f) < 0.012f) k *= 0.55f; // thin dark line
                    if (Mathf.Abs(u - 0.45f) < 0.01f || Mathf.Abs(v - 0.45f) < 0.01f) k = Mathf.Min(1f, k * 1.25f);
                    return G(k);
                }
                case OutfitPattern.Polka:
                {
                    float d1 = Vector2.Distance(new Vector2(u, v), new Vector2(0.25f, 0.25f));
                    float d2 = Vector2.Distance(new Vector2(u, v), new Vector2(0.75f, 0.75f));
                    return G(Mathf.Min(d1, d2) < 0.13f ? 0.48f : 1f);
                }
                case OutfitPattern.Stripes:
                    return G(v < 0.5f ? 0.4f : 1f);
                default:
                    return G(1f);
            }
        }

        static void PaintPatches(Color32[] px, System.Random rng)
        {
            for (int i = 0; i < Size * Size; i++) px[i] = G(0.96f);
            // A few square patches, each a different shade, with dashed stitching around them.
            var patches = new[] { (18, 20, 34, 30, 0.7f), (70, 12, 30, 36, 0.82f), (40, 72, 38, 30, 0.62f), (92, 78, 26, 28, 0.75f) };
            foreach (var (x0, y0, w, h, shade) in patches)
            {
                for (int y = y0; y < y0 + h; y++)
                    for (int x = x0; x < x0 + w; x++)
                    {
                        bool edge = x == x0 + 2 || x == x0 + w - 3 || y == y0 + 2 || y == y0 + h - 3;
                        bool dash = ((x + y) / 3) % 2 == 0;
                        px[(y % Size) * Size + (x % Size)] = G(edge && dash ? 0.35f : shade);
                    }
            }
        }

        static void PaintMud(Color32[] px, System.Random rng)
        {
            var mud = new Color(0.42f, 0.29f, 0.18f);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float v = (y + 0.5f) / Size;
                    float edge = 0.3f + 0.06f * Mathf.Sin(x * 0.21f) + 0.04f * Mathf.Sin(x * 0.53f + 1.3f);
                    float k = Mathf.Clamp01((edge - v) * 14f);
                    Color c = Color.Lerp(Color.white, mud, k);
                    px[y * Size + x] = c;
                }
            for (int i = 0; i < 26; i++) // splats above the line
            {
                int cx = rng.Next(Size), cy = (int)(Size * (0.3f + 0.25f * (float)rng.NextDouble()));
                int r = 2 + rng.Next(4);
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                        if (x * x + y * y <= r * r)
                        {
                            int yy = Mathf.Clamp(cy + y, 0, Size - 1), xx = (cx + x + Size) % Size;
                            px[yy * Size + xx] = mud;
                        }
            }
        }

        static void PaintDenim(Color32[] px, System.Random rng)
        {
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float twill = ((x + y) % 6) < 3 ? 0.9f : 1f;
                    float noise = 0.94f + 0.06f * (float)rng.NextDouble();
                    float seam = x % 64 == 0 || x % 64 == 1 ? 0.7f : 1f;
                    px[y * Size + x] = G(twill * noise * seam);
                }
        }
    }
}
