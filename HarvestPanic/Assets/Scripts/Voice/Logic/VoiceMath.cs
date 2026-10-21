using System;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Voice
{
    /// <summary>Pure voice formulas (proximity-voice GDD section 4 + adr-voice-chat). EditMode tested.</summary>
    public static class VoiceMath
    {
        /// <summary>RMS level of a block in dBFS (full-scale square = 0 dB, silence floors at -120).</summary>
        public static float RmsDbfs(float[] pcm, int offset, int count)
        {
            if (count <= 0) return -120f;
            double sum = 0;
            for (int i = 0; i < count; i++) { float s = pcm[offset + i]; sum += s * s; }
            double rms = Math.Sqrt(sum / count);
            return rms < 1e-6 ? -120f : (float)(20.0 * Math.Log10(rms));
        }

        /// <summary>GDD loudness: clamp((dBFS + offset - floor) / span, 0, 1). Defaults: -50 dBFS -> 0, -10 dBFS -> 1.</summary>
        public static float Loudness(float dbfs, float gainOffsetDb = 0f, float floorDb = -50f, float spanDb = 40f)
            => spanDb <= 0f ? 0f : Mathf.Clamp01((dbfs + gainOffsetDb - floorDb) / spanDb);

        public static byte QuantizeLoudness(float loudness) => (byte)Mathf.RoundToInt(Mathf.Clamp01(float.IsNaN(loudness) ? 0f : loudness) * 255f);
        public static float DequantizeLoudness(byte q) => q / 255f;

        /// <summary>Range in 0.5 m units on the wire (0..127.5 m).</summary>
        public static byte QuantizeRange(float metres) => (byte)Mathf.Clamp(Mathf.RoundToInt(metres * 2f), 0, 255);
        public static float DequantizeRange(byte q) => q * 0.5f;

        /// <summary>
        /// Linear distance gain: 1 within <paramref name="full"/>, log falloff full/d (-20 log10(d/full) dB) beyond,
        /// faded linearly to 0 over the last <paramref name="fadeBand"/> metres before <paramref name="range"/>.
        /// </summary>
        public static float Gain(float distance, float full, float range, float fadeBand)
        {
            if (range <= 0f || distance >= range) return 0f;
            float d = Mathf.Max(0f, distance);
            float g = d <= full || full <= 0f ? 1f : full / d;
            float fadeStart = range - Mathf.Max(0f, fadeBand);
            if (fadeBand > 0f && d > fadeStart) g *= (range - d) / fadeBand;
            return Mathf.Clamp01(g);
        }

        /// <summary>Gain in dB (-inf as -120).</summary>
        public static float GainDb(float distance, float full, float range, float fadeBand)
        {
            float g = Gain(distance, full, range, fadeBand);
            return g <= 1e-6f ? -120f : 20f * Mathf.Log10(g);
        }

        /// <summary>Base range times the day-param multiplier (Megaphone x2), clamped; NaN/<=0 multiplier counts as 1.</summary>
        public static float EffectiveRange(float baseRange, float mult, float minMult, float maxMult)
        {
            if (float.IsNaN(mult) || float.IsInfinity(mult) || mult <= 0f) mult = 1f;
            return baseRange * Mathf.Clamp(mult, minMult, maxMult);
        }

        /// <summary>Host relay test: listener within range + margin of the speaker.</summary>
        public static bool InRelayRange(Vector3 speaker, Vector3 listener, float range, float margin)
        {
            float r = range + Mathf.Max(0f, margin);
            return (speaker - listener).sqrMagnitude <= r * r;
        }

        /// <summary>Wall occlusion as linear gain: dbPerWall per wall, capped at maxWalls.</summary>
        public static float OcclusionGain(int walls, float dbPerWall, int maxWalls)
        {
            int w = Mathf.Clamp(walls, 0, Mathf.Max(0, maxWalls));
            return Mathf.Pow(10f, dbPerWall * w / 20f);
        }

        /// <summary>Custom rolloff curve for an AudioSource whose maxDistance = <paramref name="range"/> (x axis is distance / range).</summary>
        public static AnimationCurve BuildRolloffCurve(float full, float range, float fadeBand, int keys = 24)
        {
            var curve = new AnimationCurve();
            if (range <= 0f) { curve.AddKey(0f, 0f); curve.AddKey(1f, 0f); return curve; }
            keys = Mathf.Max(4, keys);
            for (int i = 0; i <= keys; i++)
            {
                float t = (float)i / keys;
                // denser sampling near the source where the 1/d curve bends most
                float x = t * t;
                curve.AddKey(new Keyframe(x, Gain(x * range, full, range, fadeBand)));
            }
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }

        /// <summary>Codec payload bytes for one frame of <paramref name="frameMs"/> at <paramref name="codecRate"/> (ADPCM only; Steam is variable).</summary>
        public static int CodecBytesPerFrame(VoiceCodec codec, int frameMs, int codecRate)
        {
            int bits = AdpcmCodec.BitsFor(codec);
            return bits == 0 ? 0 : AdpcmCodec.EncodedBytes(codecRate * frameMs / 1000, bits);
        }

        /// <summary>Kilobits per second for <paramref name="bytesPerFrame"/> sent every <paramref name="frameMs"/>.</summary>
        public static float Kbps(int bytesPerFrame, int frameMs) => frameMs <= 0 ? 0f : bytesPerFrame * 8f / frameMs;

        /// <summary>Uplink kbps per talker (codec + 8 B header).</summary>
        public static float UplinkKbps(VoiceCodec codec, int frameMs, int codecRate) => Kbps(CodecBytesPerFrame(codec, frameMs, codecRate) + VoicePacket.UpHeaderBytes, frameMs);

        /// <summary>Downlink kbps per talker per listener (codec + 17 B header).</summary>
        public static float DownlinkKbps(VoiceCodec codec, int frameMs, int codecRate) => Kbps(CodecBytesPerFrame(codec, frameMs, codecRate) + VoicePacket.DownHeaderBytes, frameMs);

        /// <summary>Transmit decision: never while mic-muted; push-to-talk needs the key (ignored while menus block input); open mic follows VAD.</summary>
        public static bool ShouldTransmit(VoiceTransmitMode mode, bool pttHeld, bool micMuted, bool vadActive, bool inputBlocked)
            => !micMuted && (mode == VoiceTransmitMode.PushToTalk ? pttHeld && !inputBlocked : vadActive);
    }

    /// <summary>Smoothed loudness and open-mic gate (attack/release smoothing + hangover).</summary>
    public sealed class VoiceActivityDetector
    {
        public float Attack = 0.05f, Release = 0.3f, Hangover = 0.4f;
        float _hang;

        /// <summary>Smoothed loudness 0..1.</summary>
        public float Loudness { get; private set; }
        /// <summary>Gate state after the last <see cref="Process"/>.</summary>
        public bool Active { get; private set; }

        /// <summary>Feeds one block's raw loudness; returns whether the gate is open for <paramref name="threshold"/>.</summary>
        public bool Process(float rawLoudness, float dt, float threshold)
        {
            float tau = rawLoudness > Loudness ? Attack : Release;
            float a = tau <= 0f ? 1f : 1f - Mathf.Exp(-dt / tau);
            Loudness += (rawLoudness - Loudness) * a;
            if (threshold <= 0f) { Active = true; _hang = Hangover; }
            else if (Loudness >= threshold) { Active = true; _hang = Hangover; }
            else if (_hang > 0f) { _hang -= dt; Active = _hang > 0f; }
            else Active = false;
            return Active;
        }

        public void Reset() { Loudness = 0f; Active = false; _hang = 0f; }
    }

    /// <summary>Per-sender token bucket (packets/s and bytes/s) used by the host.</summary>
    public sealed class VoiceRateLimiter
    {
        float _packets, _bytes;
        double _last = double.NaN;

        public bool TryConsume(int bytes, double now, float packetsPerSecond, float packetBurst, float bytesPerSecond)
        {
            if (double.IsNaN(_last)) { _packets = packetBurst; _bytes = bytesPerSecond; _last = now; }
            float dt = (float)Math.Max(0, now - _last);
            _last = now;
            _packets = Mathf.Min(packetBurst, _packets + dt * packetsPerSecond);
            _bytes = Mathf.Min(bytesPerSecond, _bytes + dt * bytesPerSecond);
            if (_packets < 1f || _bytes < bytes) return false;
            _packets -= 1f; _bytes -= bytes;
            return true;
        }
    }
}
