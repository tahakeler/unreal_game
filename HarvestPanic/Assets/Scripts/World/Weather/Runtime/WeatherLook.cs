using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Weather
{
    /// <summary>One resolved lighting state (sun, sky, ambient, fog, grade). Pure data for blending and tests.</summary>
    public struct WeatherLook
    {
        public Color SunColor; public float SunIntensity; public float SunPitch;
        public Color SkyTint; public float SkyExposure;
        public Color Ambient;   // multiplies the scene's trilight ambient
        public Color FogColor; public float FogStart, FogEnd;
        public float Saturation; public float PostExposure;

        /// <summary>Linear blend.</summary>
        public static WeatherLook Lerp(in WeatherLook a, in WeatherLook b, float t) => new WeatherLook
        {
            SunColor = Color.Lerp(a.SunColor, b.SunColor, t), SunIntensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, t), SunPitch = Mathf.Lerp(a.SunPitch, b.SunPitch, t),
            SkyTint = Color.Lerp(a.SkyTint, b.SkyTint, t), SkyExposure = Mathf.Lerp(a.SkyExposure, b.SkyExposure, t),
            Ambient = Color.Lerp(a.Ambient, b.Ambient, t), FogColor = Color.Lerp(a.FogColor, b.FogColor, t),
            FogStart = Mathf.Lerp(a.FogStart, b.FogStart, t), FogEnd = Mathf.Lerp(a.FogEnd, b.FogEnd, t),
            Saturation = Mathf.Lerp(a.Saturation, b.Saturation, t), PostExposure = Mathf.Lerp(a.PostExposure, b.PostExposure, t),
        };

        // perf: memoized -- ForPhase/WithWeather run every LateUpdate and the "#" + h concat was ~650 B/frame of GC
        static readonly System.Collections.Generic.Dictionary<string, Color> HexCache = new System.Collections.Generic.Dictionary<string, Color>();
        static Color Hex(string h)
        {
            if (HexCache.TryGetValue(h, out var c)) return c;
            ColorUtility.TryParseHtmlString("#" + h, out c);
            HexCache[h] = c;
            return c;
        }

        /// <summary>
        /// Art bible §4.3 phase table: Morning #FFE2B8 warm and low, Workday #FFF3DC bright (arc up then down),
        /// Dusk #FF9A6A low 8° with violet sky, Payout/Sunday settle into dusk-violet.
        /// </summary>
        public static WeatherLook ForPhase(DayClockPhase phase, float progress01)
        {
            var morning = new WeatherLook { SunColor = Hex("FFE2B8"), SunIntensity = 1.5f, SunPitch = 16f, SkyTint = Hex("C9D3DE"), SkyExposure = 1.05f, Ambient = Hex("FFF0DC"), FogColor = Hex("F0DCB8"), FogStart = 90f, FogEnd = 520f, Saturation = 4f, PostExposure = 0.05f };
            var noon = new WeatherLook { SunColor = Hex("FFF3DC"), SunIntensity = 2.0f, SunPitch = 52f, SkyTint = Hex("B3CCE3"), SkyExposure = 1.2f, Ambient = Color.white, FogColor = Hex("E9D9BC"), FogStart = 140f, FogEnd = 700f, Saturation = 8f, PostExposure = 0.1f };
            var afternoon = noon; afternoon.SunPitch = 32f; afternoon.SunColor = Hex("FFE9C4");
            var dusk = new WeatherLook { SunColor = Hex("FF9A6A"), SunIntensity = 1.0f, SunPitch = 8f, SkyTint = Hex("8A7AB8"), SkyExposure = 0.85f, Ambient = Hex("B9A8D0"), FogColor = Hex("E6A487"), FogStart = 60f, FogEnd = 420f, Saturation = 12f, PostExposure = -0.05f };
            // Polish-2: moonlit night (was too bright to need the lantern): cold low key light, near-black ambient and fog,
            // strong negative exposure so the hand lantern and lantern posts carry the scene. Mr. Stitches stays readable as a
            // moonlit silhouette against the lighter sky (SkyExposure above the ground's effective brightness).
            var night = new WeatherLook { SunColor = Hex("8FAEE6"), SunIntensity = 0.16f, SunPitch = 34f, SkyTint = Hex("142638"), SkyExposure = 0.28f, Ambient = Hex("24324A"), FogColor = Hex("0B1420"), FogStart = 14f, FogEnd = 150f, Saturation = -28f, PostExposure = -0.75f };
            float p = Mathf.Clamp01(progress01);
            switch (phase)
            {
                case DayClockPhase.Morning: return Lerp(morning, noon, p * 0.2f);
                case DayClockPhase.Workday:
                    if (p < 0.5f) return Lerp(Lerp(morning, noon, 0.2f), noon, p / 0.5f);
                    if (p < 0.8f) return Lerp(noon, afternoon, (p - 0.5f) / 0.3f);
                    return Lerp(afternoon, dusk, (p - 0.8f) / 0.2f); // last 20% sinks to dusk (art bible)
                case DayClockPhase.Dusk: return Lerp(dusk, night, NightBlend(p));
                case DayClockPhase.Payout:
                case DayClockPhase.Sunday:
                case DayClockPhase.Evicted: return Lerp(dusk, night, 0.4f);
                default: return afternoon; // no clock: Muddlebury's golden afternoon
            }
        }

        /// <summary>
        /// Dusk -> night blend weight for Dusk progress (polish-2): smoothstep reaching full night at 55% of Dusk
        /// (old curve stopped at 60% night), so most of the night phase is properly dark.
        /// </summary>
        public static float NightBlend(float progress01) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress01) / 0.55f);

        /// <summary>Applies the weather on top of a phase look.</summary>
        public static WeatherLook WithWeather(WeatherLook l, WeatherKind k, float fogDistance)
        {
            switch (k)
            {
                case WeatherKind.Rain:
                case WeatherKind.Storm:
                {
                    bool storm = k == WeatherKind.Storm;
                    var grey = storm ? Hex("6E7684") : Hex("9AA3AE");
                    l.SunColor = Color.Lerp(l.SunColor, grey, 0.6f);
                    l.SunIntensity *= storm ? 0.35f : 0.5f;
                    l.SkyTint = Color.Lerp(l.SkyTint, grey, 0.8f); l.SkyExposure *= storm ? 0.55f : 0.75f;
                    l.Ambient = Color.Lerp(l.Ambient, Hex("A8B0BC"), 0.6f) * (storm ? 0.75f : 0.9f);
                    l.FogColor = Color.Lerp(l.FogColor, grey, 0.8f); l.FogStart = storm ? 10f : 20f; l.FogEnd = storm ? 140f : 220f;
                    l.Saturation -= storm ? 25f : 15f; l.PostExposure -= storm ? 0.2f : 0.1f;
                    break;
                }
                case WeatherKind.Fog:
                    l.SunIntensity *= 0.6f; l.SunColor = Color.Lerp(l.SunColor, Color.white, 0.4f);
                    l.SkyTint = Color.Lerp(l.SkyTint, Hex("D6D8D4"), 0.8f);
                    l.FogColor = Color.Lerp(l.FogColor, Hex("CDD2CE"), 0.75f);
                    l.FogStart = 1f; l.FogEnd = fogDistance > 0f ? fogDistance : 22f;
                    l.Ambient = Color.Lerp(l.Ambient, Hex("D0D4D2"), 0.5f); l.Saturation -= 20f;
                    break;
                case WeatherKind.Heatwave:
                    l.SunColor = Color.Lerp(l.SunColor, Hex("FFD08A"), 0.5f); l.SunIntensity *= 1.15f;
                    l.SkyTint = Color.Lerp(l.SkyTint, Hex("E3C9A0"), 0.45f);
                    l.FogColor = Color.Lerp(l.FogColor, Hex("F2D2A0"), 0.6f); l.FogStart *= 0.6f; l.FogEnd *= 0.8f;
                    l.Saturation += 6f; l.PostExposure += 0.12f;
                    break;
                case WeatherKind.Wind:
                    l.SkyTint = Color.Lerp(l.SkyTint, Hex("A6C4E0"), 0.3f); l.FogStart *= 1.2f; l.FogEnd *= 1.2f;
                    break;
            }
            return l;
        }
    }
}
