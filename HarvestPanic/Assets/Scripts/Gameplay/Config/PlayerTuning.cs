using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Every player-character knob (design/gdd/player-character.md section 7). One shared asset lives at
    /// <c>Assets/Prefabs/Player/Tuning/PlayerTuning.asset</c>; designers tune there, never in code.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Player/Player Tuning", fileName = "PlayerTuning")]
    public sealed class PlayerTuning : ScriptableObject
    {
        [Header("Movement (m/s)")]
        public float WalkSpeed = 4.0f;
        public float SprintSpeed = 6.5f;
        public float CrouchSpeed = 2.0f;
        [Tooltip("Jump apex height (m)")] public float JumpApex = 1.1f;
        [Tooltip("Gravity used by the character controller (m/s^2); snappier than physics gravity on purpose")] public float Gravity = 20f;
        [Tooltip("Ground acceleration (m/s^2)")] public float Acceleration = 40f;
        [Tooltip("Air control acceleration (m/s^2)")] public float AirAcceleration = 8f;

        [Header("Capsule (m)")]
        public float StandHeight = 1.8f;
        public float CrouchHeight = 1.2f;
        public float Radius = 0.35f;
        [Tooltip("EyeHeight: eye above capsule centre (GDD 64 cm)")] public float EyeAboveCentre = 0.64f;
        public float MassKg = 70f;

        [Header("Stamina")]
        public float StaminaMax = 100f;
        public float SprintDrain = 15f;
        [Tooltip("carry_load_drain = CarryDrainPerLoad * load_ratio per second")] public float CarryDrainPerLoad = 8f;
        public float StaminaRegen = 25f;
        public float StaminaRegenDelay = 1.0f;
        [Tooltip("Sprint re-enabled after exhaustion once stamina reaches this")] public float SprintResumeStamina = 30f;

        [Header("Camera")]
        public float BaseFov = 90f;
        public float SprintFovKick = 8f;
        public float FovLerpSpeed = 8f;
        [Tooltip("Head bob amplitude (m) at walk speed")] public float HeadBobAmplitude = 0.016f;
        [Tooltip("Head bob frequency (Hz) at walk speed")] public float HeadBobFrequency = 1.8f;
        public float ShakeMaxDegrees = 4f;
        [Tooltip("Impulse (design N*s) giving full trauma")] public float ShakeFullImpulse = 900f;
        [Tooltip("Trauma decay per second")] public float ShakeDecay = 2f;
        [Tooltip("Horizontal speed (m/s) a downed body is dragged at (IDraggable, Mr. Stitches)")] public float DragSpeed = 2.5f;
        [Tooltip("Max drag acceleration (m/s^2) toward the drag velocity")] public float DragAccel = 12f;
        public float MouseSensitivity = 0.12f;
        public float GamepadLookSpeed = 160f;
        public float PitchLimit = 85f;

        [Header("Ragdoll and knockout")]
        [Tooltip("RagdollImpulse (design N*s). KO at >= 2x this.")] public float RagdollImpulse = 900f;
        [Tooltip("Ragdoll triggered by sprinting with load >= this ratio on a steep slope")] public float TripLoadRatio = 0.7f;
        [Tooltip("Slope angle (deg) for the loaded-sprint trip")] public float TripSlopeDegrees = 25f;
        [Tooltip("Body speed under which the ragdoll counts as at rest (m/s)")] public float RestSpeed = 0.5f;
        [Tooltip("Seconds at rest before get-up is offered")] public float RestTime = 0.5f;
        public float GetUpDuration = 1.2f;
        public float KoDuration = 8f;
        public float ReviveHold = 1.5f;
        [Tooltip("This many ragdolls inside KoWindow = KO")] public int KoRagdollCount = 3;
        public float KoWindow = 20f;
        [Tooltip("Launch speed per design N*s of impulse when ragdolled (m/s)")] public float RagdollLaunchPerImpulse = 0.006f;
        public float RagdollLaunchMax = 12f;
        [Tooltip("Stamina below which the ragdoll threshold is scaled by LowStaminaMult")] public float LowStaminaThreshold = 20f;
        public float LowStaminaMult = 0.7f;
        [Tooltip("Ragdoll threshold reduction at full load: (1 - LoadThresholdFactor * load_ratio)")] public float LoadThresholdFactor = 0.4f;

        [Header("Respawn")]
        public float RespawnDelay = 5f;
        [Tooltip("Below this world Y the player drowns/falls out (m)")] public float KillZ = -25f;
        [Tooltip("Seconds after a throw in which a drowned KO body blames the thrower (YeetedTeammate)")] public float YeetBlameWindow = 6f;

        [Header("Noise")]
        public float HollerLoudness = 0.8f;
        public float HollerCooldown = 2f;
        public float WalkFootstepLoudness = 0.10f;
        public float SprintFootstepLoudness = 0.25f;
        [Tooltip("Metres travelled per footstep")] public float StrideWalk = 1.5f;
        public float StrideSprint = 2.2f;
    }
}
