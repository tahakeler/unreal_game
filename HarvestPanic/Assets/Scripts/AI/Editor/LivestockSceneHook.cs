using System.IO;
using HarvestPanic.Core;
using HarvestPanic.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HarvestPanic.AI.Editor
{
    /// <summary>
    /// Minimal Muddlebury hook for the livestock feature: adds the Cow/Goat <see cref="LevelMarker"/>s (same positions as
    /// World's <c>BuildMuddlebury.Markers</c>) to the saved scene without a full World rebuild, plus an offscreen QA capture.
    /// <c>-executeMethod HarvestPanic.AI.Editor.LivestockSceneHook.AddMuddleburyMarkers</c> (idempotent);
    /// <c>-executeMethod HarvestPanic.AI.Editor.LivestockSceneHook.Capture -qaOut /abs/dir</c> (no -nographics).
    /// </summary>
    public static class LivestockSceneHook
    {
        public const string MuddleburyPath = "Assets/Scenes/Muddlebury.unity";
        static readonly Vector3 PigPen = new Vector3(16f, 0f, 34f);
        /// <summary>Sheep flock (5) grazing west of the pig pen; same spot as World's BuildMuddlebury marker.</summary>
        public static readonly Vector3 SheepMeadow = new Vector3(-6f, 0.1f, 20f);
        public const int SheepCount = 5;

        public static readonly (string name, string key, Vector3 pos)[] Markers =
        {
            ("CritterSpawner_Cow", "Critter.Cow", PigPen + new Vector3(-14f, 0.1f, -12f)),
            ("CritterSpawner_Goat", "Critter.Goat", PigPen + new Vector3(-11f, 0.1f, -4f)),
            ("CritterSpawner_Sheep", "Critter.Sheep", SheepMeadow),
        };

        /// <summary>Cursed-crop fields (AI-owned <see cref="Crops.CursedCropField"/> objects): mandrakes in the veg-row lanes, melons by the pumpkin patch.</summary>
        public static readonly (string name, string goodsId, Vector3 pos, int count, int columns, float spacing, float depth)[] Fields =
        {
            ("CursedCropField_Mandrake", "Crop.Mandrake", new Vector3(-74f, 0f, 34f), 6, 2, 3f, 0.25f),
            ("CursedCropField_TumbleMelon", "Crop.TumbleMelon", new Vector3(-43f, 0f, -44f), 3, 3, 1.8f, 0f),
        };

        public static void AddMuddleburyMarkers()
        {
            var scene = EditorSceneManager.OpenScene(MuddleburyPath, OpenSceneMode.Single);
            var parent = GameObject.Find("Markers");
            foreach (var m in Markers)
            {
                if (GameObject.Find(m.name) != null) continue;
                var g = new GameObject(m.name);
                if (parent != null) g.transform.SetParent(parent.transform, false);
                g.transform.position = m.pos;
                g.AddComponent<LevelMarker>().Configure(MarkerRole.CritterSpawner, m.key, m.key == "Critter.Sheep" ? SheepCount : 1);
                try { g.tag = MarkerTag.CritterSpawner; } catch (UnityException) { }
            }
            var crops = GameObject.Find("CursedCrops") ?? new GameObject("CursedCrops");
            foreach (var f in Fields)
            {
                var existing = GameObject.Find(f.name);
                var g = existing != null ? existing : new GameObject(f.name);
                g.transform.SetParent(crops.transform, false);
                g.transform.position = f.pos;
                var field = g.GetComponent<Crops.CursedCropField>() ?? g.AddComponent<Crops.CursedCropField>();
                field.Configure(f.goodsId, f.count, f.spacing, f.columns, f.depth);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[LivestockSceneHook] Muddlebury markers ok");
        }

        public static void Capture()
        {
            string outDir = Arg("-qaOut") ?? Path.GetFullPath("Logs");
            EditorSceneManager.OpenScene(MuddleburyPath, OpenSceneMode.Single);
            Content.Critters.Invalidate();
            Content.Critters.TryGet("Critter.Cow", out var cow);
            Content.Critters.TryGet("Critter.Goat", out var goat);
            Vector3 c = Ground(Markers[0].pos), g = Ground(Markers[1].pos);
            Vector3 mid = (c + g) * 0.5f;
            Vector3 cowPos = Ground(mid + new Vector3(-1.4f, 0f, 0f)), goatPos = Ground(mid + new Vector3(1.4f, 0f, 0.4f));
            Place(cow.Prefab, cowPos, 250f);
            Place(goat.Prefab, goatPos, 220f);
            var camGo = new GameObject("QA Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            Vector3 look = (cowPos + goatPos) * 0.5f + Vector3.up * 0.7f;
            cam.transform.position = look + new Vector3(0.5f, 1.4f, -5.5f);
            cam.transform.LookAt(look);
            bool ok = QaScreenshot.Capture(cam, 1280, 720, Path.Combine(outDir, "cow_goat_muddlebury.png"));
            cam.transform.position = look + new Vector3(-5f, 0.6f, 0.5f);
            cam.transform.LookAt(look);
            ok &= QaScreenshot.Capture(cam, 1280, 720, Path.Combine(outDir, "cow_goat_side.png"));
            Debug.Log($"[LivestockSceneHook] capture ok={ok} -> {outDir}");
            if (!ok && Application.isBatchMode) EditorApplication.Exit(1);
        }

        static Vector3 Ground(Vector3 p) =>
            Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var h, 40f, ~0, QueryTriggerInteraction.Ignore) ? h.point : p;

        static void Place(GameObject prefab, Vector3 pos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var anim = go.GetComponentInChildren<Animator>();
            if (anim != null && anim.runtimeAnimatorController != null && anim.runtimeAnimatorController.animationClips.Length > 0)
                anim.runtimeAnimatorController.animationClips[0].SampleAnimation(anim.gameObject, 0.3f);
        }

        static string Arg(string name)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }
    }
}
