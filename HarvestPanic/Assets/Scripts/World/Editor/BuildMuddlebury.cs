using System;
using System.Collections.Generic;
using System.IO;
using HarvestPanic.Core;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace HarvestPanic.World.Editor
{
    /// <summary>
    /// Reproducibly builds Assets/Scenes/Muddlebury.unity (Region 1 farm, GDD 11.5, docs/qa/map-progress.md).
    /// Unity -batchmode -nographics -projectPath HarvestPanic -executeMethod HarvestPanic.World.Editor.BuildMuddlebury.Build -quit -logFile /abs/build.log
    /// Coordinates: metres, +X east (toward bridge/market), +Z north, the river flows -Z. Farm ground at y = 0, water at y = -4.2.
    /// Seeded (no wall-clock randomness), so re-running produces the same scene. Split by area:
    /// Landscape (terrain grid, river, horizon), Farm, Bridge, Town, Signs, Materials. After saving, the AI team's
    /// <c>LivestockSceneHook.AddMuddleburyMarkers</c> is re-run (by reflection, no assembly reference) so a rebuild keeps
    /// their livestock markers and cursed-crop fields.
    /// </summary>
    public static partial class BuildMuddlebury
    {
        public const string ScenePath = "Assets/Scenes/Muddlebury.unity";
        const string TerrainDir = WorldAssets.ArtRoot + "/Terrain";
        const string SettingsDir = WorldAssets.ArtRoot + "/Settings";
        const string NF = "KenneyNature", FT = "KenneyFantasyTown", FD = "KenneyFood", SV = "KenneySurvival";

        // Layout constants (GDD 11.5)
        public const float RiverWest = 60f, RiverEast = 100f, RiverCentre = 80f, RiverBedY = -6f, WaterY = -4.2f;
        public const int PlankCount = 20;
        public const float PlankLength = 2f, PlankWidth = 2.4f, PlankThickness = 0.14f, BridgeSag = 0.7f;
        /// <summary>Playable rectangle (the four world-edge colliders sit just outside it).</summary>
        public const float PlayX0 = -122f, PlayX1 = 177f, PlayZ0 = -148f, PlayZ1 = 104f;
        static readonly Vector3 Barn = new Vector3(-26f, 0f, 0f);
        static readonly Vector3 PigPen = new Vector3(16f, 0f, 34f);
        static readonly Vector3 Coop = new Vector3(16f, 0f, -36f);
        static readonly Vector3 Pumpkins = new Vector3(-56f, 0f, -44f);
        static readonly Vector3 Windmill = new Vector3(-86f, 0f, -38f);
        static readonly Vector3 Pile = new Vector3(52f, 0f, -120f);
        static readonly Vector3 SalePen = new Vector3(114f, 0f, 0f);
        static readonly Vector3 Farmhouse = new Vector3(-47f, 0f, 24f);

        /// <summary>
        /// Spots random dressing must never cover: spawners, goods, starts, the pile, the AI team's cursed-crop fields and
        /// the night director's field posts / fox dens / lantern posts (AI/Night/NightMath.cs defaults).
        /// </summary>
        static readonly (Vector2 p, float r)[] KeepClear =
        {
            (new Vector2(19, 33), 3f), (new Vector2(18, -38), 3f), (new Vector2(2, 22), 4f), (new Vector2(5, 30), 3.5f), (new Vector2(-6, 20), 5f),
            (new Vector2(-11.8f, 7.2f), 3f), (new Vector2(-11, 0), 5f), (new Vector2(-14.5f, 0), 2f), (new Vector2(-56, -44), 4f), (new Vector2(52, -120), 6f),
            (new Vector2(-72.5f, 37f), 6f), (new Vector2(-41.2f, -42.2f), 4.5f),
            (new Vector2(-56, -44), 2f), (new Vector2(-48, -30), 2.5f), (new Vector2(-64, -56), 2.5f), (new Vector2(-30, -28), 2.5f), (new Vector2(0, -22), 2.5f), (new Vector2(0, 22), 2.5f), (new Vector2(30, -14), 2.5f),
            (new Vector2(40, -72), 4f), (new Vector2(-80, 60), 4f), (new Vector2(-90, -80), 4f),
            (new Vector2(-1.6f, 26f), 2.2f), (new Vector2(-17f, 30f), 2.6f), (new Vector2(8.6f, 24.2f), 1.2f), (new Vector2(-13.5f, 34f), 1.5f), // pasture props
            (new Vector2(-14, 3.2f), 1.5f), (new Vector2(2, -3.2f), 1.5f), (new Vector2(18, 3.2f), 1.5f), (new Vector2(34, -3.2f), 1.5f), (new Vector2(50, 3.2f), 1.5f), (new Vector2(100, 6f), 1.5f),
        };

        static Transform _root;
        static Random _rng;
        static readonly List<Renderer> WindowRenderers = new List<Renderer>();

        [MenuItem("Harvest Panic/World/Build Muddlebury")]
        public static void Build()
        {
            try
            {
                _rng = new Random(1977);
                WindowRenderers.Clear();
                CullGroups.Clear();
                foreach (var d in new[] { WorldAssets.MatDir, WorldAssets.TexDir, WorldAssets.MeshDir, TerrainDir, SettingsDir, "Assets/Prefabs/World", "Assets/Resources/NetworkPrefabs" })
                    Directory.CreateDirectory(d);
                WorldAssets.ConfigureModelImports();
                Mats.Init();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _root = new GameObject("Muddlebury").transform;

                Lighting();
                Landscape();
                River();
                Bridge();
                BridgeArea();
                BarnArea();
                FarmhouseArea();
                Pens();
                Pasture();
                Fields();
                WindmillArea();
                Orchard();
                BankHedge();
                LostAndFound();
                HayMeadow();
                Patchwork();
                MarketTown();
                FarmDressing();
                Boundaries();
                WorldEdges();
                Markers();
                QaCameras();
                NightLights();
                ApplyCulling();
                BakeNavMesh();
                EnsureWorldPrefabList();

                EditorSceneManager.SaveScene(scene, ScenePath);
                // In-scene NetworkObjects need their GlobalObjectIdHash generated against the saved scene.
                foreach (var no in Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
                {
                    typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.Invoke(no, null);
                    EditorUtility.SetDirty(no);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[BuildMuddlebury] Saved {ScenePath}: markers={Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None).Length} renderers={Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length}");
                RunOtherTeamSceneHooks();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Re-applies other teams' idempotent Muddlebury hooks after a rebuild (they open + save the scene themselves).
        /// Reflection keeps World free of a reference to the AI editor assembly (contracts: never reference another team).
        /// </summary>
        static void RunOtherTeamSceneHooks()
        {
            var hook = Type.GetType("HarvestPanic.AI.Editor.LivestockSceneHook, HarvestPanic.AI.Editor");
            var m = hook?.GetMethod("AddMuddleburyMarkers", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (m == null) { Debug.LogWarning("[BuildMuddlebury] LivestockSceneHook not found; cursed-crop fields not re-added"); return; }
            m.Invoke(null, null);
            Debug.Log("[BuildMuddlebury] re-ran LivestockSceneHook.AddMuddleburyMarkers");
        }

        // ------------------------------------------------------------------ lighting / post

        static void Lighting()
        {
            // Matches WeatherLook.ForPhase(None) ("golden afternoon") so WeatherPresenter takes over without a pop.
            var sunGo = new GameObject("Sun (golden afternoon)");
            sunGo.transform.SetParent(_root);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = WorldAssets.Hex("FFE9C4");
            sun.intensity = 2.0f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sunGo.transform.rotation = Quaternion.Euler(32f, 42f, 0f);
            RenderSettings.sun = sun;

            var sky = AssetDatabase.LoadAssetAtPath<Material>(WorldAssets.MatDir + "/M_World_Sky.mat");
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, WorldAssets.MatDir + "/M_World_Sky.mat"); }
            sky.SetColor("_SkyTint", WorldAssets.Hex("B3CCE3"));
            sky.SetColor("_GroundColor", WorldAssets.Hex("C9C7A8"));
            sky.SetFloat("_AtmosphereThickness", 0.85f);
            sky.SetFloat("_Exposure", 1.2f);
            sky.SetFloat("_SunSize", 0.03f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = WorldAssets.Hex("A9C4DC");
            RenderSettings.ambientEquatorColor = WorldAssets.Hex("D8C3A0");
            RenderSettings.ambientGroundColor = WorldAssets.Hex("6E5A44");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = WorldAssets.Hex("E9D9BC");
            RenderSettings.fogStartDistance = 140f;
            RenderSettings.fogEndDistance = 700f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            var profilePath = SettingsDir + "/VP_Muddlebury.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, profilePath); }
            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet<T>(out var c)) { c = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(c, profile); }
                return c;
            }
            Get<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var ca = Get<ColorAdjustments>();
            // Saturation +4 and post exposure 0.1 are the baseline WeatherPresenter's delta volume assumes; keep them.
            ca.saturation.Override(4f); ca.contrast.Override(10f); ca.postExposure.Override(0.1f);
            ca.colorFilter.Override(WorldAssets.Hex("FFF6E8"));
            var wb = Get<WhiteBalance>(); wb.temperature.Override(6f); wb.tint.Override(3f);
            var bloom = Get<Bloom>(); bloom.intensity.Override(0.4f); bloom.threshold.Override(1.2f); bloom.scatter.Override(0.6f);
            var smh = Get<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.95f, 0.94f, 1.08f, 0.02f)); // violet-tinted, slightly lifted shadows (art bible 5)
            smh.highlights.Override(new Vector4(1.02f, 1.0f, 0.97f, 0f));
            var vig = Get<Vignette>(); vig.intensity.Override(0.24f); vig.smoothness.Override(0.45f);
            var mb = Get<MotionBlur>(); mb.active = false;
            EditorUtility.SetDirty(profile);

            var volGo = new GameObject("Global Volume (art bible)");
            volGo.transform.SetParent(_root);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true; vol.priority = 10; vol.sharedProfile = profile;
        }

        // ------------------------------------------------------------------ shared helpers

        static float Smooth(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }

        static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        static float DistRect(float x, float z, float x0, float z0, float x1, float z1)
        {
            float dx = Mathf.Max(x0 - x, 0f, x - x1), dz = Mathf.Max(z0 - z, 0f, z - z1);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static Transform Group(string name, Transform parent = null)
        {
            var g = new GameObject(name).transform; g.SetParent(parent ?? _root, false); return g;
        }

        static float Rnd(float a, float b) => a + (float)_rng.NextDouble() * (b - a);
        static T Pick<T>(params T[] items) => items[_rng.Next(items.Length)];

        static bool NearKeepClear(float x, float z, float pad = 0f)
        {
            var p = new Vector2(x, z);
            foreach (var k in KeepClear) if ((p - k.p).magnitude < k.r + pad) return true;
            return false;
        }

        static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        /// <summary>Ground point (terrain height function) at x,z plus a lift.</summary>
        static Vector3 G(float x, float z, float lift = 0f) => new Vector3(x, GroundY(x, z) + lift, z);

        /// <summary>
        /// Places a CC0 model with its base on the ground, scaled uniformly so its height is <paramref name="height"/> (0 = keep).
        /// Returns null if the model is missing so a missing pack never breaks the build.
        /// </summary>
        static GameObject Place(string pack, string model, Vector3 pos, float yaw, float height, Transform parent, string collider = null, bool snap = true)
        {
            var src = WorldAssets.Model(pack, model);
            if (src == null) { Debug.LogWarning($"[BuildMuddlebury] missing model {pack}/{model}"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.position = Vector3.zero;
            if (height > 0f)
            {
                var b0 = RendererBounds(go);
                if (b0.size.y > 1e-3f) go.transform.localScale *= height / b0.size.y;
            }
            var b = RendererBounds(go);
            float gy = snap ? GroundY(pos.x, pos.z) : pos.y;
            go.transform.position = new Vector3(pos.x, gy + (snap ? pos.y : 0f), pos.z) + new Vector3(0f, -b.min.y, 0f);
            go.isStatic = true;
            if (collider == "box") AddBox(go);
            else if (collider == "trunk") AddTrunk(go);
            return go;
        }

        /// <summary>Like <see cref="Place"/> but with a free rotation and exact pivot placement (wall-hung tools, lanterns).</summary>
        static GameObject PlaceAt(string pack, string model, Vector3 pos, Quaternion rot, float scale, Transform parent)
        {
            var src = WorldAssets.Model(pack, model);
            if (src == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one * scale;
            go.isStatic = true;
            return go;
        }

        // ------------------------------------------------------------------ Tripo (owner-generated) set pieces

        const string TripoDir = "Assets/ThirdParty/Tripo";

        /// <summary>Instantiates a Tripo FBX (real size, +Z front) snapped to the ground, static, with a box collider.</summary>
        static GameObject TripoPlace(string model, Vector3 pos, float yaw, Transform parent, Material mat = null, bool collider = true)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{TripoDir}/{model}/{model}.fbx");
            if (src == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.SetPositionAndRotation(new Vector3(pos.x, GroundY(pos.x, pos.z) + pos.y, pos.z), Quaternion.Euler(0f, yaw, 0f));
            if (mat != null) foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            go.isStatic = true;
            if (collider) AddBox(go);
            return go;
        }

        /// <summary>Tripo market stall; each stall gets a tint variant of the base material (art-director to confirm tints).</summary>
        static GameObject TripoStall(string name, Vector3 pos, float yaw, Transform parent)
        {
            string basePath = "Assets/Art/Tripo/Materials/M_Tripo_MarketStall.mat";
            var baseMat = AssetDatabase.LoadAssetAtPath<Material>(basePath);
            if (baseMat == null) return null;
            Color tint = name.Contains("Greengrocer") ? WorldAssets.Hex("D9EBCF") : name.Contains("Oddities") ? WorldAssets.Hex("E3DAF0") : Color.white;
            Material mat = baseMat;
            if (tint != Color.white)
            {
                string path = $"Assets/Art/Tripo/Materials/M_Tripo_MarketStall_{name.Replace("Stall_", "")}.mat";
                mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(baseMat); AssetDatabase.CreateAsset(mat, path); }
                mat.CopyPropertiesFromMaterial(baseMat);
                mat.SetColor("_BaseColor", tint);
                EditorUtility.SetDirty(mat);
            }
            return TripoPlace("MarketStall", pos, yaw, parent, mat);
        }

        /// <summary>Tripo pen gate with its hinge post at <paramref name="hinge"/>; yaw 0 runs the gate along +X.</summary>
        static GameObject TripoGate(string name, Vector3 hinge, float yaw, Transform parent)
        {
            var go = TripoPlace("PenGate", hinge, 0f, parent);
            if (go == null) return null;
            go.name = name;
            var b = RendererBounds(go);
            float flip = b.center.x < hinge.x ? 180f : 0f;
            go.transform.rotation = Quaternion.Euler(0f, yaw + flip, 0f);
            return go;
        }

        static void AddBox(GameObject go)
        {
            var b = RendererBounds(go);
            var bc = go.AddComponent<BoxCollider>();
            bc.center = go.transform.InverseTransformPoint(b.center);
            var s = go.transform.lossyScale;
            var local = Quaternion.Inverse(go.transform.rotation);
            Vector3 ls = local * b.size; ls = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
            bc.size = new Vector3(ls.x / Mathf.Max(1e-4f, s.x), ls.y / Mathf.Max(1e-4f, s.y), ls.z / Mathf.Max(1e-4f, s.z));
        }

        static void AddTrunk(GameObject go)
        {
            var b = RendererBounds(go);
            var cap = go.AddComponent<CapsuleCollider>();
            float s = go.transform.lossyScale.y;
            cap.radius = Mathf.Max(0.25f, b.size.x * 0.08f) / s;
            cap.height = b.size.y * 0.6f / s;
            cap.center = go.transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + b.size.y * 0.3f, b.center.z));
        }

        /// <summary>Primitive cube with a palette material (collider kept).</summary>
        static GameObject Block(string name, Vector3 centre, Vector3 size, Material m, Transform parent, float yaw = 0f, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        /// <summary>Invisible static box collider (local box on a building root or world box when parent is null-rotated).</summary>
        static GameObject Solid(string name, Transform parent, Vector3 centre, Vector3 size, Quaternion rot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = rot;
            go.AddComponent<BoxCollider>().size = size;
            go.isStatic = true;
            return go;
        }

        static GameObject MeshObject(string name, Mesh mesh, Material m, Transform parent, Vector3 pos, Quaternion rot, bool collider = true, bool convex = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, rot);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (collider) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = convex; }
            go.isStatic = true;
            return go;
        }

        /// <summary>Bakes a building and registers it for the night window swap.</summary>
        static GameObject BakeBuilding(WorldMeshBuilder b, string asset, string name, Transform parent, Vector3 pos, float yaw)
        {
            var go = b.Bake(asset, name, parent, pos, Quaternion.Euler(0f, yaw, 0f));
            var r = go.GetComponent<MeshRenderer>();
            foreach (var m in r.sharedMaterials) if (m == Mats.GlassDay || m == Mats.LampDay) { WindowRenderers.Add(r); break; }
            return go;
        }

        /// <summary>A fence run from a to b using a Kenney fence module (scaled to ~1.1 m high), each segment boxed.</summary>
        static void FenceLine(Vector3 a, Vector3 b, Transform parent, string pack = NF, string model = "fence_simple", float height = 1.1f, bool colliders = true)
        {
            var src = WorldAssets.Model(pack, model);
            if (src == null) return;
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(src);
            var pb = RendererBounds(probe);
            float scale = height / Mathf.Max(0.01f, pb.size.y);
            float segLen = Mathf.Max(pb.size.x, pb.size.z) * scale;
            bool alongX = pb.size.x >= pb.size.z;
            Object.DestroyImmediate(probe);
            Vector3 dir = b - a; dir.y = 0f;
            float len = dir.magnitude;
            int n = Mathf.Max(1, Mathf.RoundToInt(len / segLen));
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg + (alongX ? -90f : 0f);
            float stretch = len / (n * segLen);
            for (int i = 0; i < n; i++)
            {
                Vector3 c = a + dir * ((i + 0.5f) / n);
                var go = Place(pack, model, c, yaw, height, parent);
                if (go == null) return;
                var s = go.transform.localScale;
                go.transform.localScale = alongX ? new Vector3(s.x * stretch, s.y, s.z) : new Vector3(s.x, s.y, s.z * stretch);
                if (colliders) AddBox(go);
                go.layer = 0;
            }
        }

        /// <summary>Rectangle fence (centre, size) with gaps: list of (side 0=S,1=E,2=N,3=W, offset along side, gap width).</summary>
        static void FenceRect(Vector3 c, float sx, float sz, Transform parent, params (int side, float at, float width)[] gaps)
        {
            Vector3[] corners = { c + new Vector3(-sx / 2, 0, -sz / 2), c + new Vector3(sx / 2, 0, -sz / 2), c + new Vector3(sx / 2, 0, sz / 2), c + new Vector3(-sx / 2, 0, sz / 2) };
            for (int s = 0; s < 4; s++)
            {
                Vector3 a = corners[s], b = corners[(s + 1) % 4];
                float L = (b - a).magnitude; Vector3 d = (b - a) / L;
                var cuts = new List<(float, float)>();
                foreach (var g in gaps) if (g.side == s) cuts.Add((g.at - g.width / 2, g.at + g.width / 2));
                cuts.Sort((p, q) => p.Item1.CompareTo(q.Item1));
                float cur = 0f;
                foreach (var cut in cuts)
                {
                    if (cut.Item1 > cur + 0.5f) FenceLine(a + d * cur, a + d * cut.Item1, parent);
                    cur = cut.Item2;
                }
                if (L > cur + 0.5f) FenceLine(a + d * cur, b, parent);
            }
        }

        static void Mud(string name, Vector3 centre, Vector3 size, Transform parent, bool wetSurface = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(centre.x, GroundY(centre.x, centre.z) + size.y / 2f - 0.2f, centre.z);
            var bc = go.AddComponent<BoxCollider>(); bc.size = size; bc.isTrigger = true;
            go.layer = LayerMask.NameToLayer(GameLayers.Interactable);
            go.AddComponent<MudZone>();
            if (!wetSurface) return; // splat-painted mud only
            // glossy wet mud (flat disc) so the wallow reads from far away (art bible 11.6.14: mud sheen tell)
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.name = "Mud Surface";
            disc.transform.SetParent(go.transform, false);
            disc.transform.localPosition = new Vector3(0f, -size.y / 2f + 0.2f + 0.02f, 0f);
            disc.transform.localScale = new Vector3(size.x * 0.7f, 0.005f, size.z * 0.7f);
            disc.GetComponent<Renderer>().sharedMaterial = WorldAssets.Mat("MudWet", WorldAssets.Hex("5E4028"), 0.55f);
        }

        // ------------------------------------------------------------------ edges, markers, cameras, navmesh

        static void WorldEdges()
        {
            var g = Group("World Edges");
            float x0 = PlayX0, x1 = PlayX1, z0 = PlayZ0, z1 = PlayZ1, h = 70f;
            void Wall(string n, Vector3 c, Vector3 s) { var go = new GameObject(n); go.transform.SetParent(g, false); go.transform.position = c; go.AddComponent<BoxCollider>().size = s; go.isStatic = true; }
            Wall("Edge_W", new Vector3(x0 - 1f, h / 2f - 12f, (z0 + z1) / 2f), new Vector3(2f, h, z1 - z0 + 4f));
            Wall("Edge_E", new Vector3(x1 + 1f, h / 2f - 12f, (z0 + z1) / 2f), new Vector3(2f, h, z1 - z0 + 4f));
            Wall("Edge_S", new Vector3((x0 + x1) / 2f, h / 2f - 12f, z0 - 1f), new Vector3(x1 - x0 + 4f, h, 2f));
            Wall("Edge_N", new Vector3((x0 + x1) / 2f, h / 2f - 12f, z1 + 1f), new Vector3(x1 - x0 + 4f, h, 2f));
        }

        static GameObject Marker(string name, Vector3 pos, Transform parent, bool prefix = true)
        {
            var go = new GameObject(prefix ? "HP_" + name : name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(pos.x, GroundY(pos.x, pos.z) + pos.y, pos.z);
            return go;
        }

        static void AddMarker(GameObject go, MarkerRole role, string key = null, int count = 1, int index = 0)
        {
            var m = go.AddComponent<LevelMarker>();
            m.Configure(role, key, count, index);
            go.tag = MarkerTag.ForRole(role);
        }

        static void Markers()
        {
            var g = Group("Markers");
            for (int i = 0; i < 4; i++)
            {
                var ps = Marker($"PlayerStart_{i}", new Vector3(-11f, 0.05f, -3f + i * 2f), g);
                ps.transform.rotation = Quaternion.Euler(0f, 90f, 0f); // face east down the road at the bridge
                AddMarker(ps, MarkerRole.PlayerStart, null, 1, i);
            }
            var barn = Marker("BarnRespawn", new Vector3(-14.5f, 0.05f, 0f), g);
            barn.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            AddMarker(barn, MarkerRole.BarnRespawn);
            AddMarker(Marker("CritterSpawner_Pig", PigPen + new Vector3(3f, 0.1f, -1f), g), MarkerRole.CritterSpawner, "Critter.Pig", 3);
            AddMarker(Marker("CritterSpawner_Chicken", Coop + new Vector3(2f, 0.1f, -2f), g), MarkerRole.CritterSpawner, "Critter.Chicken", 8);
            // Livestock (feature/cow-goat, feature/sheep-cursed-crops): same names and spots as HarvestPanic.AI.Editor.
            // LivestockSceneHook.Markers, which finds them by name (no "HP_" prefix) and so never adds a duplicate.
            AddMarker(Marker("CritterSpawner_Cow", PigPen + new Vector3(-14f, 0.1f, -12f), g, prefix: false), MarkerRole.CritterSpawner, "Critter.Cow", 1);
            AddMarker(Marker("CritterSpawner_Goat", PigPen + new Vector3(-11f, 0.1f, -4f), g, prefix: false), MarkerRole.CritterSpawner, "Critter.Goat", 1);
            AddMarker(Marker("CritterSpawner_Sheep", new Vector3(-6f, 0.1f, 20f), g, prefix: false), MarkerRole.CritterSpawner, "Critter.Sheep", 5);
            AddMarker(Marker("Goods_PrizePumpkin", Pumpkins + new Vector3(0, 0.1f, 0), g), MarkerRole.Goods, "Crop.PrizePumpkin");
            for (int i = 0; i < 4; i++)
                AddMarker(Marker($"Goods_Crate_{i}", new Vector3(-12.5f + (i % 2) * 1.4f, 0.1f, 6.5f + (i / 2) * 1.4f), g), MarkerRole.Goods, "Product.Crate", 1, i);
            AddMarker(Marker("RiverPile", Pile + new Vector3(0, 0.3f, 0), g), MarkerRole.RiverPile);
        }

        static void QaCameras()
        {
            var g = Group("QA_Cameras");
            void Cam(string n, Vector3 pos, Vector3 look, float fov = 70f)
            {
                var go = new GameObject(n);
                go.transform.SetParent(g, false);
                go.transform.position = pos;
                go.transform.LookAt(look);
                var c = go.AddComponent<Camera>();
                c.fieldOfView = fov; c.farClipPlane = 1600f; c.nearClipPlane = 0.1f;
                go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
                c.enabled = false;
            }
            Cam("01_Spawn_FirstPerson", new Vector3(-11f, 1.7f, 0f), new Vector3(80f, 0.5f, 0f), 80f);
            Cam("02_Aerial_Overview", new Vector3(-60f, 120f, -230f), new Vector3(30f, 0f, -10f), 60f);
            Cam("03_Bridge", new Vector3(63f, 2.6f, -9f), new Vector3(82f, -1f, 1f), 70f);
            Cam("04_Market_Square", new Vector3(113f, 2.2f, -9f), new Vector3(142f, 3.2f, 8f), 75f);
            Cam("05_PigPen_CornMaze", new Vector3(-2f, 9f, 12f), new Vector3(18f, 0f, 46f), 70f);
            Cam("06_Windmill_Wheat", new Vector3(-58f, 5f, 14f), new Vector3(-88f, 4f, -30f), 70f);
            Cam("07_River_Downstream_Pile", new Vector3(66f, 6f, -88f), new Vector3(54f, -1f, -120f), 70f);
            Cam("08_Barn_From_Road", new Vector3(8f, 3f, 9f), new Vector3(-26f, 4f, 0f), 70f);
            Cam("09_Farmyard_Farmhouse", new Vector3(-24f, 3.2f, 14f), new Vector3(-47f, 3f, 23f), 70f);
            Cam("10_Barn_Interior", new Vector3(-18.6f, 1.7f, -2.2f), new Vector3(-32f, 1.6f, 2.5f), 80f);
            Cam("11_Fields_Veg_Wheat", new Vector3(-50f, 6f, 46f), new Vector3(-85f, 0f, 10f), 70f);
            Cam("12_Arch_From_Bridge", new Vector3(92f, 1.7f, 0f), new Vector3(140f, 4f, 0f), 75f);
            Cam("13_Town_Hall", new Vector3(128f, 2.2f, -10f), new Vector3(168f, 9f, 0f), 70f);
            Cam("14_Forest_Edge_West", new Vector3(-90f, 2.5f, 70f), new Vector3(-130f, 6f, 90f), 75f);
            Cam("15_Market_Overview", new Vector3(96f, 26f, -48f), new Vector3(138f, 0f, 2f), 60f);
            Cam("16_Pasture_Pens", new Vector3(-16f, 3.2f, 12f), new Vector3(14f, 1f, 30f), 70f);
            Cam("17_River_North", new Vector3(80f, 2f, 1.2f), new Vector3(80f, -2f, 120f), 70f);
        }

        /// <summary>One NightWindows component swaps every building's glass/lamp submesh at dusk.</summary>
        static void NightLights()
        {
            var go = new GameObject("Night Windows");
            go.transform.SetParent(_root, false);
            go.AddComponent<NightWindows>().Configure(WindowRenderers.ToArray(), new[] { Mats.GlassDay, Mats.LampDay }, new[] { Mats.GlassNight, Mats.LampNight });
        }

        static void BakeNavMesh()
        {
            var go = new GameObject("NavMesh");
            go.transform.SetParent(_root, false);
            var s = go.AddComponent<NavMeshSurface>();
            // Only the playable area: the outer terrain tiles are scenery and have no colliders anyway.
            s.collectObjects = CollectObjects.Volume;
            s.center = new Vector3((PlayX0 + PlayX1) / 2f, 10f, (PlayZ0 + PlayZ1) / 2f);
            s.size = new Vector3(PlayX1 - PlayX0 + 6f, 60f, PlayZ1 - PlayZ0 + 6f);
            s.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            s.layerMask = ~(1 << LayerMask.NameToLayer(GameLayers.Player) | 1 << LayerMask.NameToLayer(GameLayers.Critter) | 1 << LayerMask.NameToLayer(GameLayers.Carryable) | 1 << LayerMask.NameToLayer(GameLayers.Water) | 1 << LayerMask.NameToLayer(GameLayers.Interactable));
            s.BuildNavMesh();
            string path = WorldAssets.ArtRoot + "/Terrain/NavMesh_Muddlebury.asset";
            AssetDatabase.DeleteAsset(path);
            if (s.navMeshData != null) AssetDatabase.CreateAsset(s.navMeshData, path);
        }

        static void EnsureWorldPrefabList()
        {
            const string p = "Assets/Resources/NetworkPrefabs/World.asset";
            if (AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(p) != null) return;
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            AssetDatabase.CreateAsset(list, p); // empty: the World team's only network object (Bridge) is in-scene placed
        }

        // ------------------------------------------------------------------ LOD culling for small dressing

        static readonly List<(Transform group, float cell, float distance)> CullGroups = new List<(Transform, float, float)>();

        /// <summary>Registers a dressing group whose children get cell LODGroups that cull them beyond <paramref name="distance"/>.</summary>
        static Transform Cullable(Transform group, float cell, float distance) { CullGroups.Add((group, cell, distance)); return group; }

        /// <summary>
        /// Buckets each registered group's children into XZ cells and gives every cell one LODGroup with a single LOD
        /// (culled below the screen height that matches <c>distance</c> at FOV 90, lodBias 1). Small props stop costing
        /// triangles and draws far away; colliders are untouched.
        /// </summary>
        static void ApplyCulling()
        {
            foreach (var (group, cell, distance) in CullGroups)
            {
                if (group == null) continue;
                var kids = new List<Transform>();
                foreach (Transform c in group) kids.Add(c);
                var cells = new Dictionary<Vector2Int, Transform>();
                foreach (var k in kids)
                {
                    var key = new Vector2Int(Mathf.FloorToInt(k.position.x / cell), Mathf.FloorToInt(k.position.z / cell));
                    if (!cells.TryGetValue(key, out var ct))
                    {
                        ct = new GameObject($"Cell_{key.x}_{key.y}").transform;
                        ct.SetParent(group, false);
                        ct.gameObject.isStatic = true;
                        cells[key] = ct;
                    }
                    k.SetParent(ct, true);
                }
                foreach (var ct in cells.Values)
                {
                    var rs = ct.GetComponentsInChildren<Renderer>();
                    if (rs.Length == 0) continue;
                    var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                    var lg = ct.gameObject.AddComponent<LODGroup>();
                    float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
                    float h = Mathf.Clamp(size / (2f * distance), 0.002f, 0.9f);
                    lg.SetLODs(new[] { new LOD(h, rs) });
                    lg.RecalculateBounds();
                }
            }
            CullGroups.Clear();
        }
    }
}
