using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>Wagon knobs (design/gdd/wagon-vehicles.md section 7). Asset: <c>Prefabs/Vehicles/WagonTuning.asset</c>.</summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Player/Wagon Tuning", fileName = "WagonTuning")]
    public sealed class WagonTuning : ScriptableObject
    {
        [Header("Body (rule 1)")]
        public float WagonMassKg = 120f;
        [Tooltip("Soft cap: loading stops once cargo reaches this (kg)")] public float CargoSoftCapKg = 400f;
        [Tooltip("Cargo slots in the bed (an item takes one slot per 2 carry hands)")] public int CargoSlots = 6;
        [Tooltip("Slots across the bed")] public int CargoColumns = 2;
        [Tooltip("Bed walls (m); drives the bounce-out threshold")] public float WallHeight = 0.4f;
        public float BedWidth = 1.2f, BedLength = 2.0f;

        [Header("Moving (rules 2-3, section 4)")]
        public float PushForcePerPlayer = 900f;
        public float VMax = 6f;
        public float RollingKDirt = 1.5f, RollingKMud = 3f, RollingKPlank = 1.2f;
        [Tooltip("Handle tow-bar spring (N/m)")] public float HandleStiffness = 1800f;
        [Tooltip("Handle damping (N per m/s)")] public float HandleDamping = 300f;
        [Tooltip("Grip distance kept between a player and their handle (m), clamped from the grab distance")] public Vector2 HandleRest = new Vector2(0.75f, 1.15f);
        [Tooltip("Grip breaks when the handle is this far from the holder (m)")] public float HandleSlipDistance = 2.8f;
        [Tooltip("A holder walks at most this much faster than the wagon is rolling (m/s), so pushing ramps up with the cart")] public float FollowSlack = 1.5f;
        [Tooltip("Holder closer than this to their own handle walks at the crawl multiplier (the cart is blocked) (m)")] public float HandleMinGap = 0.45f;
        [Tooltip("Movement multiplier while the cart does not give way")] public float CrawlMult = 0.12f;
        [Tooltip("Sideways tyre grip: fraction of lateral speed removed per second (fixed axles track their heading)")] public float LateralGrip = 40f;
        [Tooltip("Extra rolling drag with nobody on the handles (per second)")] public float IdleRollDrag = 3f;
        [Tooltip("Below this speed with nobody holding, the parking brake holds it (m/s)")] public float ParkSpeed = 0.6f;

        [Header("Tipping (rule 6)")]
        public float TipAngle = 50f;
        [Tooltip("Tilt must last this long to count (s)")] public float TipConfirmSeconds = 0.15f;
        [Tooltip("Seconds of enough players on the handles to right it")] public float RightingSeconds = 2f;
        public int RightingPlayers = 2;
        [Tooltip("Outward speed of spilled cargo (m/s)")] public float SpillSpeed = 3f;

        [Header("Cargo (rules 1, 5, 11)")]
        [Tooltip("Free (not held) carryables that land in the bed load automatically")] public bool AutoLoad = true;
        [Tooltip("Seconds between bed scans")] public float LoadScanInterval = 0.2f;
        [Tooltip("Items moving faster than this relative to the wagon do not load (still flying) (m/s)")] public float MaxLoadSpeed = 3.5f;
        [Tooltip("Bounce-out chance per bump above the threshold (0 disables)")] [Range(0, 1)] public float BumpSpillChance = 0.35f;
        public float BumpCooldown = 0.6f;
        [Tooltip("Minimum wagon speed for a bump to count (m/s)")] public float BumpMinSpeed = 2f;
        [Tooltip("Fraction of a hard hit on the wagon passed to each cargo item as a bruise")] [Range(0, 1)] public float CargoCushion = 0.5f;
        [Tooltip("Hits on the wagon above this (design units) bruise the cargo")] public float CargoHitMin = 600f;
        [Tooltip("The wagon knocks players over above this speed (m/s)")] public float KnockSpeed = 3f;
        [Tooltip("Bruise (design units) each goods item takes when the wagon tips: books the TippedWagon loss on the driver")] public float TipSpillImpulse = 700f;
        [Tooltip("Bruise (design units) a bounced-out item takes: books the SpilledCargo loss on the driver")] public float BumpSpillImpulse = 680f;
    }
}
