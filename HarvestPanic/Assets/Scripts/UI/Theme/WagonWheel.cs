using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    /// <summary>Busy indicator: a little wagon wheel that rolls (rotates). With reduced motion it only fades softly.</summary>
    public sealed class WagonWheel : VisualElement
    {
        static readonly Color Wood = new Color32(0x8A, 0x60, 0x40, 255), Rim = new Color32(0x5E, 0x3F, 0x27, 255);
        IVisualElementScheduledItem _spin;
        float _angle;

        public WagonWheel(float size = 64f)
        {
            AddToClassList("wagon-wheel");
            style.width = size; style.height = size; style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<AttachToPanelEvent>(_ => Start());
            RegisterCallback<DetachFromPanelEvent>(_ => { _spin?.Pause(); _spin = null; });
        }

        void Start()
        {
            _spin?.Pause();
            _spin = schedule.Execute(t =>
            {
                if (UiKit.ReducedMotion)
                {
                    style.rotate = new Rotate(0f);
                    style.opacity = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 2.2f);
                    return;
                }
                style.opacity = 1f;
                _angle = (_angle + t.deltaTime * 0.24f) % 360f;
                style.rotate = new Rotate(_angle);
            }).Every(16);
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r0 = contentRect;
            if (r0.width < 4f) return;
            var p = ctx.painter2D;
            var c = r0.center - r0.position;
            float r = Mathf.Min(r0.width, r0.height) * 0.5f - 3f;
            float lw = Mathf.Max(2.5f, r * 0.12f);
            p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            p.strokeColor = UiKit.Ink; p.lineWidth = lw + 3f;
            p.BeginPath(); p.Arc(c, r, 0f, 360f); p.Stroke();
            p.strokeColor = Rim; p.lineWidth = lw;
            p.BeginPath(); p.Arc(c, r, 0f, 360f); p.Stroke();
            p.strokeColor = UiKit.Ink; p.lineWidth = lw * 0.55f;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.BeginPath(); p.MoveTo(c + d * r * 0.22f); p.LineTo(c + d * (r - lw * 0.5f)); p.Stroke();
            }
            p.fillColor = Wood; p.strokeColor = UiKit.Ink; p.lineWidth = lw * 0.5f;
            p.BeginPath(); p.Arc(c, r * 0.24f, 0f, 360f); p.Fill(); p.Stroke();
        }
    }
}
