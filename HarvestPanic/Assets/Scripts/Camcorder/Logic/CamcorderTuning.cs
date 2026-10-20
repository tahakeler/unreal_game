using UnityEngine;

namespace HarvestPanic.Camcorder
{
    /// <summary>
    /// Every camcorder number (design/gdd/camcorder-replay.md "Tuning Knobs"). Loaded from
    /// <c>Resources/Camcorder/CamcorderTuning</c>; a default instance is used when the asset is missing.
    /// Beta deviation (documented in docs/qa/camcorder-progress.md): pre-roll is 15 s instead of 30 s.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Camcorder Tuning", fileName = "CamcorderTuning")]
    public sealed class CamcorderTuning : ScriptableObject
    {
        [Header("Recording")]
        public bool enabled = true;
        [Range(5, 20)] public float tickHz = 20f;
        [Range(5f, 45f)] public float preRollSeconds = 15f;
        [Range(1f, 5f)] public float postRollSeconds = 3f;
        [Range(10f, 60f)] public float maxTapeSeconds = 45f;
        [Range(8, 256)] public int maxActorsPerFrame = 128;
        public float relevanceRadius = 60f;
        [Tooltip("Seconds between scene scans for new actors (scans allocate a little; sampling does not).")]
        public float rescanSeconds = 1f;

        [Header("Triggers")]
        [Range(4f, 15f)] public float captureCooldown = 8f;
        [Range(0, 10)] public int manualSavesPerDay = 3;
        [Tooltip("OnDisaster events below this severity are ignored (0 = every disaster).")]
        [Range(0, 100)] public int minAutoSeverity = 0;

        [Header("Storage")]
        [Range(1, 24)] public int maxTapesPerDay = 12;
        [Range(3, 12)] public int tapesKeptAtPayout = 6;
        [Tooltip("Hard cap for all tapes held in memory (MB).")]
        public float tapeMemoryBudgetMB = 48f;

        [Header("Viewer")]
        [Range(0.25f, 1f)] public float slowMoFactor = 0.5f;
        public float slowMoWindow = 1.5f;

        [Header("Export")]
        public int exportWidth = 960;
        public int exportHeight = 540;
        public int exportFps = 15;
        public int maxExportFrames = 450;
        public int thumbnailWidth = 480;
        public int thumbnailHeight = 270;

        [Header("Input (Input System paths)")]
        public string saveKeyPath = "<Keyboard>/f8";
        public string shelfKeyPath = "<Keyboard>/f9";

        /// <summary>Frames the ring must hold so a fully extended tape still fits.</summary>
        public int RingFrames => Mathf.CeilToInt(Mathf.Max(maxTapeSeconds, preRollSeconds + postRollSeconds) * tickHz) + 2;

        static CamcorderTuning _cached;
        public static CamcorderTuning Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<CamcorderTuning>("Camcorder/CamcorderTuning");
            if (_cached == null) { _cached = CreateInstance<CamcorderTuning>(); _cached.hideFlags = HideFlags.DontSave; }
            return _cached;
        }
    }
}
