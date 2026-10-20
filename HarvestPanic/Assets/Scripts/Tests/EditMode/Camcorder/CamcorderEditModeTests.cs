using System;
using System.IO;
using HarvestPanic.Camcorder;
using HarvestPanic.Core;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Camcorder
{
    public class RingBufferTests
    {
        static ReplaySample S(int id, float x) => new ReplaySample(id, TrackKind.Critter, SampleFlags.Visible, new Vector3(x, 0, 0), Quaternion.identity);

        [Test]
        public void Ring_OverwritesOldest_WhenFull()
        {
            var r = new ReplayRingBuffer(4, 2);
            for (int i = 0; i < 6; i++) { r.BeginFrame(i); r.Add(S(1, i)); r.EndFrame(); }
            Assert.That(r.Count, Is.EqualTo(4));
            Assert.That(r.OldestTime, Is.EqualTo(2f));
            Assert.That(r.NewestTime, Is.EqualTo(5f));
            for (int i = 0; i < 4; i++) Assert.That(r.SampleAt(i, 0).Position.x, Is.EqualTo(i + 2f));
        }

        [Test]
        public void Ring_DropsSamples_PastActorCap()
        {
            var r = new ReplayRingBuffer(4, 2);
            r.BeginFrame(0); Assert.That(r.Add(S(1, 0)), Is.True); Assert.That(r.Add(S(2, 0)), Is.True); Assert.That(r.Add(S(3, 0)), Is.False); r.EndFrame();
            Assert.That(r.SampleCount(0), Is.EqualTo(2));
            Assert.That(r.DroppedSamples, Is.EqualTo(1));
        }

        [Test]
        public void Ring_MeasureAndCopy_RebasesTimes()
        {
            var r = new ReplayRingBuffer(50, 3);
            for (int i = 0; i < 40; i++) { r.BeginFrame(i * 0.05f); r.Add(S(1, i)); if (i % 2 == 0) r.Add(S(2, -i)); r.EndFrame(); }
            r.Measure(0.5f, 1.0f, out int first, out int frames, out int samples);
            Assert.That(first, Is.EqualTo(10));
            Assert.That(frames, Is.EqualTo(11));
            Assert.That(samples, Is.EqualTo(11 + 6));
            var times = new float[frames]; var starts = new int[frames]; var dst = new ReplaySample[samples];
            r.CopyFrames(first, frames, r.TimeAt(first), times, starts, dst);
            Assert.That(times[0], Is.EqualTo(0f));
            Assert.That(times[10], Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(dst[starts[1]].Position.x, Is.EqualTo(11f));
        }

        [Test]
        public void Ring_Recording_DoesNotAllocate()
        {
            var r = new ReplayRingBuffer(64, 128);
            var s = S(1, 1);
            for (int i = 0; i < 100; i++) { r.BeginFrame(i); r.Add(s); r.EndFrame(); } // warm
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++) { r.BeginFrame(i); for (int a = 0; a < 100; a++) r.Add(s); r.EndFrame(); }
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0));
        }

        [Test]
        public void Budget_DefaultRing_FitsUnder8MB()
        {
            var t = ScriptableObject.CreateInstance<CamcorderTuning>();
            var r = new ReplayRingBuffer(t.RingFrames, t.maxActorsPerFrame);
            Assert.That(t.RingFrames, Is.GreaterThanOrEqualTo(t.maxTapeSeconds * t.tickHz));
            Assert.That(r.MemoryBytes, Is.LessThan(8L * 1024 * 1024), "ring budget (docs/qa/camcorder-progress.md)");
            UnityEngine.Object.DestroyImmediate(t);
        }

        [Test]
        public void EventRing_KeepsNewest()
        {
            var e = new ReplayEventRing(3);
            for (int i = 0; i < 5; i++) e.Add(new ReplayEvent { Time = i });
            Assert.That(e.Count, Is.EqualTo(3));
            Assert.That(e[0].Time, Is.EqualTo(2f));
            Assert.That(e[2].Time, Is.EqualTo(4f));
        }
    }

    public class MomentTests
    {
        internal static Moment Make()
        {
            var m = new Moment { Id = 7, Title = "DAVE vs. THE RIVER, $−240", Severity = 82, DollarDelta = -240, Verb = BlameVerb.DroppedInRiver, PrimaryInstigator = 1, InstigatorCount = 2, Focus = new Vector3(1, 2, 3), TriggerTime = 0.5f, DayIndex = 3, SourceStart = 12f };
            m.FrameTimes = new[] { 0f, 0.05f, 0.1f };
            m.FrameStarts = new[] { 0, 2, 4 };
            m.Samples = new[]
            {
                new ReplaySample(1, TrackKind.Player, SampleFlags.Visible, new Vector3(0, 0, 0), Quaternion.identity),
                new ReplaySample(2, TrackKind.Critter, SampleFlags.Visible, new Vector3(5, 0, 0), Quaternion.identity),
                new ReplaySample(1, TrackKind.Player, SampleFlags.Visible, new Vector3(1, 0, 0), Quaternion.Euler(0, 90, 0)),
                new ReplaySample(2, TrackKind.Critter, SampleFlags.Visible, new Vector3(5, 1, 0), Quaternion.identity),
                new ReplaySample(1, TrackKind.Player, SampleFlags.None, new Vector3(2, 0, 0), Quaternion.identity),
            };
            m.Events = new[] { new ReplayEvent { Time = 0.05f, Type = ReplayEventType.Disaster, Value = 82, Code = (int)BlameVerb.DroppedInRiver, PlayerId = 1 } };
            m.Tracks.Add(new TrackInfo { Id = 1, Kind = TrackKind.Player, OwnerClientId = 1, Name = "Dave" });
            m.Tracks.Add(new TrackInfo { Id = 2, Kind = TrackKind.Critter, OwnerClientId = PlayerIds.None, Name = "Pig" });
            return m;
        }

        [Test]
        public void Evaluate_InterpolatesBetweenFrames()
        {
            var m = Make();
            Assert.That(m.Duration, Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(m.TryEvaluate(1, 0.025f, out var p, out var r, out var f), Is.True);
            Assert.That(p.x, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(Quaternion.Angle(r, Quaternion.Euler(0, 45, 0)), Is.LessThan(0.5f));
            Assert.That((f & SampleFlags.Visible) != 0);
            Assert.That(m.TryEvaluate(2, 0.1f, out _, out _, out _), Is.False, "pig missing from last frame");
            Assert.That(m.TryEvaluate(1, 0.1f, out _, out _, out var f2) && (f2 & SampleFlags.Visible) == 0);
            Assert.That(m.PlayerTrackFor(1), Is.EqualTo(1));
            Assert.That(m.KeepScore, Is.EqualTo(82 + 20));
        }

        [Test]
        public void Serializer_RoundTrips()
        {
            var m = Make();
            var back = MomentSerializer.FromBytes(MomentSerializer.ToBytes(m));
            Assert.That(back.Title, Is.EqualTo(m.Title));
            Assert.That(back.Severity, Is.EqualTo(m.Severity));
            Assert.That(back.Verb, Is.EqualTo(m.Verb));
            Assert.That(back.Focus, Is.EqualTo(m.Focus));
            Assert.That(back.SourceStart, Is.EqualTo(m.SourceStart));
            Assert.That(back.FrameTimes, Is.EqualTo(m.FrameTimes));
            Assert.That(back.FrameStarts, Is.EqualTo(m.FrameStarts));
            Assert.That(back.Samples.Length, Is.EqualTo(m.Samples.Length));
            for (int i = 0; i < m.Samples.Length; i++)
            {
                Assert.That(back.Samples[i].Id, Is.EqualTo(m.Samples[i].Id));
                Assert.That(back.Samples[i].Position, Is.EqualTo(m.Samples[i].Position));
                Assert.That(back.Samples[i].Rotation, Is.EqualTo(m.Samples[i].Rotation));
                Assert.That(back.Samples[i].Flags, Is.EqualTo(m.Samples[i].Flags));
            }
            Assert.That(back.Events.Length, Is.EqualTo(1));
            Assert.That(back.Events[0].Code, Is.EqualTo((int)BlameVerb.DroppedInRiver));
            Assert.That(back.Tracks.Count, Is.EqualTo(2));
            Assert.That(back.Tracks[0].Name, Is.EqualTo("Dave"));
        }

        [Test]
        public void Serializer_RejectsGarbage()
        {
            Assert.Throws<InvalidDataException>(() => MomentSerializer.FromBytes(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            var bytes = MomentSerializer.ToBytes(Make());
            Array.Resize(ref bytes, bytes.Length - 10);
            Assert.Throws<EndOfStreamException>(() => MomentSerializer.FromBytes(bytes));
        }
    }

    public class CaptureRulesTests
    {
        static CaptureRules Rules() => new CaptureRules { PreRoll = 15, PostRoll = 3, Cooldown = 8, MaxTape = 45, ManualPerDay = 3 };

        [Test]
        public void Disaster_OpensWindow_PreAndPostRoll()
        {
            var r = Rules();
            Assert.That(r.OnDisaster(100f, 70, 50f), Is.EqualTo(CaptureDecision.Started));
            Assert.That(r.TapeStart, Is.EqualTo(85f));
            Assert.That(r.TapeEnd, Is.EqualTo(103f));
            Assert.That(r.Tick(102.9f), Is.False);
            Assert.That(r.Tick(103f), Is.True);
            Assert.That(r.Tick(104f), Is.False, "fires once");
        }

        [Test]
        public void EarlyDisaster_StartsAtOldestBuffered()
        {
            var r = Rules();
            r.OnDisaster(5f, 70, 0f);
            Assert.That(r.TapeStart, Is.EqualTo(0f));
        }

        [Test]
        public void SecondDisaster_WhileCapturing_ExtendsSameTape()
        {
            var r = Rules();
            r.OnDisaster(100f, 70, 0f);
            Assert.That(r.OnDisaster(102f, 60, 0f), Is.EqualTo(CaptureDecision.Extended));
            Assert.That(r.TapeEnd, Is.EqualTo(105f));
            Assert.That(r.TapeStart, Is.EqualTo(85f));
        }

        [Test]
        public void DisasterWithinCooldown_ReopensFinishedTape()
        {
            var r = Rules();
            r.OnDisaster(100f, 70, 0f);
            Assert.That(r.Tick(103f), Is.True);
            Assert.That(r.OnDisaster(104f, 70, 0f), Is.EqualTo(CaptureDecision.Reopened));
            Assert.That(r.TapeStart, Is.EqualTo(85f));
            Assert.That(r.TapeEnd, Is.EqualTo(107f));
            Assert.That(r.Tick(107f), Is.True);
        }

        [Test]
        public void DisasterAfterCooldown_StartsNewTape()
        {
            var r = Rules();
            r.OnDisaster(100f, 70, 0f); r.Tick(103f);
            Assert.That(r.OnDisaster(109f, 70, 0f), Is.EqualTo(CaptureDecision.Started));
            Assert.That(r.TapeStart, Is.EqualTo(94f));
        }

        [Test]
        public void Extension_CappedAtMaxTape()
        {
            var r = Rules();
            r.OnDisaster(100f, 70, 0f);
            for (float t = 102f; t < 140f; t += 2f) r.OnDisaster(t, 70, 0f);
            Assert.That(r.TapeEnd - r.TapeStart, Is.LessThanOrEqualTo(45f + 1e-4f));
        }

        [Test]
        public void Manual_CappedPerDay_RefillsOnNewDay()
        {
            var r = Rules();
            for (int i = 0; i < 3; i++) { Assert.That(r.OnManual(100f + i * 20f, 0f), Is.Not.EqualTo(CaptureDecision.DeniedManualCap)); r.Tick(1000f); }
            Assert.That(r.OnManual(200f, 0f), Is.EqualTo(CaptureDecision.DeniedManualCap));
            Assert.That(r.ManualLeft, Is.EqualTo(0));
            r.NewDay();
            Assert.That(r.OnManual(300f, 0f), Is.EqualTo(CaptureDecision.Started));
        }

        [Test]
        public void SeverityFloor_AndOff()
        {
            var r = Rules(); r.MinSeverity = 60;
            Assert.That(r.OnDisaster(10f, 40, 0f), Is.EqualTo(CaptureDecision.Ignored));
            r.Enabled = false;
            Assert.That(r.OnDisaster(10f, 90, 0f), Is.EqualTo(CaptureDecision.DeniedOff));
            Assert.That(r.OnManual(10f, 0f), Is.EqualTo(CaptureDecision.DeniedOff));
        }
    }

    public class ShelfAndTitleTests
    {
        static Moment M(long id, byte sev, bool manual = false, int dollars = 0) => new Moment { Id = id, Severity = sev, Manual = manual, DollarDelta = dollars };

        [Test]
        public void Shelf_EvictsLowestScore_KeepsManual()
        {
            var s = new MomentShelf { MaxTapes = 3 };
            s.Add(M(1, 10, true)); s.Add(M(2, 90)); s.Add(M(3, 60)); s.Add(M(4, 70));
            Assert.That(s.Count, Is.EqualTo(3));
            Assert.That(s.Find(3), Is.Null, "lowest non-manual evicted");
            Assert.That(s.Find(1), Is.Not.Null, "manual survives");
        }

        [Test]
        public void Shelf_ReplacesSameId()
        {
            var s = new MomentShelf();
            s.Add(M(1, 60)); s.Add(M(1, 80));
            Assert.That(s.Count, Is.EqualTo(1));
            Assert.That(s.Items[0].Severity, Is.EqualTo(80));
        }

        [Test]
        public void Shelf_MemoryBudget_Evicts()
        {
            var s = new MomentShelf { BudgetBytes = 1000 };
            var a = M(1, 60); a.Samples = new ReplaySample[20];
            var b = M(2, 90); b.Samples = new ReplaySample[20];
            s.Add(a); s.Add(b);
            Assert.That(s.Count, Is.EqualTo(1));
            Assert.That(s.Items[0].Id, Is.EqualTo(2));
        }

        [Test]
        public void Payout_KeepsTopSeverityPlusManual_AndBestIsBiggestDisaster()
        {
            var s = new MomentShelf();
            s.Add(M(1, 61)); s.Add(M(2, 99, false, -100)); s.Add(M(3, 75)); s.Add(M(4, 5, true)); s.Add(M(5, 99, false, -300));
            s.PruneForPayout(2);
            Assert.That(s.Count, Is.EqualTo(3));
            Assert.That(s.Find(1), Is.Null); Assert.That(s.Find(3), Is.Null);
            Assert.That(s.Best().Id, Is.EqualTo(5), "ties broken by bigger loss");
        }

        [Test]
        public void Best_FallsBackToManual()
        {
            var s = new MomentShelf();
            Assert.That(s.Best(), Is.Null);
            s.Add(M(4, 0, true));
            Assert.That(s.Best().Id, Is.EqualTo(4));
        }

        [Test]
        public void Titles_AreTabloid()
        {
            Assert.That(TapeTitles.ForDisaster("Dave", BlameVerb.DroppedInRiver, -240), Is.EqualTo("DAVE vs. THE RIVER, $−240"));
            Assert.That(TapeTitles.ForDisaster("", BlameVerb.BrokePlank, 0), Is.EqualTo("NATURE vs. THE BRIDGE"));
            Assert.That(TapeTitles.ForManual("Ann", 0), Does.Contain("ANN").And.Contain("DAY 1"));
        }

        [Test]
        public void Export_FrameCountAndFolderName()
        {
            Assert.That(ClipExporter.FrameCount(10f, 15, 450), Is.EqualTo(151));
            Assert.That(ClipExporter.FrameCount(45f, 15, 450), Is.EqualTo(450));
            var name = ClipExporter.FolderName(new Moment { Title = "DAVE vs. THE RIVER, $-240" }, new DateTime(1994, 6, 12, 13, 5, 9));
            Assert.That(name, Is.EqualTo("19940612_130509_dave_vs_the_river_240"));
            Assert.That(ClipExporter.FfmpegHint(15), Does.Contain("frame_%04d.png").And.Contain("-framerate 15"));
        }
    }
}
