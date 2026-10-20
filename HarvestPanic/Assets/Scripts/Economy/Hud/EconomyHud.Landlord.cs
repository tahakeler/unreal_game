using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// HUD polish (hud-menus, weather-twists, landlord-events): forecast line on the morning card, a painted weather
    /// icon on the clock, "FRIENDLY REMINDER" Landlord rule memos, Landlord bark toasts, awards on receipts, and the
    /// Sunday receipt held back until the Landlord's walk-in verdict has played.
    /// </summary>
    public sealed partial class EconomyHud
    {
        Label _forecast;
        VisualElement _weatherIcon, _rules;
        WeatherKind _iconKind = (WeatherKind)255;
        string _rulesKey;
        ReceiptData _pendingSunday;

        /// <summary>Forecast text currently on the morning card (tests).</summary>
        public string ForecastShown => _forecast != null && _forecast.style.display != DisplayStyle.None ? _forecast.text : null;
        /// <summary>Weather the clock icon shows (tests).</summary>
        public WeatherKind? WeatherIconShown => _weatherIcon != null && _weatherIcon.style.display != DisplayStyle.None ? _iconKind : (WeatherKind?)null;
        /// <summary>Number of Landlord rule memos on screen (tests).</summary>
        public int RuleCardsShown => _rules?.childCount ?? 0;
        /// <summary>True when the receipt overlay is visible (tests).</summary>
        public bool ReceiptVisible => _overlay != null && _overlay.style.display == DisplayStyle.Flex && !_shopShown;

        void BuildLandlord(VisualElement orderCard)
        {
            _forecast = Text(orderCard, "", "hp-forecast");
            _forecast.style.display = DisplayStyle.None;
            _weatherIcon = Add(_clock, "hp-weather");
            _weatherIcon.generateVisualContent += PaintWeather;
            _weatherIcon.style.display = DisplayStyle.None;
            _rules = Add(_root, "hp-rules");
        }

        void RefreshLandlord()
        {
            var d = _dir;
            bool hasWeather = Services.TryGet(out IWeatherService w) && w != null;
            bool morning = d.Phase == DayPhase.Morning;
            string fc = hasWeather ? w.ForecastText : null;
            _forecast.text = string.IsNullOrEmpty(fc) ? "" : "FORECAST: " + fc;
            _forecast.style.display = morning && !string.IsNullOrEmpty(fc) ? DisplayStyle.Flex : DisplayStyle.None;
            _weatherIcon.style.display = hasWeather ? DisplayStyle.Flex : DisplayStyle.None;
            if (hasWeather && w.Current != _iconKind) { _iconKind = w.Current; _weatherIcon.MarkDirtyRepaint(); }

            string key = "";
            foreach (var m in d.ActiveLandlordRules) key += m.ModifierId + ";";
            if (key != _rulesKey)
            {
                _rulesKey = key;
                _rules.Clear();
                foreach (var m in d.ActiveLandlordRules)
                {
                    var card = Add(_rules, "hp-rule-card", "hp-shadow");
                    Text(card, "FRIENDLY REMINDER", "hp-rule-stamp", true);
                    Text(card, m.CardText, "hp-rule-text");
                }
            }

            if (_pendingSunday != null && (LandlordVisitor.Current == null || !LandlordVisitor.Current.IsPlaying))
            {
                var r = _pendingSunday; _pendingSunday = null;
                ShowReceipt(r);
            }
        }

        /// <summary>True when the Sunday receipt should wait for the walk-in.</summary>
        bool DeferSunday(ReceiptData r)
        {
            if (!r.IsSunday || LandlordVisitor.Current == null || !LandlordVisitor.Current.IsPlaying) return false;
            _pendingSunday = r;
            return true;
        }

        void OnLandlordBark(string line) => Toast("THE LANDLORD: " + line, "hp-toast-landlord");

        void AddAwards(VisualElement body, ReceiptData r)
        {
            if (r.Awards == null || r.Awards.Count == 0) return;
            Text(body, r.IsSunday ? "WEEKLY AWARDS" : "TODAY'S AWARDS", "hp-r-section", true);
            foreach (var a in r.Awards)
            {
                var row = Add(body, "hp-award");
                Text(row, "★ " + a.Title, "hp-award-title", true);
                Text(row, a.Name + " — " + a.Reason, "hp-award-who");
            }
        }

        void PaintWeather(MeshGenerationContext ctx)
        {
            var p = ctx.painter2D;
            var r = _weatherIcon.contentRect;
            Vector2 c = r.center; float s = Mathf.Min(r.width, r.height) * 0.5f;
            var ink = new Color32(0x2B, 0x21, 0x18, 0xFF);
            void Circle(Vector2 at, float rad, Color fill) { p.fillColor = fill; p.strokeColor = ink; p.lineWidth = 2f; p.BeginPath(); p.Arc(at, rad, 0f, 360f); p.ClosePath(); p.Fill(); p.Stroke(); }
            void Line(Vector2 a, Vector2 b, Color col, float wdt) { p.strokeColor = col; p.lineWidth = wdt; p.BeginPath(); p.MoveTo(a); p.LineTo(b); p.Stroke(); }
            void Cloud(Color fill) { Circle(c + new Vector2(-s * 0.35f, 0f), s * 0.32f, fill); Circle(c + new Vector2(s * 0.3f, s * 0.02f), s * 0.36f, fill); Circle(c + new Vector2(0f, -s * 0.25f), s * 0.38f, fill); }
            var sun = new Color32(0xF5, 0xC5, 0x42, 0xFF);
            switch (_iconKind)
            {
                case WeatherKind.Clear:
                case WeatherKind.Heatwave:
                    var col = _iconKind == WeatherKind.Heatwave ? new Color32(0xE5, 0x6B, 0x3B, 0xFF) : sun;
                    for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); Line(c + dir * s * 0.55f, c + dir * s * 0.9f, ink, 3f); }
                    Circle(c, s * 0.45f, col);
                    break;
                case WeatherKind.Rain:
                case WeatherKind.Storm:
                    Cloud(_iconKind == WeatherKind.Storm ? new Color(0.45f, 0.45f, 0.5f) : new Color(0.85f, 0.88f, 0.92f));
                    for (int i = -1; i <= 1; i++) Line(c + new Vector2(i * s * 0.35f, s * 0.42f), c + new Vector2(i * s * 0.35f - s * 0.1f, s * 0.8f), new Color32(0x3A, 0x7B, 0xD5, 0xFF), 3f);
                    if (_iconKind == WeatherKind.Storm) { p.fillColor = sun; p.BeginPath(); p.MoveTo(c + new Vector2(0.05f, 0.3f) * s); p.LineTo(c + new Vector2(-0.2f, 0.65f) * s); p.LineTo(c + new Vector2(0f, 0.6f) * s); p.LineTo(c + new Vector2(-0.12f, 0.95f) * s); p.LineTo(c + new Vector2(0.2f, 0.5f) * s); p.LineTo(c + new Vector2(0f, 0.52f) * s); p.ClosePath(); p.Fill(); }
                    break;
                case WeatherKind.Fog:
                    for (int i = -1; i <= 1; i++) Line(c + new Vector2(-s * 0.8f, i * s * 0.4f), c + new Vector2(s * 0.8f, i * s * 0.4f), new Color(0.55f, 0.55f, 0.6f), 5f);
                    break;
                case WeatherKind.Wind:
                    for (int i = -1; i <= 1; i++) { p.strokeColor = ink; p.lineWidth = 3f; p.BeginPath(); p.MoveTo(c + new Vector2(-s * 0.85f, i * s * 0.38f)); p.BezierCurveTo(c + new Vector2(0f, i * s * 0.38f - s * 0.3f), c + new Vector2(s * 0.4f, i * s * 0.38f + s * 0.2f), c + new Vector2(s * 0.85f, i * s * 0.38f)); p.Stroke(); }
                    break;
            }
        }
    }
}
