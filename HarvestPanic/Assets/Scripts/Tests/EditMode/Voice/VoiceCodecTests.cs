using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using HarvestPanic.Voice;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Voice
{
    /// <summary>Codec round-trip, packet format, bandwidth budget and the capture->wire->playout pipeline (no audio device).</summary>
    public class VoiceCodecTests
    {
        static float[] Sine(int n, float freq, float rate, float amp, int phase0 = 0)
        {
            var a = new float[n];
            for (int i = 0; i < n; i++) a[i] = amp * Mathf.Sin(2f * Mathf.PI * freq * (i + phase0) / rate);
            return a;
        }

        static float SnrDb(float[] reference, float[] decoded, int from, int to)
        {
            double sig = 0, err = 0;
            for (int i = from; i < to; i++) { sig += reference[i] * reference[i]; double e = reference[i] - decoded[i]; err += e * e; }
            return err <= 1e-12 ? 120f : (float)(10.0 * Math.Log10(sig / err));
        }

        [TestCase(4, 25f)] // measured 29.9 dB
        [TestCase(3, 20f)] // measured 24.6 dB
        public void Adpcm_RoundTrip_SineKeepsShape(int bits, float minSnr)
        {
            const int n = 320; // one 40 ms frame at 8 kHz
            var pcm = Sine(n, 440f, 8000f, 0.3f);
            var st = new AdpcmCodec.State();
            var block = new byte[AdpcmCodec.EncodedBytes(n, bits)];
            int bytes = AdpcmCodec.Encode(pcm, 0, n, bits, ref st, block, 0);
            Assert.AreEqual(AdpcmCodec.EncodedBytes(n, bits), bytes);
            var outPcm = new float[n];
            Assert.AreEqual(n, AdpcmCodec.Decode(block, 0, bytes, bits, outPcm, 0, n));
            float snr = SnrDb(pcm, outPcm, 40, n); // skip the step-size warm-up of a fresh spurt
            TestContext.WriteLine($"ADPCM {bits}-bit SNR = {snr:0.0} dB");
            Assert.That(snr, Is.GreaterThan(minSnr));
        }

        [TestCase(4)]
        [TestCase(3)]
        public void Adpcm_SecondPacketDecodesAlone_NoWarmUp(int bits)
        {
            // Packet 2 carries the encoder state in its 3-byte header, so losing packet 1 does not desync the decoder.
            const int n = 320;
            var all = Sine(2 * n, 300f, 8000f, 0.25f);
            var st = new AdpcmCodec.State();
            var b1 = new byte[AdpcmCodec.EncodedBytes(n, bits)];
            var b2 = new byte[AdpcmCodec.EncodedBytes(n, bits)];
            AdpcmCodec.Encode(all, 0, n, bits, ref st, b1, 0);
            int len2 = AdpcmCodec.Encode(all, n, n, bits, ref st, b2, 0);
            var ref2 = new float[n];
            Array.Copy(all, n, ref2, 0, n);
            var dec = new float[n];
            AdpcmCodec.Decode(b2, 0, len2, bits, dec, 0, n);
            float snr = SnrDb(ref2, dec, 0, n);
            TestContext.WriteLine($"ADPCM {bits}-bit packet-2-alone SNR = {snr:0.0} dB");
            Assert.That(snr, Is.GreaterThan(bits == 4 ? 28f : 22f), "packet 2 alone must decode without a warm-up");
        }

        [Test]
        public void Adpcm_ClipsAndSilence_AreSafe()
        {
            var loud = new float[320];
            for (int i = 0; i < loud.Length; i++) loud[i] = i % 2 == 0 ? 1.7f : -1.7f;
            var st = new AdpcmCodec.State();
            var block = new byte[AdpcmCodec.EncodedBytes(320, 3)];
            int len = AdpcmCodec.Encode(loud, 0, 320, 3, ref st, block, 0);
            var dec = new float[320];
            AdpcmCodec.Decode(block, 0, len, 3, dec, 0, 320);
            foreach (var s in dec) Assert.That(s, Is.InRange(-1f, 1f));

            st = new AdpcmCodec.State();
            len = AdpcmCodec.Encode(new float[320], 0, 320, 3, ref st, block, 0);
            AdpcmCodec.Decode(block, 0, len, 3, dec, 0, 320);
            Assert.That(VoiceMath.RmsDbfs(dec, 0, 320), Is.LessThan(-60f), "silence stays silent");
        }

        [Test]
        public void Bandwidth_DefaultCodec_WithinBudgetPerTalker()
        {
            var cfg = ScriptableObject.CreateInstance<VoiceConfig>();
            try
            {
                Assert.AreEqual(VoiceCodec.Adpcm3, cfg.PcmCodec);
                Assert.AreEqual(123, VoiceMath.CodecBytesPerFrame(VoiceCodec.Adpcm3, cfg.FrameMs, cfg.CodecRate));
                float up = VoiceMath.UplinkKbps(cfg.PcmCodec, cfg.FrameMs, cfg.CodecRate);
                float down = VoiceMath.DownlinkKbps(cfg.PcmCodec, cfg.FrameMs, cfg.CodecRate);
                TestContext.WriteLine($"Adpcm3 up {up:0.0} kbps, down {down:0.0} kbps per talker per listener");
                Assert.That(up, Is.LessThanOrEqualTo(32f));
                Assert.That(down, Is.LessThanOrEqualTo(32f));
                Assert.That(VoiceMath.UplinkKbps(VoiceCodec.Adpcm4, 40, 8000), Is.GreaterThan(32f), "4-bit is documented as LAN-only");
                // server enforcement leaves 2x headroom over the budget but stops floods
                Assert.That(cfg.MaxBytesPerSecond * 8f / 1000f, Is.EqualTo(64f).Within(0.01f));
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }

        [Test]
        public void Packet_UpDownMute_RoundTripAndRejects()
        {
            var payload = new byte[123];
            for (int i = 0; i < payload.Length; i++) payload[i] = (byte)i;
            var buf = new byte[1024];
            var h = new VoiceHeader { Codec = VoiceCodec.Adpcm3, Seq = 65535, Flags = VoicePacket.FlagSpurtStart, Loudness = 200, Length = payload.Length };
            int len = VoicePacket.WriteUp(buf, h, payload, 0, payload.Length);
            Assert.AreEqual(VoicePacket.UpHeaderBytes + 123, len);
            Assert.IsTrue(VoicePacket.TryReadUp(buf, len, 600, out var r, out int off));
            Assert.AreEqual(65535, r.Seq); Assert.AreEqual(VoiceCodec.Adpcm3, r.Codec); Assert.AreEqual(200, r.Loudness);
            Assert.AreEqual(VoicePacket.FlagSpurtStart, r.Flags); Assert.AreEqual(123, r.Length); Assert.AreEqual(42, buf[off + 42]);
            Assert.IsFalse(VoicePacket.TryReadUp(buf, len - 1, 600, out _, out _), "length mismatch");
            Assert.IsFalse(VoicePacket.TryReadUp(buf, len, 100, out _, out _), "over max payload");
            buf[1] = 77; Assert.IsFalse(VoicePacket.TryReadUp(buf, len, 600, out _, out _), "unknown codec");
            buf[1] = (byte)VoiceCodec.Adpcm3; buf[0] = 9; Assert.IsFalse(VoicePacket.TryReadUp(buf, len, 600, out _, out _), "unknown version");

            h.Speaker = 0x0102030405060708UL; h.RangeQ = VoiceMath.QuantizeRange(50f);
            len = VoicePacket.WriteDown(buf, h, payload, 0, payload.Length);
            Assert.AreEqual(VoicePacket.DownHeaderBytes + 123, len);
            Assert.IsTrue(VoicePacket.TryReadDown(buf, len, 600, out r, out off));
            Assert.AreEqual(0x0102030405060708UL, r.Speaker);
            Assert.AreEqual(50f, VoiceMath.DequantizeRange(r.RangeQ));
            Assert.AreEqual(7, buf[off + 7]);

            var ids = new List<ulong> { 1, 3, ulong.MaxValue };
            len = VoicePacket.WriteMute(buf, ids);
            var back = new List<ulong>();
            Assert.IsTrue(VoicePacket.TryReadMute(buf, len, back));
            CollectionAssert.AreEqual(ids, back);
            Assert.IsFalse(VoicePacket.TryReadMute(buf, len + 3, back), "trailing garbage");
        }

        [Test]
        public void SteamBlocks_PackAndIterate()
        {
            var dst = new byte[64];
            int pos = 0;
            Assert.IsTrue(SteamVoiceBlocks.TryAppend(dst, ref pos, 64, new byte[] { 1, 2, 3 }, 0, 3));
            Assert.IsTrue(SteamVoiceBlocks.TryAppend(dst, ref pos, 64, new byte[] { 9, 8 }, 0, 2));
            Assert.IsFalse(SteamVoiceBlocks.TryAppend(dst, ref pos, 64, new byte[80], 0, 80), "over capacity");
            Assert.AreEqual(9, pos);
            int p = 0, n = 0, total = 0;
            while (SteamVoiceBlocks.TryNext(dst, ref p, pos, out int bo, out int bl)) { n++; total += bl; Assert.That(dst[bo], Is.EqualTo(n == 1 ? 1 : 9)); }
            Assert.AreEqual(2, n); Assert.AreEqual(5, total);
            dst[0] = 200; p = 0;
            Assert.IsFalse(SteamVoiceBlocks.TryNext(dst, ref p, pos, out _, out _), "length past end is malformed");
        }

        [Test]
        public void Resampling_DecimatorPassesVoiceBandAndRejectsHighs()
        {
            var dec = new Decimator2();
            var low = Sine(1600, 1000f, 16000f, 0.5f);
            var high = Sine(1600, 7000f, 16000f, 0.5f);
            var o = new float[800];
            dec.Process(low, 0, 1600, o, 0);
            float lowDb = VoiceMath.RmsDbfs(o, 100, 700);
            dec.Reset();
            dec.Process(high, 0, 1600, o, 0);
            float highDb = VoiceMath.RmsDbfs(o, 100, 700);
            TestContext.WriteLine($"decimator 1 kHz {lowDb:0.0} dBFS, 7 kHz {highDb:0.0} dBFS");
            Assert.That(lowDb, Is.GreaterThan(-10f));
            Assert.That(highDb, Is.LessThan(lowDb - 15f), "aliasing tone must be strongly attenuated");

            var rs = new LinearResampler();
            var outBuf = new float[4096];
            int total = 0;
            for (int k = 0; k < 10; k++) total += rs.Process(Sine(480, 440f, 48000f, 0.3f, k * 480), 480, 48000, 16000, outBuf, outBuf.Length);
            Assert.That(total, Is.InRange(1598, 1601), "48 kHz -> 16 kHz keeps real time");
        }

        [Test]
        public void Ring_OverflowDropsOldest_AndReadMultiplyResamples()
        {
            var ring = new VoiceRing(16);
            var src = new float[20];
            for (int i = 0; i < 20; i++) src[i] = i;
            Assert.AreEqual(4, ring.Write(src, 0, 20));
            var dst = new float[16];
            Assert.AreEqual(16, ring.Read(dst, 0, 16));
            Assert.AreEqual(4f, dst[0], "oldest 4 were dropped");

            ring.Clear();
            var ones = new float[300];
            for (int i = 0; i < ones.Length; i++) ones[i] = 0.5f;
            ring.Write(ones, 0, 16);
            var data = new float[2 * 48]; // stereo, 48 output frames = 16 ring samples at step 1/3
            for (int i = 0; i < data.Length; i++) data[i] = 1f;
            ring.ReadMultiply(data, 2, 1.0 / 3.0);
            Assert.AreEqual(0.5f, data[0], 1e-5f);
            Assert.AreEqual(0.5f, data[1], 1e-5f, "same gain on both channels keeps the 3D pan");
            Assert.That(ring.Count, Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void Pipeline_EncoderToWireToSpeakerStream_ReproducesTone()
        {
            var cfg = ScriptableObject.CreateInstance<VoiceConfig>();
            try
            {
                double t = 0;
                var src = new SyntheticPcmSource(16000) { Frequency = 440f, Amplitude = 0.3f, Clock = () => t };
                var enc = new PcmVoiceEncoder(src, cfg, cfg.PcmCodec);
                Assert.IsTrue(enc.Start());
                var stream = new VoiceSpeakerStream(7, cfg);
                var frames = new List<VoiceOutFrame>();
                var up = new byte[1024];
                var down = new byte[1024];
                var gate = new VoiceGateInput { Mode = VoiceTransmitMode.OpenMic, Threshold = 0f };
                ushort seq = 100;
                int sent = 0;
                for (int step = 0; step < 50; step++) // 50 x 40 ms = 2 s
                {
                    t += 0.04;
                    frames.Clear();
                    enc.Tick(0.04f, gate, frames);
                    foreach (var f in frames)
                    {
                        var h = new VoiceHeader { Codec = f.Codec, Seq = seq++, Flags = f.Flags, Loudness = VoiceMath.QuantizeLoudness(f.Loudness), Length = f.Length };
                        int ul = VoicePacket.WriteUp(up, h, f.Data, 0, f.Length);
                        Assert.IsTrue(VoicePacket.TryReadUp(up, ul, cfg.MaxPayloadBytes, out var hu, out int uo));
                        hu.Speaker = 7; hu.RangeQ = VoiceMath.QuantizeRange(25f);
                        int dl = VoicePacket.WriteDown(down, hu, up, uo, hu.Length);
                        Assert.IsTrue(VoicePacket.TryReadDown(down, dl, cfg.MaxPayloadBytes, out var hd, out int dOff));
                        stream.Push(hd, down, dOff, t);
                        sent++;
                        if (sent == 1) Assert.AreEqual(VoicePacket.FlagSpurtStart, f.Flags & VoicePacket.FlagSpurtStart, "first frame starts a spurt");
                        else Assert.AreEqual(0, f.Flags & VoicePacket.FlagSpurtStart);
                    }
                    stream.Tick(0.04);
                }
                Assert.That(sent, Is.InRange(48, 50), "25 frames/s");
                Assert.That(stream.FramesPlayed, Is.GreaterThan(40));
                Assert.AreEqual(0, stream.FramesLost);
                Assert.AreEqual(25f, stream.RangeMetres);
                Assert.That(stream.Loudness, Is.GreaterThan(0.8f), "0.3 amplitude tone ~ -13.5 dBFS ~ loudness 0.9");
                float expected = 0.3f / Mathf.Sqrt(2f);
                Assert.That(stream.LastFrameRms, Is.EqualTo(expected).Within(expected * 0.25f), "decoded tone keeps its level");
                Assert.That(stream.IsSpeaking(t, cfg.SpeakerTimeout), Is.True);
                Assert.That(stream.IsSpeaking(t + 1.0, cfg.SpeakerTimeout), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(cfg); }
        }
    }
}
