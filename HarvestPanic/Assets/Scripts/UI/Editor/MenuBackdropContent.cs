using System;
using System.Collections.Generic;
using System.IO;
using HarvestPanic.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HarvestPanic.UI.EditorTools
{
    /// <summary>
    /// Main-menu backdrop content (UiSceneBuilder.BuildMainMenu): imports Muddlebury's visuals, the camera framings per menu
    /// screen and the wandering critter models; plus a windowless look-dev capture of the framings:
    /// Unity -batchmode -projectPath HarvestPanic -executeMethod HarvestPanic.UI.EditorTools.MenuBackdropContent.CaptureShots
    ///   -menuOut /abs/dir [-menuCands "x,y,z>lx,ly,lz@fov;..."] -quit -logFile /abs/log   (needs a GPU: no -nographics)
    /// </summary>
    public static class MenuBackdropContent
    {
        public const string FarmScene = "Assets/Scenes/Muddlebury.unity";

        /// <summary>Muddlebury groups that are gameplay plumbing or out of every framing (not imported).</summary>
        static readonly HashSet<string> SkipGroups = new HashSet<string>
        {
            "Markers", "QA_Cameras", "NavMesh", "World Edges", "Night Windows", "CursedCrops", "Spit-out Pile", "Market Town",
        };

        /// <summary>Components that stay on imported objects (everything else is stripped).</summary>
        static readonly HashSet<string> KeepTypes = new HashSet<string>
        {
            "Transform", "MeshFilter", "MeshRenderer", "SkinnedMeshRenderer", "LODGroup", "Terrain", "Light", "ReflectionProbe",
            "LightProbeGroup", "ParticleSystem", "ParticleSystemRenderer", "Animator", "TextMesh", "LineRenderer", "TrailRenderer",
            "BillboardRenderer", "Volume", "UniversalAdditionalLightData", "BoxCollider", "MeshCollider", "CapsuleCollider", "SphereCollider",
            "TerrainCollider",
            // cosmetic World scripts (client-only visuals)
            "InstancedForest", "Spinner", "Bobber", "LitText",
        };

        static Vector3 Euler(Vector3 pos, Vector3 look) => Quaternion.LookRotation(look - pos, Vector3.up).eulerAngles;

        static MenuBackdrop.Shot Shot(MenuScreen s, Vector3 pos, Vector3 look, float fov) =>
            new MenuBackdrop.Shot { screen = s, position = pos, euler = Euler(pos, look), fov = fov };

        /// <summary>One framing per menu screen. The menu cards sit on the left, so subjects are framed right of centre.</summary>
        public static readonly MenuBackdrop.Shot[] Shots =
        {
            Shot(MenuScreen.Title, new Vector3(14f, 2.6f, 24f), new Vector3(-26f, 4f, -4f), 48f),
            Shot(MenuScreen.Play, new Vector3(-56f, 4.5f, 10f), new Vector3(-90f, 6f, -36f), 52f),
            Shot(MenuScreen.Host, new Vector3(-24f, 2.6f, 12f), new Vector3(-56f, 3.8f, 20f), 54f),
            Shot(MenuScreen.Join, new Vector3(-60f, 3f, -10f), new Vector3(-20f, 4f, 20f), 55f),
            Shot(MenuScreen.Settings, new Vector3(-62f, 30f, -150f), new Vector3(0f, 0f, 0f), 50f),
            Shot(MenuScreen.Credits, new Vector3(-70f, 2.5f, -20f), new Vector3(-88f, 8f, -38f), 55f),
        };

        // ------------------------------------------------------------------ import
        /// <summary>Moves Muddlebury's visual groups into <paramref name="menu"/>. False when the farm scene is missing.</summary>
        public static bool ImportFarm(Scene menu)
        {
            if (!File.Exists(FarmScene)) { Debug.LogWarning("[MenuBackdrop] " + FarmScene + " not found"); return false; }
            var farm = EditorSceneManager.OpenScene(FarmScene, OpenSceneMode.Additive);
            try
            {
                CopyLighting(farm, menu);
                var backdrop = new GameObject("Farm Backdrop");
                SceneManager.MoveGameObjectToScene(backdrop, menu);
                foreach (var root in farm.GetRootGameObjects())
                {
                    if (SkipGroups.Contains(root.name)) continue;
                    if (root.transform.childCount > 0 && root.name == "Muddlebury")
                    {
                        var kids = new List<Transform>();
                        foreach (Transform c in root.transform) kids.Add(c);
                        foreach (var c in kids) if (SkipGroups.Contains(c.name)) Object.DestroyImmediate(c.gameObject);
                    }
                    else if (root.GetComponentInChildren<Renderer>(true) == null && root.GetComponentInChildren<Light>(true) == null && root.GetComponentInChildren<Terrain>(true) == null
                             && root.GetComponent("Volume") == null) continue;
                    Strip(root);
                    SceneManager.MoveGameObjectToScene(root, menu);
                    root.transform.SetParent(backdrop.transform, true);
                }
                var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                foreach (var l in sun) if (l.type == LightType.Directional && l.gameObject.scene == menu) { RenderSettings.sun = l; break; }
                int stripped = 0;
                foreach (var c in backdrop.GetComponentsInChildren<Component>(true)) if (c != null && !KeepTypes.Contains(c.GetType().Name)) stripped++;
                Debug.Log($"[MenuBackdrop] imported farm: renderers={backdrop.GetComponentsInChildren<Renderer>(true).Length} leftover-nonvisual={stripped}");
                return true;
            }
            finally
            {
                EditorSceneManager.CloseScene(farm, true); // never saved: Muddlebury.unity is untouched
                SceneManager.SetActiveScene(menu);
            }
        }

        /// <summary>Removes every component that is not cosmetic. Dependencies are resolved over a few passes.</summary>
        static void Strip(GameObject root)
        {
            for (int pass = 0; pass < 6; pass++)
            {
                bool left = false;
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || KeepTypes.Contains(c.GetType().Name)) continue;
                    try { Object.DestroyImmediate(c, true); }
                    catch (Exception) { left = true; }
                    if (c != null) left = true;
                }
                if (!left) return;
            }
        }

        static void CopyLighting(Scene from, Scene to)
        {
            SceneManager.SetActiveScene(from);
            var skybox = RenderSettings.skybox; var mode = RenderSettings.ambientMode;
            Color sky = RenderSettings.ambientSkyColor, eq = RenderSettings.ambientEquatorColor, gr = RenderSettings.ambientGroundColor, amb = RenderSettings.ambientLight;
            float ai = RenderSettings.ambientIntensity;
            bool fog = RenderSettings.fog; var fm = RenderSettings.fogMode; Color fc = RenderSettings.fogColor;
            float fd = RenderSettings.fogDensity, fs = RenderSettings.fogStartDistance, fe = RenderSettings.fogEndDistance;
            var refl = RenderSettings.defaultReflectionMode; float ri = RenderSettings.reflectionIntensity;
            SceneManager.SetActiveScene(to);
            RenderSettings.skybox = skybox; RenderSettings.ambientMode = mode;
            RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = gr; RenderSettings.ambientLight = amb;
            RenderSettings.ambientIntensity = ai;
            RenderSettings.fog = fog; RenderSettings.fogMode = fm; RenderSettings.fogColor = fc;
            RenderSettings.fogDensity = fd; RenderSettings.fogStartDistance = fs; RenderSettings.fogEndDistance = fe;
            RenderSettings.defaultReflectionMode = refl; RenderSettings.reflectionIntensity = ri;
        }

        // ------------------------------------------------------------------ critters
        struct Spot { public string prefab; public Vector3 home; public float roam, speed; public bool escape; public Vector3 from, to; }

        static readonly Spot[] Critters =
        {
            // title framing: pigs in front of the barn, an escapee that bolts across the yard, chickens, a sheep and a cow
            new Spot { prefab = "Pig", home = new Vector3(-15f, 0f, 7f), roam = 3f, speed = 0.8f },
            new Spot { prefab = "Pig", home = new Vector3(-11f, 0f, 10f), roam = 3f, speed = 0.7f },
            new Spot { prefab = "Pig", home = new Vector3(-20f, 0f, 34f), roam = 1.5f, speed = 0.9f, escape = true, from = new Vector3(6f, 0f, -2f), to = new Vector3(-17.7f, 0f, 31.2f) },
            new Spot { prefab = "Pig", home = new Vector3(15f, 0f, 33f), roam = 4f, speed = 0.8f },
            new Spot { prefab = "Chicken", home = new Vector3(-20f, 0f, 16f), roam = 3f, speed = 0.7f },
            new Spot { prefab = "Chicken", home = new Vector3(-22f, 0f, 18f), roam = 3f, speed = 0.8f },
            new Spot { prefab = "Chicken", home = new Vector3(-38f, 0f, 17f), roam = 3f, speed = 0.7f },
            new Spot { prefab = "Sheep", home = new Vector3(-12f, 0f, 20f), roam = 4f, speed = 0.5f },
            new Spot { prefab = "Cow", home = new Vector3(-30f, 0f, 18f), roam = 3f, speed = 0.4f },
            new Spot { prefab = "Goat", home = new Vector3(-40f, 0f, 0f), roam = 4f, speed = 0.6f },
        };

        /// <summary>Adds the wandering animal models (visual model of each critter prefab; no AI, physics or networking).</summary>
        public static void AddCritters(Transform parent)
        {
            var root = new GameObject("Menu Critters").transform;
            root.SetParent(parent, false);
            int seed = 11;
            foreach (var spot in Critters)
            {
                var model = CritterModel(spot.prefab);
                if (model == null) continue;
                model.name = "Menu " + spot.prefab;
                model.transform.SetParent(root, true);
                model.transform.position = Ground(spot.home);
                model.transform.rotation = Quaternion.Euler(0f, seed * 47f % 360f, 0f);
                var anim = model.GetComponentInChildren<Animator>(true);
                if (anim != null) { anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms; }
                var mc = model.AddComponent<MenuCritter>();
                mc.Configure(anim, spot.home, spot.roam, spot.speed, seed++);
                if (spot.escape) mc.ConfigureEscape(spot.from, spot.to, 4.6f, 16f);
            }
        }

        /// <summary>The "Model" child of Critters/Critter_X.prefab as a standalone object (scripts stripped).</summary>
        static GameObject CritterModel(string kind)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Critters/Critter_" + kind + ".prefab");
            if (prefab == null) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            var anim = inst.GetComponentInChildren<Animator>(true);
            Transform model = inst.transform.Find("Model") ?? (anim != null ? anim.transform : null);
            if (model == null) { Object.DestroyImmediate(inst); return null; }
            var holder = new GameObject("Critter");
            holder.transform.SetPositionAndRotation(inst.transform.position, inst.transform.rotation);
            model.SetParent(holder.transform, true);
            Object.DestroyImmediate(inst);
            Strip(holder);
            foreach (var col in holder.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            return holder;
        }

        static Vector3 Ground(Vector3 p)
        {
            foreach (var t in Terrain.activeTerrains)
            {
                var o = t.GetPosition(); var size = t.terrainData.size;
                if (p.x < o.x || p.z < o.z || p.x > o.x + size.x || p.z > o.z + size.z) continue;
                p.y = o.y + t.SampleHeight(p);
                return p;
            }
            if (Physics.Raycast(p + Vector3.up * 50f, Vector3.down, out var hit, 100f)) p.y = hit.point.y;
            return p;
        }

        // ------------------------------------------------------------------ look-dev capture
        public static void CaptureShots()
        {
            string outDir = Arg("-menuOut") ?? Path.GetFullPath("../Logs/menu_shots");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { Debug.LogError("[MenuBackdrop] null graphics device"); if (Application.isBatchMode) EditorApplication.Exit(1); return; }
            var cam = Object.FindFirstObjectByType<Camera>();
            var shots = new List<(string name, Vector3 pos, Vector3 look, float fov)>();
            string cands = Arg("-menuCands");
            if (!string.IsNullOrEmpty(cands))
            {
                int i = 0;
                foreach (var c in cands.Split(';'))
                {
                    var a = c.Split('>', '@');
                    if (a.Length < 3) continue;
                    shots.Add(("cand_" + (i++), V(a[0]), V(a[1]), float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture)));
                }
            }
            else foreach (var s in Shots) shots.Add(("shot_" + s.screen, s.position, s.position + Quaternion.Euler(s.euler) * Vector3.forward, s.fov));
            foreach (var s in shots)
            {
                cam.transform.SetPositionAndRotation(s.pos, Quaternion.LookRotation(s.look - s.pos));
                cam.fieldOfView = s.fov;
                Render(cam, 1280, 720, Path.Combine(outDir, s.name + ".png"));
            }
            Debug.Log($"[MenuBackdrop] wrote {shots.Count} captures to {outDir}");
        }

        static Vector3 V(string s)
        {
            var p = s.Split(',');
            float F(string x) => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture);
            return new Vector3(F(p[0]), F(p[1]), F(p[2]));
        }

        static void Render(Camera cam, int width, int height, string path)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            rt.Create();
            try
            {
                var req = new RenderPipeline.StandardRequest { destination = rt };
                for (int pass = 0; pass < 3; pass++)
                {
                    if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
                    else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
                }
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally { rt.Release(); Object.DestroyImmediate(rt); }
        }

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }
    }
}
