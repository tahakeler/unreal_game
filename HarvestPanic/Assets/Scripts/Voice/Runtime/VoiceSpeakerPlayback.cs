using System.Threading;
using UnityEngine;

namespace HarvestPanic.Voice
{
    /// <summary>
    /// One remote speaker's 3D voice. A looping constant-1 clip keeps the <see cref="AudioSource"/> "playing" so Unity
    /// applies spatial pan and the custom distance curve to it; <see cref="OnAudioFilterRead"/> (audio thread) multiplies
    /// that by the decoded voice from <see cref="VoiceSpeakerStream.Ring"/>, resampled 16 kHz -> output rate. Unity's
    /// built-in Logarithmic rolloff never reaches silence at maxDistance, so the curve is Custom: 1 within 3 m, 3/d
    /// (-6 dB per doubling) beyond, faded to 0 over the last 3 m before the range (25 m, x2 with the Megaphone).
    /// The service moves this object to the speaker's head every frame.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class VoiceSpeakerPlayback : MonoBehaviour
    {
        static AudioClip _ones;

        VoiceSpeakerStream _stream;
        VoiceConfig _cfg;
        AudioSource _src;
        double _step = 1.0 / 3.0;
        float _range = -1f;
        int _filterReads;

        public VoiceSpeakerStream Stream => _stream;
        public AudioSource Source => _src;
        /// <summary>Audio range currently baked into the rolloff curve (m).</summary>
        public float Range => _range;
        /// <summary>Audio-thread callbacks so far (0 when there is no audio device, e.g. batchmode).</summary>
        public int FilterReads => Volatile.Read(ref _filterReads);
        /// <summary>Peak absolute sample of the last audio-thread buffer after 3D gain and voice (diagnostics/tests).</summary>
        public float OutputPeak => Volatile.Read(ref _peak);
        float _peak;
        /// <summary>True when the head of the speaker was known last frame (3D); false = 2D fallback.</summary>
        public bool Spatial { get; private set; } = true;

        public void Init(VoiceSpeakerStream stream, VoiceConfig cfg, float range)
        {
            _stream = stream;
            _cfg = cfg;
            _src = GetComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.clip = OnesClip();
            _src.loop = true;
            _src.spatialBlend = 1f;
            _src.dopplerLevel = 0f;
            _src.spread = 0f;
            _src.priority = 16; // voices beat SFX for real channels
            _src.reverbZoneMix = 0.3f;
            _src.rolloffMode = AudioRolloffMode.Custom;
            int rate = AudioSettings.outputSampleRate;
            _step = rate > 0 ? (double)cfg.SampleRate / rate : 1.0 / 3.0;
            ApplyRange(range);
            _src.Play();
        }

        /// <summary>Re-bakes the rolloff curve when the audible range changes (Megaphone, Quiet Hours).</summary>
        public void ApplyRange(float range)
        {
            if (_src == null || range <= 0f || Mathf.Abs(range - _range) < 0.25f) return;
            _range = range;
            _src.minDistance = Mathf.Min(_cfg.FullVolumeRadius, range * 0.5f);
            _src.maxDistance = range;
            _src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, VoiceMath.BuildRolloffCurve(_cfg.FullVolumeRadius, range, _cfg.FadeBand));
        }

        /// <summary>3D at the head, or a quiet 2D fallback while the speaker's player object is unknown.</summary>
        public void SetSpatial(bool spatial)
        {
            if (spatial == Spatial || _src == null) return;
            Spatial = spatial;
            _src.spatialBlend = spatial ? 1f : 0f;
        }

        public void SetVolume(float v)
        {
            if (_src != null) _src.volume = Mathf.Clamp01(v);
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            var s = _stream;
            if (s == null) { System.Array.Clear(data, 0, data.Length); return; }
            s.Ring.ReadMultiply(data, channels, _step);
            float peak = 0f;
            for (int i = 0; i < data.Length; i++) { float a = data[i] < 0f ? -data[i] : data[i]; if (a > peak) peak = a; }
            Volatile.Write(ref _peak, peak);
            Interlocked.Increment(ref _filterReads);
        }

        void OnDestroy() => _stream = null;

        static AudioClip OnesClip()
        {
            if (_ones != null) return _ones;
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            int n = rate / 10;
            _ones = AudioClip.Create("HP_VoiceCarrier", n, 1, rate, false);
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = 1f;
            _ones.SetData(d, 0);
            return _ones;
        }
    }
}
