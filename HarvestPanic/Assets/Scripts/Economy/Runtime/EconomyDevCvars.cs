using System;
using System.Globalization;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Dev console variables for the day loop. <see cref="DayScale"/> multiplies every phase length
    /// (0.1 = ten times faster). Set via command line <c>-hpDayScale 0.1</c>, env <c>HP_DAY_SCALE=0.1</c>,
    /// or assign at runtime (tests, debug UI). Values are clamped to 0.01..10.
    /// </summary>
    public static class EconomyDevCvars
    {
        static float? _dayScale;

        /// <summary>Phase duration multiplier.</summary>
        public static float DayScale
        {
            get
            {
                if (_dayScale.HasValue) return _dayScale.Value;
                float v = 1f;
                string env = Environment.GetEnvironmentVariable("HP_DAY_SCALE");
                if (!string.IsNullOrEmpty(env)) float.TryParse(env, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-hpDayScale") float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out v);
                _dayScale = Clamp(v);
                return _dayScale.Value;
            }
            set => _dayScale = Clamp(value);
        }

        static float Clamp(float v) => v <= 0f || float.IsNaN(v) ? 1f : Math.Max(0.01f, Math.Min(10f, v));
    }
}
