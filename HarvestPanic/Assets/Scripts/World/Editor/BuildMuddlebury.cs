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
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace HarvestPanic.World.Editor
{
    /// <summary>
    /// Reproducibly builds Assets/Scenes/Muddlebury.unity (Region 1 farm, design/levels/muddlebury-farm.md).
    /// Unity -batchmode -nographics -projectPath HarvestPanic -executeMethod HarvestPanic.World.Editor.BuildMuddlebury.Build -quit -logFile /abs/build.log
    /// Coordinates: metres, +X east (toward bridge/market), +Z north, river flows -Z. Farm ground at y = 0, water at y = -4.2.
    /// Seeded (no wall-clock randomness), so re-running produces the same scene.
    /// </summary>
    public static class BuildMuddlebury
    {
        public const string ScenePath = "Assets/Scenes/Muddlebury.unity";
        const string TerrainDir = WorldAssets.ArtRoot + "/Terrain";
        const string SettingsDir = WorldAssets.ArtRoot + "/Settings";
        const string NF = "KenneyNature", FT = "KenneyFantasyTown", FD = "KenneyFood", SV = "KenneySurvival";

        // Layout constants (design/levels/muddlebury-farm.md)
        public const float RiverWest = 60f, RiverEast = 100f, RiverCentre = 80f, RiverBedY = -6f, WaterY = -4.2f;
        public const int PlankCount = 20;
        public const float PlankLength = 2f, PlankWidth = 2.4f, PlankThickness = 0.14f, BridgeSag = 0.7f;
        static readonly Vector3 Barn = new Vector3(-26f, 0f, 0f);
        static readonly Vector3 PigPen = new Vector3(16f, 0f, 34f);
        static readonly Vector3 Coop = new Vector3(16f, 0f, -36f);
        static readonly Vector3 Pumpkins = new Vector3(-56f, 0f, -44f);
        static readonly Vector3 Windmill = new Vector3(-86f, 0f, -38f);
        static readonly Vector3 Pile = new Vector3(52f, 0f, -120f);
        static readonly Vector3 SalePen = new Vector3(114f, 0f, 0f);

        // Terrain extents
        const float TX0 = -135f, TZ0 = -155f, TSX = 320f, TSZ = 270f, TH = 24f, TY0 = -12f;

        static Transform _root;
        static Random _rng;

        [MenuItem("Harvest Panic/World/Build Muddlebury")]
        public static void Build()
        {
            try
            {
                _rng = new Random(1977);
                foreach (var d in new[] { WorldAssets.MatDir, WorldAssets.TexDir, WorldAssets.MeshDir, TerrainDir, SettingsDir, "Assets/Prefabs/World", "Assets/Resources/NetworkPrefabs" })
                    Directory.CreateDirectory(d);
                WorldAssets.ConfigureModelImports();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _root = new GameObject("Muddlebury").transform;

                Lighting();
                var terrain = Ground();
                River();
                Bridge();
                BarnArea();
                Pens();
                Fields();
                Landmarks();
                MarketTown();
                Dressing();
                WorldEdges();
                Markers();
                QaCameras();
                BakeNavMesh(terrain);
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
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        // ------------------------------------------------------------------ lighting / post

        static void Lighting()
        {
            var sunGo = new GameObject("Sun (golden afternoon)");
            sunGo.transform.SetParent(_root);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = WorldAssets.Hex("FFE2B8");
            sun.intensity = 1.9f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sunGo.transform.rotation = Quaternion.Euler(36f, 58f, 0f);
            RenderSettings.sun = sun;

            var sky = AssetDatabase.LoadAssetAtPath<Material>(WorldAssets.MatDir + "/M_World_Sky.mat");
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, WorldAssets.MatDir + "/M_World_Sky.mat"); }
            sky.SetColor("_SkyTint", WorldAssets.Hex("B9CFE0"));
            sky.SetColor("_GroundColor", WorldAssets.Hex("A9B88E"));
            sky.SetFloat("_AtmosphereThickness", 0.9f);
            sky.SetFloat("_Exposure", 1.15f);
            sky.SetFloat("_SunSize", 0.025f);
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
            ca.saturation.Override(4f); ca.contrast.Override(12f); ca.postExposure.Override(0.1f);
            ca.colorFilter.Override(WorldAssets.Hex("FFF6E8"));
            var wb = Get<WhiteBalance>(); wb.temperature.Override(6f); wb.tint.Override(4f);
            var bloom = Get<Bloom>(); bloom.intensity.Override(0.35f); bloom.threshold.Override(1.1f); bloom.scatter.Override(0.6f);
            var smh = Get<ShadowsMidtonesHighlights>();
            smh.shadows.Override(new Vector4(0.96f, 0.94f, 1.06f, 0f)); // violet-tinted lifted shadows
            var vig = Get<Vignette>(); vig.intensity.Override(0.2f); vig.smoothness.Override(0.45f);
            var mb = Get<MotionBlur>(); mb.active = false;
            EditorUtility.SetDirty(profile);

            var volGo = new GameObject("Global Volume (art bible)");
            volGo.transform.SetParent(_root);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true; vol.priority = 10; vol.sharedProfile = profile;
        }

        // ------------------------------------------------------------------ terrain

        static float Smooth(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }

        static float DistSeg(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Ground height (world y) at x,z.</summary>
        public static float GroundY(float x, float z)
        {
            float d = Mathf.Abs(x - RiverCentre);
            // river channel: steep banks between 17 and 20.5 m from the centre
            float river = Mathf.Lerp(RiverBedY, 0f, Smooth(16.5f, 20.5f, d));
            // gentle rolling noise on the farm (kept low where things stand), hills ringing the play area
            float n = (Mathf.PerlinNoise(x * 0.025f + 3f, z * 0.025f + 7f) - 0.5f) * 0.6f;
            float outside = Mathf.Max(Mathf.Max(-120f - x, x - 175f), Mathf.Max(z - 102f, -146f - z));
            float hill = Smooth(0f, 24f, outside) * (7f + Mathf.PerlinNoise(x * 0.04f, z * 0.04f) * 9f);
            float flat = 1f - Smooth(0f, 6f, Mathf.Min(Mathf.Min(DistRect(x, z, -42, -12, -6, 12), DistRect(x, z, 40, -8, 120, 8)), DistRect(x, z, 100, -40, 175, 40)));
            float y = n * (1f - flat) + hill;
            return d < 20.5f ? Mathf.Min(river, river + y * Smooth(18f, 20.5f, d)) : y;
        }

        static float DistRect(float x, float z, float x0, float z0, float x1, float z1)
        {
            float dx = Mathf.Max(x0 - x, 0f, x - x1), dz = Mathf.Max(z0 - z, 0f, z - z1);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static Terrain Ground()
        {
            int res = 513;
            var tdPath = TerrainDir + "/TD_Muddlebury.asset";
            AssetDatabase.DeleteAsset(tdPath);
            var td = new TerrainData { heightmapResolution = res };
            td.size = new Vector3(TSX, TH, TSZ);
            td.alphamapResolution = 512;
            AssetDatabase.CreateAsset(td, tdPath); // create first so the splat textures are saved as sub-assets
            var h = new float[res, res];
            for (int j = 0; j < res; j++)
            for (int i = 0; i < res; i++)
            {
                float x = TX0 + i / (float)(res - 1) * TSX, z = TZ0 + j / (float)(res - 1) * TSZ;
                h[j, i] = Mathf.Clamp01((GroundY(x, z) - TY0) / TH);
            }
            td.SetHeights(0, 0, h);

            // Layers: 0 grass, 1 road dirt, 2 tilled soil, 3 mud, 4 cobble, 5 bank sand, 6 meadow (lighter grass)
            var layers = new[]
            {
                Layer("Grass", WorldAssets.NoiseTexture("Grass", WorldAssets.Hex("86A84E"), WorldAssets.Hex("769A46"), WorldAssets.Hex("A9C46A"), 0.03f, seed: 2), 6f),
                Layer("Road", WorldAssets.NoiseTexture("Road", WorldAssets.Hex("C4A272"), WorldAssets.Hex("B08E60"), WorldAssets.Hex("8A6E4A"), 0.04f, seed: 3), 5f),
                Layer("Soil", WorldAssets.NoiseTexture("Soil", WorldAssets.Hex("8E6640"), WorldAssets.Hex("76522F"), WorldAssets.Hex("A87C52"), 0.02f, seed: 4, stripes: true), 4f),
                Layer("Mud", WorldAssets.NoiseTexture("Mud", WorldAssets.Hex("6B4A2E"), WorldAssets.Hex("5A3C24"), WorldAssets.Hex("8A6644"), 0.02f, seed: 5), 3f),
                Layer("Cobble", WorldAssets.CobbleTexture("Cobble", WorldAssets.Hex("C9BBA2"), WorldAssets.Hex("8F8270")), 3f),
                Layer("Bank", WorldAssets.NoiseTexture("Bank", WorldAssets.Hex("B49A72"), WorldAssets.Hex("9C8462"), WorldAssets.Hex("D2BE98"), 0.05f, seed: 6), 4f),
                Layer("Meadow", WorldAssets.NoiseTexture("Meadow", WorldAssets.Hex("94AE58"), WorldAssets.Hex("8DB052"), WorldAssets.Hex("E6D88A"), 0.04f, seed: 8), 7f),
            };
            td.terrainLayers = layers;
            int ar = 512;
            td.alphamapResolution = ar;
            var a = new float[ar, ar, layers.Length];
            var w = new float[layers.Length];
            for (int j = 0; j < ar; j++)
            for (int i = 0; i < ar; i++)
            {
                float x = TX0 + (i + 0.5f) / ar * TSX, z = TZ0 + (j + 0.5f) / ar * TSZ;
                Paint(x, z, w);
                float sum = 0f; foreach (var v in w) sum += v;
                for (int k = 0; k < w.Length; k++) a[j, i, k] = w[k] / sum;
            }
            td.SetAlphamaps(0, 0, a);
            foreach (var at in td.alphamapTextures) if (!AssetDatabase.Contains(at)) AssetDatabase.AddObjectToAsset(at, td);
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Ground (Terrain)";
            go.transform.SetParent(_root);
            go.transform.position = new Vector3(TX0, TY0, TZ0);
            go.isStatic = true;
            var t = go.GetComponent<Terrain>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat");
            if (mat != null) t.materialTemplate = mat;
            t.heightmapPixelError = 4f;
            t.basemapDistance = 400f;
            t.drawTreesAndFoliage = false;
            // Distant ground skirt outside the terrain so the horizon reads as land (4 slabs around the terrain rectangle).
            var far = WorldAssets.Mat("DistantGround", WorldAssets.Hex("7E9A55"));
            float big = 1500f, ex0 = TX0, ex1 = TX0 + TSX, ez0 = TZ0, ez1 = TZ0 + TSZ;
            Block("Distant Ground W", new Vector3(ex0 - big / 2f + 1f, 4f, (ez0 + ez1) / 2f), new Vector3(big, 1f, big * 2f), far, _root, collider: false);
            Block("Distant Ground E", new Vector3(ex1 + big / 2f - 1f, 4f, (ez0 + ez1) / 2f), new Vector3(big, 1f, big * 2f), far, _root, collider: false);
            // North/south slabs leave a gap for the river so it flows on past the horizon instead of hitting a wall.
            float rw0 = RiverWest - 3f, rw1 = RiverEast + 3f;
            foreach (var (zc, label) in new[] { (ez1 + big / 2f - 1f, "N"), (ez0 - big / 2f + 1f, "S") })
            {
                Block("Distant Ground " + label + "W", new Vector3((ex0 + rw0) / 2f, 4f, zc), new Vector3(rw0 - ex0, 1f, big), far, _root, collider: false);
                Block("Distant Ground " + label + "E", new Vector3((rw1 + ex1) / 2f, 4f, zc), new Vector3(ex1 - rw1, 1f, big), far, _root, collider: false);
                Block("Distant Riverbed " + label, new Vector3(RiverCentre, RiverBedY, zc), new Vector3(rw1 - rw0, 1f, big), WorldAssets.Mat("Riverbed", WorldAssets.Hex("8C7A5A")), _root, collider: false);
                foreach (float bx in new[] { rw0 - 2f, rw1 + 2f })
                    Block("Distant Bank " + label, new Vector3(bx, -1f, zc), new Vector3(4f, 10f, big), far, _root, collider: false);
            }
            return t;
        }

        static TerrainLayer Layer(string name, Texture2D tex, float tile)
        {
            string p = $"{TerrainDir}/TL_{name}.terrainlayer";
            var l = AssetDatabase.LoadAssetAtPath<TerrainLayer>(p);
            bool create = l == null;
            if (create) l = new TerrainLayer();
            l.diffuseTexture = tex; l.tileSize = new Vector2(tile, tile); l.smoothness = 0f; l.metallic = 0f;
            if (create) AssetDatabase.CreateAsset(l, p); else EditorUtility.SetDirty(l);
            return l;
        }

        static readonly (Vector2 a, Vector2 b, float w)[] Roads =
        {
            (new Vector2(-17, 0), new Vector2(62, 0), 2.6f),     // barn -> bridge, 5 m
            (new Vector2(7, 0), new Vector2(16, 26), 1.7f),      // fork north to the pig pen
            (new Vector2(7, 0), new Vector2(16, -27), 1.7f),     // fork south to the coop
            (new Vector2(-34, 4), new Vector2(-66, 22), 1.6f),   // west to veg rows
            (new Vector2(-34, -4), new Vector2(-56, -34), 1.6f), // to pumpkin patch
            (new Vector2(-56, -34), new Vector2(-82, -34), 1.4f),// to the windmill
            (new Vector2(16, -44), new Vector2(24, -56), 1.4f),  // coop -> orchard
            (new Vector2(16, 42), new Vector2(14, 55), 1.4f),    // pen -> corn maze
            (new Vector2(30, -8), new Vector2(46, -116), 1.3f),  // bank path to the spit-out pile
            (new Vector2(46, -116), new Vector2(54, -120), 1.3f),
            (new Vector2(98, 0), new Vector2(110, 0), 2.6f),     // bridge -> market
        };

        static void Paint(float x, float z, float[] w)
        {
            Array.Clear(w, 0, w.Length);
            var p = new Vector2(x, z);
            float meadow = Mathf.PerlinNoise(x * 0.04f + 11f, z * 0.04f + 5f);
            w[0] = 1f - Smooth(0.45f, 0.65f, meadow);
            w[6] = Smooth(0.45f, 0.65f, meadow);
            float road = 0f;
            foreach (var r in Roads) road = Mathf.Max(road, 1f - Smooth(r.w - 0.6f, r.w + 0.6f, DistSeg(p, r.a, r.b)));
            road = Mathf.Max(road, 1f - Smooth(12f, 15f, (p - new Vector2(Barn.x + 8f, 0f)).magnitude));  // barn yard
            float soil = 0f;
            soil = Mathf.Max(soil, 1f - Smooth(0f, 1.2f, DistRect(x, z, -79, 25, -61, 43)));      // veg rows
            soil = Mathf.Max(soil, (1f - Smooth(0f, 1.5f, DistRect(x, z, -109, -16, -67, 19))) * 0.8f); // wheat
            soil = Mathf.Max(soil, 1f - Smooth(0f, 1.2f, DistRect(x, z, -64, -52, -48, -36)));    // pumpkin patch
            soil = Mathf.Max(soil, (1f - Smooth(0f, 1.5f, DistRect(x, z, -9, 55, 43, 87))) * 0.85f); // corn maze
            float mud = 0f;
            mud = Mathf.Max(mud, 1f - Smooth(0.7f, 1.0f, Ellipse(x, z, PigPen.x - 3f, PigPen.z + 1f, 5.5f, 3.6f)));
            mud = Mathf.Max(mud, 1f - Smooth(0.6f, 1.0f, Ellipse(x, z, 53f, 4.5f, 3.2f, 2.2f)));
            mud = Mathf.Max(mud, 1f - Smooth(0.6f, 1.0f, Ellipse(x, z, -4f, -7f, 2.8f, 2f)));
            float cobble = 1f - Smooth(0f, 1.5f, DistRect(x, z, 103, -32, 168, 32));
            cobble = Mathf.Max(cobble, (1f - Smooth(1.4f, 2.4f, DistSeg(p, new Vector2(100, 0), new Vector2(104, 0)))));
            float bank = 1f - Smooth(19.2f, 22.5f, Mathf.Abs(x - RiverCentre));
            w[1] = road; w[2] = soil; w[3] = mud; w[4] = cobble; w[5] = bank;
            float top = Mathf.Max(Mathf.Max(road, soil), Mathf.Max(Mathf.Max(mud, cobble), bank));
            w[0] *= 1f - top; w[6] *= 1f - top;
            w[0] += 1e-4f;
        }

        static float Ellipse(float x, float z, float cx, float cz, float rx, float rz)
        {
            float dx = (x - cx) / rx, dz = (z - cz) / rz; return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ------------------------------------------------------------------ helpers

        static Transform Group(string name, Transform parent = null)
        {
            var g = new GameObject(name).transform; g.SetParent(parent ?? _root, false); return g;
        }

        static float Rnd(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        static Bounds RendererBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

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
            foreach (Transform c in go.GetComponentsInChildren<Transform>()) c.gameObject.isStatic = true;
            if (collider == "box") AddBox(go);
            else if (collider == "trunk") AddTrunk(go);
            return go;
        }

        // ------------------------------------------------------------------ Tripo (owner-generated) set pieces

        const string TripoDir = "Assets/ThirdParty/Tripo";

        /// <summary>Instantiates a Tripo FBX (real size, +Z front) snapped to the ground, static, with a box collider.</summary>
        static GameObject TripoPlace(string model, Vector3 pos, float yaw, Transform parent, Material mat = null)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{TripoDir}/{model}/{model}.fbx");
            if (src == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.SetPositionAndRotation(new Vector3(pos.x, GroundY(pos.x, pos.z) + pos.y, pos.z), Quaternion.Euler(0f, yaw, 0f));
            if (mat != null) foreach (var r in go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            go.isStatic = true;
            foreach (Transform c in go.GetComponentsInChildren<Transform>()) c.gameObject.isStatic = true;
            AddBox(go);
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
            // The export pivots on the hinge; make sure the leaf runs along +X before swinging it open.
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
            Vector3 size = b.size;
            // axis-aligned approximation in local space
            Vector3 ls = local * size; ls = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
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

        /// <summary>A fence run from a to b using a Kenney fence module (scaled to ~1.1 m high), each segment boxed.</summary>
        static void FenceLine(Vector3 a, Vector3 b, Transform parent, string pack = NF, string model = "fence_simple", float height = 1.1f)
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
                AddBox(go);
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

        // ------------------------------------------------------------------ river + bridge

        static void River()
        {
            var g = Group("River");
            var water = WorldAssets.SaveMesh(WorldAssets.RiverMesh(RiverEast - RiverWest + 6f, TSZ + 1200f, 24, 400), "SM_World_River");
            string wm = WorldAssets.MatDir + "/M_World_Water.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(wm);
            if (mat == null) { mat = new Material(Shader.Find("HarvestPanic/StylizedWater")); AssetDatabase.CreateAsset(mat, wm); }
            var surface = MeshObject("Water Surface", water, mat, g, new Vector3(RiverCentre, WaterY, TZ0 + TSZ / 2f), Quaternion.identity, collider: false);
            surface.isStatic = false;
            var mr = surface.GetComponent<MeshRenderer>(); mr.shadowCastingMode = ShadowCastingMode.Off;

            // River trigger volume on the Water layer (sees Player, Critter, Carryable, Ragdoll).
            var vol = new GameObject("River Volume");
            vol.transform.SetParent(g, false);
            vol.layer = LayerMask.NameToLayer(GameLayers.Water);
            vol.transform.position = new Vector3(RiverCentre, (RiverBedY - 2f + WaterY + 0.6f) / 2f, TZ0 + TSZ / 2f);
            var box = vol.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(RiverEast - RiverWest - 1f, (WaterY + 0.6f) - (RiverBedY - 2f), TSZ);
            var rv = vol.AddComponent<RiverVolume>();
            rv.Configure(WaterY, new[]
            {
                new Vector3(RiverCentre, WaterY, -20f), new Vector3(RiverCentre - 4f, WaterY, -60f),
                new Vector3(RiverCentre - 10f, WaterY, -100f), new Vector3(RiverWest + 3f, WaterY, -118f),
            });
            AddMarker(vol, MarkerRole.River);

            // Stepping-stone bait 65 m downstream (gaps too wide to cross).
            for (int i = 0; i < 6; i++)
            {
                float x = RiverWest + 3f + i * 6.4f;
                Place(NF, i % 2 == 0 ? "rock_largeA" : "rock_largeB", new Vector3(x, WaterY - 0.9f, -65f + Rnd(-1f, 1f)), Rnd(0, 360), 1.6f, g, "box", snap: false);
            }
            // Bank rocks and reeds
            for (int i = 0; i < 70; i++)
            {
                bool west = i % 2 == 0;
                float z = Rnd(TZ0 + 10f, TZ0 + TSZ - 10f);
                if (Mathf.Abs(z) < 6f) continue;
                float x = west ? RiverWest + Rnd(-0.5f, 2.5f) : RiverEast - Rnd(-0.5f, 2.5f);
                Place(NF, i % 3 == 0 ? "rock_smallA" : i % 3 == 1 ? "rock_smallC" : "grass_leafsLarge", new Vector3(x, 0, z), Rnd(0, 360), Rnd(0.5f, 1.3f), g);
            }
            for (int i = 0; i < 10; i++)
                Place(NF, i % 2 == 0 ? "lily_large" : "lily_small", new Vector3(RiverWest + Rnd(1.5f, 4f), WaterY + 0.05f, -100f - i * 3.3f), Rnd(0, 360), 0f, g, snap: false);
        }

        static void Bridge()
        {
            var g = Group("Bridge");
            g.position = Vector3.zero;
            var bridge = g.gameObject;
            bridge.AddComponent<NetworkObject>();
            bridge.AddComponent<BridgeController>();
            bridge.AddComponent<BridgeNetSync>();

            var wood = new[] { WorldAssets.Mat("Plank_A", WorldAssets.Hex("9C7652")), WorldAssets.Mat("Plank_B", WorldAssets.Hex("8C6846")), WorldAssets.Mat("Plank_C", WorldAssets.Hex("A8825C")) };
            var cracked = WorldAssets.Mat("Plank_Cracked", WorldAssets.Hex("6E4A30"));
            var post = WorldAssets.Mat("Post", WorldAssets.Hex("7A5236"));
            var rope = WorldAssets.Mat("Rope", WorldAssets.Hex("C8A870"));
            int bridgeLayer = LayerMask.NameToLayer(GameLayers.Bridge);

            float x0 = RiverWest;
            for (int i = 0; i < PlankCount; i++)
            {
                float t0 = i / (float)PlankCount, t1 = (i + 1) / (float)PlankCount, tm = (t0 + t1) / 2f;
                float y0 = PlankMath.Sag(t0, BridgeSag), y1 = PlankMath.Sag(t1, BridgeSag);
                float pitch = Mathf.Atan2(y1 - y0, PlankLength) * Mathf.Rad2Deg;
                var plank = new GameObject($"BridgePlank_{i:00}");
                plank.transform.SetParent(g, false);
                plank.transform.SetPositionAndRotation(new Vector3(x0 + PlankLength * (i + 0.5f), PlankMath.Sag(tm, BridgeSag) - PlankThickness / 2f, 0f), Quaternion.Euler(0f, 0f, pitch));
                plank.layer = bridgeLayer;
                var bc = plank.AddComponent<BoxCollider>();
                bc.size = new Vector3(PlankLength, PlankThickness, PlankWidth);
                var rb = plank.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
                var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(vis.GetComponent<Collider>());
                vis.name = "Visual";
                vis.transform.SetParent(plank.transform, false);
                vis.transform.localScale = new Vector3(PlankLength - 0.08f, PlankThickness, PlankWidth + Rnd(-0.15f, 0.15f));
                vis.transform.localRotation = Quaternion.Euler(0f, Rnd(-2f, 2f), 0f);
                vis.GetComponent<Renderer>().sharedMaterial = wood[i % wood.Length];
                vis.layer = bridgeLayer;
                var bp = plank.AddComponent<BridgePlank>();
                bp.Configure(i, vis.transform, cracked);
                AddMarker(plank, MarkerRole.BridgePlank, null, 1, i);
            }
            // Under-beams (stringers) are left out on purpose: the planks are the bridge.
            // Posts and rope rails (1 m above the deck).
            foreach (float side in new[] { -1f, 1f })
            {
                float z = side * (PlankWidth / 2f + 0.15f);
                foreach (float x in new[] { RiverWest - 1.2f, RiverEast + 1.2f })
                {
                    var p = Block("Post", new Vector3(x, 0.4f, z), new Vector3(0.45f, 3.2f, 0.45f), post, g);
                    p.transform.position = new Vector3(x, GroundY(x, z) + 0.8f, z);
                    Block("PostCap", p.transform.position + Vector3.up * 1.65f, new Vector3(0.6f, 0.15f, 0.6f), post, g, collider: false);
                }
                int segs = 24;
                for (int s = 0; s < segs; s++)
                {
                    float ta = s / (float)segs, tb = (s + 1) / (float)segs;
                    var pa = new Vector3(Mathf.Lerp(RiverWest - 1.2f, RiverEast + 1.2f, ta), PlankMath.Sag(ta, BridgeSag * 0.85f) + 1.05f, z);
                    var pb = new Vector3(Mathf.Lerp(RiverWest - 1.2f, RiverEast + 1.2f, tb), PlankMath.Sag(tb, BridgeSag * 0.85f) + 1.05f, z);
                    Rope(pa, pb, 0.07f, rope, g);
                }
                for (int i = 0; i <= PlankCount; i += 2)
                {
                    float t = i / (float)PlankCount;
                    float x = RiverWest + i * PlankLength;
                    var top = new Vector3(x, PlankMath.Sag(t, BridgeSag * 0.85f) + 1.05f, z);
                    var bottom = new Vector3(x, PlankMath.Sag(t, BridgeSag), z);
                    Rope(bottom, top, 0.035f, rope, g);
                }
            }
            // Abutment decks so the bridge meets the road cleanly.
            foreach (float x in new[] { RiverWest - 2.5f, RiverEast + 2.5f })
                Block("Abutment", new Vector3(x, -0.07f, 0f), new Vector3(5f, 0.3f, PlankWidth + 0.6f), wood[1], g);
        }

        static void Rope(Vector3 a, Vector3 b, float r, Material m, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "Rope";
            go.transform.SetParent(parent, false);
            go.transform.position = (a + b) / 2f;
            go.transform.up = (b - a).normalized;
            go.transform.localScale = new Vector3(r * 2f, (b - a).magnitude / 2f, r * 2f);
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ barn

        static void BarnArea()
        {
            var g = Group("Barn");
            var red = WorldAssets.Mat("BarnBrick", WorldAssets.Hex("9C4A3A"));
            var trim = WorldAssets.Mat("Trim", WorldAssets.Hex("F3E6CC"));
            var roof = WorldAssets.Mat("BarnRoof", WorldAssets.Hex("5A4A44"));
            var dark = WorldAssets.Mat("Interior", WorldAssets.Hex("3A2A20"));
            var hay = WorldAssets.Mat("Hay", WorldAssets.Hex("E3C46E"));
            float w = 14f, d = 18f, wallH = 6f;
            // gambrel profile in the z-y plane (across the barn); extruded along local Z then rotated so the length runs along X
            var profile = new[]
            {
                new Vector2(-w / 2, 0), new Vector2(w / 2, 0), new Vector2(w / 2, wallH), new Vector2(w / 2 - 2.6f, wallH + 3.2f),
                new Vector2(0, wallH + 4.6f), new Vector2(-w / 2 + 2.6f, wallH + 3.2f), new Vector2(-w / 2, wallH),
            };
            var body = WorldAssets.SaveMesh(WorldAssets.Prism(profile, d), "SM_World_BarnBody");
            var rot = Quaternion.Euler(0f, 90f, 0f); // profile x -> world -z, extrusion z -> world x
            MeshObject("Barn Body", body, red, g, Barn, rot, collider: true, convex: true);
            // Roof shell: slightly larger gambrel, only the four slopes (thin slabs).
            Vector2[] roofPts = { new Vector2(w / 2 + 0.5f, wallH - 0.3f), new Vector2(w / 2 - 2.6f, wallH + 3.35f), new Vector2(0, wallH + 4.85f), new Vector2(-w / 2 + 2.6f, wallH + 3.35f), new Vector2(-w / 2 - 0.5f, wallH - 0.3f) };
            for (int i = 0; i < roofPts.Length - 1; i++)
            {
                Vector2 a = roofPts[i], b = roofPts[i + 1];
                Vector2 mid = (a + b) / 2f; float len = (b - a).magnitude + 0.25f;
                float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                var slab = Block("Roof", Barn + new Vector3(0f, mid.y, -mid.x), new Vector3(d + 1.2f, 0.3f, len), roof, g, collider: false);
                slab.transform.rotation = Quaternion.Euler(-ang, 0f, 0f) * Quaternion.Euler(0f, 0f, 0f);
                slab.transform.rotation = Quaternion.AngleAxis(ang, Vector3.right) * Quaternion.identity;
                slab.transform.rotation = Quaternion.Euler(ang, 0f, 0f);
            }
            // East gable: big doorway, white trim, hay loft door. Faces +X (toward the road and the bridge).
            float fx = Barn.x + d / 2f + 0.03f;
            Block("Doorway", new Vector3(fx, 2.6f, 0f), new Vector3(0.12f, 5.2f, 5.4f), dark, g, collider: false);
            Block("DoorLeafN", new Vector3(fx + 1.3f, 2.6f, 3.9f), new Vector3(2.7f, 5.2f, 0.15f), red, g, 0f);
            Block("DoorLeafS", new Vector3(fx + 1.3f, 2.6f, -3.9f), new Vector3(2.7f, 5.2f, 0.15f), red, g, 0f);
            Block("TrimTop", new Vector3(fx + 0.05f, 5.3f, 0f), new Vector3(0.2f, 0.3f, 6f), trim, g, collider: false);
            Block("TrimN", new Vector3(fx + 0.05f, 2.6f, 2.85f), new Vector3(0.2f, 5.4f, 0.3f), trim, g, collider: false);
            Block("TrimS", new Vector3(fx + 0.05f, 2.6f, -2.85f), new Vector3(0.2f, 5.4f, 0.3f), trim, g, collider: false);
            foreach (float s in new[] { -1f, 1f })
            {
                var x = Block("DoorX", new Vector3(fx + 1.3f, 2.6f, s * 3.98f), new Vector3(0.18f, 6.6f, 0.06f), trim, g, collider: false);
                x.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(0f, 0f, 0f);
                x.transform.rotation = Quaternion.LookRotation(Vector3.forward) * Quaternion.Euler(0f, 0f, 0f);
                x.transform.localScale = new Vector3(0.2f, 6.2f, 0.06f);
                x.transform.rotation = Quaternion.Euler(0f, 0f, 0f) * Quaternion.AngleAxis(25f, Vector3.forward);
                x.transform.position = new Vector3(fx + 1.3f, 2.6f, s * 3.99f);
                x.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                x.transform.rotation = Quaternion.AngleAxis(27f, Vector3.forward);
                var x2 = Object.Instantiate(x, g); x2.name = "DoorX2"; x2.transform.rotation = Quaternion.AngleAxis(-27f, Vector3.forward);
            }
            Block("LoftDoor", new Vector3(fx, 8.0f, 0f), new Vector3(0.12f, 2.2f, 2.4f), dark, g, collider: false);
            Block("LoftTrim", new Vector3(fx + 0.04f, 8.0f, 0f), new Vector3(0.12f, 2.5f, 2.7f), trim, g, collider: false).transform.localScale = new Vector3(0.1f, 2.5f, 2.7f);
            Block("CornerNE", new Vector3(fx, 3f, w / 2f), new Vector3(0.3f, 6f, 0.3f), trim, g, collider: false);
            Block("CornerSE", new Vector3(fx, 3f, -w / 2f), new Vector3(0.3f, 6f, 0.3f), trim, g, collider: false);
            Block("Foundation", Barn + new Vector3(0f, 0.15f, 0f), new Vector3(d + 0.4f, 0.5f, w + 0.4f), WorldAssets.Mat("Stone", WorldAssets.Hex("A89C88")), g, collider: false);
            // Weather vane rooster stand-in + hay loft spill
            Block("Vane", Barn + new Vector3(0f, wallH + 5.6f, 0f), new Vector3(0.08f, 1.4f, 0.08f), post(), g, collider: false);
            for (int i = 0; i < 4; i++)
                Block("HayBale", new Vector3(fx + 3f + (i % 2) * 1.3f, 0.45f + (i / 2) * 0.9f, -6f - (i % 2) * 0.2f), new Vector3(1.2f, 0.9f, 1.8f), hay, g, Rnd(-6, 6));
            Block("HayBale", new Vector3(fx + 2.5f, 0.45f, 6.5f), new Vector3(1.2f, 0.9f, 1.8f), hay, g, 80f);
            // Yard props
            Place(FT, "cart", new Vector3(Barn.x + 16f, 0, 9f), 200f, 1.8f, g, "box");
            Place(SV, "barrel", new Vector3(Barn.x + 10.5f, 0, -8f), 0f, 1.1f, g, "box");
            Place(SV, "barrel", new Vector3(Barn.x + 11.4f, 0, -8.6f), 30f, 1.1f, g, "box");
            Place(SV, "box-large", new Vector3(Barn.x + 10.5f, 0, 8.5f), 10f, 0.9f, g, "box");
            Place(FT, "lantern", new Vector3(fx + 0.5f, 0, 3.6f), 0f, 2.4f, g);
            Place(SV, "signpost", new Vector3(5f, 0, -3.8f), 90f, 2.2f, g, "box");
            // Wardrobe (Customization reads the marker): a wooden cabinet just inside the doors.
            var wardrobe = Block("Wardrobe", new Vector3(fx - 2.2f, 1.3f, 5.3f), new Vector3(1.0f, 2.6f, 1.8f), WorldAssets.Mat("WardrobeWood", WorldAssets.Hex("8A6040")), g);
            Material post() => WorldAssets.Mat("Post", WorldAssets.Hex("7A5236"));
            AddMarker(Marker("Wardrobe", new Vector3(fx - 1.2f, 0f, 5.3f), g), MarkerRole.Wardrobe);
            _ = wardrobe;
        }

        // ------------------------------------------------------------------ pens

        static void Pens()
        {
            var pig = Group("Pig Pen");
            // 18 x 12, south gate (gap), north critter hole behind hay.
            FenceRect(PigPen, 18f, 12f, pig, (0, 9f, 3f), (2, 13f, 1.2f));
            // Owner's Tripo gate hinged on the west edge of the south gap, swung open outward (Kenney gate as fallback).
            if (TripoGate("PenGate_PigPen", PigPen + new Vector3(-1.5f, 0, -6f), 55f, pig) == null)
                Place(NF, "fence_gate", PigPen + new Vector3(-2.2f, 0, -6.4f), 30f, 1.2f, pig);
            var hay = WorldAssets.Mat("Hay", WorldAssets.Hex("E3C46E"));
            Block("HayBale (hole cover)", PigPen + new Vector3(-4f, 0.45f, 7.6f), new Vector3(1.9f, 0.9f, 1.2f), hay, pig);
            Block("Trough", PigPen + new Vector3(6f, 0.3f, 3.5f), new Vector3(2.6f, 0.6f, 0.8f), WorldAssets.Mat("Post", WorldAssets.Hex("7A5236")), pig);
            Mud("Mud Wallow", PigPen + new Vector3(-3f, 0f, 1f), new Vector3(11f, 2f, 7.2f), pig, true);
            var shedWood = WorldAssets.Mat("Post", WorldAssets.Hex("7A5236"));
            foreach (var o in new[] { new Vector3(4.5f, 0, -5f), new Vector3(8f, 0, -5f), new Vector3(4.5f, 0, -1.5f), new Vector3(8f, 0, -1.5f) })
                Block("ShelterPost", PigPen + o + new Vector3(0, 1.1f, 0), new Vector3(0.25f, 2.2f, 0.25f), shedWood, pig);
            var shedRoof = Block("ShelterRoof", PigPen + new Vector3(6.25f, 2.3f, -3.25f), new Vector3(4.4f, 0.18f, 4.4f), WorldAssets.Mat("RoofTeal", WorldAssets.Hex("5E9A8A")), pig, collider: false);
            shedRoof.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            var coop = Group("Coop Yard");
            FenceRect(Coop, 15f, 15f, coop, (2, 4f, 2.2f), (1, 10f, 2.2f), (0, 7.5f, 3f));
            var coopWall = WorldAssets.Mat("CoopWall", WorldAssets.Hex("E9D8B4"));
            var coopRoof = WorldAssets.Mat("RoofTeal", WorldAssets.Hex("5E9A8A"));
            House("Coop", Coop + new Vector3(-3.5f, 0, 3.5f), 3.2f, 4f, 2.2f, 1.6f, coopWall, coopRoof, coop, 0f);
            Block("Ramp", Coop + new Vector3(-1.2f, 0.25f, 3.5f), new Vector3(1.6f, 0.08f, 0.8f), WorldAssets.Mat("Plank_A", WorldAssets.Hex("B5875A")), coop, 0f).transform.rotation = Quaternion.Euler(0, 0, -18f);
            for (int i = 0; i < 6; i++)
                Place(NF, "grass", Coop + new Vector3(Rnd(-6, 6), 0, Rnd(-6, 6)), Rnd(0, 360), 0.4f, coop);
            Place(FD, "bag", Coop + new Vector3(4.5f, 0, 5f), 20f, 0.8f, coop);
            TripoGate("PenGate_Coop", Coop + new Vector3(-1.5f, 0, -7.5f), 55f, coop);
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
            // glossy wet mud decal (flat disc) so the wallow reads from far away
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.name = "Mud Surface";
            disc.transform.SetParent(go.transform, false);
            disc.transform.localPosition = new Vector3(0f, -size.y / 2f + 0.2f + 0.02f, 0f);
            disc.transform.localScale = new Vector3(size.x * 0.7f, 0.005f, size.z * 0.7f);
            disc.GetComponent<Renderer>().sharedMaterial = WorldAssets.Mat("MudWet", WorldAssets.Hex("5E4028"), 0.55f);
        }

        /// <summary>Simple storybook house: box body + gable roof prism with overhang, door and windows facing local +X.</summary>
        static void House(string name, Vector3 pos, float lenX, float widZ, float wallH, float roofH, Material wall, Material roofMat, Transform parent, float yaw)
        {
            var h = new GameObject(name).transform;
            h.SetParent(parent, false);
            h.position = new Vector3(pos.x, GroundY(pos.x, pos.z), pos.z);
            h.rotation = Quaternion.Euler(0f, yaw, 0f);
            var body = Block("Body", Vector3.zero, new Vector3(lenX, wallH, widZ), wall, h);
            body.transform.localPosition = new Vector3(0, wallH / 2f, 0); body.transform.localRotation = Quaternion.identity;
            var roofMesh = WorldAssets.SaveMesh(WorldAssets.Prism(new[] { new Vector2(-widZ / 2 - 0.4f, 0), new Vector2(widZ / 2 + 0.4f, 0), new Vector2(0, roofH) }, lenX + 0.6f), $"SM_World_Roof_{lenX:0.0}x{widZ:0.0}x{roofH:0.0}");
            var roof = MeshObject("Roof", roofMesh, roofMat, h, Vector3.zero, Quaternion.identity, collider: false);
            roof.transform.localPosition = new Vector3(0, wallH - 0.05f, 0); roof.transform.localRotation = Quaternion.Euler(0, 90, 0);
            var trim = WorldAssets.Mat("Trim", WorldAssets.Hex("F3E6CC"));
            var glass = WorldAssets.Mat("Window", WorldAssets.Hex("4A6A82"), 0.6f);
            var door = Block("Door", Vector3.zero, new Vector3(0.1f, Mathf.Min(2.1f, wallH * 0.75f), Mathf.Min(1.1f, widZ * 0.3f)), WorldAssets.Mat("DoorWood", WorldAssets.Hex("7A5236")), h, collider: false);
            door.transform.localPosition = new Vector3(lenX / 2f + 0.03f, door.transform.localScale.y / 2f, 0); door.transform.localRotation = Quaternion.identity;
            if (wallH > 2.6f)
                foreach (float s in new[] { -1f, 1f })
                {
                    var wdw = Block("Window", Vector3.zero, new Vector3(0.1f, 0.9f, 0.8f), glass, h, collider: false);
                    wdw.transform.localPosition = new Vector3(lenX / 2f + 0.03f, wallH * 0.62f, s * widZ * 0.3f); wdw.transform.localRotation = Quaternion.identity;
                    var fr = Block("WindowTrim", Vector3.zero, new Vector3(0.06f, 1.1f, 1.0f), trim, h, collider: false);
                    fr.transform.localPosition = new Vector3(lenX / 2f + 0.01f, wallH * 0.62f, s * widZ * 0.3f); fr.transform.localRotation = Quaternion.identity;
                }
        }

        // ------------------------------------------------------------------ fields

        static void Fields()
        {
            CornMaze();
            var veg = Group("Veg Rows");
            string[] crops = { "crop_carrot", "crop_turnip", "crops_leafsStageB", "crop_melon" };
            for (int r = 0; r < 6; r++)
            {
                float x = -77f + r * 3f;
                for (float z = 27f; z <= 41f; z += 1.6f)
                    Place(NF, crops[r % crops.Length], new Vector3(x, 0, z + Rnd(-0.2f, 0.2f)), Rnd(0, 360), r % 4 == 3 ? 0.55f : 0.45f, veg);
            }
            FenceRect(new Vector3(-70f, 0, 34f), 18f, 18f, veg, (1, 9f, 3f), (0, 4f, 2.5f));

            var wheat = Group("Wheat Field");
            for (float x = -107f; x <= -69f; x += 1.25f)
            for (float z = -14f; z <= 17f; z += 1.25f)
            {
                if (Mathf.Abs(z - 1.5f) < 1.2f && x > -100f) continue; // a trampled path through the middle
                Place(NF, ((int)(x * 7 + z * 3) & 1) == 0 ? "crops_wheatStageB" : "crops_wheatStageA", new Vector3(x + Rnd(-0.35f, 0.35f), 0, z + Rnd(-0.35f, 0.35f)), Rnd(0, 360), Rnd(1.0f, 1.25f), wheat);
            }

            var patch = Group("Pumpkin Patch");
            FenceRect(Pumpkins, 16f, 16f, patch, (2, 8f, 3f));
            for (float x = -6f; x <= 6f; x += 2.4f)
            for (float z = -6f; z <= 6f; z += 2.4f)
            {
                if (Mathf.Abs(x) < 2.5f && Mathf.Abs(z) < 2.5f) continue; // prize pumpkin spot
                Place(NF, "crop_pumpkin", Pumpkins + new Vector3(x + Rnd(-0.4f, 0.4f), 0, z + Rnd(-0.4f, 0.4f)), Rnd(0, 360), Rnd(0.5f, 0.8f), patch);
            }
            Place(SV, "signpost-single", Pumpkins + new Vector3(-1.5f, 0, -8.8f), 0f, 1.8f, patch);
        }

        static void CornMaze()
        {
            var g = Group("Corn Maze");
            int cw = 20, ch = 12; float cell = 2.5f; float ox = -8f, oz = 56f;
            var visited = new bool[cw, ch];
            var wallE = new bool[cw, ch]; var wallN = new bool[cw, ch];
            for (int i = 0; i < cw; i++) for (int j = 0; j < ch; j++) { wallE[i, j] = true; wallN[i, j] = true; }
            var stack = new Stack<Vector2Int>();
            var mazeRng = new Random(42);
            stack.Push(new Vector2Int(0, 0)); visited[0, 0] = true;
            while (stack.Count > 0)
            {
                var c = stack.Peek();
                var nb = new List<Vector2Int>();
                foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    var n = c + d;
                    if (n.x >= 0 && n.y >= 0 && n.x < cw && n.y < ch && !visited[n.x, n.y]) nb.Add(n);
                }
                if (nb.Count == 0) { stack.Pop(); continue; }
                var next = nb[mazeRng.Next(nb.Count)];
                if (next.x > c.x) wallE[c.x, c.y] = false; else if (next.x < c.x) wallE[next.x, next.y] = false;
                else if (next.y > c.y) wallN[c.x, c.y] = false; else wallN[next.x, next.y] = false;
                visited[next.x, next.y] = true; stack.Push(next);
            }
            // a few loops so it is a hiding ground, not a puzzle
            for (int k = 0; k < 30; k++) { int i = mazeRng.Next(cw - 1), j = mazeRng.Next(ch - 1); if (k % 2 == 0) wallE[i, j] = false; else wallN[i, j] = false; }
            void Wall(Vector3 a, Vector3 b)
            {
                for (float t = 0.125f; t <= 1.0f; t += 0.25f)
                {
                    var p = Vector3.Lerp(a, b, t);
                    Place(NF, ((int)(p.x * 3 + p.z) & 3) == 0 ? "crops_cornStageC" : "crops_cornStageD", p + new Vector3(Rnd(-0.2f, 0.2f), 0, Rnd(-0.2f, 0.2f)), Rnd(0, 360), Rnd(2.3f, 2.7f), g);
                }
            }
            for (int i = 0; i < cw; i++)
            for (int j = 0; j < ch; j++)
            {
                var o = new Vector3(ox + i * cell, 0, oz + j * cell);
                if (wallE[i, j] && i < cw - 1) Wall(o + new Vector3(cell, 0, 0), o + new Vector3(cell, 0, cell));
                if (wallN[i, j] && j < ch - 1) Wall(o + new Vector3(0, 0, cell), o + new Vector3(cell, 0, cell));
            }
            // outer border with entrances on the south (toward the pig pen) and east
            for (int i = 0; i < cw; i++)
            {
                if (i != 9 && i != 10) Wall(new Vector3(ox + i * cell, 0, oz), new Vector3(ox + (i + 1) * cell, 0, oz));
                if (i != 3) Wall(new Vector3(ox + i * cell, 0, oz + ch * cell), new Vector3(ox + (i + 1) * cell, 0, oz + ch * cell));
            }
            for (int j = 0; j < ch; j++)
            {
                Wall(new Vector3(ox, 0, oz + j * cell), new Vector3(ox, 0, oz + (j + 1) * cell));
                if (j != 5) Wall(new Vector3(ox + cw * cell, 0, oz + j * cell), new Vector3(ox + cw * cell, 0, oz + (j + 1) * cell));
            }
            Place(NF, "sign", new Vector3(ox + 9.5f * cell, 0, oz - 1.5f), 180f, 1.6f, g);
        }

        // ------------------------------------------------------------------ landmarks

        static void Landmarks()
        {
            var g = Group("Windmill");
            var wall = WorldAssets.Mat("WindmillWall", WorldAssets.Hex("EFE2C8"));
            var roof = WorldAssets.Mat("WindmillRoof", WorldAssets.Hex("7A5A4A"));
            var stone = WorldAssets.Mat("Stone", WorldAssets.Hex("A89C88"));
            float gy = GroundY(Windmill.x, Windmill.z);
            MeshObject("Tower Base", WorldAssets.SaveMesh(WorldAssets.Frustum(8, 4.4f, 4.2f, 1.2f), "SM_World_WindmillBase"), stone, g, new Vector3(Windmill.x, gy - 0.2f, Windmill.z), Quaternion.identity, true, true);
            MeshObject("Tower", WorldAssets.SaveMesh(WorldAssets.Frustum(8, 4f, 2.7f, 11f), "SM_World_WindmillTower"), wall, g, new Vector3(Windmill.x, gy + 1f, Windmill.z), Quaternion.identity, true, true);
            MeshObject("Cap", WorldAssets.SaveMesh(WorldAssets.Frustum(8, 3.3f, 0f, 3.6f), "SM_World_WindmillCap"), roof, g, new Vector3(Windmill.x, gy + 12f, Windmill.z), Quaternion.identity, false);
            var door = Block("Door", new Vector3(Windmill.x + 3.85f, gy + 2.2f, Windmill.z), new Vector3(0.3f, 2.4f, 1.4f), WorldAssets.Mat("DoorWood", WorldAssets.Hex("7A5236")), g, collider: false);
            _ = door;
            var hub = new GameObject("Sails").transform;
            hub.SetParent(g, false);
            hub.position = new Vector3(Windmill.x + 3.3f, gy + 11.2f, Windmill.z);
            hub.rotation = Quaternion.Euler(0f, 90f, 0f);
            var sailWood = WorldAssets.Mat("SailFrame", WorldAssets.Hex("8A6040"));
            var sailCloth = WorldAssets.Mat("SailCloth", WorldAssets.Hex("F6EBD3"));
            for (int i = 0; i < 4; i++)
            {
                var arm = new GameObject("Arm" + i).transform; arm.SetParent(hub, false); arm.localRotation = Quaternion.Euler(0, 0, i * 90f + 15f);
                var spar = Block("Spar", Vector3.zero, new Vector3(0.3f, 9f, 0.25f), sailWood, arm, collider: false);
                spar.transform.localPosition = new Vector3(0, 4.6f, 0); spar.transform.localRotation = Quaternion.identity;
                var cloth = Block("Sail", Vector3.zero, new Vector3(1.9f, 6.6f, 0.08f), sailCloth, arm, collider: false);
                cloth.transform.localPosition = new Vector3(1.1f, 5.4f, 0.1f); cloth.transform.localRotation = Quaternion.identity;
                foreach (Transform t in arm.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;
            }
            var hubBlock = Block("Hub", Vector3.zero, new Vector3(0.9f, 0.9f, 0.9f), sailWood, hub, collider: false);
            hubBlock.transform.localPosition = Vector3.zero; hubBlock.transform.localRotation = Quaternion.identity; hubBlock.isStatic = false;
            hub.gameObject.AddComponent<Spinner>().Configure(Vector3.forward, 22f);
            hub.gameObject.isStatic = false;
            for (int i = 0; i < 3; i++) Place(FD, "bag", Windmill + new Vector3(5.5f, 0, -2f + i * 1.2f), Rnd(0, 360), 0.9f, g);

            var orchard = Group("Orchard");
            for (int i = 0; i < 5; i++)
            for (int j = 0; j < 3; j++)
            {
                var p = new Vector3(3f + i * 11f + Rnd(-1.5f, 1.5f), 0, -78f + j * 10f + Rnd(-1.5f, 1.5f));
                Place(NF, (i + j) % 3 == 0 ? "tree_default" : (i + j) % 3 == 1 ? "tree_oak" : "tree_fat", p, Rnd(0, 360), Rnd(5.5f, 7f), orchard, "trunk");
                if ((i + j) % 2 == 0) Place(FD, "apple", p + new Vector3(Rnd(-1.6f, 1.6f), 0, Rnd(-1.6f, 1.6f)), Rnd(0, 360), 0.25f, orchard);
            }
            Place(FD, "barrel", new Vector3(28f, 0, -54f), 0f, 1.1f, orchard, "box");
            Place(FT, "stall-bench", new Vector3(24f, 0, -54f), 90f, 0.9f, orchard, "box");

            var hedge = Group("Bank Hedge");
            for (float z = -140f; z <= 100f; z += 2.6f)
            {
                if (Mathf.Abs(z) < 7f) continue; // bridge approach
                if (Mathf.Abs(z + 118f) < 7f) continue; // pile path
                if (((int)((z + 300f) / 2.6f)) % 9 == 0) continue; // bush gaps to hide in / slip through
                Place(NF, ((int)z & 1) == 0 ? "plant_bushLarge" : "plant_bushDetailed", new Vector3(55f + Rnd(-0.8f, 0.8f), 0, z + Rnd(-0.5f, 0.5f)), Rnd(0, 360), Rnd(1.6f, 2.2f), hedge, "box");
            }

            var pile = Group("Spit-out Pile");
            Place(SV, "signpost-single", Pile + new Vector3(3f, 0, 3f), -90f, 2.2f, pile, "box");
            var sign = Block("Lost & Found board", Pile + new Vector3(3f, 2.0f, 3f), new Vector3(0.12f, 0.8f, 1.8f), WorldAssets.Mat("SignBoard", WorldAssets.Hex("E8D4A8")), pile, collider: false);
            sign.transform.position = new Vector3(Pile.x + 3f, GroundY(Pile.x + 3f, Pile.z + 3f) + 2.0f, Pile.z + 3f);
            Place(SV, "box", Pile + new Vector3(-2f, 0, 2f), 20f, 0.6f, pile, "box");
            Place(SV, "barrel-open", Pile + new Vector3(-3f, 0, -1f), 0f, 0.9f, pile, "box");
            Place(NF, "log_stack", Pile + new Vector3(1f, 0, -3.5f), 70f, 0.9f, pile, "box");
        }

        // ------------------------------------------------------------------ market town

        static void MarketTown()
        {
            var g = Group("Market Town");
            // Arch at the bridge end of the square
            var stone = WorldAssets.Mat("Stone", WorldAssets.Hex("A89C88"));
            var wood = WorldAssets.Mat("Post", WorldAssets.Hex("7A5236"));
            Block("ArchPillarN", new Vector3(104f, 2.5f, 4.2f), new Vector3(1.1f, 5f, 1.1f), stone, g);
            Block("ArchPillarS", new Vector3(104f, 2.5f, -4.2f), new Vector3(1.1f, 5f, 1.1f), stone, g);
            Block("ArchBeam", new Vector3(104f, 5.3f, 0f), new Vector3(1.2f, 0.7f, 10f), wood, g);
            Block("ArchSign", new Vector3(103.3f, 4.4f, 0f), new Vector3(0.15f, 1.0f, 4.6f), WorldAssets.Mat("SignBoard", WorldAssets.Hex("E8D4A8")), g, collider: false);

            // Sale pen 8 x 8, open toward the bridge (west).
            var pen = Group("Sale Pen", g);
            FenceRect(SalePen, 8f, 8f, pen, (3, 4f, 8f));
            var straw = Block("Straw", SalePen + new Vector3(0, 0.02f, 0), new Vector3(7.6f, 0.04f, 7.6f), WorldAssets.Mat("Straw", WorldAssets.Hex("E6CF8A")), pen, collider: false);
            straw.transform.position = new Vector3(SalePen.x, 0.03f, SalePen.z);
            var penMarker = Marker("MarketPen", SalePen, pen);
            var trig = penMarker.AddComponent<BoxCollider>(); trig.isTrigger = true; trig.size = new Vector3(8f, 3f, 8f); trig.center = new Vector3(0, 1.5f, 0);
            penMarker.layer = LayerMask.NameToLayer(GameLayers.Interactable);
            AddMarker(penMarker, MarkerRole.MarketPen);

            // Three stalls: Butcher, Greengrocer, Oddities (north / east / south edges of the square)
            var stallSpecs = new (string model, Vector3 pos, float yaw, string name, string goods)[]
            {
                ("stall", new Vector3(130f, 0, 22f), 180f, "Stall_Butcher", "meat-ribs"),
                ("stall-green", new Vector3(150f, 0, 0f), -90f, "Stall_Greengrocer", "cabbage"),
                ("stall-red", new Vector3(130f, 0, -22f), 0f, "Stall_Oddities", "pumpkin-basic"),
            };
            foreach (var s in stallSpecs)
            {
                var st = TripoStall(s.name, s.pos, s.yaw, g) ?? Place(FT, s.model, s.pos, s.yaw, 3.2f, g, "box");
                if (st != null) st.name = s.name;
                for (int i = 0; i < 3; i++)
                    Place(FD, s.goods == "meat-ribs" ? (i == 0 ? "barrel" : "bag") : s.goods, s.pos + Quaternion.Euler(0, s.yaw, 0) * new Vector3(-0.8f + i * 0.8f, 0, 1.6f), Rnd(0, 360), 0.45f, g);
            }

            // Town bell, central in the square.
            var bell = Group("Town Bell", g);
            var bp = new Vector3(134f, 0, 0f);
            Block("BellPostL", bp + new Vector3(0, 2.2f, -1.1f), new Vector3(0.35f, 4.4f, 0.35f), wood, bell);
            Block("BellPostR", bp + new Vector3(0, 2.2f, 1.1f), new Vector3(0.35f, 4.4f, 0.35f), wood, bell);
            Block("BellBeam", bp + new Vector3(0, 4.4f, 0), new Vector3(0.4f, 0.4f, 2.8f), wood, bell);
            Block("BellRoof", bp + new Vector3(0, 4.85f, 0), new Vector3(1.6f, 0.2f, 3.2f), WorldAssets.Mat("RoofTeal", WorldAssets.Hex("5E9A8A")), bell, collider: false);
            MeshObject("Bell", WorldAssets.SaveMesh(WorldAssets.Frustum(10, 0.75f, 0.35f, 1.0f), "SM_World_Bell"), WorldAssets.Mat("Bronze", WorldAssets.Hex("A8844E"), 0.45f), bell, bp + new Vector3(0, 3.1f, 0), Quaternion.identity, false);
            Block("BellBase", bp + new Vector3(0, 0.15f, 0), new Vector3(2f, 0.3f, 3.4f), stone, bell);

            // Town buildings ring the square (storybook pastel walls, muted roofs; no reserved red/gold).
            var walls = new[] { WorldAssets.Mat("WallCream", WorldAssets.Hex("EFE0C2")), WorldAssets.Mat("WallSage", WorldAssets.Hex("C9D4AE")), WorldAssets.Mat("WallPeach", WorldAssets.Hex("EBC9A6")), WorldAssets.Mat("WallSky", WorldAssets.Hex("BFD3DA")) };
            var roofs = new[] { WorldAssets.Mat("RoofTeal", WorldAssets.Hex("5E9A8A")), WorldAssets.Mat("RoofPlumMuted", WorldAssets.Hex("7E6A8E")), WorldAssets.Mat("RoofSlate", WorldAssets.Hex("6A7E9A")), WorldAssets.Mat("RoofTerracotta", WorldAssets.Hex("B0745C")) };
            var spots = new (Vector3 p, float yaw, float l, float w, float h)[]
            {
                (new Vector3(116f, 0, 38f), -90f, 8f, 9f, 5f), (new Vector3(134f, 0, 39f), -90f, 10f, 9f, 6f), (new Vector3(152f, 0, 38f), -90f, 8f, 9f, 4.5f),
                (new Vector3(172f, 0, 20f), 180f, 9f, 10f, 5.5f), (new Vector3(173f, 0, -2f), 180f, 8f, 12f, 6.5f), (new Vector3(172f, 0, -22f), 180f, 9f, 9f, 5f),
                (new Vector3(116f, 0, -38f), 90f, 8f, 9f, 4.5f), (new Vector3(134f, 0, -39f), 90f, 10f, 9f, 6f), (new Vector3(152f, 0, -38f), 90f, 8f, 9f, 5f),
            };
            for (int i = 0; i < spots.Length; i++)
            {
                var s = spots[i];
                House($"TownHouse_{i}", s.p, s.l, s.w, s.h, s.h * 0.55f + 1f, walls[i % walls.Length], roofs[(i * 3 + 1) % roofs.Length], g, s.yaw);
            }
            // Square dressing
            Place(FT, "fountain-round", new Vector3(146f, 0, 22f), 0f, 1.2f, g, "box");
            foreach (var lp in new[] { new Vector3(110f, 0, 12f), new Vector3(110f, 0, -12f), new Vector3(158f, 0, 14f), new Vector3(158f, 0, -14f), new Vector3(122f, 0, 0f) })
                Place(FT, "lantern", lp, 0f, 3f, g);
            Place(FT, "cart", new Vector3(160f, 0, -26f), 30f, 1.8f, g, "box");
            Place(SV, "barrel", new Vector3(141f, 0, 18f), 0f, 1.1f, g, "box");
            Place(SV, "box-large", new Vector3(122f, 0, -18f), 15f, 0.9f, g, "box");
            Place(FT, "banner-green", new Vector3(104.6f, 0, 5.2f), 90f, 4.0f, g);
        }

        // ------------------------------------------------------------------ dressing

        static void Dressing()
        {
            var g = Group("Nature Dressing");
            // Forest ring on the surrounding hills (reads as a cosy bowl, hides the world edge).
            string[] trees = { "tree_default", "tree_oak", "tree_fat", "tree_detailed", "tree_tall", "tree_pineRoundA", "tree_pineRoundC", "tree_cone" };
            for (int k = 0; k < 260; k++)
            {
                float x = Rnd(TX0 + 4f, TX0 + TSX - 4f), z = Rnd(TZ0 + 4f, TZ0 + TSZ - 4f);
                float outside = Mathf.Max(Mathf.Max(-121f - x, x - 176f), Mathf.Max(z - 103f, -147f - z));
                if (outside < 0f) continue;
                if (Mathf.Abs(x - RiverCentre) < 22f) continue;
                Place(NF, trees[_rng.Next(trees.Length)], new Vector3(x, 0, z), Rnd(0, 360), Rnd(7f, 12f), g, "trunk");
            }
            // Scattered farm trees, bushes, flowers, rocks inside the farm (keep clear of roads/fields).
            for (int k = 0; k < 260; k++)
            {
                float x = Rnd(-118f, 52f), z = Rnd(-140f, 100f);
                if (Blocked(x, z)) continue;
                int kind = _rng.Next(10);
                if (kind < 1) Place(NF, k % 2 == 0 ? "tree_default" : "tree_oak", new Vector3(x, 0, z), Rnd(0, 360), Rnd(6f, 9f), g, "trunk");
                else if (kind < 4) Place(NF, k % 2 == 0 ? "plant_bush" : "plant_bushSmall", new Vector3(x, 0, z), Rnd(0, 360), Rnd(0.8f, 1.4f), g);
                else if (kind < 8) Place(NF, new[] { "flower_yellowA", "flower_purpleA", "flower_yellowB", "flower_purpleB", "flower_redA" }[_rng.Next(5)], new Vector3(x, 0, z), Rnd(0, 360), Rnd(0.35f, 0.55f), g);
                else if (kind < 9) Place(NF, "rock_smallB", new Vector3(x, 0, z), Rnd(0, 360), Rnd(0.4f, 0.9f), g);
                else Place(NF, "grass_large", new Vector3(x, 0, z), Rnd(0, 360), Rnd(0.4f, 0.7f), g);
            }
            // Market side greenery behind the houses
            for (int k = 0; k < 40; k++)
            {
                float x = Rnd(104f, 176f), z = k % 2 == 0 ? Rnd(46f, 100f) : Rnd(-100f, -46f);
                Place(NF, trees[_rng.Next(trees.Length)], new Vector3(x, 0, z), Rnd(0, 360), Rnd(6f, 10f), g, "trunk");
            }
            // Flower beds along the main road
            for (float x = -8f; x < 52f; x += 3.2f)
                foreach (float s in new[] { -1f, 1f })
                    if (_rng.NextDouble() < 0.55) Place(NF, _rng.NextDouble() < 0.5 ? "flower_yellowC" : "flower_purpleA", new Vector3(x + Rnd(-0.8f, 0.8f), 0, s * Rnd(3.6f, 4.6f)), Rnd(0, 360), 0.45f, g);
            // Tree groves and roadside cover so the spawn view has depth and hiding spots.
            foreach (var grove in new[] { new Vector2(-8f, 24f), new Vector2(-6f, -24f), new Vector2(36f, 16f), new Vector2(38f, -20f), new Vector2(-40f, 30f), new Vector2(-38f, -26f) })
                for (int k = 0; k < 6; k++)
                {
                    var p = new Vector3(grove.x + Rnd(-6f, 6f), 0, grove.y + Rnd(-5f, 5f));
                    if (k < 3) Place(NF, k % 2 == 0 ? "tree_oak" : "tree_default", p, Rnd(0, 360), Rnd(6f, 8.5f), g, "trunk");
                    else Place(NF, "plant_bushLarge", p, Rnd(0, 360), Rnd(1.2f, 1.8f), g, "box");
                }
            var hayMat = WorldAssets.Mat("Hay", WorldAssets.Hex("E3C46E"));
            foreach (var hp in new[] { new Vector3(14f, 0, 5.5f), new Vector3(26f, 0, -5.8f), new Vector3(-2f, 0, 6f), new Vector3(40f, 0, 6.2f) })
                Block("HayBale", hp + new Vector3(0, 0.45f, 0), new Vector3(1.2f, 0.9f, 1.8f), hayMat, g, Rnd(-20, 20));
            FenceLine(new Vector3(-6f, 0, 3.8f), new Vector3(6f, 0, 3.8f), g);
            FenceLine(new Vector3(9f, 0, -3.8f), new Vector3(24f, 0, -3.8f), g);
            // Fence the road edge near the bridge so the chokepoint reads.
            FenceLine(new Vector3(44f, 0, 3.6f), new Vector3(57.5f, 0, 3.4f), g);
            FenceLine(new Vector3(44f, 0, -3.6f), new Vector3(57.5f, 0, -3.4f), g);
            Mud("Mud Patch (bridge approach)", new Vector3(53f, 0, 4.5f), new Vector3(6.4f, 2f, 4.4f), g);
            Mud("Mud Patch (fork)", new Vector3(-4f, 0, -7f), new Vector3(5.6f, 2f, 4f), g);
        }

        static bool Blocked(float x, float z)
        {
            var p = new Vector2(x, z);
            foreach (var r in Roads) if (DistSeg(p, r.a, r.b) < r.w + 2f) return true;
            if (DistRect(x, z, -46, -14, -4, 14) < 2f) return true;            // barn + yard
            if (DistRect(x, z, 5, 26, 27, 42) < 2f) return true;              // pig pen
            if (DistRect(x, z, 7, -45, 25, -27) < 2f) return true;            // coop
            if (DistRect(x, z, -10, 54, 44, 88) < 1f) return true;            // corn maze
            if (DistRect(x, z, -80, 24, -60, 44) < 1f) return true;           // veg
            if (DistRect(x, z, -110, -17, -66, 20) < 1f) return true;         // wheat
            if (DistRect(x, z, -66, -54, -46, -34) < 1f) return true;         // pumpkins
            if ((p - new Vector2(Windmill.x, Windmill.z)).magnitude < 8f) return true;
            if (DistRect(x, z, 0, -82, 52, -54) < 1f) return true;            // orchard
            if (x > 50f) return true;                                          // bank hedge strip
            if ((p - new Vector2(Pile.x, Pile.z)).magnitude < 8f) return true;
            return false;
        }

        // ------------------------------------------------------------------ edges, markers, cameras, navmesh

        static void WorldEdges()
        {
            var g = Group("World Edges");
            float x0 = -122f, x1 = 177f, z0 = -148f, z1 = 104f, h = 40f;
            void Wall(string n, Vector3 c, Vector3 s) { var go = new GameObject(n); go.transform.SetParent(g, false); go.transform.position = c; go.AddComponent<BoxCollider>().size = s; go.isStatic = true; }
            Wall("Edge_W", new Vector3(x0 - 1f, h / 2f - 10f, (z0 + z1) / 2f), new Vector3(2f, h, z1 - z0 + 4f));
            Wall("Edge_E", new Vector3(x1 + 1f, h / 2f - 10f, (z0 + z1) / 2f), new Vector3(2f, h, z1 - z0 + 4f));
            Wall("Edge_S", new Vector3((x0 + x1) / 2f, h / 2f - 10f, z0 - 1f), new Vector3(x1 - x0 + 4f, h, 2f));
            Wall("Edge_N", new Vector3((x0 + x1) / 2f, h / 2f - 10f, z1 + 1f), new Vector3(x1 - x0 + 4f, h, 2f));
        }

        static GameObject Marker(string name, Vector3 pos, Transform parent)
        {
            var go = new GameObject("HP_" + name);
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
                c.fieldOfView = fov; c.farClipPlane = 900f; c.nearClipPlane = 0.1f;
                go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
                c.enabled = false;
            }
            Cam("01_Spawn_FirstPerson", new Vector3(-11f, 1.7f, 0f), new Vector3(80f, 0f, 0f), 80f);
            Cam("02_Aerial_Overview", new Vector3(-20f, 95f, -165f), new Vector3(20f, 0f, -10f), 60f);
            Cam("03_Bridge", new Vector3(63f, 2.6f, -9f), new Vector3(82f, -1f, 1f), 70f);
            Cam("04_Market_Square", new Vector3(103f, 3.2f, -14f), new Vector3(135f, 1.5f, 6f), 75f);
            Cam("05_PigPen_CornMaze", new Vector3(-2f, 9f, 12f), new Vector3(18f, 0f, 46f), 70f);
            Cam("06_Windmill_Wheat", new Vector3(-58f, 5f, 14f), new Vector3(-88f, 4f, -30f), 70f);
            Cam("07_River_Downstream_Pile", new Vector3(66f, 6f, -88f), new Vector3(54f, -1f, -120f), 70f);
            Cam("08_Barn_From_Road", new Vector3(8f, 3f, 9f), new Vector3(-26f, 4f, 0f), 70f);
        }

        static void BakeNavMesh(Terrain terrain)
        {
            var go = new GameObject("NavMesh");
            go.transform.SetParent(_root, false);
            var s = go.AddComponent<NavMeshSurface>();
            s.collectObjects = CollectObjects.All;
            s.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            s.layerMask = ~(1 << LayerMask.NameToLayer(GameLayers.Player) | 1 << LayerMask.NameToLayer(GameLayers.Critter) | 1 << LayerMask.NameToLayer(GameLayers.Carryable) | 1 << LayerMask.NameToLayer(GameLayers.Water) | 1 << LayerMask.NameToLayer(GameLayers.Interactable));
            s.BuildNavMesh();
            string path = WorldAssets.ArtRoot + "/Terrain/NavMesh_Muddlebury.asset";
            AssetDatabase.DeleteAsset(path);
            if (s.navMeshData != null) AssetDatabase.CreateAsset(s.navMeshData, path);
            _ = terrain;
        }

        static void EnsureWorldPrefabList()
        {
            const string p = "Assets/Resources/NetworkPrefabs/World.asset";
            if (AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(p) != null) return;
            var list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            AssetDatabase.CreateAsset(list, p); // empty: the World team's only network object (Bridge) is in-scene placed
        }
    }
}
