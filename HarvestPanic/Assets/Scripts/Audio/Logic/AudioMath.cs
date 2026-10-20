using System;

namespace HarvestPanic.Audio
{
    /// <summary>Pure formulas from design/gdd/audio-feedback.md (Formulas, Core Rules 2-3, 5).</summary>
    public static class AudioMath
    {
        /// <summary>Decibels to linear gain (0 dB = 1).</summary>
        public static float DbToLinear(float db) => db <= -80f ? 0f : (float)Math.Pow(10.0, db / 20.0);

        /// <summary>Linear gain to decibels (clamped at -80 dB).</summary>
        public static float LinearToDb(float linear) => linear <= 0.0001f ? -80f : 20f * (float)Math.Log10(linear);

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary><c>ImpactGain_dB = -24 + 18*clamp(ImpactSpeed/12,0,1) + 6*clamp(Mass/400,0,1)</c>.</summary>
        public static float ImpactGainDb(float impactSpeed, float massKg) =>
            -24f + 18f * Clamp01(impactSpeed / 12f) + 6f * Clamp01(massKg / 400f);

        /// <summary>Bruise coin ticks: <c>floor(DollarLoss/10)</c>, max 5 (Core Rule 3).</summary>
        public static int CoinTicks(int dollarLoss) => dollarLoss <= 0 ? 0 : Math.Min(5, dollarLoss / 10);

        /// <summary><c>ChaosLevel = clamp(0.4*Fleeing/8 + 0.3*MajorMods/2 + 0.3*DisastersLast60s/2, 0, 1)</c>.</summary>
        public static float ChaosLevel(int fleeingCritters, int activeMajorModifiers, int disastersLast60s) =>
            Clamp01(0.4f * fleeingCritters / 8f + 0.3f * activeMajorModifiers / 2f + 0.3f * disastersLast60s / 2f);

        /// <summary><c>MusicLayer = 1 + floor(ChaosLevel * 2.99)</c>, 1..3.</summary>
        public static int MusicLayer(float chaos) => 1 + (int)Math.Floor(Clamp01(chaos) * 2.99f);

        /// <summary>Semitones to playback pitch ratio.</summary>
        public static float SemitonesToPitch(float semis) => (float)Math.Pow(2.0, semis / 12.0);

        /// <summary>One-pole smoothing factor for a time constant (attack/release), frame-rate independent.</summary>
        public static float SmoothAlpha(float dt, float tau) => tau <= 0f ? 1f : 1f - (float)Math.Exp(-dt / tau);
    }
}
