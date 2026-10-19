using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Physics-carry knobs (design/gdd/physics-carry.md section 7). Shared asset:
    /// <c>Assets/Prefabs/Player/Tuning/CarryTuning.asset</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Player/Carry Tuning", fileName = "CarryTuning")]
    public sealed class CarryTuning : ScriptableObject
    {
        [Header("Grab")]
        [Tooltip("Reach from the camera (m)")] public float GrabRange = 1.8f;
        [Tooltip("Server accepts a grab up to GrabRange + this (latency slack, m)")] public float GrabRangeSlack = 0.75f;
        [Tooltip("Hold point distance in front of the eyes (m)")] public float HoldDistance = 1.25f;
        [Tooltip("Hold point drop below the eye line (m)")] public float HoldDrop = 0.35f;
        [Tooltip("Rigid tool hold offset in camera space (m)")] public Vector3 ToolHoldOffset = new Vector3(0.32f, -0.38f, 0.65f);
        [Tooltip("Quick tap shorter than this on a KO'd player grabs; holding longer revives (s)")] public float TapVsHoldSeconds = 0.25f;

        [Header("Hand spring (per hand)")]
        public float HandStiffness = 1500f;
        public float HandDamping = 120f;
        public float HandMaxForce = 2500f;
        [Tooltip("Angular damping applied while carried")] public float CarriedAngularDamping = 2.5f;

        [Header("Slip")]
        public float SlipDistance = 1.2f;
        public float SlipTime = 0.3f;

        [Header("Load / speed")]
        public float KgPerHand = 25f;
        public float CarrySpeedFloor = 0.35f;

        [Header("Throw")]
        public float ThrowSpeedMin = 4f;
        public float ThrowSpeedMax = 14f;
        [Tooltip("Seconds of holding Throw for full charge")] public float ThrowChargeTime = 1.0f;
        [Tooltip("v *= clamp(ThrowMassRef / mass, ThrowMassFloor, 1)")] public float ThrowMassRef = 10f;
        public float ThrowMassFloor = 0.3f;
        [Tooltip("Max mass (kg) a single player can heave when the item needs 2 hands")] public float HeaveMaxMass = 40f;
        [Tooltip("A thrown body stays 'Thrown' until contact or this long (s)")] public float ThrownWindow = 2f;

        [Header("Bruise / impact")]
        [Tooltip("Unity collision impulse (kg*m/s) is multiplied by this to get design N*s (GDD values are Unreal-scale). See docs/architecture/unity-player.md")]
        public float ImpulseScale = 10f;
        [Tooltip("BruiseImpactMin (design N*s), mirror of goods-value I_min")] public float BruiseImpactMin = 400f;
        [Tooltip("Above this (design N*s) an impact is 'heavy'")] public float HeavyImpact = 800f;
        public float LightImpactLoudness = 0.3f;
        public float HeavyImpactLoudness = 0.6f;
        [Tooltip("Impacts are reported this long after release (s)")] public float ImpactReportWindow = 2f;

        [Header("Touch history")]
        public float TouchWindow = 3f;
        public int TouchCapacity = 3;
        [Tooltip("Holding refreshes the holder's touch every N seconds")] public float HoldTouchRefresh = 1f;

        [Header("Wobble")]
        [Tooltip("amp = WobbleAmplitude * (1 + 2 * speed_ratio) * (1 + struggle)")] public float WobbleAmplitude = 0.05f;
        public float WobbleFrequency = 1.3f;
    }
}
