using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// First-person arm knobs (design/gdd/player-character.md 5b; art-bible §6.6 first-person framing).
    /// All positions are in camera-pivot space (metres; +X right, +Y up, +Z forward) for the LEFT arm;
    /// the right arm mirrors X. Asset: <c>Assets/Prefabs/Player/Tuning/ArmsTuning.asset</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Player/Arms Tuning", fileName = "ArmsTuning")]
    public sealed class ArmsTuning : ScriptableObject
    {
        [Header("Skeleton (left arm, camera space)")]
        public Vector3 Shoulder = new Vector3(-0.2f, -0.3f, -0.05f);
        public float UpperLength = 0.3f;
        public float ForeLength = 0.32f;
        [Tooltip("Elbow pole direction (left arm): elbows sit down and outward")] public Vector3 ElbowPole = new Vector3(-0.7f, -1f, -0.25f);
        [Tooltip("Cartoon stretch: arms may lengthen up to this factor to reach a held object's sides")] public float MaxStretch = 1.45f;

        [Header("Poses (palm centre, left hand)")]
        public Vector3 IdleHand = new Vector3(-0.2f, -0.235f, 0.42f);
        [Tooltip("Reach: hands move toward the aim point up to this distance from the eyes")] public float ReachDistance = 0.62f;
        public float ReachHalfSpread = 0.13f;
        public float ReachDrop = 0.07f;
        [Tooltip("Blend from idle to the reach pose (0..1)")] public float ReachBlend = 0.8f;
        [Tooltip("Palm sits this far outside a held object's side")] public float GripInset = 0.02f;
        [Tooltip("Hands grip this fraction down from the object centre toward its bottom")] public float GripLow = 0.25f;
        [Tooltip("Max hand spread from centre line when gripping very wide items")] public float GripMaxHalfSpread = 0.42f;
        [Tooltip("Hand pull-back at full throw charge (m)")] public float ThrowPull = 0.16f;
        public float ThrowLift = 0.06f;
        [Tooltip("Throw follow-through: hand position at the peak of the swing")] public Vector3 SwingHand = new Vector3(-0.12f, -0.1f, 0.66f);
        public float SwingDuration = 0.3f;
        [Tooltip("Follow sharpness of hand targets (1/s)")] public float Sharpness = 16f;

        [Header("Sway")]
        public float BreathAmplitude = 0.004f;
        public float BreathHz = 0.28f;
        [Tooltip("Hand lag per degree/second of look rotation (m)")] public float LookSway = 0.00022f;
        public float LookSwayMax = 0.035f;
        public float LookSwaySharpness = 10f;
        [Tooltip("Hand bob at walk speed (m)")] public float WalkBob = 0.012f;

        [Header("Shape (m)")]
        public float UpperRadius = 0.058f;
        public float ElbowRadius = 0.052f;
        public float WristRadius = 0.04f;
        [Tooltip("Sleeve covers this fraction of the forearm from the elbow")] public float SleeveFraction = 0.5f;
        [Tooltip("Glove gauntlet starts at this fraction of the forearm")] public float GloveStart = 0.78f;
        public float GauntletRadius = 0.054f;
        [Tooltip("Uniform hand scale (props and hands read ~1.2x in first person)")] public float HandScale = 1.25f;

        [Header("Default colours (overridden by the farmer loadout)")]
        public Color Shirt = new Color(0.78f, 0.33f, 0.24f);
        public Color Glove = new Color(0.56f, 0.38f, 0.22f);
        public Color Skin = new Color(0.93f, 0.72f, 0.56f);
        [Tooltip("Rolled cuff is the shirt colour times this")] public float CuffShade = 0.82f;
        [Range(0f, 1f)] public float Smoothness = 0.2f;
    }
}
