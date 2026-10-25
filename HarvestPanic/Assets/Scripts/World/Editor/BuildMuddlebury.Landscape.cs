using System;
using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HarvestPanic.World.Editor
{
    public static partial class BuildMuddlebury
    {
        // Centre terrain tile = the playable farm + town (unchanged extents), with a 3 x 3 grid of scenery tiles around it.
        const float TX0 = -135f, TZ0 = -155f, TSX = 320f, TSZ = 270f, TY0 = -12f, CentreTH = 40f, OuterTH = 110f;
        const int CentreRes = 513, OuterRes = 129; // 4:1 so the outer tiles' border samples land on every 4th centre sample
        static readonly Vector2 WorldCentre = new Vector2(25f, -20f);

        // ------------------------------------------------------------------ height function

        /// <summary>River centreline x at z: straight (x 80) through the playable area, bending away beyond both edges.</summary>
        public static float RiverCentreAt(float z)
        {
            if (z > 100f) return RiverCentre - 95f * Smooth(100f, 400f, z);
            if (z < -144f) return RiverCentre + 95f * Smooth(-144f, -444f, z);
            return RiverCentre;
        }

        /// <summary>Irregular bank line (metres added to the distance from the centreline), zero at the bridge.</summary>
        static float BankWobble(float z, float side) =>
            (Mathf.PerlinNoise(z * 0.045f + (side > 0f ? 31.7f : 3.3f), 4.2f) - 0.5f) * 2.6f * Smooth(8f, 22f, Mathf.Abs(z));

        /// <summary>Signed distance outside the playable rectangle (negative inside).</summary>
        public static float Outside(float x, float z) => Mathf.Max(Mathf.Max(PlayX0 - x, x - PlayX1), Mathf.Max(z - PlayZ1, PlayZ0 - z));

        /// <summary>1 where built things need level ground (yards, pens, roads to the bridge, the whole town side).</summary>
        static float FlatMask(float x, float z)
        {
            float d = DistRect(x, z, -46, -14, -4, 14);                       // barn + yard
            d = Mathf.Min(d, DistRect(x, z, -20, -8, 62, 8));                  // main road to the bridge
            d = Mathf.Min(d, DistRect(x, z, 100, -160, 190, 120));             // town side of the river
            d = Mathf.Min(d, DistRect(x, z, -60, 11, -34, 34));                // farmhouse + garden
            d = Mathf.Min(d, DistRect(x, z, 5, 26, 27, 42));                   // pig pen
            d = Mathf.Min(d, DistRect(x, z, 6, -46, 26, -26));                 // coop
            d = Mathf.Min(d, DistRect(x, z, -66, -54, -46, -34));              // pumpkin patch
            d = Mathf.Min(d, DistRect(x, z, -81, 23, -59, 45));                // veg rows
            d = Mathf.Min(d, DistRect(x, z, -10, 54, 44, 88));                 // corn maze
            d = Mathf.Min(d, DistRect(x, z, -96, -48, -76, -28));              // windmill
            d = Mathf.Min(d, DistRect(x, z, 38, -130, 60, -108));              // Lost & Found
            return 1f - Smooth(0f, 10f, d);
        }

        /// <summary>Ground height (world y) at x,z. Shared by the terrain, every placement and the EditMode layout tests.</summary>
        public static float GroundY(float x, float z)
        {
            float rc = RiverCentreAt(z);
            float dx = x - rc;
            float d = Mathf.Abs(dx) + BankWobble(z, Mathf.Sign(dx));
            float river = Mathf.Lerp(RiverBedY, 0f, Smooth(16.5f, 20.5f, d));
            float flat = FlatMask(x, z);
            float n = (Mathf.PerlinNoise(x * 0.025f + 3f, z * 0.025f + 7f) - 0.5f) * 0.6f;
            float roll = (Mathf.PerlinNoise(x * 0.011f + 17f, z * 0.011f + 5f) - 0.5f) * 3.2f;
            float o = Outside(x, z);
            float hill = 0f;
            if (o > -8f)
            {
                float p1 = Mathf.PerlinNoise(x * 0.045f, z * 0.045f), p2 = Mathf.PerlinNoise(x * 0.0075f + 5f, z * 0.0075f + 9f), p3 = Mathf.PerlinNoise(x * 0.02f + 1f, z * 0.02f + 2f);
                hill = Smooth(-6f, 28f, o) * (4f + p1 * 7f) + Smooth(10f, 240f, o) * (16f + p2 * 38f + p3 * 8f);
                hill *= Smooth(24f, 80f, Mathf.Abs(dx)); // the river valley stays low
            }
            float y = (n + roll) * (1f - flat) + hill;
            return d < 20.5f ? Mathf.Min(river, river + y * Smooth(18f, 20.5f, d)) : y;
        }

        /// <summary>0..1 forest density used by terrain trees and the forest-floor paint (0 inside the playable area).</summary>
        static float ForestDensity(float x, float z)
        {
            float o = Outside(x, z);
            if (o < 1.5f) return 0f;
            float clump = Smooth(0.36f, 0.62f, Mathf.PerlinNoise(x * 0.012f + 40f, z * 0.012f + 40f));
            float wall = Smooth(1.5f, 7f, o) * (1f - Smooth(40f, 90f, o)); // dense green wall right behind the boundary
            float dens = Mathf.Max(wall * 0.9f, clump * 0.85f);
            dens *= Smooth(22f, 42f, Mathf.Abs(x - RiverCentreAt(z)));     // open river valley
            return dens;
        }

        // ------------------------------------------------------------------ terrain grid

        static TerrainLayer[] _layers;

        static void Landscape()
        {
            // Layers: 0 grass, 1 road dirt, 2 tilled soil, 3 mud, 4 cobble, 5 bank sand, 6 meadow, 7 forest floor
            _layers = new[]
            {
                Layer("Grass", WorldTextures.GrassStrokes("GrassPaint", WorldAssets.Hex("86A84E"), WorldAssets.Hex("6E9440"), WorldAssets.Hex("A9C46A"), 2), 6f),
                Layer("Road", WorldAssets.NoiseTexture("Road", WorldAssets.Hex("C4A272"), WorldAssets.Hex("B08E60"), WorldAssets.Hex("8A6E4A"), 0.04f, seed: 3), 5f),
                Layer("Soil", WorldAssets.NoiseTexture("Soil", WorldAssets.Hex("8E6640"), WorldAssets.Hex("76522F"), WorldAssets.Hex("A87C52"), 0.02f, seed: 4, stripes: true), 4f),
                Layer("Mud", WorldAssets.NoiseTexture("Mud", WorldAssets.Hex("6B4A2E"), WorldAssets.Hex("5A3C24"), WorldAssets.Hex("8A6644"), 0.02f, seed: 5), 3f),
                Layer("Cobble", WorldAssets.CobbleTexture("Cobble", WorldAssets.Hex("C9BBA2"), WorldAssets.Hex("8F8270")), 3f),
                Layer("Bank", WorldAssets.NoiseTexture("Bank", WorldAssets.Hex("B49A72"), WorldAssets.Hex("9C8462"), WorldAssets.Hex("D2BE98"), 0.05f, seed: 6), 4f),
                Layer("Meadow", WorldTextures.GrassStrokes("MeadowPaint", WorldAssets.Hex("97B05A"), WorldAssets.Hex("82A04C"), WorldAssets.Hex("E3D68A"), 8), 7f),
                Layer("Forest", WorldAssets.NoiseTexture("ForestFloor", WorldAssets.Hex("5E7A3E"), WorldAssets.Hex("4E6A34"), WorldAssets.Hex("8A7A4A"), 0.05f, seed: 9), 6f),
            };
            var protos = TreePrototypes();
            var tiles = new Terrain[3, 3];
            for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
                tiles[i + 1, j + 1] = Tile(i, j, protos);
            for (int j = 0; j < 3; j++)
            for (int i = 0; i < 3; i++)
            {
                if (i == 1 && j == 1) continue; // the centre has a finer grid; outer tiles stitch among themselves only
                Terrain T(int a, int b) => a < 0 || b < 0 || a > 2 || b > 2 || (a == 1 && b == 1) ? null : tiles[a, b];
                tiles[i, j].SetNeighbors(T(i - 1, j), T(i, j + 1), T(i + 1, j), T(i, j - 1));
            }
            HorizonRing();
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

        static readonly string[] TreeModels = { "tree_default", "tree_oak", "tree_fat", "tree_tall", "tree_plateau", "tree_pineRoundA", "tree_pineRoundC", "tree_pineTallA", "tree_cone", "tree_simple" };
        static float[] _treeHeights;

        static TreePrototype[] TreePrototypes()
        {
            var list = new List<TreePrototype>();
            _treeHeights = new float[TreeModels.Length];
            for (int i = 0; i < TreeModels.Length; i++)
            {
                var src = WorldAssets.Model(NF, TreeModels[i]);
                var probe = (GameObject)Object.Instantiate(src);
                _treeHeights[i] = Mathf.Max(0.2f, RendererBounds(probe).size.y);
                Object.DestroyImmediate(probe);
                list.Add(new TreePrototype { prefab = src, bendFactor = 0f });
            }
            return list.ToArray();
        }

        static Terrain Tile(int i, int j, TreePrototype[] protos)
        {
            bool centre = i == 0 && j == 0;
            int res = centre ? CentreRes : OuterRes;
            float th = centre ? CentreTH : OuterTH;
            float x0 = TX0 + i * TSX, z0 = TZ0 + j * TSZ;
            string tdPath = centre ? TerrainDir + "/TD_Muddlebury.asset" : $"{TerrainDir}/TD_Muddlebury_Outer_{i + 1}{j + 1}.asset";
            AssetDatabase.DeleteAsset(tdPath);
            var td = new TerrainData { heightmapResolution = res };
            td.size = new Vector3(TSX, th, TSZ);
            int ar = centre ? 512 : 64;
            td.alphamapResolution = ar;
            td.baseMapResolution = centre ? 512 : 128;
            AssetDatabase.CreateAsset(td, tdPath); // create first so the splat textures are saved as sub-assets

            var world = new float[res, res];
            for (int jj = 0; jj < res; jj++)
            for (int ii = 0; ii < res; ii++)
                world[jj, ii] = GroundY(x0 + ii / (float)(res - 1) * TSX, z0 + jj / (float)(res - 1) * TSZ);
            if (centre) MatchCoarseBorder(world, res, 4);
            var h = new float[res, res];
            for (int jj = 0; jj < res; jj++)
            for (int ii = 0; ii < res; ii++)
                h[jj, ii] = Mathf.Clamp01((world[jj, ii] - TY0) / th);
            td.SetHeights(0, 0, h);

            td.terrainLayers = _layers;
            var a = new float[ar, ar, _layers.Length];
            var w = new float[_layers.Length];
            for (int jj = 0; jj < ar; jj++)
            for (int ii = 0; ii < ar; ii++)
            {
                float x = x0 + (ii + 0.5f) / ar * TSX, z = z0 + (jj + 0.5f) / ar * TSZ;
                Paint(x, z, w);
                float sum = 0f; foreach (var v in w) sum += v;
                for (int k = 0; k < w.Length; k++) a[jj, ii, k] = w[k] / sum;
            }
            td.SetAlphamaps(0, 0, a);
            foreach (var at in td.alphamapTextures) if (!AssetDatabase.Contains(at)) AssetDatabase.AddObjectToAsset(at, td);

            td.treePrototypes = protos;
            td.RefreshPrototypes();
            td.SetTreeInstances(Trees(x0, z0, th).ToArray(), false);
            EditorUtility.SetDirty(td);
            AssetDatabase.SaveAssets();

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = centre ? "Ground (Terrain)" : $"Scenery Terrain {i + 1}{j + 1}";
            go.transform.SetParent(_root);
            go.transform.position = new Vector3(x0, TY0, z0);
            go.isStatic = true;
            var t = go.GetComponent<Terrain>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat");
            if (mat != null) t.materialTemplate = mat;
            t.heightmapPixelError = centre ? 4f : 8f;
            t.basemapDistance = centre ? 400f : 250f;
            t.drawTreesAndFoliage = true;
            t.treeDistance = 520f;
            t.treeBillboardDistance = 1000f;
            t.treeMaximumFullLODCount = 20000;
            t.allowAutoConnect = !centre;
            t.groupingID = centre ? 1 : 2;
            t.shadowCastingMode = ShadowCastingMode.TwoSided;
            var col = go.GetComponent<TerrainCollider>();
            if (!centre) Object.DestroyImmediate(col); // nobody can reach the scenery tiles (tree prototypes carry no colliders)
            return t;
        }

        /// <summary>Linear-interpolates the centre tile's border between every <paramref name="step"/>-th sample so it matches the coarse neighbours exactly (no cracks).</summary>
        static void MatchCoarseBorder(float[,] world, int res, int step)
        {
            for (int k = 0; k < res; k++)
            {
                if (k % step == 0) continue;
                int k0 = k - k % step, k1 = Mathf.Min(res - 1, k0 + step);
                float t = (k - k0) / (float)(k1 - k0);
                world[0, k] = Mathf.Lerp(world[0, k0], world[0, k1], t);
                world[res - 1, k] = Mathf.Lerp(world[res - 1, k0], world[res - 1, k1], t);
                world[k, 0] = Mathf.Lerp(world[k0, 0], world[k1, 0], t);
                world[k, res - 1] = Mathf.Lerp(world[k0, res - 1], world[k1, res - 1], t);
            }
        }

        static List<TreeInstance> Trees(float x0, float z0, float th)
        {
            var list = new List<TreeInstance>();
            const float step = 6.5f;
            for (float z = z0 + step * 0.5f; z < z0 + TSZ; z += step)
            for (float x = x0 + step * 0.5f; x < x0 + TSX; x += step)
            {
                float jx = x + Rnd(-2.6f, 2.6f), jz = z + Rnd(-2.6f, 2.6f);
                float roll = (float)_rng.NextDouble();
                if (roll > ForestDensity(jx, jz)) continue;
                float y = GroundY(jx, jz);
                if (y < WaterY + 0.8f) continue;
                // pines in the cooler clumps, broadleaf elsewhere
                bool pine = Mathf.PerlinNoise(jx * 0.02f + 70f, jz * 0.02f + 70f) > 0.55f;
                int p = pine ? 5 + _rng.Next(4) : Pick(0, 1, 2, 3, 4, 9);
                float height = Rnd(7.5f, 13.5f) * (pine ? 1.15f : 1f);
                float s = height / _treeHeights[p];
                list.Add(new TreeInstance
                {
                    position = new Vector3((jx - x0) / TSX, (y - TY0) / th, (jz - z0) / TSZ),
                    widthScale = s * Rnd(0.85f, 1.15f), heightScale = s,
                    rotation = Rnd(0f, Mathf.PI * 2f), prototypeIndex = p,
                    color = Color.white, lightmapColor = Color.white,
                });
            }
            return list;
        }

        // ------------------------------------------------------------------ painting

        static readonly (Vector2 a, Vector2 b, float w)[] Roads =
        {
            (new Vector2(-17, 0), new Vector2(62, 0), 2.6f),     // barn -> bridge, 5 m
            (new Vector2(7, 0), new Vector2(16, 26), 1.7f),      // fork north to the pig pen
            (new Vector2(7, 0), new Vector2(16, -27), 1.7f),     // fork south to the coop
            (new Vector2(-34, 4), new Vector2(-66, 22), 1.6f),   // west to veg rows
            (new Vector2(-47, 11), new Vector2(-47, 19), 1.1f),  // farmhouse garden path
            (new Vector2(-34, -4), new Vector2(-56, -34), 1.6f), // to pumpkin patch
            (new Vector2(-56, -34), new Vector2(-82, -34), 1.4f),// to the windmill
            (new Vector2(-82, -34), new Vector2(-100, -56), 1.2f),// windmill -> hay meadow
            (new Vector2(16, -44), new Vector2(24, -56), 1.4f),  // coop -> orchard
            (new Vector2(16, 42), new Vector2(14, 55), 1.4f),    // pen -> corn maze
            (new Vector2(30, -8), new Vector2(46, -116), 1.3f),  // bank path to the spit-out pile
            (new Vector2(46, -116), new Vector2(54, -120), 1.3f),
            (new Vector2(98, 0), new Vector2(110, 0), 2.6f),     // bridge -> market
            (new Vector2(136, 30), new Vector2(136, 96), 2.2f),  // town lane north
            (new Vector2(136, -30), new Vector2(136, -140), 2.2f),// town lane south
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
            road = Mathf.Max(road, 1f - Smooth(0f, 1.5f, DistRect(x, z, -36, -8, -16, 8)));               // barn floor
            road = Mathf.Max(road, (1f - Smooth(0f, 2f, DistRect(x, z, -53, 14, -41, 19))) * 0.55f);      // farmhouse front yard
            float soil = 0f;
            soil = Mathf.Max(soil, 1f - Smooth(0f, 1.2f, DistRect(x, z, -79, 25, -61, 43)));      // veg rows
            soil = Mathf.Max(soil, (1f - Smooth(0f, 1.5f, DistRect(x, z, -109, -16, -67, 19))) * 0.8f); // wheat
            soil = Mathf.Max(soil, 1f - Smooth(0f, 1.2f, DistRect(x, z, -64, -52, -48, -36)));    // pumpkin patch
            soil = Mathf.Max(soil, (1f - Smooth(0f, 1.5f, DistRect(x, z, -9, 55, 43, 87))) * 0.85f); // corn maze
            soil = Mathf.Max(soil, 1f - Smooth(0f, 0.8f, DistRect(x, z, -55, 27.5f, -50, 31)));   // farmhouse kitchen garden
            float mud = 0f;
            mud = Mathf.Max(mud, 1f - Smooth(0.7f, 1.0f, Ellipse(x, z, PigPen.x - 3f, PigPen.z + 1f, 5.5f, 3.6f)));
            mud = Mathf.Max(mud, 1f - Smooth(0.6f, 1.0f, Ellipse(x, z, 53f, 4.5f, 3.2f, 2.2f)));
            mud = Mathf.Max(mud, 1f - Smooth(0.6f, 1.0f, Ellipse(x, z, -4f, -7f, 2.8f, 2f)));
            float cobble = 1f - Smooth(0f, 1.5f, DistRect(x, z, 103, -32, 168, 32));
            cobble = Mathf.Max(cobble, 1f - Smooth(1.4f, 2.4f, DistSeg(p, new Vector2(100, 0), new Vector2(104, 0))));
            cobble = Mathf.Max(cobble, 1f - Smooth(0f, 1.2f, DistRect(x, z, 160, -9, 168, 9)));   // town hall forecourt
            float rc = RiverCentreAt(z);
            float bank = 1f - Smooth(19.2f, 22.5f, Mathf.Abs(x - rc) + BankWobble(z, Mathf.Sign(x - rc)));
            float forest = Mathf.Max(ForestDensity(x, z) * 0.85f, Smooth(-14f, 0f, Outside(x, z)) * 0.55f);
            w[1] = road; w[2] = soil; w[3] = mud; w[4] = cobble; w[5] = bank;
            float top = Mathf.Max(Mathf.Max(road, soil), Mathf.Max(Mathf.Max(mud, cobble), bank));
            w[7] = forest * (1f - top);
            w[0] *= (1f - top) * (1f - forest); w[6] *= (1f - top) * (1f - forest);
            w[0] += 1e-4f;
        }

        static float Ellipse(float x, float z, float cx, float cz, float rx, float rz)
        {
            float dx = (x - cx) / rx, dz = (z - cz) / rz; return Mathf.Sqrt(dx * dx + dz * dz);
        }

        // ------------------------------------------------------------------ horizon

        /// <summary>Low-poly hill silhouettes on a ring beyond the scenery tiles (inside the 1000 m player far plane); fog does the aerial perspective.</summary>
        static void HorizonRing()
        {
            var mb = new WorldMeshBuilder();
            var mat = WorldAssets.Mat("HorizonHills", WorldAssets.Hex("7F9A72"));
            float[] radii = { 560f, 660f, 780f, 900f, 985f };
            const int seg = 120;
            float H(int ring, int s)
            {
                float a = s * Mathf.PI * 2f / seg;
                float nx = Mathf.Cos(a) * 3f + 10f, nz = Mathf.Sin(a) * 3f + 10f;
                float n1 = Mathf.PerlinNoise(nx + ring * 1.7f, nz), n2 = Mathf.PerlinNoise(nx * 2.3f + 4f, nz * 2.3f + ring);
                switch (ring)
                {
                    case 0: return -15f;
                    case 1: return 35f + n1 * 55f;
                    case 2: return 70f + n1 * 85f + n2 * 30f;
                    case 3: return 55f + n2 * 75f;
                    default: return 5f;
                }
            }
            Vector3 P(int ring, int s)
            {
                float a = s * Mathf.PI * 2f / seg;
                return new Vector3(WorldCentre.x + Mathf.Cos(a) * radii[ring], H(ring, s % seg), WorldCentre.y + Mathf.Sin(a) * radii[ring]);
            }
            for (int r = 0; r < radii.Length - 1; r++)
            for (int s = 0; s < seg; s++)
            {
                Vector3 a = P(r, s), b = P(r, s + 1), c = P(r + 1, s + 1), d = P(r + 1, s);
                Vector3 mid = (a + b + c + d) * 0.25f, inward = new Vector3(WorldCentre.x - mid.x, 0f, WorldCentre.y - mid.z).normalized;
                Vector3 n1 = Vector3.Cross(b - a, c - a).normalized; if (Vector3.Dot(n1, Vector3.up + inward) < 0f) n1 = -n1;
                Vector3 n2 = Vector3.Cross(c - a, d - a).normalized; if (Vector3.Dot(n2, Vector3.up + inward) < 0f) n2 = -n2;
                mb.Tri(a, b, c, n1, mat, Vector2.zero, Vector2.right, Vector2.one);
                mb.Tri(a, c, d, n2, mat, Vector2.zero, Vector2.one, Vector2.up);
            }
            mb.Bake("SM_World_HorizonRing", "Horizon Hills", _root, Vector3.zero, Quaternion.identity, ShadowCastingMode.Off);
        }

        // ------------------------------------------------------------------ river

        static void River()
        {
            var g = Group("River");
            // Water ribbon along the centreline (UV.x across, UV.y = metres from the south end, so streaks flow south).
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tris = new List<int>();
            const int across = 10; const float half = 18.7f, step = 2.5f;
            float zS = TZ0 - TSZ, zN = TZ0 + 2f * TSZ;
            float s = 0f; Vector3 prev = Vector3.zero;
            int rows = 0;
            for (float z = zS; z <= zN + 0.01f; z += step, rows++)
            {
                float x = RiverCentreAt(z);
                var c = new Vector3(x, 0f, z);
                if (rows > 0) s += (c - prev).magnitude;
                prev = c;
                float dxdz = (RiverCentreAt(z + 0.5f) - RiverCentreAt(z - 0.5f));
                var tangent = new Vector3(dxdz, 0f, 1f).normalized;
                var side = new Vector3(tangent.z, 0f, -tangent.x); // +X-ish across
                for (int i = 0; i <= across; i++)
                {
                    float t = i / (float)across;
                    v.Add(c + side * Mathf.Lerp(-half, half, t));
                    uv.Add(new Vector2(t, s));
                }
            }
            for (int r = 0; r < rows - 1; r++)
            for (int i = 0; i < across; i++)
            {
                int a = r * (across + 1) + i, b = a + 1, c = a + across + 1, d = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
            var m = new Mesh { name = "SM_World_River", indexFormat = IndexFormat.UInt32 };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tris, 0); m.RecalculateNormals(); m.RecalculateBounds();
            if (m.normals.Length > 0 && m.normals[0].y < 0f)
            {
                var tr = m.triangles; for (int i = 0; i < tr.Length; i += 3) (tr[i + 1], tr[i + 2]) = (tr[i + 2], tr[i + 1]);
                m.triangles = tr; m.RecalculateNormals();
            }
            var water = WorldAssets.SaveMesh(m, "SM_World_River");
            var surface = MeshObject("Water Surface", water, Mats.Water, g, new Vector3(0f, WaterY, 0f), Quaternion.identity, collider: false);
            surface.isStatic = false;
            surface.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // River trigger volume on the Water layer (sees Player, Critter, Carryable, Ragdoll). Straight inside the play area.
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

            // Stepping-stone bait 65 m downstream (gaps too wide to cross; GDD hotspot 5).
            for (int i = 0; i < 6; i++)
            {
                float x = RiverWest + 3f + i * 6.4f;
                Place(NF, i % 2 == 0 ? "rock_largeA" : "rock_largeB", new Vector3(x, WaterY - 0.9f, -65f + Rnd(-1f, 1f)), Rnd(0, 360), 1.6f, g, "box", snap: false);
            }
            // Bank dressing at the waterline: reeds, rocks, lilies (culled beyond 70 m).
            var bank = Cullable(Group("Bank Dressing", g), 12f, 70f);
            for (int i = 0; i < 230; i++)
            {
                float z = Rnd(PlayZ0 + 4f, PlayZ1 - 2f);
                if (Mathf.Abs(z) < 7f || Mathf.Abs(z + 120f) < 4f) continue;
                float side = i % 2 == 0 ? -1f : 1f;
                float d = WaterlineDistance(z, side) + Rnd(-0.6f, 1.6f);
                float x = RiverCentre + side * d;
                int kind = _rng.Next(10);
                if (kind < 5) Place(NF, "grass_leafsLarge", new Vector3(x, 0, z), Rnd(0, 360), Rnd(0.5f, 0.9f), bank);
                else if (kind < 7) Place(NF, "crops_cornStageB", new Vector3(x + side * 0.4f, 0, z), Rnd(0, 360), Rnd(0.9f, 1.4f), bank); // reed clump
                else if (kind < 9) Place(NF, Pick("rock_smallA", "rock_smallC", "rock_largeC"), new Vector3(x, 0, z), Rnd(0, 360), Rnd(0.35f, 0.9f), bank);
                else Place(NF, "grass_large", new Vector3(x + side * 1.2f, 0, z), Rnd(0, 360), Rnd(0.4f, 0.6f), bank);
            }
            for (int i = 0; i < 26; i++)
            {
                float side = i % 3 == 0 ? 1f : -1f, z = i < 12 ? -100f - i * 3.3f : Rnd(PlayZ0 + 10f, PlayZ1 - 10f);
                if (Mathf.Abs(z) < 12f) continue;
                Place(NF, i % 2 == 0 ? "lily_large" : "lily_small", new Vector3(RiverCentre + side * (WaterlineDistance(z, side) - Rnd(1.5f, 4f)), WaterY + 0.05f, z), Rnd(0, 360), Rnd(0.25f, 0.45f), bank, snap: false);
            }
            // Boundary weirs: a broken line of rocks across the water just inside each edge, so the river "ends" visibly.
            foreach (float zw in new[] { PlayZ1 - 3f, PlayZ0 + 3f })
                for (float x = RiverWest + 2f; x < RiverEast - 1f; x += Rnd(2.6f, 4.2f))
                    Place(NF, Pick("rock_largeA", "rock_largeB", "rock_largeC", "rock_tallA"), new Vector3(x, WaterY - 0.7f, zw + Rnd(-1.2f, 1.2f)), Rnd(0, 360), Rnd(1.1f, 2.0f), g, snap: false);
        }

        /// <summary>Distance from the centreline where the bank meets the water surface (WaterY), per side.</summary>
        static float WaterlineDistance(float z, float side)
        {
            for (float d = 15f; d < 21f; d += 0.1f)
                if (GroundY(RiverCentre + side * d, z) > WaterY + 0.05f) return d;
            return 18f;
        }
    }
}
