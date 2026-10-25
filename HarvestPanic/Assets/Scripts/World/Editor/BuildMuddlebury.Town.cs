using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HarvestPanic.World.Editor
{
    public static partial class BuildMuddlebury
    {
        // ------------------------------------------------------------------ storybook house generator

        /// <summary>Parameters for <see cref="House"/>. Local frame: front faces +X, width W along Z, depth D along X.</summary>
        sealed class HouseSpec
        {
            public float W = 8f, D = 7f, H1 = 3.0f, H2 = 2.6f, Pitch = 0.75f;
            public int Wall, Upper = -1, Roof, Shutter;
            public bool GableFront, Jetty = true, Timber = true, Chimney = true, StoneGround, Porch;
            public int FrontWindows = 2, DoorSlot = -1; // DoorSlot: index among front slots (-1 = centre)
            public string Shop;         // fascia lettering (null = no shop front)
            public int Awning = -1;
            public bool FlowerBoxes = true;
        }

        sealed class HouseOut
        {
            public readonly List<(Vector3 c, Vector3 s)> Solids = new List<(Vector3, Vector3)>();
            public readonly List<Vector3> FlowerBoxes = new List<Vector3>(); // local positions (box top centre), front-facing
            public Vector3 Fascia; public float FasciaW; public bool HasFascia;
            public float RoofTop;
        }

        /// <summary>Builds a whole house into <paramref name="b"/> (local frame), returns colliders and decoration anchors.</summary>
        static HouseOut House(WorldMeshBuilder b, HouseSpec s)
        {
            var o = new HouseOut();
            var wall = Mats.Wall(s.Wall);
            var upperM = s.Upper < 0 ? wall : Mats.Wall(s.Upper);
            var roofM = Mats.Roof(s.Roof);
            var shut = Mats.Shutter(s.Shutter);
            float j = s.H2 > 0f && s.Jetty ? 0.28f : 0f;
            float D2 = s.D + 2f * j, W2 = s.W + (j > 0f ? 0.3f : 0f);
            float top = s.H1 + s.H2;

            // plinth, ground storey, upper storey
            b.Box(new Vector3(0f, 0.18f, 0f), new Vector3(s.D + 0.14f, 0.5f, s.W + 0.14f), Mats.StoneDark, 1.2f, skip: 4);
            b.Box(new Vector3(0f, s.H1 * 0.5f, 0f), new Vector3(s.D, s.H1, s.W), s.StoneGround ? Mats.Stone : wall, s.StoneGround ? 1.4f : 2.5f, skip: 4);
            o.Solids.Add((new Vector3(0f, s.H1 * 0.5f, 0f), new Vector3(s.D, s.H1, s.W)));
            if (s.H2 > 0f)
            {
                b.Box(new Vector3(0f, s.H1 + s.H2 * 0.5f, 0f), new Vector3(D2, s.H2, W2), upperM, 2.5f);
                b.Box(new Vector3(0f, s.H1 + 0.08f, 0f), new Vector3(D2 + 0.12f, 0.24f, W2 + 0.12f), Mats.WoodDark, 1f);
                o.Solids.Add((new Vector3(0f, s.H1 + s.H2 * 0.5f, 0f), new Vector3(D2, s.H2, W2)));
            }

            // front slots (z positions) shared by both floors so posts never cut a window
            int n = Mathf.Max(1, s.FrontWindows + 1);
            var slots = new float[n];
            for (int i = 0; i < n; i++) slots[i] = Mathf.Lerp(-s.W * 0.5f, s.W * 0.5f, (i + 0.5f) / n);
            int door = s.DoorSlot >= 0 ? Mathf.Min(s.DoorSlot, n - 1) : n / 2;

            // ground floor front
            for (int i = 0; i < n; i++)
            {
                var face = new Vector3(s.D * 0.5f, 0f, slots[i]);
                if (i == door) Door(b, face, roofM);
                else if (s.Shop != null) Window(b, face + Vector3.up * 1.55f, Vector3.right, Mathf.Min(1.9f, s.W / n - 0.5f), 1.45f, shut, false, null);
                else Window(b, face + Vector3.up * 1.6f, Vector3.right, 0.95f, 1.15f, shut, s.FlowerBoxes, o);
            }
            if (s.Shop != null)
            {
                o.HasFascia = true; o.FasciaW = s.W * 0.72f;
                o.Fascia = new Vector3(s.D * 0.5f + 0.06f, s.H1 - 0.42f, 0f);
                b.Box(o.Fascia - Vector3.right * 0.02f, new Vector3(0.08f, 0.62f, o.FasciaW + 0.16f), Mats.SignBoardDark, 1f);
                if (s.Awning >= 0)
                    for (int i = 0; i < n; i++)
                        if (i != door) Awning(b, new Vector3(s.D * 0.5f, s.H1 - 0.85f, slots[i]), Mathf.Min(2.6f, s.W / n + 0.1f), Mats.Awning(s.Awning));
            }
            // upper floor front
            if (s.H2 > 0f)
                for (int i = 0; i < n; i++)
                    Window(b, new Vector3(D2 * 0.5f, s.H1 + s.H2 * 0.52f, slots[i]), Vector3.right, 0.85f, 1.05f, shut, s.FlowerBoxes && i != door, o);
            // sides + back: one window per floor
            foreach (float side in new[] { -1f, 1f })
            {
                Window(b, new Vector3(0f, 1.6f, side * s.W * 0.5f), new Vector3(0, 0, side), 0.8f, 1.0f, shut, false, null);
                if (s.H2 > 0f) Window(b, new Vector3(0f, s.H1 + s.H2 * 0.52f, side * W2 * 0.5f), new Vector3(0, 0, side), 0.8f, 1.0f, shut, false, null);
            }
            Window(b, new Vector3(-s.D * 0.5f, 1.6f, -s.W * 0.2f), Vector3.left, 0.9f, 1.1f, shut, false, null);

            // timber framing on the upper storey (front + sides)
            if (s.H2 > 0f && s.Timber)
            {
                float fx = D2 * 0.5f + 0.035f, y0 = s.H1 + 0.2f, y1 = top;
                var posts = new List<float> { -W2 * 0.5f + 0.09f, W2 * 0.5f - 0.09f };
                for (int i = 0; i < n - 1; i++) posts.Add((slots[i] + slots[i + 1]) * 0.5f);
                foreach (float z in posts) b.Box(new Vector3(fx, (y0 + y1) * 0.5f, z), new Vector3(0.07f, y1 - y0, 0.17f), Mats.WoodDark, 1f);
                b.Box(new Vector3(fx, y1 - 0.09f, 0f), new Vector3(0.07f, 0.18f, W2), Mats.WoodDark, 1f);
                foreach (float side in new[] { -1f, 1f })
                {
                    float fz = side * (W2 * 0.5f + 0.035f);
                    foreach (float x in new[] { -D2 * 0.5f + 0.09f, 0f, D2 * 0.5f - 0.09f })
                        b.Box(new Vector3(x, (y0 + y1) * 0.5f, fz), new Vector3(0.17f, y1 - y0, 0.07f), Mats.WoodDark, 1f);
                    // braces either side of the middle post
                    float h = y1 - y0, half = D2 * 0.5f - 0.2f, len = Mathf.Sqrt(h * h + half * half), ang = Mathf.Atan2(half, h) * Mathf.Rad2Deg;
                    b.Box(new Vector3(-D2 * 0.25f, (y0 + y1) * 0.5f, fz), new Vector3(0.14f, len, 0.06f), Mats.WoodDark, 1f, roll: ang);
                    b.Box(new Vector3(D2 * 0.25f, (y0 + y1) * 0.5f, fz), new Vector3(0.14f, len, 0.06f), Mats.WoodDark, 1f, roll: -ang);
                }
            }
            else if (!s.StoneGround)
            {
                // corner boards on plain houses
                foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    b.Box(new Vector3(sx * (D2 * 0.5f + 0.02f), top * 0.5f + 0.2f, sz * (W2 * 0.5f + 0.02f)), new Vector3(0.18f, top - 0.2f, 0.18f), Mats.Trim, 1f);
            }

            // roof
            float span = s.GableFront ? W2 : D2, length = s.GableFront ? D2 : W2;
            b.Push(new Vector3(0f, top, 0f), Quaternion.Euler(0f, s.GableFront ? 90f : 0f, 0f));
            float rise = Roof(b, span, length, s.Pitch, roofM, upperM);
            if (s.H2 > 0f) // attic windows in both gables
            {
                Window(b, new Vector3(0f, rise * 0.36f, length * 0.5f), Vector3.forward, 0.7f, 0.7f, null, false, null);
                if (!s.GableFront) Window(b, new Vector3(0f, rise * 0.36f, -length * 0.5f), Vector3.back, 0.7f, 0.7f, null, false, null);
            }
            b.Pop();
            o.RoofTop = top + rise;
            if (s.Chimney)
            {
                float cz = s.GableFront ? -W2 * 0.5f + 0.7f : W2 * 0.5f - 0.9f, cx = s.GableFront ? -D2 * 0.2f : -D2 * 0.18f;
                float ch = top + rise * 0.85f + 0.9f;
                b.Box(new Vector3(cx, ch * 0.5f + top * 0.5f, cz), new Vector3(0.75f, ch - top + 0.2f, 0.75f), Mats.Stone, 1.2f);
                b.Box(new Vector3(cx, ch + 0.32f, cz), new Vector3(0.95f, 0.14f, 0.95f), Mats.StoneDark, 1.2f);
            }
            return o;
        }

        /// <summary>Gable roof with the ridge along local Z (span along X), eaves at y = 0. Returns the rise.</summary>
        static float Roof(WorldMeshBuilder b, float span, float length, float pitch, Material roofM, Material gableM, bool gableNeg = true, bool gablePos = true)
        {
            float over = 0.45f, gableOver = 0.35f;
            float hs = span * 0.5f + over, rise = (span * 0.5f) * pitch, riseO = hs * pitch;
            float th = Mathf.Atan2(riseO, hs) * Mathf.Rad2Deg;
            float L = Mathf.Sqrt(hs * hs + riseO * riseO) + 0.06f, wl = length + 2f * gableOver;
            float yBase = rise - riseO; // eave line sits below the wall top by the overhang drop
            foreach (float side in new[] { 1f, -1f })
            {
                var c = new Vector3(side * hs * 0.5f, yBase + riseO * 0.5f + 0.09f, 0f);
                b.Box(c, new Vector3(L, 0.2f, wl), roofM, 1.6f, roll: -side * th);
                // barge boards at both gable ends
                foreach (float e in new[] { -1f, 1f })
                    b.Box(c + new Vector3(0f, -0.06f, e * (wl * 0.5f - 0.03f)), new Vector3(L, 0.3f, 0.07f), Mats.Trim, 1f, roll: -side * th);
                // fascia board along the eave
                b.Box(new Vector3(side * (hs - 0.04f), yBase + 0.02f, 0f), new Vector3(0.07f, 0.24f, wl), Mats.Trim, 1f);
            }
            b.Box(new Vector3(0f, rise + 0.16f, 0f), new Vector3(0.34f, 0.18f, wl + 0.04f), Mats.WoodDark, 1f); // ridge cap
            // gable triangles (wall material), closing the attic at both ends
            var tri = new[] { new Vector2(-span * 0.5f, 0f), new Vector2(span * 0.5f, 0f), new Vector2(0f, rise) };
            foreach (float e in new[] { -1f, 1f })
            {
                if (e < 0f && !gableNeg || e > 0f && !gablePos) continue;
                b.Push(new Vector3(0f, 0f, e * (length * 0.5f - 0.1f)), Quaternion.identity);
                b.Prism(tri, 0.2f, gableM, gableM, 2.5f);
                b.Pop();
            }
            return rise;
        }

        /// <summary>Framed window with shutters (and optional flower box) on a wall face; normal = outward.</summary>
        static void Window(WorldMeshBuilder b, Vector3 face, Vector3 normal, float w, float h, Material shutter, bool flowerBox, HouseOut o)
        {
            b.Push(face, Quaternion.LookRotation(normal, Vector3.up));
            float hw = w * 0.5f, hh = h * 0.5f;
            b.Quad(new Vector3(hw, -hh, 0.03f), new Vector3(-hw, -hh, 0.03f), new Vector3(-hw, hh, 0.03f), new Vector3(hw, hh, 0.03f), Vector3.forward, Mats.GlassDay,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
            b.Box(new Vector3(0f, hh + 0.06f, 0.05f), new Vector3(w + 0.22f, 0.12f, 0.1f), Mats.Trim, 1f);
            b.Box(new Vector3(0f, -hh - 0.05f, 0.08f), new Vector3(w + 0.3f, 0.1f, 0.16f), Mats.Trim, 1f);
            b.Box(new Vector3(hw + 0.05f, 0f, 0.05f), new Vector3(0.1f, h, 0.1f), Mats.Trim, 1f);
            b.Box(new Vector3(-hw - 0.05f, 0f, 0.05f), new Vector3(0.1f, h, 0.1f), Mats.Trim, 1f);
            if (shutter != null)
                foreach (float sd in new[] { -1f, 1f })
                    b.Box(new Vector3(sd * (hw + 0.12f + hw * 0.5f), 0f, 0.04f), new Vector3(hw, h, 0.05f), shutter, 1f);
            if (flowerBox && o != null)
            {
                b.Box(new Vector3(0f, -hh - 0.24f, 0.17f), new Vector3(w + 0.1f, 0.22f, 0.26f), Mats.WoodMid, 1f);
                // record the box top centre in the house frame: transform via the pushed frame
                o.FlowerBoxes.Add(face + Quaternion.LookRotation(normal, Vector3.up) * new Vector3(0f, -hh - 0.13f, 0.17f));
            }
            b.Pop();
        }

        static void Door(WorldMeshBuilder b, Vector3 face, Material hood)
        {
            b.Push(face, Quaternion.LookRotation(Vector3.right, Vector3.up));
            b.Box(new Vector3(0f, 1.05f, 0.04f), new Vector3(1.05f, 2.1f, 0.08f), Mats.DoorWood, 1f);
            b.Box(new Vector3(0f, 2.18f, 0.06f), new Vector3(1.35f, 0.14f, 0.12f), Mats.Trim, 1f);
            b.Box(new Vector3(0.6f, 1.05f, 0.06f), new Vector3(0.12f, 2.1f, 0.12f), Mats.Trim, 1f);
            b.Box(new Vector3(-0.6f, 1.05f, 0.06f), new Vector3(0.12f, 2.1f, 0.12f), Mats.Trim, 1f);
            b.Box(new Vector3(0.36f, 1.05f, 0.1f), new Vector3(0.07f, 0.07f, 0.05f), Mats.Bronze, 1f); // knob
            b.Box(new Vector3(0f, 0.09f, 0.32f), new Vector3(1.5f, 0.18f, 0.6f), Mats.Stone, 1f);    // step
            b.Box(new Vector3(0f, 2.55f, 0.38f), new Vector3(1.6f, 0.08f, 0.85f), hood, 1f, pitch: 14f); // hood
            b.Box(new Vector3(0.68f, 2.35f, 0.25f), new Vector3(0.07f, 0.35f, 0.07f), Mats.WoodDark, 1f, pitch: -40f);
            b.Box(new Vector3(-0.68f, 2.35f, 0.25f), new Vector3(0.07f, 0.35f, 0.07f), Mats.WoodDark, 1f, pitch: -40f);
            b.Pop();
        }

        /// <summary>Striped canvas awning sloping out from a facade (stripes run wall -> edge) with a scalloped valance.</summary>
        static void Awning(WorldMeshBuilder b, Vector3 wallTop, float width, Material m)
        {
            float depth = 1.35f, drop = 0.5f, hw = width * 0.5f;
            Vector3 a = wallTop + new Vector3(0f, 0f, -hw), c = wallTop + new Vector3(0f, 0f, hw);
            Vector3 d = wallTop + new Vector3(depth, -drop, -hw), e = wallTop + new Vector3(depth, -drop, hw);
            var up = Vector3.Cross(c - a, d - a).normalized; if (up.y < 0f) up = -up;
            float u = width / 0.8f; // ~4 stripes per 0.8 m texture repeat
            b.Quad(a, c, e, d, up, m, new Vector2(0, 1), new Vector2(u, 1), new Vector2(u, 0), new Vector2(0, 0));
            b.Quad(a, d, e, c, -up, m, new Vector2(0, 1), new Vector2(0, 0), new Vector2(u, 0), new Vector2(u, 1));
            Vector3 v0 = d, v1 = e, v2 = e + Vector3.down * 0.28f, v3 = d + Vector3.down * 0.28f;
            b.Quad(v0, v1, v2, v3, Vector3.right, m, new Vector2(0, 1), new Vector2(u, 1), new Vector2(u, 0.7f), new Vector2(0, 0.7f));
            b.Quad(v1, v0, v3, v2, Vector3.left, m, new Vector2(u, 1), new Vector2(0, 1), new Vector2(0, 0.7f), new Vector2(u, 0.7f));
        }

        /// <summary>Places a house: baked mesh, colliders, fascia lettering and window-box flowers. Returns the root.</summary>
        static GameObject PlaceHouse(string name, HouseSpec s, Vector3 pos, float yaw, Transform parent, Transform flowers)
        {
            var b = new WorldMeshBuilder();
            var o = House(b, s);
            if (s.Porch) Porch(b, s, o);
            float gy = GroundY(pos.x, pos.z);
            var root = BakeBuilding(b, "SM_World_" + name, name, parent, new Vector3(pos.x, gy, pos.z), yaw);
            int k = 0;
            foreach (var sol in o.Solids) Solid("Collider_" + k++, root.transform, sol.c, sol.s, Quaternion.identity);
            if (o.HasFascia)
            {
                var wp = root.transform.TransformPoint(o.Fascia + Vector3.right * 0.03f);
                Lettering(s.Shop, wp, yaw + 90f, o.FasciaW, 0.5f, root.transform, WorldAssets.Hex("F4E9CF"));
            }
            if (flowers != null)
                foreach (var fb in o.FlowerBoxes)
                {
                    var wp = root.transform.TransformPoint(fb);
                    for (int i = -1; i <= 1; i++)
                    {
                        var p = wp + root.transform.forward * (i * 0.28f) + root.transform.right * Rnd(-0.03f, 0.03f);
                        Place(NF, Pick("flower_yellowA", "flower_purpleA", "flower_redA", "flower_yellowB", "flower_purpleB"), new Vector3(p.x, p.y, p.z), Rnd(0, 360), Rnd(0.28f, 0.38f), flowers, snap: false);
                    }
                }
            return root;
        }

        /// <summary>Farmhouse porch across the front: deck, steps, posts, lean-to roof (added to the house mesh).</summary>
        static void Porch(WorldMeshBuilder b, HouseSpec s, HouseOut o)
        {
            float x0 = s.D * 0.5f, depth = 2.3f, w = s.W - 0.6f, deckY = 0.28f;
            b.Box(new Vector3(x0 + depth * 0.5f, deckY * 0.5f, 0f), new Vector3(depth, deckY, w), Mats.Floor, 1.2f);
            b.Box(new Vector3(x0 + depth + 0.3f, 0.07f, 0f), new Vector3(0.6f, 0.14f, 1.8f), Mats.Floor, 1.2f); // step
            o.Solids.Add((new Vector3(x0 + depth * 0.5f, deckY * 0.5f, 0f), new Vector3(depth, deckY, w)));
            float roofLow = 2.85f, roofHigh = s.H1 - 0.05f;
            foreach (float z in new[] { -w * 0.5f + 0.15f, -w * 0.17f, w * 0.17f, w * 0.5f - 0.15f })
            {
                b.Box(new Vector3(x0 + depth - 0.15f, (deckY + roofLow) * 0.5f, z), new Vector3(0.16f, roofLow - deckY, 0.16f), Mats.Trim, 1f);
                o.Solids.Add((new Vector3(x0 + depth - 0.15f, (deckY + roofLow) * 0.5f, z), new Vector3(0.16f, roofLow - deckY, 0.16f)));
            }
            // railings between the outer posts, leaving the middle open for the steps
            foreach (float sz in new[] { -1f, 1f })
            {
                float zc = sz * (w * 0.5f - 0.15f + w * 0.17f) * 0.5f, len = (w * 0.5f - 0.15f) - w * 0.17f;
                b.Box(new Vector3(x0 + depth - 0.15f, deckY + 0.85f, zc), new Vector3(0.08f, 0.08f, len), Mats.Trim, 1f);
                for (float t = -len * 0.5f + 0.2f; t < len * 0.5f; t += 0.28f)
                    b.Box(new Vector3(x0 + depth - 0.15f, deckY + 0.43f, zc + t), new Vector3(0.05f, 0.82f, 0.05f), Mats.Trim, 1f);
            }
            float run = depth + 0.35f, ang = Mathf.Atan2(roofHigh - roofLow, run) * Mathf.Rad2Deg, L = Mathf.Sqrt(run * run + (roofHigh - roofLow) * (roofHigh - roofLow));
            b.Box(new Vector3(x0 + run * 0.5f, (roofLow + roofHigh) * 0.5f + 0.1f, 0f), new Vector3(L, 0.14f, w + 0.4f), Mats.Roof(s.Roof), 1.6f, roll: -ang);
            b.Box(new Vector3(x0 + run - 0.02f, roofLow + 0.02f, 0f), new Vector3(0.08f, 0.22f, w + 0.4f), Mats.Trim, 1f);
        }

        // ------------------------------------------------------------------ market town

        static void MarketTown()
        {
            var g = Group("Market Town");
            var flowers = Cullable(Group("Window Flowers", g), 16f, 55f);
            var props = Cullable(Group("Market Props", g), 14f, 60f);

            // --- Arch at the bridge end of the square: stone pillars, timber beam, painted name board (both faces).
            var arch = new WorldMeshBuilder();
            foreach (float z in new[] { -4.2f, 4.2f })
            {
                arch.Box(new Vector3(104f, 2.5f, z), new Vector3(1.2f, 5f, 1.2f), Mats.Stone, 1.2f);
                arch.Box(new Vector3(104f, 0.25f, z), new Vector3(1.6f, 0.5f, 1.6f), Mats.StoneDark, 1.2f);
                arch.Box(new Vector3(104f, 5.05f, z), new Vector3(1.45f, 0.18f, 1.45f), Mats.StoneDark, 1.2f);
            }
            arch.Box(new Vector3(104f, 5.45f, 0f), new Vector3(1.0f, 0.62f, 10.6f), Mats.WoodDark, 1f);
            arch.Box(new Vector3(104f, 5.9f, 0f), new Vector3(1.3f, 0.16f, 11f), Mats.Roof(0), 1.6f);
            foreach (float z in new[] { -3.2f, 3.2f }) arch.Box(new Vector3(104f, 4.55f, z), new Vector3(0.22f, 0.9f, 0.22f), Mats.WoodDark, 1f, pitch: z > 0 ? -38f : 38f);
            BakeBuilding(arch, "SM_World_MarketArch", "Market Arch", g, Vector3.zero, 0f);
            Solid("ArchPillarN", g, new Vector3(104f, 2.5f, 4.2f), new Vector3(1.2f, 5f, 1.2f), Quaternion.identity);
            Solid("ArchPillarS", g, new Vector3(104f, 2.5f, -4.2f), new Vector3(1.2f, 5f, 1.2f), Quaternion.identity);
            SignBoard("MUDDLEBURY\nMARKET", new Vector3(104f, 4.35f, 0f), -90f, 5.0f, 1.25f, g, Mats.SignBoard, WorldAssets.Hex("2B2118"), twoSided: true, thickness: 0.14f);
            Place(FT, "banner-green", new Vector3(104.7f, 0, 5.2f), 90f, 4.0f, g);
            Place(FT, "banner-green", new Vector3(104.7f, 0, -5.2f), 90f, 4.0f, g);

            // --- Sale pen 8 x 8, open toward the bridge (west). Gameplay trigger unchanged.
            var pen = Group("Sale Pen", g);
            FenceRect(SalePen, 8f, 8f, pen, (3, 4f, 8f));
            var straw = Block("Straw", SalePen + new Vector3(0, 0.02f, 0), new Vector3(7.6f, 0.04f, 7.6f), Mats.Straw, pen, collider: false);
            straw.transform.position = new Vector3(SalePen.x, 0.03f, SalePen.z);
            var penMarker = Marker("MarketPen", SalePen, pen);
            var trig = penMarker.AddComponent<BoxCollider>(); trig.isTrigger = true; trig.size = new Vector3(8f, 3f, 8f); trig.center = new Vector3(0, 1.5f, 0);
            penMarker.layer = LayerMask.NameToLayer(GameLayers.Interactable);
            AddMarker(penMarker, MarkerRole.MarketPen);
            SignPost("SALE PEN", SalePen + new Vector3(-4.6f, 0f, 4.4f), -90f, 1.6f, 0.55f, pen, Mats.SignBoardDark, WorldAssets.Hex("F4E9CF"), postHeight: 1.5f);
            Place(SV, "bucket", SalePen + new Vector3(3.2f, 0, -3.2f), 0f, 0.45f, pen);
            Place(FD, "bag", SalePen + new Vector3(3.3f, 0, 3.0f), 30f, 0.7f, pen);

            // --- Stalls: the three Tripo hero stalls (Butcher N, Greengrocer E, Oddities S) + four Kenney stalls.
            var stallSpecs = new (string model, Vector3 pos, float yaw, string name, string goods, string sign)[]
            {
                ("stall", new Vector3(130f, 0, 22f), 180f, "Stall_Butcher", "meat-ribs", "BUTCHER"),
                ("stall-green", new Vector3(150f, 0, 0f), -90f, "Stall_Greengrocer", "cabbage", "GREENGROCER"),
                ("stall-red", new Vector3(130f, 0, -22f), 0f, "Stall_Oddities", "pumpkin-basic", "ODDITIES"),
            };
            foreach (var s in stallSpecs)
            {
                var st = TripoStall(s.name, s.pos, s.yaw, g) ?? Place(FT, s.model, s.pos, s.yaw, 3.2f, g, "box");
                if (st != null) st.name = s.name;
                var fwd = Quaternion.Euler(0, s.yaw, 0) * Vector3.forward;
                for (int i = 0; i < 3; i++)
                    Place(FD, s.goods == "meat-ribs" ? (i == 0 ? "barrel" : "bag") : s.goods, s.pos + Quaternion.Euler(0, s.yaw, 0) * new Vector3(-0.8f + i * 0.8f, 0, 1.6f), Rnd(0, 360), 0.45f, props);
                SignPost(s.sign, s.pos + fwd * 2.3f + Quaternion.Euler(0, s.yaw, 0) * new Vector3(1.9f, 0, 0), s.yaw, 1.7f, 0.5f, g, Mats.SignBoard, WorldAssets.Hex("2B2118"), postHeight: 1.7f, twoSided: true);
            }
            var extra = new (string model, Vector3 pos, float yaw, string[] goods)[]
            {
                ("stall-green", new Vector3(120f, 0, 19f), 180f, new[] { "carrot", "leek", "onion" }),
                ("stall-red", new Vector3(144f, 0, -19f), 0f, new[] { "tomato", "apple", "beet" }),
                ("stall-green", new Vector3(157f, 0, 17f), -135f, new[] { "watermelon", "corn", "cabbage" }),
                ("stall-red", new Vector3(157f, 0, -17f), -45f, new[] { "pumpkin", "apple", "carrot" }),
            };
            foreach (var e in extra)
            {
                Place(FT, e.model, e.pos, e.yaw, 2.9f, g, "box");
                var rot = Quaternion.Euler(0, e.yaw, 0);
                for (int i = 0; i < 3; i++)
                {
                    var crate = Place(SV, "box-open", e.pos + rot * new Vector3(-0.9f + i * 0.9f, 0, 1.75f), e.yaw + Rnd(-8, 8), 0.5f, props);
                    if (crate != null) Place(FD, e.goods[i], crate.transform.position + Vector3.up * 0.32f, Rnd(0, 360), 0.3f, props, snap: false);
                }
            }

            // --- Town bell, central in the square (GDD hotspot 4).
            var bell = new WorldMeshBuilder();
            var bp = new Vector3(134f, 0, 0f);
            bell.Box(bp + new Vector3(0, 0.15f, 0), new Vector3(2.2f, 0.3f, 3.6f), Mats.Stone, 1.2f);
            foreach (float z in new[] { -1.15f, 1.15f }) bell.Box(bp + new Vector3(0, 2.3f, z), new Vector3(0.36f, 4.3f, 0.36f), Mats.WoodDark, 1f);
            bell.Box(bp + new Vector3(0, 4.45f, 0), new Vector3(0.42f, 0.42f, 2.9f), Mats.WoodDark, 1f);
            bell.Push(bp + new Vector3(0, 4.66f, 0), Quaternion.Euler(0f, 90f, 0f));
            Roof(bell, 1.7f, 3.0f, 0.8f, Mats.Roof(0), Mats.WoodMid);
            bell.Pop();
            bell.Cylinder(bp + new Vector3(0, 3.05f, 0), 0.78f, 0.36f, 1.05f, 12, Mats.Bronze, Mats.Bronze);
            bell.Cylinder(bp + new Vector3(0, 4.1f, 0), 0.22f, 0.22f, 0.24f, 8, Mats.Bronze, Mats.Bronze);
            BakeBuilding(bell, "SM_World_TownBell", "Town Bell", g, Vector3.zero, 0f);
            Solid("BellBase", g, bp + new Vector3(0, 0.15f, 0), new Vector3(2.2f, 0.3f, 3.6f), Quaternion.identity);
            foreach (float z in new[] { -1.15f, 1.15f }) Solid("BellPost", g, bp + new Vector3(0, 2.3f, z), new Vector3(0.36f, 4.3f, 0.36f), Quaternion.identity);

            // --- Houses ring the square (fronts face the cobbles), a second row behind, and the town hall on the road axis.
            string[] shopsN = { "BAKERY", "TEA ROOM", "HATS & BONNETS", "SEEDS", "POST", "INN" };
            string[] shopsS = { "TAILOR", "CHEESE", "TOOLS", "BOOKS", "ODDS & ENDS", "BANK" };
            float[] rowX = { 113f, 122f, 131f, 140f, 149f, 158f };
            for (int i = 0; i < rowX.Length; i++)
            {
                var sn = new HouseSpec { W = 8.2f, D = 7.5f, H1 = 3.1f, H2 = i % 3 == 1 ? 0f : 2.7f, Wall = i, Upper = i % 2 == 0 ? 6 : -1, Roof = i * 2 + 1, Shutter = i, GableFront = i % 2 == 1, Shop = shopsN[i], Awning = i, FrontWindows = 2, Pitch = i % 2 == 1 ? 0.95f : 0.7f };
                if (sn.H2 == 0f) { sn.H1 = 3.6f; sn.Pitch = 1.1f; sn.GableFront = true; }
                PlaceHouse($"House_N{i}", sn, new Vector3(rowX[i], 0, 38.5f), 90f, g, flowers);
                bool bank = shopsS[i] == "BANK";
                var ss = new HouseSpec { W = 8.2f, D = 7.5f, H1 = 3.1f, H2 = i % 3 == 2 ? 0f : 2.7f, Wall = bank ? 5 : i + 3, Upper = i % 2 == 1 ? 6 : -1, Roof = bank ? 1 : i * 2 + 4, Shutter = bank ? 2 : i + 2, GableFront = i % 2 == 0, Shop = bank ? "BANK  -  Loans & Regrets" : shopsS[i], Awning = bank ? -1 : i + 1, FrontWindows = 2, StoneGround = bank, Pitch = i % 2 == 0 ? 0.95f : 0.7f };
                if (ss.H2 == 0f) { ss.H1 = 3.6f; ss.Pitch = 1.1f; ss.GableFront = true; }
                PlaceHouse($"House_S{i}", ss, new Vector3(rowX[i], 0, -38.5f), -90f, g, flowers);
            }
            // East row either side of the town hall, fronts facing west.
            float[] eastZ = { 23f, 13.5f, -13.5f, -23f };
            for (int i = 0; i < eastZ.Length; i++)
            {
                var se = new HouseSpec { W = 8.4f, D = 8f, H1 = 3.1f, H2 = 2.7f, Wall = i + 1, Upper = 6, Roof = i + 2, Shutter = i + 3, GableFront = i % 2 == 0, FrontWindows = 2, Shop = i == 1 ? "APOTHECARY" : i == 2 ? "SADDLERY" : null, Awning = i == 1 ? 2 : i == 2 ? 3 : -1 };
                PlaceHouse($"House_E{i}", se, new Vector3(171.5f, 0, eastZ[i]), 180f, g, flowers);
            }
            // Riverside houses either side of the arch, fronts facing east into the square (frame the gate).
            foreach (var (z, idx) in new[] { (17f, 0), (26.5f, 1), (-17f, 2), (-26.5f, 3) })
            {
                var sr = new HouseSpec { W = 8f, D = 6.5f, H1 = 3f, H2 = idx % 2 == 0 ? 2.6f : 0f, Wall = idx + 4, Upper = 6, Roof = idx + 3, Shutter = idx + 1, GableFront = idx % 2 == 1, FrontWindows = 2, Pitch = 0.9f };
                if (sr.H2 == 0f) { sr.H1 = 3.4f; sr.Pitch = 1.05f; }
                PlaceHouse($"House_W{idx}", sr, new Vector3(107f, 0, z), 0f, g, flowers);
            }
            TownHall(g);
            // Second rows (north and south lanes): plainer cottages and gardens so the square is a town, not a stage set.
            int c = 0;
            foreach (float zr in new[] { 58f, -58f })
                for (float x = 114f; x < 166f; x += Rnd(10.5f, 13f), c++)
                {
                    if (Mathf.Abs(x - 136f) < 6f) continue; // the lane
                    var sc = new HouseSpec { W = Rnd(6.5f, 8.5f), D = Rnd(6f, 7f), H1 = 3f, H2 = c % 3 == 0 ? 2.5f : 0f, Wall = c + 2, Upper = 6, Roof = c, Shutter = c, GableFront = c % 2 == 0, FrontWindows = c % 2 == 0 ? 1 : 2, Jetty = false, Timber = c % 3 == 0, Pitch = Rnd(0.85f, 1.1f) };
                    PlaceHouse($"Cottage_{c}", sc, new Vector3(x, 0, zr + Rnd(-1.5f, 1.5f)), zr > 0 ? 90f : -90f, g, null);
                    Place(NF, Pick("tree_default", "tree_oak", "tree_detailed"), new Vector3(x + Rnd(-3f, 3f), 0, zr + (zr > 0 ? 9f : -9f)), Rnd(0, 360), Rnd(6f, 8f), g, "trunk");
                }

            // --- Square dressing: fountain, lamps, benches, planters, carts, barrels, notice board, bunting.
            Place(FT, "fountain-round", new Vector3(146f, 0, 22f), 0f, 0.75f, g, "box");
            Place(FT, "fountain-center", new Vector3(146f, 0.75f, 22f), 0f, 1.6f, g);
            foreach (var lp in new[] { new Vector3(110f, 0, 12f), new Vector3(110f, 0, -12f), new Vector3(160f, 0, 9f), new Vector3(160f, 0, -9f), new Vector3(124f, 0, -7f), new Vector3(144f, 0, 7f), new Vector3(136f, 0, 31f), new Vector3(136f, 0, -31f) })
                Lamp(lp, g);
            foreach (var bp2 in new[] { (new Vector3(141f, 0, 25.5f), 90f), (new Vector3(151f, 0, 25.5f), 90f), (new Vector3(124f, 0, 28f), 0f), (new Vector3(146f, 0, -28f), 180f) })
                Place(FT, "stall-bench", bp2.Item1, bp2.Item2, 0.55f, props, "box");
            foreach (var pp in new[] { new Vector3(108.5f, 0, 31f), new Vector3(108.5f, 0, -31f), new Vector3(164f, 0, 30f), new Vector3(164f, 0, -30f), new Vector3(126f, 0, 10f), new Vector3(126f, 0, -10f) })
                Planter(pp, g);
            Place(FT, "cart", new Vector3(160f, 0, -27f), 30f, 1.8f, g, "box");
            Place(FT, "cart-high", new Vector3(116f, 0, 28.5f), -70f, 1.9f, g, "box");
            Place(SV, "barrel", new Vector3(141f, 0, 18f), 0f, 1.1f, props, "box");
            Place(SV, "barrel", new Vector3(141.8f, 0, 18.7f), 40f, 1.1f, props, "box");
            Place(SV, "box-large", new Vector3(122f, 0, -18f), 15f, 0.9f, props, "box");
            Place(SV, "box", new Vector3(122.7f, 0, -18.9f), 40f, 0.6f, props, "box");
            Place(SV, "box", new Vector3(122.3f, 0.6f, -18.5f), 70f, 0.5f, props);
            var nb = new WorldMeshBuilder();
            nb.Box(new Vector3(0, 1.15f, 0), new Vector3(0.12f, 1.2f, 2.0f), Mats.WoodMid, 1f);
            foreach (float z in new[] { -0.95f, 0.95f }) nb.Box(new Vector3(0, 0.95f, z), new Vector3(0.14f, 1.9f, 0.14f), Mats.WoodDark, 1f);
            nb.Box(new Vector3(0, 1.98f, 0), new Vector3(0.5f, 0.1f, 2.3f), Mats.Roof(2), 1.6f, roll: 0f);
            for (int i = 0; i < 5; i++) nb.Box(new Vector3(-0.07f, 0.9f + (i % 2) * 0.42f, -0.65f + i * 0.33f), new Vector3(0.01f, 0.32f, 0.26f), Mats.Laundry, 1f, roll: Rnd(-6f, 6f));
            BakeBuilding(nb, "SM_World_NoticeBoard", "Notice Board", g, new Vector3(119f, GroundY(119f, -9f), -9f), 0f);
            SignBoard("TODAY'S ORDERS", new Vector3(118.86f, GroundY(119f, -9f) + 2.32f, -9f), -90f, 2.2f, 0.42f, g, Mats.SignBoard, WorldAssets.Hex("2B2118"), twoSided: true);
            Solid("NoticeBoard", g, new Vector3(119f, 1f, -9f), new Vector3(0.3f, 2f, 2f), Quaternion.identity);
            Bunting(g);
        }

        static void Lamp(Vector3 p, Transform parent)
        {
            var b = new WorldMeshBuilder();
            b.Cylinder(Vector3.zero, 0.2f, 0.16f, 0.35f, 8, Mats.StoneDark, Mats.StoneDark);
            b.Cylinder(new Vector3(0, 0.35f, 0), 0.07f, 0.06f, 2.85f, 8, Mats.Iron, Mats.Iron);
            b.Box(new Vector3(0, 3.35f, 0), new Vector3(0.36f, 0.5f, 0.36f), Mats.LampDay, 1f);
            b.Cylinder(new Vector3(0, 3.6f, 0), 0.3f, 0.0f, 0.3f, 4, Mats.Iron, null, phase: Mathf.PI * 0.25f);
            b.Box(new Vector3(0, 3.1f, 0), new Vector3(0.44f, 0.06f, 0.44f), Mats.Iron, 1f);
            foreach (float s in new[] { -1f, 1f }) { b.Box(new Vector3(s * 0.19f, 3.35f, 0f), new Vector3(0.03f, 0.5f, 0.38f), Mats.Iron, 1f); b.Box(new Vector3(0f, 3.35f, s * 0.19f), new Vector3(0.38f, 0.5f, 0.03f), Mats.Iron, 1f); }
            var go = BakeBuilding(b, "SM_World_Lamp", "Lamp", parent, new Vector3(p.x, GroundY(p.x, p.z), p.z), 0f);
            Solid("Collider", go.transform, new Vector3(0, 1.5f, 0), new Vector3(0.3f, 3f, 0.3f), Quaternion.identity);
        }

        static void Planter(Vector3 p, Transform parent)
        {
            var b = new WorldMeshBuilder();
            b.Box(new Vector3(0, 0.3f, 0), new Vector3(1.6f, 0.6f, 1.6f), Mats.Stone, 1.2f);
            b.Box(new Vector3(0, 0.58f, 0), new Vector3(1.4f, 0.06f, 1.4f), WorldAssets.Mat("Soil", WorldAssets.Hex("6E5034")), 1f);
            var go = BakeBuilding(b, "SM_World_Planter", "Planter", parent, new Vector3(p.x, GroundY(p.x, p.z), p.z), 0f);
            Solid("Collider", go.transform, new Vector3(0, 0.3f, 0), new Vector3(1.6f, 0.6f, 1.6f), Quaternion.identity);
            Place(NF, Pick("tree_detailed", "tree_default", "tree_plateau"), p + Vector3.up * 0.6f, Rnd(0, 360), Rnd(3.2f, 4.2f), go.transform);
        }

        /// <summary>The town hall + clock tower on the road axis: the market's skyline anchor seen from the spawn (GDD 11.6 rule 15).</summary>
        static void TownHall(Transform parent)
        {
            var spec = new HouseSpec { W = 14f, D = 10f, H1 = 3.8f, H2 = 3.2f, Wall = 2, Upper = 6, Roof = 2, Shutter = 3, GableFront = false, StoneGround = true, Jetty = false, Timber = true, FrontWindows = 4, Chimney = false, FlowerBoxes = true, Pitch = 0.8f };
            var hall = PlaceHouse("TownHall", spec, new Vector3(172f, 0, 0f), 180f, parent, null);
            // Clock tower standing in front of the hall's west facade.
            var b = new WorldMeshBuilder();
            float tw = 4.4f, tH = 15.5f;
            b.Box(new Vector3(0, 0.3f, 0), new Vector3(tw + 0.5f, 0.6f, tw + 0.5f), Mats.StoneDark, 1.2f, skip: 4);
            b.Box(new Vector3(0, tH * 0.5f, 0), new Vector3(tw, tH, tw), Mats.Stone, 1.4f, skip: 4);
            for (int i = 1; i <= 3; i++) b.Box(new Vector3(0, i * 4.6f, 0), new Vector3(tw + 0.24f, 0.22f, tw + 0.24f), Mats.StoneDark, 1.2f);
            // belfry: four corner piers and a balustrade, bell inside
            float by = tH, bh = 3.4f;
            foreach (float sx in new[] { -1f, 1f })
            foreach (float sz in new[] { -1f, 1f })
                b.Box(new Vector3(sx * (tw * 0.5f - 0.4f), by + bh * 0.5f, sz * (tw * 0.5f - 0.4f)), new Vector3(0.8f, bh, 0.8f), Mats.Stone, 1.4f);
            b.Box(new Vector3(0, by + 0.45f, 0), new Vector3(tw - 0.6f, 0.9f, tw - 0.6f), Mats.Stone, 1.4f, skip: 4);
            b.Box(new Vector3(0, by + bh + 0.2f, 0), new Vector3(tw + 0.4f, 0.4f, tw + 0.4f), Mats.StoneDark, 1.2f);
            b.Cylinder(new Vector3(0, by + 1.2f, 0), 0.9f, 0.45f, 1.3f, 12, Mats.Bronze, Mats.Bronze);
            // pyramid spire + finial
            b.Cylinder(new Vector3(0, by + bh + 0.4f, 0), tw * 0.5f * 1.48f, 0f, 6.5f, 4, Mats.Roof(2), null, phase: Mathf.PI * 0.25f);
            b.Cylinder(new Vector3(0, by + bh + 6.8f, 0), 0.05f, 0.05f, 1.6f, 6, Mats.Iron, Mats.Iron);
            b.Box(new Vector3(0, by + bh + 7.9f, 0), new Vector3(0.05f, 0.05f, 0.9f), Mats.Iron, 1f);
            b.Box(new Vector3(0.3f, by + bh + 7.95f, 0), new Vector3(0.6f, 0.35f, 0.04f), Mats.Iron, 1f);
            // clock faces on all four sides (hands at ten to five: sundown nerves)
            foreach (var n in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            {
                b.Push(new Vector3(0, 12.4f, 0) + n * (tw * 0.5f + 0.06f), Quaternion.LookRotation(n));
                b.Push(Vector3.zero, Quaternion.Euler(90f, 0f, 0f)); // cylinder axis Y -> facade normal
                b.Cylinder(new Vector3(0, -0.07f, 0), 1.55f, 1.55f, 0.12f, 20, Mats.StoneDark, Mats.StoneDark);
                b.Pop();
                b.Quad(new Vector3(1.35f, -1.35f, 0.06f), new Vector3(-1.35f, -1.35f, 0.06f), new Vector3(-1.35f, 1.35f, 0.06f), new Vector3(1.35f, 1.35f, 0.06f), Vector3.forward, Mats.ClockFace, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1));
                // viewer's clockwise = local -X: a hand at clock angle a points (-sin a, cos a)
                b.Box(new Vector3(-0.27f, -0.39f, 0.09f), new Vector3(0.1f, 0.95f, 0.03f), Mats.Gold, 1f, roll: 145f); // hour hand: ten to five
                b.Box(new Vector3(0.476f, 0.275f, 0.11f), new Vector3(0.07f, 1.1f, 0.03f), Mats.Gold, 1f, roll: -60f); // minute hand
                b.Pop();
            }
            // arched door with lettering above
            b.Push(new Vector3(tw * 0.5f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
            b.Box(new Vector3(0, 1.3f, 0.04f), new Vector3(1.6f, 2.6f, 0.08f), Mats.DoorWood, 1f);
            b.Box(new Vector3(0, 2.7f, 0.08f), new Vector3(2.0f, 0.2f, 0.16f), Mats.StoneDark, 1f);
            b.Pop();
            Window(b, new Vector3(tw * 0.5f, 7.5f, 0f), Vector3.right, 0.9f, 1.6f, null, false, null);
            Window(b, new Vector3(0f, 7.5f, tw * 0.5f), Vector3.forward, 0.9f, 1.6f, null, false, null);
            Window(b, new Vector3(0f, 7.5f, -tw * 0.5f), Vector3.back, 0.9f, 1.6f, null, false, null);
            var tower = BakeBuilding(b, "SM_World_ClockTower", "Clock Tower", hall.transform.parent, new Vector3(164.6f, GroundY(164.6f, 0f), 0f), 0f);
            Solid("Collider", tower.transform, new Vector3(0, tH * 0.5f, 0), new Vector3(tw, tH, tw), Quaternion.identity);
            SignBoard("TOWN HALL", tower.transform.position + new Vector3(tw * 0.5f + 0.1f, 3.25f, 0f), 90f, 2.6f, 0.55f, hall.transform.parent, Mats.SignBoardDark, WorldAssets.Hex("F4E9CF"));
        }

        /// <summary>Strings of pastel bunting over the square (one combined mesh, no shadows).</summary>
        static void Bunting(Transform parent)
        {
            var b = new WorldMeshBuilder();
            void String(Vector3 a, Vector3 c, float sag)
            {
                float len = (c - a).magnitude;
                int segs = Mathf.Max(6, Mathf.RoundToInt(len / 1.5f));
                Vector3 P(float t) => Vector3.Lerp(a, c, t) + Vector3.down * (sag * 4f * t * (1f - t));
                for (int i = 0; i < segs; i++)
                {
                    Vector3 p0 = P(i / (float)segs), p1 = P((i + 1) / (float)segs), mid = (p0 + p1) * 0.5f, d = p1 - p0;
                    b.Push(mid, Quaternion.LookRotation(d.normalized, Vector3.up));
                    b.Box(Vector3.zero, new Vector3(0.03f, 0.03f, d.magnitude), Mats.Rope, 1f);
                    b.Pop();
                }
                int flags = Mathf.RoundToInt(len / 0.85f);
                var across = Vector3.Cross(Vector3.up, (c - a).normalized).normalized;
                for (int i = 1; i < flags; i++)
                {
                    float t = i / (float)flags;
                    Vector3 top = P(t), dir = (P(t + 0.01f) - P(t - 0.01f)).normalized;
                    Vector3 l = top - dir * 0.22f, r = top + dir * 0.22f, tip = top + Vector3.down * 0.42f + across * 0.02f;
                    float u = ((i * 7 + Mathf.RoundToInt(a.x)) % 6 + 0.5f) / 6f;
                    var uv = new Vector2(u, 0.5f);
                    b.Tri(l, r, tip, across, Mats.Bunting, uv, uv, uv);
                    b.Tri(r, l, tip, -across, Mats.Bunting, uv, uv, uv);
                }
            }
            // along both house rows, and from the bell's roof out to the square's corners
            for (float x = 111f; x < 160f; x += 9f)
            {
                String(new Vector3(x, 5.6f, 34.3f), new Vector3(x + 9f, 5.6f, 34.3f), 0.8f);
                String(new Vector3(x, 5.6f, -34.3f), new Vector3(x + 9f, 5.6f, -34.3f), 0.8f);
            }
            foreach (var corner in new[] { new Vector3(117f, 6.2f, 34.3f), new Vector3(153f, 6.2f, 34.3f), new Vector3(117f, 6.2f, -34.3f), new Vector3(153f, 6.2f, -34.3f), new Vector3(104f, 6f, 4.6f), new Vector3(104f, 6f, -4.6f) })
                String(new Vector3(134f, 5.4f, 0f), corner, 1.8f);
            b.Bake("SM_World_Bunting", "Bunting", parent, Vector3.zero, Quaternion.identity, ShadowCastingMode.Off);
        }
    }
}
