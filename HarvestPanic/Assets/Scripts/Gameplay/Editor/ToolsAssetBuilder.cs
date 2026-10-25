using System.Collections.Generic;
using System.IO;
using HarvestPanic.Core;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarvestPanic.Gameplay.Editor
{
    /// <summary>
    /// Idempotent generator for the farm-tools and wagon assets (feature/wagon-lasso-whistle): Lasso and Whistle tool
    /// prefabs (Prefabs/Tools), the Wagon, its tuning and the HitchingPost (Prefabs/Vehicles), the PlayerToolbelt on
    /// Player.prefab, and the new entries in Resources/NetworkPrefabs/Player.asset (appended, never cleared).
    /// Batchmode: Unity -batchmode -nographics -quit -projectPath HarvestPanic -executeMethod HarvestPanic.Gameplay.Editor.ToolsAssetBuilder.Build
    /// </summary>
    public static class ToolsAssetBuilder
    {
        const string ToolsDir = "Assets/Prefabs/Tools";
        const string MeshDir = ToolsDir + "/Meshes";
        const string VehDir = "Assets/Prefabs/Vehicles";
        const string MatDir = "Assets/Prefabs/Player/Materials";
        const string ListPath = "Assets/Resources/NetworkPrefabs/Player.asset";

        [MenuItem("Harvest Panic/Player/Build Tools + Wagon")]
        public static void Build()
        {
            foreach (var d in new[] { ToolsDir, MeshDir, VehDir, MatDir }) EnsureFolder(d);
            var tt = LoadOrCreate<ToolTuning>(ToolsDir + "/ToolTuning.asset");
            var wt = LoadOrCreate<WagonTuning>(VehDir + "/WagonTuning.asset");

            var lasso = BuildLasso(tt);
            var whistle = BuildWhistle(tt);
            var wagon = BuildWagon(wt);
            var post = BuildHitchingPost();

            var net = AssetDatabase.LoadAssetAtPath<GameObject>(ToolsDir + "/Net.prefab");
            var bucket = AssetDatabase.LoadAssetAtPath<GameObject>(ToolsDir + "/FeedBucket.prefab");
            AddToolbelt(tt, net, bucket, lasso, whistle);

            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(ListPath);
            if (list != null)
            {
                var have = new HashSet<GameObject>();
                foreach (var p in list.PrefabList) if (p.Prefab != null) have.Add(p.Prefab);
                foreach (var p in new[] { lasso, whistle, wagon, post })
                    if (p != null && !have.Contains(p)) list.Add(new NetworkPrefab { Prefab = p });
                EditorUtility.SetDirty(list);
            }
            else Debug.LogError("[ToolsAssetBuilder] " + ListPath + " missing; run PlayerAssetBuilder.BuildAll first");
            AssetDatabase.SaveAssets();
            Debug.Log("[ToolsAssetBuilder] Done.");
        }

        // ---- Player prefab ---------------------------------------------------------------------------------------

        static void AddToolbelt(ToolTuning tt, GameObject net, GameObject bucket, GameObject lasso, GameObject whistle)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerAssetBuilder.PlayerPrefabPath);
            try
            {
                var belt = root.GetComponent<PlayerToolbelt>();
                if (belt == null) belt = root.AddComponent<PlayerToolbelt>();
                belt.Configure(tt, net, bucket, lasso, whistle);
                EditorUtility.SetDirty(belt);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerAssetBuilder.PlayerPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ---- Tools -----------------------------------------------------------------------------------------------

        static GameObject NetProp(string name, out Transform visual)
        {
            var root = new GameObject(name);
            root.layer = LayerMask.NameToLayer(GameLayers.Carryable);
            var rb = root.AddComponent<Rigidbody>();
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            root.AddComponent<NetworkRigidbody>();
            visual = Child(root.transform, "Visual", Vector3.zero);
            return root;
        }

        static GameObject BuildLasso(ToolTuning tt)
        {
            var root = NetProp("Lasso", out var v);
            var box = root.AddComponent<BoxCollider>(); box.size = new Vector3(0.32f, 0.32f, 0.12f);
            var ropeMat = LitMat("LassoRope", new Color(0.86f, 0.72f, 0.45f), 0.15f);
            var coil = Child(v, "Coil", Vector3.zero);
            var coilMesh = SaveMesh(CoilMesh(), "LassoCoil");
            var cm = new GameObject("CoilMesh");
            cm.layer = root.layer;
            cm.transform.SetParent(coil, false);
            cm.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // ring faces sideways like a coil on the hip
            cm.AddComponent<MeshFilter>().sharedMesh = coilMesh;
            cm.AddComponent<MeshRenderer>().sharedMaterial = ropeMat;
            // A short tail hanging from the coil.
            var tail = Prim(PrimitiveType.Cylinder, "Tail", coil, new Vector3(0f, -0.15f, 0.02f), new Vector3(0.025f, 0.08f, 0.025f), ropeMat);
            tail.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
            var grip = Child(v, "Grip", new Vector3(0f, 0.12f, -0.01f));
            var gripL = Child(v, "GripL", new Vector3(-0.13f, -0.02f, 0.02f));

            var lineMat = LineMat("LassoLine", new Color(0.86f, 0.72f, 0.45f));
            var aimMat = LineMat("LassoAim", Color.white);
            var rope = Line(root.transform, "Rope", lineMat, 0.028f, new Color(0.86f, 0.72f, 0.45f));
            var loop = Line(root.transform, "Loop", lineMat, 0.032f, new Color(0.86f, 0.72f, 0.45f));
            var arc = Line(root.transform, "AimArc", aimMat, 0.02f, new Color(1f, 0.96f, 0.84f, 0.8f));
            var ring = Line(root.transform, "AimRing", aimMat, 0.035f, new Color(1f, 0.96f, 0.84f, 0.8f));

            var tool = root.AddComponent<LassoTool>();
            tool.Configure(1, 1.5f, true);
            tool.Configure(tt, rope, loop, arc, ring, coil, grip, gripL);
            return Save(root, ToolsDir + "/Lasso.prefab");
        }

        static GameObject BuildWhistle(ToolTuning tt)
        {
            var root = NetProp("Whistle", out var v);
            var box = root.AddComponent<BoxCollider>(); box.size = new Vector3(0.1f, 0.08f, 0.14f);
            var brass = LitMat("WhistleBrass", new Color(0.93f, 0.74f, 0.3f), 0.75f, 0.85f);
            var cord = LitMat("WhistleCord", new Color(0.8f, 0.2f, 0.18f), 0.1f);
            var body = Prim(PrimitiveType.Cylinder, "Barrel", v, new Vector3(0f, 0f, 0.01f), new Vector3(0.045f, 0.04f, 0.045f), brass);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Prim(PrimitiveType.Sphere, "Chamber", v, new Vector3(0f, -0.006f, -0.03f), new Vector3(0.058f, 0.05f, 0.05f), brass);
            var mouth = Prim(PrimitiveType.Cube, "Mouthpiece", v, new Vector3(0f, 0.004f, 0.06f), new Vector3(0.03f, 0.016f, 0.04f), brass);
            Prim(PrimitiveType.Sphere, "Pea", v, new Vector3(0f, 0.022f, -0.02f), Vector3.one * 0.012f, cord);
            var ring = Prim(PrimitiveType.Cylinder, "CordRing", v, new Vector3(0f, -0.005f, -0.065f), new Vector3(0.022f, 0.004f, 0.022f), cord);
            ring.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var grip = Child(v, "Grip", new Vector3(0f, -0.03f, -0.03f));
            var tool = root.AddComponent<WhistleTool>();
            tool.Configure(1, 0.15f, true);
            tool.Configure(tt, v, grip);
            return Save(root, ToolsDir + "/Whistle.prefab");
        }

        // ---- Wagon -----------------------------------------------------------------------------------------------

        static GameObject BuildWagon(WagonTuning wt)
        {
            var root = new GameObject("Wagon");
            int layer = LayerMask.NameToLayer(GameLayers.Carryable);
            root.layer = layer;
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = wt.WagonMassKg;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.6f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.automaticCenterOfMass = false;
            rb.centerOfMass = new Vector3(0f, 0.42f, 0f);
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            root.AddComponent<NetworkRigidbody>();

            var wood = LitMat("WagonWood", new Color(0.62f, 0.42f, 0.24f), 0.2f);
            var woodDark = LitMat("WagonWoodDark", new Color(0.45f, 0.29f, 0.16f), 0.2f);
            var red = LitMat("WagonRed", new Color(0.72f, 0.2f, 0.16f), 0.25f);
            var iron = LitMat("WagonIron", new Color(0.24f, 0.24f, 0.26f), 0.5f, 0.6f);
            var wheelMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(VehDir + "/WagonWheel.physicMaterial");
            if (wheelMat == null)
            {
                wheelMat = new PhysicsMaterial("WagonWheel") { dynamicFriction = 0f, staticFriction = 0f, bounciness = 0f, frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
                AssetDatabase.CreateAsset(wheelMat, VehDir + "/WagonWheel.physicMaterial");
            }

            float w = wt.BedWidth, l = wt.BedLength, wall = wt.WallHeight, floorTop = 0.61f, r = 0.32f;
            var v = Child(root.transform, "Visual", Vector3.zero);
            // Bed floor and walls (colliders on the root's compound body).
            Box(root, "Floor", new Vector3(0f, floorTop - 0.04f, 0f), new Vector3(w + 0.1f, 0.08f, l + 0.1f));
            Box(root, "WallL", new Vector3(-(w * 0.5f + 0.03f), floorTop + wall * 0.5f, 0f), new Vector3(0.06f, wall, l + 0.1f));
            Box(root, "WallR", new Vector3(w * 0.5f + 0.03f, floorTop + wall * 0.5f, 0f), new Vector3(0.06f, wall, l + 0.1f));
            Box(root, "WallF", new Vector3(0f, floorTop + wall * 0.5f, l * 0.5f + 0.03f), new Vector3(w + 0.1f, wall, 0.06f));
            Box(root, "WallB", new Vector3(0f, floorTop + wall * 0.5f, -(l * 0.5f + 0.03f)), new Vector3(w + 0.1f, wall, 0.06f));
            // Floor planks + sides (visual).
            for (int i = 0; i < 5; i++)
                Prim(PrimitiveType.Cube, "Plank" + i, v, new Vector3(-w * 0.5f + (i + 0.5f) * w / 5f, floorTop - 0.04f, 0f), new Vector3(w / 5f - 0.015f, 0.07f, l + 0.1f), i % 2 == 0 ? wood : woodDark);
            foreach (float s in new[] { -1f, 1f })
            {
                for (int k = 0; k < 2; k++)
                    Prim(PrimitiveType.Cube, "Side" + k, v, new Vector3(s * (w * 0.5f + 0.03f), floorTop + 0.1f + k * 0.2f, 0f), new Vector3(0.05f, 0.17f, l + 0.1f), k == 0 ? red : wood);
                Prim(PrimitiveType.Cube, "End" + s, v, new Vector3(0f, floorTop + wall * 0.5f, s * (l * 0.5f + 0.03f)), new Vector3(w + 0.1f, wall, 0.05f), red);
                foreach (float z in new[] { -l * 0.5f, 0f, l * 0.5f })
                    Prim(PrimitiveType.Cube, "Stake", v, new Vector3(s * (w * 0.5f + 0.07f), floorTop + wall * 0.5f, z), new Vector3(0.06f, wall + 0.08f, 0.07f), woodDark);
            }
            // Axles and wheels: frictionless spheres roll; the cart applies its own grip and rolling drag.
            var wheelVis = new List<Transform>();
            foreach (float z in new[] { l * 0.35f, -l * 0.35f })
            {
                var axle = Prim(PrimitiveType.Cylinder, "Axle", v, new Vector3(0f, r, z), new Vector3(0.06f, w * 0.5f + 0.22f, 0.06f), iron);
                axle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                foreach (float s in new[] { -1f, 1f })
                {
                    // Sphere colliders sit under the bed (inside the walls) so the walls are the widest part of the
                    // cart (1.38 m) between the 2.25 m bridge-entrance posts; the visual wheels sit outside.
                    var pos = new Vector3(s * (w * 0.5f + 0.16f), r, z);
                    var sc = new GameObject("WheelCollider");
                    sc.layer = layer;
                    sc.transform.SetParent(root.transform, false);
                    sc.transform.localPosition = new Vector3(s * 0.3f, r, z);
                    var sph = sc.AddComponent<SphereCollider>(); sph.radius = r; sph.sharedMaterial = wheelMat;
                    var wv = Child(v, "Wheel", pos);
                    var rim = Prim(PrimitiveType.Cylinder, "Rim", wv, Vector3.zero, new Vector3(r * 2f, 0.05f, r * 2f), woodDark);
                    Prim(PrimitiveType.Cylinder, "Tyre", wv, Vector3.zero, new Vector3(r * 2f + 0.03f, 0.04f, r * 2f + 0.03f), iron);
                    Prim(PrimitiveType.Cylinder, "Hub", wv, Vector3.zero, new Vector3(0.12f, 0.08f, 0.12f), iron);
                    for (int k = 0; k < 3; k++)
                    {
                        var spoke = Prim(PrimitiveType.Cube, "Spoke" + k, wv, Vector3.zero, new Vector3(0.04f, 0.06f, r * 1.8f), wood);
                        spoke.transform.localRotation = Quaternion.Euler(0f, k * 60f, 0f);
                    }
                    wv.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    wheelVis.Add(wv);
                }
            }
            // Front pull handle (shafts + crossbar) and rear push bar; grips are where hands go.
            foreach (float s in new[] { -1f, 1f })
            {
                var shaft = Prim(PrimitiveType.Cube, "Shaft", v, new Vector3(s * 0.26f, floorTop + 0.08f, l * 0.5f + 0.36f), new Vector3(0.06f, 0.06f, 0.78f), woodDark);
                shaft.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);
                Prim(PrimitiveType.Cube, "PushPost", v, new Vector3(s * 0.42f, floorTop + wall + 0.12f, -(l * 0.5f + 0.06f)), new Vector3(0.06f, 0.26f, 0.06f), woodDark);
            }
            var cross = Prim(PrimitiveType.Cylinder, "Crossbar", v, new Vector3(0f, floorTop + 0.15f, l * 0.5f + 0.72f), new Vector3(0.06f, 0.32f, 0.06f), woodDark);
            cross.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var push = Prim(PrimitiveType.Cylinder, "PushBar", v, new Vector3(0f, floorTop + wall + 0.26f, -(l * 0.5f + 0.06f)), new Vector3(0.06f, 0.48f, 0.06f), woodDark);
            push.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Box(root, "HandleFront", new Vector3(0f, floorTop + 0.12f, l * 0.5f + 0.42f), new Vector3(0.7f, 0.14f, 0.74f));
            Box(root, "HandleRear", new Vector3(0f, floorTop + wall + 0.22f, -(l * 0.5f + 0.06f)), new Vector3(1.0f, 0.14f, 0.1f));
            var grips = new[]
            {
                Child(root.transform, "GripFrontL", new Vector3(-0.2f, floorTop + 0.15f, l * 0.5f + 0.72f)),
                Child(root.transform, "GripFrontR", new Vector3(0.2f, floorTop + 0.15f, l * 0.5f + 0.72f)),
                Child(root.transform, "GripRearL", new Vector3(-0.36f, floorTop + wall + 0.26f, -(l * 0.5f + 0.06f))),
                Child(root.transform, "GripRearR", new Vector3(0.36f, floorTop + wall + 0.26f, -(l * 0.5f + 0.06f))),
            };
            var bed = Child(root.transform, "Bed", new Vector3(0f, floorTop, 0f));
            var hitch = Child(root.transform, "Hitch", new Vector3(0f, floorTop + 0.15f, l * 0.5f + 0.8f));
            var tie = hitch.gameObject.AddComponent<LassoTiePoint>();
            tie.Label = "wagon";
            Prim(PrimitiveType.Sphere, "HitchRing", hitch, Vector3.zero, new Vector3(0.1f, 0.1f, 0.03f), iron);

            var cart = root.AddComponent<WagonCart>();
            cart.Configure(wt, bed, grips, wheelVis.ToArray(), r);
            return Save(root, VehDir + "/Wagon.prefab");
        }

        static GameObject BuildHitchingPost()
        {
            var root = new GameObject("HitchingPost");
            root.AddComponent<NetworkObject>();
            root.AddComponent<HitchingPost>();
            var wood = LitMat("PostWood", new Color(0.5f, 0.34f, 0.2f), 0.2f);
            var iron = LitMat("WagonIron", new Color(0.24f, 0.24f, 0.26f), 0.5f, 0.6f);
            var col = root.AddComponent<BoxCollider>(); col.size = new Vector3(0.22f, 1.3f, 0.22f); col.center = new Vector3(0f, 0.65f, 0f);
            var v = Child(root.transform, "Visual", Vector3.zero);
            Prim(PrimitiveType.Cylinder, "Post", v, new Vector3(0f, 0.65f, 0f), new Vector3(0.18f, 0.65f, 0.18f), wood);
            Prim(PrimitiveType.Cylinder, "Cap", v, new Vector3(0f, 1.32f, 0f), new Vector3(0.24f, 0.03f, 0.24f), wood);
            var ring = Prim(PrimitiveType.Cylinder, "Ring", v, new Vector3(0f, 1.0f, 0.12f), new Vector3(0.14f, 0.012f, 0.14f), iron);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var knot = Child(root.transform, "Knot", new Vector3(0f, 1.0f, 0.12f));
            knot.gameObject.AddComponent<LassoTiePoint>().Label = "post";
            return Save(root, VehDir + "/HitchingPost.prefab");
        }

        // ---- Helpers ---------------------------------------------------------------------------------------------

        static void Box(GameObject root, string name, Vector3 centre, Vector3 size)
        {
            var g = new GameObject(name);
            g.layer = root.layer;
            g.transform.SetParent(root.transform, false);
            g.transform.localPosition = centre;
            g.AddComponent<BoxCollider>().size = size;
        }

        static LineRenderer Line(Transform parent, string name, Material m, float width, Color c)
        {
            var g = new GameObject(name);
            g.layer = parent.gameObject.layer;
            g.transform.SetParent(parent, false);
            var lr = g.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.sharedMaterial = m;
            lr.widthMultiplier = width;
            lr.numCapVertices = 2;
            lr.numCornerVertices = 2;
            lr.positionCount = 0;
            lr.startColor = lr.endColor = c;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.textureMode = LineTextureMode.Stretch;
            lr.enabled = false;
            return lr;
        }

        /// <summary>Three loose rope rings, slightly offset: a coiled lasso (radius ~0.14 m).</summary>
        static Mesh CoilMesh()
        {
            var parts = new List<CombineInstance>();
            for (int i = 0; i < 3; i++)
            {
                parts.Add(new CombineInstance
                {
                    mesh = Torus(0.13f + i * 0.008f, 0.014f, 28, 8),
                    transform = Matrix4x4.TRS(new Vector3(0f, -i * 0.006f, i * 0.012f - 0.012f), Quaternion.Euler(90f + i * 6f, i * 10f, 0f), Vector3.one),
                });
            }
            var m = new Mesh { name = "LassoCoil" };
            m.CombineMeshes(parts.ToArray(), true, true);
            m.RecalculateNormals();
            m.RecalculateBounds();
            foreach (var p in parts) Object.DestroyImmediate(p.mesh);
            return m;
        }

        static Mesh Torus(float major, float minor, int seg, int sides)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i / (float)seg * Mathf.PI * 2f;
                Vector3 c = new Vector3(Mathf.Cos(a) * major, 0f, Mathf.Sin(a) * major);
                Vector3 outDir = c.normalized;
                for (int j = 0; j <= sides; j++)
                {
                    float b = j / (float)sides * Mathf.PI * 2f;
                    verts.Add(c + outDir * Mathf.Cos(b) * minor + Vector3.up * Mathf.Sin(b) * minor);
                }
            }
            int row = sides + 1;
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a0 = i * row + j, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    tris.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
                }
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            return m;
        }

        static Mesh SaveMesh(Mesh m, string name)
        {
            string path = MeshDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(m, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(m);
                return existing;
            }
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static Material LitMat(string name, Color c, float smooth = 0.25f, float metal = 0f)
        {
            string path = MatDir + "/M_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Unlit vertex-colour material for LineRenderers (URP Particles/Unlit).</summary>
        static Material LineMat(string name, Color c)
        {
            string path = MatDir + "/M_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else if (sh != null && m.shader != sh) m.shader = sh;
            m.SetColor("_BaseColor", c);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Transform Child(Transform parent, string name, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.gameObject.layer = parent.gameObject.layer;
            return t;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localScale = scale;
            g.layer = parent.gameObject.layer;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static GameObject Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            // Persist the NetworkObject GlobalObjectIdHash (NGO only computes it in OnValidate).
            var no = asset.GetComponent<NetworkObject>();
            if (no != null)
            {
                typeof(NetworkObject).GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.Invoke(no, null);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
            return asset;
        }
    }
}
