using UnityEngine;

namespace HarvestPanic.Voice
{
    /// <summary>Wire codec ids (add-only; the byte value is on the wire).</summary>
    public enum VoiceCodec : byte { None = 0, Adpcm4 = 1, Adpcm3 = 2, Steam = 3 }

    /// <summary>
    /// Every proximity-voice tuning number (docs/architecture/adr-voice-chat.md). Loaded from
    /// <c>Resources/VoiceConfig</c>; falls back to these defaults when the asset is missing.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Voice Config", fileName = "VoiceConfig")]
    public sealed class VoiceConfig : ScriptableObject
    {
        public const string ResourcePath = "VoiceConfig";

        [Header("Audio format")]
        [Tooltip("Capture and playback rate (Hz).")] public int SampleRate = 16000;
        [Tooltip("ADPCM codec rate (Hz); capture is decimated 2:1 to this.")] public int CodecRate = 8000;
        [Tooltip("Frame length (ms). 40 ms = 25 packets/s while talking.")] public int FrameMs = 40;
        [Tooltip("Codec of the PCM (Microphone) backend. Adpcm3 = 26.2 kbps up / 28.0 kbps down per talker (inside the 32 kbps budget); Adpcm4 is LAN-only (34-36 kbps).")]
        public VoiceCodec PcmCodec = VoiceCodec.Adpcm3;
        [Tooltip("Max audio queued for playback per speaker (ms); older samples are dropped to bound latency.")] public int PlayoutRingMs = 240;

        [Header("Transmit")]
        [Tooltip("Input System path of the push-to-talk key.")] public string PushToTalkBinding = "<Keyboard>/v";
        [Tooltip("Default open-mic threshold (loudness 0..1).")] public float DefaultOpenMicThreshold = 0.2f;
        [Tooltip("Loudness smoothing attack (s).")] public float VadAttack = 0.05f;
        [Tooltip("Loudness smoothing release (s).")] public float VadRelease = 0.3f;
        [Tooltip("Keep sending this long after loudness drops below the threshold (s).")] public float VadHangover = 0.4f;
        [Tooltip("dBFS mapped to loudness 0 (noise gate).")] public float LoudnessFloorDb = -50f;
        [Tooltip("dB span from loudness 0 to 1.")] public float LoudnessSpanDb = 40f;

        [Header("Range and attenuation")]
        [Tooltip("Full volume within this distance (m).")] public float FullVolumeRadius = 3f;
        [Tooltip("Base audible range (m); multiplied by DayParams Noise.VoiceRadiusMult (Megaphone x2).")] public float Range = 25f;
        [Tooltip("Fade to silence over the last metres before the range.")] public float FadeBand = 3f;
        [Tooltip("Host relays to listeners within range + this margin (m).")] public float RelayMargin = 2f;
        public float MinRangeMult = 0.5f, MaxRangeMult = 3f;
        [Tooltip("Occlusion per blocking wall (dB).")] public float OcclusionDbPerWall = -9f;
        public int MaxOccludingWalls = 2;
        [Tooltip("Occlusion re-cast rate (Hz).")] public float OcclusionHz = 10f;
        [Tooltip("Layers that occlude voice.")] public LayerMask OcclusionMask = 1; // Default
        [Tooltip("Cast occlusion rays (GDD proximity-voice rule 2).")] public bool UseOcclusion = true;

        [Header("Jitter / loss")]
        public int JitterSlots = 16;
        [Tooltip("Frames buffered before a talk spurt starts playing.")] public int JitterPrefillFrames = 2;
        [Tooltip("Consecutive empty pulls before the spurt ends and the buffer re-prefills.")] public int MaxUnderruns = 3;
        [Tooltip("Speaker considered silent after this long without packets (s).")] public float SpeakerTimeout = 0.4f;
        [Tooltip("Speaker playback objects are destroyed after this long without packets (s).")] public float SpeakerIdleDestroy = 20f;
        [Tooltip("Gain of the repeated frame used to conceal one lost packet.")] public float ConcealGain = 0.5f;

        [Header("Server enforcement")]
        public int MaxPayloadBytes = 600;
        public float MaxPacketsPerSecond = 60f, PacketBurst = 20f;
        [Tooltip("2x the 32 kbps budget.")] public float MaxBytesPerSecond = 8000f;

        [Header("Integration")]
        [Tooltip("Report ducking to Audio while a speaker is within this distance (m).")] public float DuckRadius = 15f;
        [Tooltip("Voice loudness that becomes a Noise Event (GDD 0.35).")] public float VoiceNoiseThreshold = 0.35f;
        [Tooltip("Min seconds between voice Noise Events per player.")] public float VoiceNoiseInterval = 0.5f;
        [Tooltip("Host option 'Voice draws critters'.")] public bool VoiceDrawsCritters = true;
        [Tooltip("Speaking icon height above the head pivot when the player has no NameTag (m).")] public float IndicatorHeight = 0.75f;
        [Tooltip("Speaking icon offset above the NameTag (m).")] public float IndicatorAboveNameTag = 0.42f;
        [Tooltip("Steam backend: keep recording this long after push-to-talk is released so the last syllable is sent (s).")] public float SteamPttTail = 0.25f;

        public int FrameSamples => SampleRate * FrameMs / 1000;

        static VoiceConfig _cached;

        /// <summary>The Resources asset, or a defaults instance.</summary>
        public static VoiceConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<VoiceConfig>(ResourcePath);
            if (_cached == null) { _cached = CreateInstance<VoiceConfig>(); _cached.hideFlags = HideFlags.DontSave; }
            return _cached;
        }
    }
}
