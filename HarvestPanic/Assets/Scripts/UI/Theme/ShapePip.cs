using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    public enum PipShape { Circle, Square, Triangle, Diamond, Check }

    /// <summary>
    /// Small vector shape: team pips pair colour with a shape (art-bible 7.6: never colour alone), and a check mark for ready.
    /// </summary>
    public sealed class ShapePip : VisualElement
    {
        PipShape _shape;
        Color _fill = Color.white;
        static readonly Color Ink = new Color(0.17f, 0.13f, 0.09f);

        public ShapePip(PipShape shape, Color fill, float size = 44f)
        {
            _shape = shape; _fill = fill;
            style.width = size; style.height = size;
            style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(PipShape shape, Color fill)
        {
            _shape = shape; _fill = fill; MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 1f) return;
            var p = ctx.painter2D;
            float cx = r.width * 0.5f, cy = r.height * 0.5f, h = Mathf.Min(r.width, r.height) * 0.5f - 3f;
            p.lineWidth = 4f; p.strokeColor = Ink; p.fillColor = _fill; p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
            p.BeginPath();
            switch (_shape)
            {
                case PipShape.Circle: p.Arc(new Vector2(cx, cy), h, 0f, 360f); break;
                case PipShape.Square: p.MoveTo(new Vector2(cx - h * .85f, cy - h * .85f)); p.LineTo(new Vector2(cx + h * .85f, cy - h * .85f)); p.LineTo(new Vector2(cx + h * .85f, cy + h * .85f)); p.LineTo(new Vector2(cx - h * .85f, cy + h * .85f)); p.ClosePath(); break;
                case PipShape.Triangle: p.MoveTo(new Vector2(cx, cy - h)); p.LineTo(new Vector2(cx + h, cy + h * .8f)); p.LineTo(new Vector2(cx - h, cy + h * .8f)); p.ClosePath(); break;
                case PipShape.Diamond: p.MoveTo(new Vector2(cx, cy - h)); p.LineTo(new Vector2(cx + h, cy)); p.LineTo(new Vector2(cx, cy + h)); p.LineTo(new Vector2(cx - h, cy)); p.ClosePath(); break;
                case PipShape.Check:
                    p.lineWidth = 7f;
                    p.MoveTo(new Vector2(cx - h * .75f, cy)); p.LineTo(new Vector2(cx - h * .15f, cy + h * .6f)); p.LineTo(new Vector2(cx + h * .8f, cy - h * .6f));
                    p.Stroke();
                    return;
            }
            p.Fill(); p.Stroke();
        }

        /// <summary>Colourblind-safe (Okabe-Ito) team colours and their paired shapes, by slot 0..3.</summary>
        public static Color TeamColor(int slot)
        {
            switch (((slot % 4) + 4) % 4)
            {
                case 0: return new Color32(0xE6, 0x9F, 0x00, 255);
                case 1: return new Color32(0x56, 0xB4, 0xE9, 255);
                case 2: return new Color32(0x00, 0x9E, 0x73, 255);
                default: return new Color32(0xCC, 0x79, 0xA7, 255);
            }
        }

        public static PipShape TeamShape(int slot)
        {
            switch (((slot % 4) + 4) % 4)
            {
                case 0: return PipShape.Circle;
                case 1: return PipShape.Square;
                case 2: return PipShape.Triangle;
                default: return PipShape.Diamond;
            }
        }
    }
}
