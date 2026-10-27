using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Weather
{
    /// <summary>
    /// One resolved lighting state (sun, sky, ambient, fog, grade, stylized-shading globals). Pure data for blending and
    /// tests; <see cref="WeatherLookApplier"/> pushes it to the scene. Visual overhaul (feature/visual-overhaul): the sky
    /// is a gradient (SkyTint = zenith, Horizon) with clouds/stars, fog takes the horizon colour (aerial perspective, not
    /// beige haze), and the post stack (contrast, lift, bloom, vignette, white balance) moves with the phase.
    /// </summary>
    public struct WeatherLook
    {
        public Color SunColor; public float SunIntensity; public float SunPitch;
        public Color SkyTint; public float SkyExposure;   // SkyTint = zenith colour of the stylized sky
        public Color Horizon;                             // sky horizon band
        public Color Ambient;   // multiplies the scene's trilight ambient
        public Color FogColor; public float FogStart, FogEnd;
        public float Saturation; public float PostExposure;
        // Sky extras
        public float CloudCover;      // 0..1
        public float Stars;           // 0..2 star brightness
        public Color SunDisc;         // HDR disc colour (sun by day, moon at night)
        // Stylized shading globals (HP_StylizedLighting.hlsl)
        public Color ShadowTint;      // multiplies the ambient on the unlit side of every stylized surface (white = none)
        public float AmbientFloor;    // linear light added to the ambient (night readability floor)
        public float RimBoost;        // rim *= 1 + RimBoost (silhouettes at night)
        // Grade
        public float Contrast;
        public Color Lift;            // LiftGammaGain lift: rgb tint, a = offset
        public float BloomIntensity, BloomThreshold;
        public float Vignette;
        public float Temperature;

        /// <summary>Linear blend.</summary>
        public static WeatherLook Lerp(in WeatherLook a, in WeatherLook b, float t) => new WeatherLook
        {
            SunColor = Color.Lerp(a.SunColor, b.SunColor, t), SunIntensity = Mathf.Lerp(a.SunIntensity, b.SunIntensity, t), SunPitch = Mathf.Lerp(a.SunPitch, b.SunPitch, t),
            SkyTint = Color.Lerp(a.SkyTint, b.SkyTint, t), SkyExposure = Mathf.Lerp(a.SkyExposure, b.SkyExposure, t), Horizon = Color.Lerp(a.Horizon, b.Horizon, t),
            Ambient = Color.Lerp(a.Ambient, b.Ambient, t), FogColor = Color.Lerp(a.FogColor, b.FogColor, t),
            FogStart = Mathf.Lerp(a.FogStart, b.FogStart, t), FogEnd = Mathf.Lerp(a.FogEnd, b.FogEnd, t),
            Saturation = Mathf.Lerp(a.Saturation, b.Saturation, t), PostExposure = Mathf.Lerp(a.PostExposure, b.PostExposure, t),
            CloudCover = Mathf.Lerp(a.CloudCover, b.CloudCover, t), Stars = Mathf.Lerp(a.Stars, b.Stars, t), SunDisc = Color.Lerp(a.SunDisc, b.SunDisc, t),
            ShadowTint = Color.Lerp(a.ShadowTint, b.ShadowTint, t), AmbientFloor = Mathf.Lerp(a.AmbientFloor, b.AmbientFloor, t), RimBoost = Mathf.Lerp(a.RimBoost, b.RimBoost, t),
            Contrast = Mathf.Lerp(a.Contrast, b.Contrast, t), Lift = Color.Lerp(a.Lift, b.Lift, t),
            BloomIntensity = Mathf.Lerp(a.BloomIntensity, b.BloomIntensity, t), BloomThreshold = Mathf.Lerp(a.BloomThreshold, b.BloomThreshold, t),
            Vignette = Mathf.Lerp(a.Vignette, b.Vignette, t), Temperature = Mathf.Lerp(a.Temperature, b.Temperature, t),
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

        static Color Hdr(string h, float intensity) { var c = Hex(h) * intensity; c.a = 1f; return c; }

        static WeatherLook _morning, _noon, _afternoon, _dusk, _night;
        static bool _built;

        static void Build()
        {
            // Art bible 4.3: Morning warm/low, Workday bright honey-gold, Dusk coral + violet, Night teal-black lit by lanterns.
            // Warm key light against cool sky ambient and a violet-blue shadow tint is what gives the stylized shading its form.
            _morning = new WeatherLook
            {
                SunColor = Hex("FFDDB0"), SunIntensity = 1.75f, SunPitch = 16f,
                SkyTint = Hex("7FA8D6"), Horizon = Hex("F4DDBE"), SkyExposure = 1.0f,
                Ambient = Hex("FFF2E2"), FogColor = Hex("EADCC6"), FogStart = 110f, FogEnd = 820f,
                Saturation = 8f, PostExposure = 0.05f,
                CloudCover = 0.36f, Stars = 0f, SunDisc = Hdr("FFE6C0", 7f),
                ShadowTint = Hex("D2C8EE"), AmbientFloor = 0f, RimBoost = 0f,
                Contrast = 12f, Lift = new Color(1f, 1f, 1f, 0f), BloomIntensity = 0.38f, BloomThreshold = 1.05f, Vignette = 0.22f, Temperature = 8f,
            };
            _noon = new WeatherLook
            {
                SunColor = Hex("FFF1D8"), SunIntensity = 2.15f, SunPitch = 52f,
                SkyTint = Hex("4C8BD3"), Horizon = Hex("CFE2EC"), SkyExposure = 1.0f,
                Ambient = Color.white, FogColor = Hex("CFDFE6"), FogStart = 170f, FogEnd = 1050f,
                Saturation = 12f, PostExposure = 0.0f,
                CloudCover = 0.42f, Stars = 0f, SunDisc = Hdr("FFF4E0", 8f),
                ShadowTint = Hex("C8CCF2"), AmbientFloor = 0f, RimBoost = 0f,
                Contrast = 14f, Lift = new Color(1f, 1f, 1f, 0f), BloomIntensity = 0.32f, BloomThreshold = 1.1f, Vignette = 0.2f, Temperature = 3f,
            };
            _afternoon = _noon;
            _afternoon.SunPitch = 32f; _afternoon.SunColor = Hex("FFE6BE"); _afternoon.SunIntensity = 2.05f;
            _afternoon.SkyTint = Hex("5590D0"); _afternoon.Horizon = Hex("DCE4E2"); _afternoon.FogColor = Hex("D8E0DE");
            _afternoon.Temperature = 7f;
            _dusk = new WeatherLook
            {
                SunColor = Hex("FF9A66"), SunIntensity = 1.45f, SunPitch = 8f,
                SkyTint = Hex("5D5C9E"), Horizon = Hex("FFB28A"), SkyExposure = 1.0f,
                Ambient = Hex("C2AED8"), FogColor = Hex("E8A88C"), FogStart = 80f, FogEnd = 620f,
                Saturation = 16f, PostExposure = 0.0f,
                CloudCover = 0.46f, Stars = 0.1f, SunDisc = Hdr("FFB070", 6f),
                ShadowTint = Hex("B8A2E6"), AmbientFloor = 0.01f, RimBoost = 0.25f,
                Contrast = 14f, Lift = new Color(1f, 0.98f, 1.04f, 0f), BloomIntensity = 0.5f, BloomThreshold = 1.0f, Vignette = 0.28f, Temperature = 10f,
            };
            // Night (visual overhaul): moonlit and READABLE - a cool moon key, a teal ambient floor so shapes and paths
            // read at the edges, lifted teal blacks, stars and a pale moon disc; the warm lanterns (hand lantern, posts,
            // windows) still carry the colour and bloom. Darkness is for the edges of the screen (art bible 4.3).
            _night = new WeatherLook
            {
                SunColor = Hex("9EC4FF"), SunIntensity = 0.42f, SunPitch = 34f,
                SkyTint = Hex("0C1A33"), Horizon = Hex("1E3850"), SkyExposure = 1.0f,
                Ambient = Hex("3A5470"), FogColor = Hex("15263A"), FogStart = 30f, FogEnd = 260f,
                Saturation = -18f, PostExposure = -0.35f,
                CloudCover = 0.3f, Stars = 1f, SunDisc = Hdr("DCE8FF", 2.6f),
                ShadowTint = Hex("8FB4CC"), AmbientFloor = 0.035f, RimBoost = 0.8f,
                Contrast = 8f, Lift = new Color(0.96f, 1.0f, 1.06f, 0.02f), BloomIntensity = 0.85f, BloomThreshold = 0.9f, Vignette = 0.4f, Temperature = -6f,
            };
            _built = true;
        }

        /// <summary>
        /// Art bible §4.3 phase table: Morning warm and low, Workday bright (arc up then down), Dusk low 8° coral with a
        /// violet sky, Payout/Sunday settle into dusk-violet, Night moonlit teal.
        /// </summary>
        public static WeatherLook ForPhase(DayClockPhase phase, float progress01)
        {
            if (!_built) Build();
            float p = Mathf.Clamp01(progress01);
            switch (phase)
            {
                case DayClockPhase.Morning: return Lerp(_morning, _noon, p * 0.2f);
                case DayClockPhase.Workday:
                    if (p < 0.5f) return Lerp(Lerp(_morning, _noon, 0.2f), _noon, p / 0.5f);
                    if (p < 0.8f) return Lerp(_noon, _afternoon, (p - 0.5f) / 0.3f);
                    return Lerp(_afternoon, _dusk, (p - 0.8f) / 0.2f); // last 20% sinks to dusk (art bible)
                case DayClockPhase.Dusk: return Lerp(_dusk, _night, NightBlend(p));
                case DayClockPhase.Payout:
                case DayClockPhase.Sunday:
                case DayClockPhase.Evicted: return Lerp(_dusk, _night, 0.4f);
                default: return _afternoon; // no clock: Muddlebury's golden afternoon
            }
        }

        /// <summary>
        /// Dusk -> night blend weight for Dusk progress (polish-2): smoothstep reaching full night at 55% of Dusk
        /// (old curve stopped at 60% night), so most of the night phase is properly dark.
        /// </summary>
        public static float NightBlend(float progress01) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress01) / 0.55f);

        /// <summary>
        /// Relative brightness of the key light after exposure (sun intensity x 2^PostExposure). Night must stay well
        /// under the day's so the lanterns matter, but above zero so the farm is moonlit rather than black.
        /// </summary>
        public float KeyLightAfterExposure => SunIntensity * Mathf.Pow(2f, PostExposure);

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
                    l.SkyTint = Color.Lerp(l.SkyTint, grey, 0.8f); l.SkyExposure *= storm ? 0.7f : 0.85f;
                    l.Horizon = Color.Lerp(l.Horizon, grey, 0.7f);
                    l.Ambient = Color.Lerp(l.Ambient, Hex("A8B0BC"), 0.6f) * (storm ? 0.75f : 0.9f);
                    l.FogColor = Color.Lerp(l.FogColor, grey, 0.8f); l.FogStart = storm ? 10f : 20f; l.FogEnd = storm ? 140f : 220f;
                    l.Saturation -= storm ? 25f : 15f; l.PostExposure -= storm ? 0.2f : 0.1f;
                    l.CloudCover = storm ? 1f : 0.9f; l.Stars = 0f; l.SunDisc = Color.black;
                    l.ShadowTint = Color.Lerp(l.ShadowTint, Color.white, 0.5f);
                    l.Contrast -= 4f; l.BloomIntensity *= 0.7f;
                    break;
                }
                case WeatherKind.Fog:
                    l.SunIntensity *= 0.6f; l.SunColor = Color.Lerp(l.SunColor, Color.white, 0.4f);
                    l.SkyTint = Color.Lerp(l.SkyTint, Hex("D6D8D4"), 0.8f);
                    l.FogColor = Color.Lerp(l.FogColor, Hex("CDD2CE"), 0.75f);
                    l.Horizon = l.FogColor;
                    l.FogStart = 1f; l.FogEnd = fogDistance > 0f ? fogDistance : 22f;
                    l.Ambient = Color.Lerp(l.Ambient, Hex("D0D4D2"), 0.5f); l.Saturation -= 20f;
                    l.CloudCover = 0.85f; l.Stars *= 0.2f; l.SunDisc *= 0.3f;
                    l.ShadowTint = Color.Lerp(l.ShadowTint, Color.white, 0.6f); l.Contrast -= 6f;
                    break;
                case WeatherKind.Heatwave:
                    l.SunColor = Color.Lerp(l.SunColor, Hex("FFD08A"), 0.5f); l.SunIntensity *= 1.15f;
                    l.SkyTint = Color.Lerp(l.SkyTint, Hex("E3C9A0"), 0.45f);
                    l.Horizon = Color.Lerp(l.Horizon, Hex("F2D2A0"), 0.6f);
                    l.FogColor = Color.Lerp(l.FogColor, Hex("F2D2A0"), 0.6f); l.FogStart *= 0.6f; l.FogEnd *= 0.8f;
                    l.Saturation += 6f; l.PostExposure += 0.12f;
                    l.CloudCover *= 0.25f; l.Temperature += 12f; l.BloomIntensity += 0.1f;
                    break;
                case WeatherKind.Wind:
                    l.SkyTint = Color.Lerp(l.SkyTint, Hex("A6C4E0"), 0.3f); l.FogStart *= 1.2f; l.FogEnd *= 1.2f;
                    l.CloudCover = Mathf.Max(l.CloudCover, 0.55f);
                    break;
            }
            return l;
        }
    }
}
