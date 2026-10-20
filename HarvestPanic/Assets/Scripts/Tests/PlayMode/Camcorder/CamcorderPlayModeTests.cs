using System.Collections;
using System.IO;
using HarvestPanic.Camcorder;
using HarvestPanic.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.PlayMode.Camcorder
{
    /// <summary>Record -> save -> replay reproduces positions (design/gdd/camcorder-replay.md).</summary>
    public class CamcorderPlayModeTests
    {
        GameObject[] _cubes;
        CamcorderService _svc;

        [SetUp]
        public void SetUp()
        {
            if (CamcorderService.Instance != null) Object.DestroyImmediate(CamcorderService.Instance.gameObject);
            _svc = CamcorderService.EnsureExists();
            _svc.enabled = false; // driven by Step() for deterministic timing
            Services.Register<ICamcorderService>(_svc);
        }

        [TearDown]
        public void TearDown()
        {
            if (_svc != null) { _svc.Viewer.Close(); Object.DestroyImmediate(_svc.gameObject); }
            if (_cubes != null) foreach (var c in _cubes) if (c != null) Object.DestroyImmediate(c);
            InputModal.Clear();
        }

        static Vector3 Path(int i, float c) => new Vector3(c * 2f + i * 3f, Mathf.Sin(c * 3f) + i, Mathf.Cos(c) * i);

        IEnumerator Record(int steps, float dt, int disasterAt, System.Action<int> perStep = null)
        {
            for (int s = 0; s < steps; s++)
            {
                float next = _svc.Clock + dt;
                for (int i = 0; i < _cubes.Length; i++)
                {
                    _cubes[i].transform.position = Path(i, next);
                    _cubes[i].transform.rotation = Quaternion.Euler(0f, next * 40f * (i + 1), 0f);
                }
                perStep?.Invoke(s);
                _svc.Step(dt);
                if (s == disasterAt)
                    _svc.OnDisaster(new BlameEvent { Severity = 80, Verb = BlameVerb.DroppedInRiver, DollarDelta = -240, PrimaryInstigator = 0, Instigators = new ulong[] { 0 }, Location = _cubes[0].transform.position });
                if (s % 20 == 0) yield return null;
            }
        }

        void MakeCubes(int n)
        {
            _cubes = new GameObject[n];
            for (int i = 0; i < n; i++)
            {
                _cubes[i] = GameObject.CreatePrimitive(i == 0 ? PrimitiveType.Capsule : PrimitiveType.Cube);
                _cubes[i].name = "Actor" + i;
                _svc.Track(_cubes[i].transform, i == 0 ? TrackKind.Player : TrackKind.Critter, i == 0 ? 0UL : PlayerIds.None, i == 0 ? "Dave" : null);
            }
        }

        [UnityTest]
        public IEnumerator Record_Save_Replay_ReproducesPositions()
        {
            MakeCubes(3);
            const float dt = 0.05f;
            yield return Record(400, dt, 350, s => { if (s == 300) _cubes[2].GetComponent<Renderer>().enabled = false; });
            // Post-roll (3 s = 60 steps) not finished yet -> no tape; finish it.
            Assert.That(_svc.Shelf.Count, Is.EqualTo(0));
            yield return Record(60, dt, -1);
            Assert.That(_svc.Shelf.Count, Is.EqualTo(1), "tape cut after post-roll");
            var m = _svc.Shelf.Items[0];
            Assert.That(m.Title, Does.EndWith("vs. THE RIVER, $\u2212240"));
            Assert.That(m.TriggerTime, Is.EqualTo(_svc.Tuning.preRollSeconds).Within(0.06f), "full pre-roll before impact");
            Assert.That(m.Duration - m.TriggerTime, Is.EqualTo(_svc.Tuning.postRollSeconds).Within(0.06f), "post-roll after impact");
            Assert.That(m.Tracks.Count, Is.EqualTo(3));

            _svc.Watch(m);
            Assert.That(_svc.Viewer.IsOpen, Is.True);
            Assert.That(_svc.IsViewing, Is.True);
            Assert.That(InputModal.IsBlocking, Is.True, "viewer blocks gameplay input");
            Assert.That(_cubes[1].GetComponent<Renderer>().forceRenderingOff, Is.True, "live actor hidden while watching");
            _svc.Viewer.Playing = false;
            for (int k = 0; k < m.FrameCount; k += 7)
            {
                float t = m.FrameTimes[k];
                _svc.Viewer.SetTime(t);
                float abs = m.SourceStart + t;
                for (int i = 0; i < _cubes.Length; i++)
                {
                    var tr = m.Tracks.Find(x => x.Name == "Actor" + i || (i == 0 && x.Name == "Dave"));
                    var ghost = _svc.Viewer.GhostFor(tr.Id);
                    Assert.That(ghost, Is.Not.Null);
                    Assert.That(Vector3.Distance(ghost.position, Path(i, abs)), Is.LessThan(1e-3f), $"actor {i} at t={t}");
                    Assert.That(Quaternion.Angle(ghost.rotation, Quaternion.Euler(0f, abs * 40f * (i + 1), 0f)), Is.LessThan(0.1f));
                    bool shouldShow = !(i == 2 && abs > 300 * dt + dt * 0.5f);
                    Assert.That(ghost.gameObject.activeSelf, Is.EqualTo(shouldShow), $"visibility actor {i} t={t}");
                }
            }
            // Ghosts are visual only.
            Assert.That(_svc.Viewer.GhostFor(m.Tracks[1].Id).GetComponentInChildren<Collider>(), Is.Null);
            Assert.That(_svc.Viewer.GhostFor(m.Tracks[1].Id).GetComponentInChildren<Rigidbody>(), Is.Null);

            // Playback advances on its own and slow-mo engages near the impact.
            _svc.Viewer.SetTime(m.TriggerTime);
            Assert.That(_svc.Viewer.EffectiveSpeed, Is.EqualTo(_svc.Tuning.slowMoFactor).Within(1e-4f));
            _svc.Viewer.SetTime(1f); _svc.Viewer.Playing = true;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(_svc.Viewer.Time, Is.GreaterThan(1.05f));

            // Serialized tape replays identically.
            var back = MomentSerializer.FromBytes(MomentSerializer.ToBytes(m));
            _svc.Viewer.Close();
            Assert.That(_cubes[1].GetComponent<Renderer>().forceRenderingOff, Is.False, "live actor restored");
            Assert.That(InputModal.IsBlocking, Is.False);
            _svc.Watch(back);
            _svc.Viewer.Playing = false;
            _svc.Viewer.SetTime(m.TriggerTime);
            var g0 = _svc.Viewer.GhostFor(m.Tracks[0].Id);
            Assert.That(Vector3.Distance(g0.position, Path(0, m.SourceStart + m.TriggerTime)), Is.LessThan(0.2f));
        }

        [UnityTest]
        public IEnumerator Manual_Save_CappedPerDay_AndSteadyStateSamplingIsAllocationFree()
        {
            MakeCubes(4);
            yield return Record(400, 0.05f, -1);
            _svc.SampleNow();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 400; i++) _svc.SampleNow();
            Assert.That(System.GC.GetAllocatedBytesForCurrentThread() - before, Is.EqualTo(0), "no GC per recorded frame");

            for (int i = 0; i < _svc.Tuning.manualSavesPerDay; i++)
            {
                Assert.That(_svc.SaveManualMoment(), Is.True);
                yield return Record(70, 0.05f, -1);
            }
            Assert.That(_svc.SaveManualMoment(), Is.False, "cap reached");
            Assert.That(_svc.Shelf.Count, Is.EqualTo(_svc.Tuning.manualSavesPerDay));
            Assert.That(_svc.Shelf.Items[0].Manual, Is.True);
            Assert.That(_svc.Shelf.Items[0].Duration, Is.GreaterThan(_svc.Tuning.preRollSeconds));
            _svc.NewDay();
            Assert.That(_svc.Shelf.Count, Is.EqualTo(0));
            Assert.That(_svc.SaveManualMoment(), Is.True);
        }

        [UnityTest]
        public IEnumerator Export_WritesFramesSheetThumbAndTape()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("needs a GPU (run without -nographics)");
            var camGo = new GameObject("TestCam", typeof(Camera)) { tag = "MainCamera" };
            camGo.transform.position = new Vector3(0, 5, -15);
            MakeCubes(3);
            yield return Record(60, 0.05f, 30);
            var m = _svc.FinalizeNow();
            Assert.That(m, Is.Not.Null);
            Assert.That(m.Thumbnail, Is.Not.Null, "live best-shot thumbnail");
            _svc.Watch(m);
            string root = System.IO.Path.Combine(Application.temporaryCachePath, "ClipsTest");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            string folder = null, err = null;
            yield return ClipExporter.Export(_svc.Viewer, _svc.Tuning, (f, e) => { folder = f; err = e; }, root);
            Assert.That(err, Is.Null);
            Assert.That(Directory.GetFiles(System.IO.Path.Combine(folder, "frames"), "*.png").Length, Is.EqualTo(ClipExporter.FrameCount(m.Duration, _svc.Tuning.exportFps, _svc.Tuning.maxExportFrames)));
            Assert.That(File.Exists(System.IO.Path.Combine(folder, "contact_sheet.png")));
            Assert.That(File.Exists(System.IO.Path.Combine(folder, "thumb.png")));
            Assert.That(File.Exists(System.IO.Path.Combine(folder, "README.txt")));
            var back = MomentSerializer.FromBytes(File.ReadAllBytes(System.IO.Path.Combine(folder, "moment.hpm")));
            Assert.That(back.FrameCount, Is.EqualTo(m.FrameCount));
            Object.Destroy(camGo);
            Directory.Delete(root, true);
        }
    }
}
