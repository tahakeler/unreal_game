using System.Collections.Generic;
using HarvestPanic.Core;
using HarvestPanic.Voice;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Voice
{
    /// <summary>VAD and transmit gating, range/attenuation math, host relay rules, jitter ordering and loss handling.</summary>
    public class VoiceLogicTests
    {
        VoiceConfig _cfg;

        [SetUp] public void SetUp() => _cfg = ScriptableObject.CreateInstance<VoiceConfig>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(_cfg);

        // ------------------------------------------------------------------ VAD / gate

        [Test]
        public void Vad_OpensFast_HoldsThroughHangover_ThenCloses()
        {
            var vad = new VoiceActivityDetector { Attack = 0.05f, Release = 0.3f, Hangover = 0.4f };
            Assert.IsFalse(vad.Process(0f, 0.04f, 0.2f));
            int opened = -1;
            for (int i = 0; i < 5 && opened < 0; i++) if (vad.Process(0.8f, 0.04f, 0.2f)) opened = i;
            Assert.That(opened, Is.InRange(0, 1), "attack 50 ms opens within two 40 ms frames");

            int stillOpen = 0;
            for (int i = 0; i < 50; i++) { if (vad.Process(0f, 0.04f, 0.2f)) stillOpen++; else break; }
            float held = stillOpen * 0.04f;
            Assert.That(held, Is.GreaterThanOrEqualTo(0.4f), "hangover keeps the tail of a word");
            Assert.That(held, Is.LessThan(1.5f), "but the gate closes after silence");
            Assert.IsFalse(vad.Active);
        }

        [Test]
        public void Vad_QuietNoiseBelowThreshold_NeverOpens_ZeroThresholdAlwaysOpen()
        {
            var vad = new VoiceActivityDetector();
            for (int i = 0; i < 25; i++) Assert.IsFalse(vad.Process(0.1f, 0.04f, 0.2f));
            Assert.IsTrue(new VoiceActivityDetector().Process(0f, 0.04f, 0f));
        }

        [Test]
        public void ShouldTransmit_TruthTable()
        {
            Assert.IsTrue(VoiceMath.ShouldTransmit(VoiceTransmitMode.PushToTalk, true, false, false, false));
            Assert.IsFalse(VoiceMath.ShouldTransmit(VoiceTransmitMode.PushToTalk, false, false, true, false), "PTT ignores VAD");
            Assert.IsFalse(VoiceMath.ShouldTransmit(VoiceTransmitMode.PushToTalk, true, false, false, true), "menus block PTT");
            Assert.IsTrue(VoiceMath.ShouldTransmit(VoiceTransmitMode.OpenMic, false, false, true, false));
            Assert.IsFalse(VoiceMath.ShouldTransmit(VoiceTransmitMode.OpenMic, false, false, false, false));
            Assert.IsFalse(VoiceMath.ShouldTransmit(VoiceTransmitMode.OpenMic, true, true, true, false), "mic mute wins");
        }

        [Test]
        public void Encoder_Gating_OpenMicSilence_Ptt()
        {
            double t = 0;
            var src = new SyntheticPcmSource(16000) { Amplitude = 0.3f, Clock = () => t, Silent = true };
            var enc = new PcmVoiceEncoder(src, _cfg, VoiceCodec.Adpcm3);
            enc.Start();
            var frames = new List<VoiceOutFrame>();
            int Run(VoiceGateInput g, int steps)
            {
                int n = 0;
                for (int i = 0; i < steps; i++) { t += 0.04; frames.Clear(); enc.Tick(0.04f, g, frames); n += frames.Count; }
                return n;
            }
            var open = new VoiceGateInput { Mode = VoiceTransmitMode.OpenMic, Threshold = 0.2f };
            Assert.AreEqual(0, Run(open, 25), "open mic sends nothing while silent");
            src.Silent = false;
            Assert.That(Run(open, 25), Is.InRange(23, 25), "open mic sends while talking");
            var ptt = new VoiceGateInput { Mode = VoiceTransmitMode.PushToTalk, PttHeld = false };
            Assert.AreEqual(0, Run(ptt, 10), "push-to-talk not held");
            Assert.IsFalse(enc.Transmitting);
            ptt.PttHeld = true;
            Assert.That(Run(ptt, 10), Is.InRange(9, 10));
            Assert.IsTrue(enc.Transmitting);
            ptt.MicMuted = true;
            Assert.AreEqual(0, Run(ptt, 10), "mic muted");
        }

        // ------------------------------------------------------------------ range / attenuation

        [Test]
        public void Gain_FullNear_LogFalloff_SilentAtRange()
        {
            const float full = 3f, range = 25f, fade = 3f;
            Assert.AreEqual(1f, VoiceMath.Gain(0f, full, range, fade));
            Assert.AreEqual(1f, VoiceMath.Gain(3f, full, range, fade));
            Assert.AreEqual(0.5f, VoiceMath.Gain(6f, full, range, fade), 1e-4f, "-6 dB per doubling");
            Assert.AreEqual(-12.04f, VoiceMath.GainDb(12f, full, range, fade), 0.05f);
            Assert.AreEqual(0f, VoiceMath.Gain(25f, full, range, fade));
            Assert.AreEqual(0f, VoiceMath.Gain(40f, full, range, fade));
            float inFade = VoiceMath.Gain(23.5f, full, range, fade);
            Assert.That(inFade, Is.GreaterThan(0f).And.LessThan(3f / 23.5f), "faded over the last 3 m");
            float prev = 2f;
            for (float d = 0f; d <= 30f; d += 0.25f)
            {
                float g = VoiceMath.Gain(d, full, range, fade);
                Assert.That(g, Is.LessThanOrEqualTo(prev + 1e-6f), "monotonic at " + d);
                prev = g;
            }
        }

        [Test]
        public void RolloffCurve_MatchesGainFormula()
        {
            var curve = VoiceMath.BuildRolloffCurve(3f, 25f, 3f);
            for (float d = 0f; d <= 25f; d += 0.5f)
                Assert.AreEqual(VoiceMath.Gain(d, 3f, 25f, 3f), curve.Evaluate(d / 25f), 0.04f, "at " + d + " m");
            Assert.AreEqual(0f, curve.Evaluate(1f), 1e-3f, "silent at max distance (built-in Logarithmic never is)");
        }

        [Test]
        public void EffectiveRange_MegaphoneDoubles_Clamped()
        {
            Assert.AreEqual(25f, VoiceMath.EffectiveRange(25f, 1f, 0.5f, 3f));
            Assert.AreEqual(50f, VoiceMath.EffectiveRange(25f, 2f, 0.5f, 3f), "Megaphone upgrade x2");
            Assert.AreEqual(62.5f, VoiceMath.EffectiveRange(25f, 2.5f, 0.5f, 3f), "Megaphone x Quiet Hours stacks");
            Assert.AreEqual(75f, VoiceMath.EffectiveRange(25f, 9f, 0.5f, 3f));
            Assert.AreEqual(12.5f, VoiceMath.EffectiveRange(25f, 0.1f, 0.5f, 3f));
            Assert.AreEqual(25f, VoiceMath.EffectiveRange(25f, float.NaN, 0.5f, 3f));
            Assert.AreEqual(25f, VoiceMath.EffectiveRange(25f, -2f, 0.5f, 3f));
            Assert.AreEqual(50f, VoiceMath.DequantizeRange(VoiceMath.QuantizeRange(50f)));
            Assert.AreEqual(127.5f, VoiceMath.DequantizeRange(VoiceMath.QuantizeRange(500f)), "saturates");
        }

        [Test]
        public void Occlusion_NineDbPerWall_CappedAtTwo()
        {
            Assert.AreEqual(1f, VoiceMath.OcclusionGain(0, -9f, 2));
            Assert.AreEqual(-9f, 20f * Mathf.Log10(VoiceMath.OcclusionGain(1, -9f, 2)), 0.01f);
            Assert.AreEqual(VoiceMath.OcclusionGain(2, -9f, 2), VoiceMath.OcclusionGain(5, -9f, 2));
        }

        [Test]
        public void Loudness_GddFormula()
        {
            Assert.AreEqual(0f, VoiceMath.Loudness(-50f));
            Assert.AreEqual(0.5f, VoiceMath.Loudness(-30f), 1e-5f, "normal speech");
            Assert.AreEqual(1f, VoiceMath.Loudness(-10f));
            Assert.AreEqual(0f, VoiceMath.Loudness(-120f));
            Assert.AreEqual(255, VoiceMath.QuantizeLoudness(3f));
            Assert.AreEqual(0, VoiceMath.QuantizeLoudness(float.NaN));
        }

        // ------------------------------------------------------------------ relay rules

        [Test]
        public void Relay_SelfMutedRangeDeliver()
        {
            var a = Vector3.zero;
            Assert.AreEqual(VoiceRoute.Self, VoiceRelay.Decide(1, 1, false, true, a, a, 25f, 2f));
            Assert.AreEqual(VoiceRoute.Muted, VoiceRelay.Decide(1, 2, true, true, a, new Vector3(1, 0, 0), 25f, 2f));
            Assert.AreEqual(VoiceRoute.Deliver, VoiceRelay.Decide(1, 2, false, true, a, new Vector3(26.9f, 0, 0), 25f, 2f), "margin");
            Assert.AreEqual(VoiceRoute.OutOfRange, VoiceRelay.Decide(1, 2, false, true, a, new Vector3(27.1f, 0, 0), 25f, 2f));
            Assert.AreEqual(VoiceRoute.Deliver, VoiceRelay.Decide(1, 2, false, true, a, new Vector3(40f, 0, 0), 50f, 2f), "Megaphone range");
            Assert.AreEqual(VoiceRoute.Deliver, VoiceRelay.Decide(1, 2, false, false, a, new Vector3(999f, 0, 0), 25f, 2f), "unknown positions deliver (2D fallback)");
        }

        [Test]
        public void MuteTable_PerListener_ForgetsOnDisconnect()
        {
            var t = new VoiceMuteTable();
            t.Set(2, new ulong[] { 1, 2, 3 });
            Assert.IsTrue(t.IsMuted(2, 1));
            Assert.IsTrue(t.IsMuted(2, 3));
            Assert.IsFalse(t.IsMuted(2, 2), "a listener cannot mute itself");
            Assert.IsFalse(t.IsMuted(1, 2), "mutes are per listener");
            t.Set(2, new ulong[] { 3 });
            Assert.IsFalse(t.IsMuted(2, 1), "Set replaces the list");
            t.Remove(3);
            Assert.IsFalse(t.IsMuted(2, 3), "disconnected speaker forgotten");
            var many = new List<ulong>();
            for (ulong i = 10; i < 100; i++) many.Add(i);
            t.Set(5, many);
            Assert.AreEqual(VoiceMuteTable.MaxEntries, t.CountFor(5), "bounded");
        }

        [Test]
        public void NoiseGate_ThresholdAndInterval()
        {
            var g = new VoiceNoiseGate();
            Assert.IsFalse(g.ShouldEmit(1, 0.3f, 0.0, 0.35f, 0.5f), "below 0.35");
            Assert.IsTrue(g.ShouldEmit(1, 0.5f, 0.0, 0.35f, 0.5f));
            Assert.IsFalse(g.ShouldEmit(1, 0.9f, 0.3, 0.35f, 0.5f), "at most once per 0.5 s");
            Assert.IsTrue(g.ShouldEmit(2, 0.9f, 0.3, 0.35f, 0.5f), "per player");
            Assert.IsTrue(g.ShouldEmit(1, 0.9f, 0.5, 0.35f, 0.5f));
        }

        [Test]
        public void RateLimiter_BurstThenRefill()
        {
            var r = new VoiceRateLimiter();
            int ok = 0;
            for (int i = 0; i < 30; i++) if (r.TryConsume(131, 0.0, 60f, 20f, 8000f)) ok++;
            Assert.AreEqual(20, ok, "burst of 20 packets");
            Assert.IsTrue(r.TryConsume(131, 1.0, 60f, 20f, 8000f), "refills over time");
            var bytes = new VoiceRateLimiter();
            Assert.IsFalse(bytes.TryConsume(9000, 0.0, 60f, 20f, 8000f), "byte budget");
        }

        // ------------------------------------------------------------------ jitter buffer

        static readonly byte[] P = { 1, 2, 3, 4 };

        static List<string> Drain(VoiceJitterBuffer jb, int pops)
        {
            var seen = new List<string>();
            for (int i = 0; i < pops; i++)
            {
                var r = jb.Pop(out var f);
                seen.Add(r == JitterPop.Frame ? f.Seq.ToString() : r.ToString());
            }
            return seen;
        }

        [Test]
        public void Jitter_ReordersDuringPrefill()
        {
            var jb = new VoiceJitterBuffer(16, 2, 3, 600);
            jb.Push(12, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4);
            jb.Push(10, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4);
            jb.Push(11, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4);
            jb.Push(13, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4);
            CollectionAssert.AreEqual(new[] { "10", "11", "12", "13" }, Drain(jb, 4));
        }

        [Test]
        public void Jitter_ReordersWhilePlaying_ReportsLoss_DropsDuplicateAndLate()
        {
            var jb = new VoiceJitterBuffer(16, 2, 3, 600);
            jb.Push(0, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4);
            jb.Push(1, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4);
            CollectionAssert.AreEqual(new[] { "0" }, Drain(jb, 1));
            jb.Push(3, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4);
            Assert.AreEqual(JitterPush.Duplicate, jb.Push(3, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4));
            jb.Push(5, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4);
            jb.Push(4, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4);
            CollectionAssert.AreEqual(new[] { "1", "Lost", "3", "4", "5" }, Drain(jb, 5));
            Assert.AreEqual(JitterPush.Late, jb.Push(2, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4), "too late to play");
        }

        [Test]
        public void Jitter_SequenceWrap()
        {
            var jb = new VoiceJitterBuffer(16, 2, 3, 600);
            foreach (ushort s in new ushort[] { 65534, 0, 65535, 1 }) jb.Push(s, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4);
            CollectionAssert.AreEqual(new[] { "65534", "65535", "0", "1" }, Drain(jb, 4));
        }

        [Test]
        public void Jitter_UnderrunsEndSpurt_BigJumpResets()
        {
            var jb = new VoiceJitterBuffer(16, 2, 3, 600);
            jb.Push(0, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4);
            jb.Push(1, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4);
            CollectionAssert.AreEqual(new[] { "0", "1", "Underrun", "Underrun", "Underrun", "Empty" }, Drain(jb, 6));
            Assert.IsFalse(jb.Playing, "spurt ended");
            jb.Push(500, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4);
            Assert.AreEqual("Empty", Drain(jb, 1)[0], "re-prefills before playing again");
            jb.Push(501, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4);
            Assert.AreEqual(JitterPush.Reset, jb.Push(900, VoiceCodec.Adpcm3, 0, 0f, P, 0, 4), "jump beyond capacity resets");
            Assert.AreEqual(JitterPush.TooLarge, jb.Push(901, VoiceCodec.Adpcm3, 0, 0f, new byte[700], 0, 700));
        }

        [Test]
        public void SpeakerStream_ConcealsOneLostFrame_ThenSilence_AndReanchorsNewSpurt()
        {
            var stream = new VoiceSpeakerStream(3, _cfg);
            var frame = new byte[AdpcmCodec.EncodedBytes(320, 3)];
            var pcm = new float[320];
            for (int i = 0; i < 320; i++) pcm[i] = 0.3f * Mathf.Sin(i * 0.3f);
            var st = new AdpcmCodec.State();
            int len = AdpcmCodec.Encode(pcm, 0, 320, 3, ref st, frame, 0);
            void Push(ushort seq, byte flags = 0) => stream.Push(new VoiceHeader { Speaker = 3, Codec = VoiceCodec.Adpcm3, Seq = seq, Flags = flags, Length = len, Loudness = 128 }, frame, 0, 0);

            Push(0, VoicePacket.FlagSpurtStart); Push(1); Push(4); Push(5);
            stream.Tick(0.0);   // prefilled: plays 0 and pre-buffers 1
            Assert.AreEqual(2, stream.FramesPlayed);
            stream.Tick(0.04);  // 2 lost -> concealed with frame 1 at half gain
            stream.Tick(0.04);  // 3 lost -> silence
            stream.Tick(0.04);  // 4
            stream.Tick(0.04);  // 5
            Assert.AreEqual(4, stream.FramesPlayed);
            Assert.AreEqual(2, stream.FramesLost);
            Assert.AreEqual(1, stream.FramesConcealed, "only one repeat, then silence");
            Assert.AreEqual(6 * 640, stream.SamplesQueued, "timing kept: every slot produced a 16 kHz frame");

            stream.Tick(0.04);  // underrun 1 (spurt still 'playing')
            Push(6, VoicePacket.FlagSpurtStart); Push(7);
            Assert.AreEqual(0, stream.Late, "a new spurt re-anchors instead of being dropped as late");
        }
    }
}
