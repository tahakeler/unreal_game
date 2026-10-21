using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.Customization
{
    /// <summary>Reserved meaning colours used by the creator (same table as Frontend's UiPalette; tests keep them equal).</summary>
    public enum CreatorReserved { Money, Loss, Landlord, Danger, Good }

    /// <summary>
    /// Look of the character creator: Grandpa's ledger/scrapbook (art bible section 7). Reserved colours follow the
    /// colour-blind mode published in <see cref="UiAccessibility"/> (Okabe-Ito swaps, identical to UiPalette), and
    /// paper/desk/tape textures are generated once at runtime (no texture assets to import).
    /// </summary>
    public static class CreatorTheme
    {
        static readonly Color[] Normal = { Hex(0xF2B72B), Hex(0xE5533D), Hex(0x7E4A8C), Hex(0xB4E04A), Hex(0x7FA34A) };
        static readonly Color[] RedGreen = { Hex(0xE69F00), Hex(0xD55E00), Hex(0xCC79A7), Hex(0xF0E442), Hex(0x0072B2) };
        static readonly Color[] Tritan = { Hex(0xE69F00), Hex(0xC0392B), Hex(0x4B4B9A), Hex(0xFF5FA2), Hex(0x009E73) };

        /// <summary>Ink (text and outlines).</summary>
        public static readonly Color Ink = Hex(0x2B2118);
        /// <summary>Ledger paper.</summary>
        public static readonly Color Paper = Hex(0xF6EBD3);
        /// <summary>Darker paper (thumbnail backgrounds).</summary>
        public static readonly Color PaperDark = Hex(0xE3D3AA);
        /// <summary>Sticky-note colours cycled on the tabs.</summary>
        public static readonly Color[] Sticky = { Hex(0xF7E27A), Hex(0xF5B5C8), Hex(0xB8E3C4), Hex(0xB6DAF0), Hex(0xF6C99A) };

        /// <summary>Reserved colour for the current colour-blind mode.</summary>
        public static Color Get(CreatorReserved c) => Get(c, UiAccessibility.Colorblind);

        /// <summary>Reserved colour for <paramref name="mode"/>.</summary>
        public static Color Get(CreatorReserved c, UiColorblind mode)
        {
            var table = mode == UiColorblind.Off ? Normal : mode == UiColorblind.Tritanopia ? Tritan : RedGreen;
            return table[(int)c];
        }

        /// <summary>USS class Frontend uses for the mode ("" when off); the creator stylesheet keys off the same names.</summary>
        public static string RootClass(UiColorblind mode)
        {
            switch (mode)
            {
                case UiColorblind.Deuteranopia: return "hp-root--cb-deutan";
                case UiColorblind.Protanopia: return "hp-root--cb-protan";
                case UiColorblind.Tritanopia: return "hp-root--cb-tritan";
                default: return "";
            }
        }

        /// <summary>Applies colour-blind / motion / text-size classes to the creator root.</summary>
        public static void ApplyRootClasses(VisualElement root)
        {
            foreach (UiColorblind m in System.Enum.GetValues(typeof(UiColorblind)))
            {
                string cls = RootClass(m);
                if (cls.Length > 0) root.EnableInClassList(cls, m == UiAccessibility.Colorblind);
            }
            root.EnableInClassList("cc-colorblind", UiAccessibility.Colorblind != UiColorblind.Off);
            root.EnableInClassList("cc-root--reduced-motion", UiAccessibility.ReducedMotion);
            root.EnableInClassList("cc-root--text-small", UiAccessibility.TextSize == 0);
            root.EnableInClassList("cc-root--text-large", UiAccessibility.TextSize == 2);
        }

        static Texture2D _desk, _paper, _tape, _backdrop;

        /// <summary>Dark kraft desk with a soft vignette (screen background).</summary>
        public static Texture2D Desk => _desk != null ? _desk : _desk = MakeDesk();
        /// <summary>Ledger paper: cream, faint blue rules and a red margin line.</summary>
        public static Texture2D LedgerPaper => _paper != null ? _paper : _paper = MakePaper();
        /// <summary>Masking-tape strip.</summary>
        public static Texture2D Tape => _tape != null ? _tape : _tape = MakeTape();
        /// <summary>Photo-studio backdrop for the 3D turntable (warm sky to straw, light behind the farmer).</summary>
        public static Texture2D Backdrop => _backdrop != null ? _backdrop : _backdrop = MakeBackdrop();

        static Texture2D New(int w, int h, string name, TextureWrapMode wrap = TextureWrapMode.Clamp) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = wrap, filterMode = FilterMode.Bilinear };

        static Texture2D MakeDesk()
        {
            const int w = 256, h = 144;
            var t = New(w, h, "HP_CreatorDesk");
            var rng = new System.Random(7);
            var px = new Color[w * h];
            Color a = Hex(0x4A3322), b = Hex(0x2E1F15);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1), v = y / (float)(h - 1);
                    float grain = 0.5f + 0.5f * Mathf.Sin(v * 90f + Mathf.Sin(u * 7f) * 3f + Mathf.Sin(u * 31f) * 0.6f);
                    float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.45f, 0.55f));
                    float vignette = Mathf.Clamp01(1.1f - d * 1.25f);
                    Color c = Color.Lerp(b, a, vignette) * (0.93f + 0.07f * grain);
                    c *= 0.985f + 0.03f * (float)rng.NextDouble();
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        static Texture2D MakePaper()
        {
            const int w = 512, h = 512;
            var t = New(w, h, "HP_CreatorPaper");
            var rng = new System.Random(11);
            var px = new Color[w * h];
            Color paper = Paper, rule = new Color(0.55f, 0.7f, 0.85f, 1f), margin = new Color(0.85f, 0.45f, 0.4f, 1f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color c = paper * (0.975f + 0.025f * (float)rng.NextDouble());
                    int fromTop = h - 1 - y;
                    if (fromTop > 40 && fromTop % 34 == 0) c = Color.Lerp(c, rule, 0.35f);
                    if (x == 40 || x == 43) c = Color.Lerp(c, margin, 0.4f);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        static Texture2D MakeTape()
        {
            const int w = 128, h = 40;
            var t = New(w, h, "HP_CreatorTape");
            var rng = new System.Random(3);
            var px = new Color[w * h];
            var tape = new Color(0.93f, 0.88f, 0.72f, 0.82f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int jag = (int)(3 + 3 * Mathf.Sin(y * 1.7f) + 2 * (float)rng.NextDouble());
                    bool inside = x > jag && x < w - 1 - jag;
                    var c = tape * (0.96f + 0.04f * (float)rng.NextDouble());
                    c.a = inside ? tape.a : 0f;
                    px[y * w + x] = c;
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        static Texture2D MakeBackdrop()
        {
            const int w = 128, h = 256;
            var t = New(w, h, "HP_CreatorBackdrop");
            var px = new Color[w * h];
            Color top = Hex(0xBFE0EC), mid = Hex(0xF6E7C4), bottom = Hex(0xE2B877);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = y / (float)(h - 1), u = x / (float)(w - 1);
                    Color c = v > 0.45f ? Color.Lerp(mid, top, Mathf.SmoothStep(0f, 1f, (v - 0.45f) / 0.55f)) : Color.Lerp(bottom, mid, Mathf.SmoothStep(0f, 1f, v / 0.45f));
                    float glow = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v * 1.4f), new Vector2(0.5f, 0.7f)) * 1.6f);
                    c = Color.Lerp(c, Color.white, glow * 0.35f);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
    }

    /// <summary>Vector glyphs for the creator (no icon font): padlock, check stamp, dice, revert arrow, play, name-tag shapes.</summary>
    public enum CreatorGlyph { Lock, Check, Dice, Revert, Play, Shape, Spin }

    /// <summary>A small vector icon drawn with Painter2D. Size it with USS width/height; colour with <see cref="Tint"/>.</summary>
    public sealed class CreatorIcon : VisualElement
    {
        CreatorGlyph _glyph;
        TagShape _shape;
        Color _tint = CreatorTheme.Ink, _outline = CreatorTheme.Ink;
        float _outlineWidth;

        /// <summary>Creates an icon.</summary>
        public CreatorIcon(CreatorGlyph glyph, Color tint, TagShape shape = TagShape.Circle)
        {
            _glyph = glyph; _tint = tint; _shape = shape;
            pickingMode = PickingMode.Ignore;
            AddToClassList("cc-icon");
            generateVisualContent += Draw;
        }

        /// <summary>Fill colour.</summary>
        public Color Tint { get => _tint; set { _tint = value; MarkDirtyRepaint(); } }

        /// <summary>Shape (for <see cref="CreatorGlyph.Shape"/>).</summary>
        public TagShape TagShape { get => _shape; set { _shape = value; MarkDirtyRepaint(); } }

        /// <summary>Optional ink outline width (px) for shapes.</summary>
        public void SetOutline(Color c, float width) { _outline = c; _outlineWidth = width; MarkDirtyRepaint(); }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float s = Mathf.Min(r.width, r.height);
            if (s <= 1f) return;
            var c = new Vector2(r.x + r.width * 0.5f, r.y + r.height * 0.5f);
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
            switch (_glyph)
            {
                case CreatorGlyph.Lock:
                {
                    p.strokeColor = _tint; p.lineWidth = s * 0.12f;
                    p.BeginPath();
                    Circle(p, c + new Vector2(0, -s * 0.12f), s * 0.2f, 180f, 360f, false);
                    p.Stroke();
                    p.fillColor = _tint;
                    Rect(p, c.x - s * 0.33f, c.y - s * 0.08f, s * 0.66f, s * 0.48f, s * 0.08f);
                    p.Fill();
                    p.fillColor = new Color(1f, 1f, 1f, 0.85f);
                    p.BeginPath(); Circle(p, c + new Vector2(0, s * 0.12f), s * 0.07f, 0f, 360f, true); p.Fill();
                    break;
                }
                case CreatorGlyph.Check:
                    p.strokeColor = _tint; p.lineWidth = s * 0.16f;
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(-s * 0.3f, 0f));
                    p.LineTo(c + new Vector2(-s * 0.08f, s * 0.22f));
                    p.LineTo(c + new Vector2(s * 0.32f, -s * 0.26f));
                    p.Stroke();
                    break;
                case CreatorGlyph.Dice:
                {
                    p.fillColor = new Color(1f, 0.99f, 0.95f, 1f);
                    p.strokeColor = CreatorTheme.Ink; p.lineWidth = Mathf.Max(2f, s * 0.08f);
                    Rect(p, c.x - s * 0.4f, c.y - s * 0.4f, s * 0.8f, s * 0.8f, s * 0.16f);
                    p.Fill(); p.Stroke();
                    p.fillColor = _tint;
                    foreach (var d in new[] { new Vector2(-0.2f, -0.2f), new Vector2(0.2f, 0.2f), Vector2.zero, new Vector2(0.2f, -0.2f), new Vector2(-0.2f, 0.2f) })
                    { p.BeginPath(); Circle(p, c + d * s, s * 0.075f, 0f, 360f, true); p.Fill(); }
                    break;
                }
                case CreatorGlyph.Revert:
                case CreatorGlyph.Spin:
                {
                    p.strokeColor = _tint; p.lineWidth = s * 0.13f;
                    p.BeginPath();
                    Circle(p, c, s * 0.3f, _glyph == CreatorGlyph.Revert ? 200f : 20f, _glyph == CreatorGlyph.Revert ? 480f : 300f, false);
                    p.Stroke();
                    float a = (_glyph == CreatorGlyph.Revert ? 200f : 300f) * Mathf.Deg2Rad;
                    var tip = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * 0.3f;
                    p.fillColor = _tint;
                    p.BeginPath();
                    p.MoveTo(tip + new Vector2(-s * 0.17f, -s * 0.02f));
                    p.LineTo(tip + new Vector2(s * 0.14f, -s * 0.08f));
                    p.LineTo(tip + new Vector2(0.0f, s * 0.18f));
                    p.ClosePath(); p.Fill();
                    break;
                }
                case CreatorGlyph.Play:
                    p.fillColor = _tint;
                    p.BeginPath();
                    p.MoveTo(c + new Vector2(-s * 0.22f, -s * 0.3f));
                    p.LineTo(c + new Vector2(s * 0.32f, 0f));
                    p.LineTo(c + new Vector2(-s * 0.22f, s * 0.3f));
                    p.ClosePath(); p.Fill();
                    break;
                case CreatorGlyph.Shape:
                {
                    var pts = NameTag.ShapeOutline(_shape);
                    p.fillColor = _tint;
                    p.BeginPath();
                    for (int i = 0; i < pts.Count; i++)
                    {
                        var q = c + new Vector2(pts[i].x, -pts[i].y) * s * 0.92f;
                        if (i == 0) p.MoveTo(q); else p.LineTo(q);
                    }
                    p.ClosePath();
                    p.Fill();
                    if (_outlineWidth > 0f) { p.strokeColor = _outline; p.lineWidth = _outlineWidth; p.Stroke(); }
                    break;
                }
            }
        }

        static void Circle(Painter2D p, Vector2 c, float radius, float fromDeg, float toDeg, bool closed)
        {
            int seg = Mathf.Max(8, Mathf.CeilToInt(Mathf.Abs(toDeg - fromDeg) / 12f));
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, i / (float)seg) * Mathf.Deg2Rad;
                var q = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                if (i == 0) p.MoveTo(q); else p.LineTo(q);
            }
            if (closed) p.ClosePath();
        }

        static void Rect(Painter2D p, float x, float y, float w, float h, float r)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x + r, y));
            p.LineTo(new Vector2(x + w - r, y));
            p.LineTo(new Vector2(x + w, y + r));
            p.LineTo(new Vector2(x + w, y + h - r));
            p.LineTo(new Vector2(x + w - r, y + h));
            p.LineTo(new Vector2(x + r, y + h));
            p.LineTo(new Vector2(x, y + h - r));
            p.LineTo(new Vector2(x, y + r));
            p.ClosePath();
        }
    }
}
