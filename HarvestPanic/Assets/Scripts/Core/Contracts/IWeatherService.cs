using System;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>Today's weather card (weather-twists §3.1). Values are replicated as bytes: add-only, never reorder.</summary>
    public enum WeatherKind : byte { Clear, Rain, Wind, Fog, Heatwave, Storm }

    /// <summary>
    /// Read access to the replicated weather (provider: World/Weather; consumers: HUD, Audio, AI). Gameplay effects
    /// are NOT read from here: they arrive as Day Modifiers (<c>Mod.Weather.*</c>) through <see cref="IDayModifierService"/>.
    /// </summary>
    public interface IWeatherService
    {
        /// <summary>Current weather on this peer.</summary>
        WeatherKind Current { get; }
        /// <summary>Forecast line for the morning card / HUD, e.g. "Rain: mud is slower, goods get soaked".</summary>
        string ForecastText { get; }
        /// <summary>World-space wind velocity in m/s (zero unless windy).</summary>
        Vector3 Wind { get; }
        /// <summary>Raised on every peer when the weather changes (old, new).</summary>
        event Action<WeatherKind, WeatherKind> OnWeatherChanged;
    }

    /// <summary>
    /// Server-side write access to the day modifier stack (add-only companion of <see cref="IDayModifierService"/>).
    /// Economy's director should implement it natively; until then the Integration assembly bridges it.
    /// </summary>
    public interface IDayModifierWriter
    {
        /// <summary>Server: applies <paramref name="mod"/>; false when ignored/queued or on clients.</summary>
        bool ApplyModifier(DayModifier mod, bool bypassCap);
    }

    /// <summary>Day phase mirror for presentation (lighting, audio). Ordinals match Economy's DayPhase.</summary>
    public enum DayClockPhase : byte { None, Morning, Workday, Dusk, Payout, Sunday, Evicted }

    /// <summary>Read-only day clock for presentation (provider: Economy, bridged by Integration).</summary>
    public interface IDayClockService
    {
        /// <summary>Current phase on this peer.</summary>
        DayClockPhase Phase { get; }
        /// <summary>0..1 progress through the current timed phase (0 for untimed phases).</summary>
        float PhaseProgress01 { get; }
    }
}
