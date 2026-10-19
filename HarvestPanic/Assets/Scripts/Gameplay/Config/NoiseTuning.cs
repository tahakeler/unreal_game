using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Noise Event contract knobs (design/gdd/proximity-voice.md, "Contract: Noise Event"). Loaded by
    /// <see cref="NoiseService"/> from <c>Resources/HarvestPanic/NoiseTuning</c> when present, defaults otherwise.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Player/Noise Tuning", fileName = "NoiseTuning")]
    public sealed class NoiseTuning : ScriptableObject
    {
        /// <summary>Resources path the service loads.</summary>
        public const string ResourcePath = "HarvestPanic/NoiseTuning";

        [Tooltip("Radius = Min + (Max - Min) * loudness^Exponent")] public float RadiusMin = 4f;
        public float RadiusMax = 50f;
        public float Exponent = 1.5f;
        [Tooltip("Per instigator per source (s)")] public float RateWindow = 0.25f;
        [Tooltip("Global events per second")] public int GlobalCap = 60;
    }
}
