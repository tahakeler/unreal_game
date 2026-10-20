using System;
using HarvestPanic.Core;

namespace HarvestPanic.Weather
{
    /// <summary>
    /// Seeded weather draw (weather-twists §3.0 weights, §3.3 deck rules). Pure logic:
    /// weighted draw; no weather more than 2 days in a row; Storm/Fog never on Day 1 (dayIndex 0).
    /// The sequence is recomputed from day 0 so a given (seed, day) always yields the same card.
    /// </summary>
    public static class WeatherDeck
    {
        /// <summary>Number of weather kinds.</summary>
        public const int KindCount = 6;

        /// <summary>Default deck weights indexed by <see cref="WeatherKind"/> (GDD §3.1: Clear 30, Mud/Rain 20, Gusty 15, Fog 10, Heatwave 10, Storm 10).</summary>
        public static readonly float[] DefaultWeights = { 30f, 20f, 15f, 10f, 10f, 10f };

        /// <summary>Weather for <paramref name="dayIndex"/> of the run seeded with <paramref name="seed"/>.</summary>
        public static WeatherKind Roll(int seed, int dayIndex, float[] weights = null)
        {
            weights = weights ?? DefaultWeights;
            WeatherKind prev1 = (WeatherKind)255, prev2 = (WeatherKind)255, k = WeatherKind.Clear;
            for (int d = 0; d <= Math.Max(0, dayIndex); d++)
            {
                k = Draw(seed, d, weights, prev1, prev2);
                prev2 = prev1; prev1 = k;
            }
            return k;
        }

        /// <summary>True if <paramref name="k"/> may be drawn on <paramref name="day"/> after prev1, prev2.</summary>
        public static bool Allowed(WeatherKind k, int day, WeatherKind prev1, WeatherKind prev2)
        {
            if (day == 0 && (k == WeatherKind.Storm || k == WeatherKind.Fog)) return false;
            if (k == prev1 && k == prev2) return false;
            return true;
        }

        static WeatherKind Draw(int seed, int day, float[] w, WeatherKind p1, WeatherKind p2)
        {
            var rng = new Random(unchecked(seed * 486187739 + day * 16777619 + 7));
            float total = 0f;
            for (int i = 0; i < KindCount && i < w.Length; i++) if (Allowed((WeatherKind)i, day, p1, p2)) total += Math.Max(0f, w[i]);
            if (total <= 0f) return WeatherKind.Clear;
            double r = rng.NextDouble() * total;
            for (int i = 0; i < KindCount && i < w.Length; i++)
            {
                if (!Allowed((WeatherKind)i, day, p1, p2)) continue;
                r -= Math.Max(0f, w[i]);
                if (r < 0) return (WeatherKind)i;
            }
            return WeatherKind.Clear;
        }

        /// <summary>Day Modifier id carrying the card's gameplay effects.</summary>
        public static string ModifierId(WeatherKind k) => "Mod.Weather.Day." + k;

        /// <summary>World tag every card grants so the director can see its modifier is live.</summary>
        public static string WorldTag(WeatherKind k) => "World.Weather." + k;

        /// <summary>Short forecast for HUD / morning card.</summary>
        public static string Forecast(WeatherKind k)
        {
            switch (k)
            {
                case WeatherKind.Rain: return "Rain: slick grass, slower mud, goods left out get soaked, the river runs fast.";
                case WeatherKind.Wind: return "Gusty: light things blow away. Carry low, hold on tight.";
                case WeatherKind.Fog: return "Fog: you can't see past the fence. Shout a lot.";
                case WeatherKind.Heatwave: return "Heatwave: crops wilt faster, farmers tire faster.";
                case WeatherKind.Storm: return "Storm: rain, wind, a raging river and a nervous bridge.";
                default: return "Clear skies. No excuses today.";
            }
        }

        /// <summary>Parses a weather name (case-insensitive), e.g. from <c>-hpWeather Rain</c>.</summary>
        public static bool TryParse(string s, out WeatherKind k) => Enum.TryParse(s ?? "", true, out k) && (int)k < KindCount;
    }
}
