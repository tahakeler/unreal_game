using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Random = System.Random;

namespace HarvestPanic.World.Editor
{
    public static partial class BuildMuddlebury
    {
        static readonly Color Ink = new Color(0.17f, 0.13f, 0.09f), Cream = new Color(0.96f, 0.91f, 0.81f);

        /// <summary>Rectangular straw bale with twine (into a combined mesh) plus its collider.</summary>
        static void HayBale(WorldMeshBuilder b, Transform colliderParent, Vector3 centre, float yaw, Vector3? size = null)
        {
            var s = size ?? new Vector3(1.2f, 0.9f, 1.8f);
            b.Box(centre, s, Mats.Hay, 1f, yaw);
            var r = Quaternion.Euler(0f, yaw, 0f);
            foreach (float t in new[] { -0.28f, 0.28f })
                b.Box(centre + r * new Vector3(0f, 0f, t * s.z), new Vector3(s.x + 0.02f, s.y + 0.02f, 0.05f), Mats.WoodDark, 1f, yaw);
            if (colliderParent != null) Solid("HayBale", colliderParent, centre, s, r);
        }

        /// <summary>Round bale lying on its side (axis along local X after yaw).</summary>
        static void RoundBale(WorldMeshBuilder b, Transform colliderParent, Vector3 groundCentre, float yaw, float radius = 0.85f, float length = 1.3f)
        {
            b.Push(groundCentre + Vector3.up * radius, Quaternion.Euler(0f, yaw, 90f));
            b.Cylinder(new Vector3(0f, -length * 0.5f, 0f), radius, radius, length, 14, Mats.Hay, Mats.HayEnd, 1f, capTop: true, capBottom: true);
            b.Pop();
            if (colliderParent != null) Solid("RoundBale", colliderParent, groundCentre + Vector3.up * radius, new Vector3(length, radius * 1.8f, radius * 1.8f), Quaternion.Euler(0f, yaw, 0f));
        }

        // ------------------------------------------------------------------ barn (hollow, with interior and the wardrobe corner)

        static void BarnArea()
        {
            var g = Group("Barn");
            var b = new WorldMeshBuilder();
            const float x0 = -35f, x1 = -17f, z0 = -7f, z1 = 7f, t = 0.3f, H = 6f, doorW = 5.4f, doorH = 5.2f;
            float w = z1 - z0, d = x1 - x0, cx = (x0 + x1) * 0.5f;
            var wall = Mats.BarnWall;

            // shell: side walls, west gable, east gable with the big doorway, gambrel gable tops
            b.Box(new Vector3(cx, H * 0.5f, z1 - t * 0.5f), new Vector3(d, H, t), wall, 2.4f);
            b.Box(new Vector3(cx, H * 0.5f, z0 + t * 0.5f), new Vector3(d, H, t), wall, 2.4f);
            b.Box(new Vector3(x0 + t * 0.5f, H * 0.5f, 0f), new Vector3(t, H, w - 2f * t), wall, 2.4f);
            float pw = (z1 - t) - doorW * 0.5f, pz = (doorW * 0.5f + (z1 - t)) * 0.5f;
            b.Box(new Vector3(x1 - t * 0.5f, H * 0.5f, pz), new Vector3(t, H, pw), wall, 2.4f);
            b.Box(new Vector3(x1 - t * 0.5f, H * 0.5f, -pz), new Vector3(t, H, pw), wall, 2.4f);
            b.Box(new Vector3(x1 - t * 0.5f, (doorH + H) * 0.5f, 0f), new Vector3(t, H - doorH, doorW), wall, 2.4f);
            Solid("WallN", g, new Vector3(cx, H * 0.5f, z1 - t * 0.5f), new Vector3(d, H, t), Quaternion.identity);
            Solid("WallS", g, new Vector3(cx, H * 0.5f, z0 + t * 0.5f), new Vector3(d, H, t), Quaternion.identity);
            Solid("WallW", g, new Vector3(x0 + t * 0.5f, H * 0.5f, 0f), new Vector3(t, H, w), Quaternion.identity);
            Solid("WallE_N", g, new Vector3(x1 - t * 0.5f, H * 0.5f, pz), new Vector3(t, H, pw), Quaternion.identity);
            Solid("WallE_S", g, new Vector3(x1 - t * 0.5f, H * 0.5f, -pz), new Vector3(t, H, pw), Quaternion.identity);
            Solid("Lintel", g, new Vector3(x1 - t * 0.5f, (doorH + H) * 0.5f, 0f), new Vector3(t, H - doorH, doorW), Quaternion.identity);
            var prof = new[] { new Vector2(-w * 0.5f, H), new Vector2(w * 0.5f, H), new Vector2(w * 0.5f - 2.6f, H + 3.2f), new Vector2(0f, H + 4.6f), new Vector2(-w * 0.5f + 2.6f, H + 3.2f) };
            foreach (float gx in new[] { x0 + t * 0.5f, x1 - t * 0.5f })
            {
                b.Push(new Vector3(gx, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
                b.Prism(prof, t, wall, wall, 2.4f);
                b.Pop();
            }
            // gambrel roof: four shingled slopes, barge boards, ridge cap
            Vector2[] roofPts = { new Vector2(w * 0.5f + 0.5f, H - 0.3f), new Vector2(w * 0.5f - 2.6f, H + 3.35f), new Vector2(0f, H + 4.85f), new Vector2(-w * 0.5f + 2.6f, H + 3.35f), new Vector2(-w * 0.5f - 0.5f, H - 0.3f) };
            for (int i = 0; i < roofPts.Length - 1; i++)
            {
                Vector2 a = roofPts[i], c = roofPts[i + 1];
                Vector2 mid = (a + c) * 0.5f; float len = (c - a).magnitude + 0.25f;
                float ang = Mathf.Atan2(c.y - a.y, c.x - a.x) * Mathf.Rad2Deg;
                b.Box(new Vector3(cx, mid.y, -mid.x), new Vector3(d + 1.2f, 0.3f, len), Mats.BarnRoof, 1.6f, pitch: ang);
                foreach (float bx in new[] { x0 - 0.62f, x1 + 0.62f })
                    b.Box(new Vector3(bx, mid.y - 0.04f, -mid.x), new Vector3(0.09f, 0.42f, len), Mats.Trim, 1f, pitch: ang);
            }
            b.Box(new Vector3(cx, H + 5.02f, 0f), new Vector3(d + 1.3f, 0.2f, 0.55f), Mats.WoodDark, 1f);
            // white trim: corners, eaves, door frame, loft door
            foreach (float tx in new[] { x0, x1 })
            foreach (float cz in new[] { z0, z1 })
                b.Box(new Vector3(tx, H * 0.5f, cz), new Vector3(0.36f, H, 0.36f), Mats.Trim, 1f);
            foreach (float ez in new[] { z0 - 0.04f, z1 + 0.04f }) b.Box(new Vector3(cx, H - 0.12f, ez), new Vector3(d + 0.1f, 0.24f, 0.1f), Mats.Trim, 1f);
            b.Box(new Vector3(x1 + 0.06f, doorH + 0.16f, 0f), new Vector3(0.2f, 0.32f, doorW + 0.62f), Mats.Trim, 1f);
            foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(x1 + 0.06f, (doorH + 0.32f) * 0.5f, s * (doorW * 0.5f + 0.15f)), new Vector3(0.2f, doorH + 0.32f, 0.3f), Mats.Trim, 1f);
            b.Box(new Vector3(x1 + 0.03f, 8.1f, 0f), new Vector3(0.06f, 2.1f, 2.3f), Mats.WoodDark, 1f);
            b.Box(new Vector3(x1 + 0.05f, 8.1f, 0f), new Vector3(0.08f, 2.4f, 2.6f), Mats.Trim, 1f, skip: 2);
            b.Box(new Vector3(x1 + 1.1f, H + 4.1f, 0f), new Vector3(2.3f, 0.26f, 0.26f), Mats.WoodDark, 1f); // hay hoist beam
            b.Cylinder(new Vector3(x1 + 2.1f, H + 3.6f, 0f), 0.05f, 0.05f, 0.5f, 6, Mats.Rope, Mats.Rope);
            // open door leaves, swung out flat against nothing (perpendicular to the gable), board + X brace
            foreach (float s in new[] { -1f, 1f })
            {
                var lc = new Vector3(x1 + 1.4f, doorH * 0.5f, s * (doorW * 0.5f + 0.12f));
                b.Box(lc, new Vector3(2.7f, doorH - 0.1f, 0.12f), wall, 2.4f);
                foreach (float f in new[] { -1f, 1f })
                {
                    var o = new Vector3(0f, 0f, f * 0.08f);
                    b.Box(lc + o + new Vector3(0f, doorH * 0.5f - 0.15f, 0f), new Vector3(2.7f, 0.2f, 0.05f), Mats.Trim, 1f);
                    b.Box(lc + o - new Vector3(0f, doorH * 0.5f - 0.15f, 0f), new Vector3(2.7f, 0.2f, 0.05f), Mats.Trim, 1f);
                    b.Box(lc + o + new Vector3(1.25f, 0f, 0f), new Vector3(0.2f, doorH - 0.1f, 0.05f), Mats.Trim, 1f);
                    b.Box(lc + o - new Vector3(1.25f, 0f, 0f), new Vector3(0.2f, doorH - 0.1f, 0.05f), Mats.Trim, 1f);
                    float dl = Mathf.Sqrt(2.5f * 2.5f + 4.8f * 4.8f), da = Mathf.Atan2(2.5f, 4.8f) * Mathf.Rad2Deg;
                    b.Box(lc + o, new Vector3(0.18f, dl, 0.05f), Mats.Trim, 1f, roll: da);
                    b.Box(lc + o, new Vector3(0.18f, dl, 0.05f), Mats.Trim, 1f, roll: -da);
                }
                Solid("DoorLeaf", g, lc, new Vector3(2.7f, doorH, 0.15f), Quaternion.identity);
            }
            // stone footing band outside
            b.Box(new Vector3(cx, 0.2f, z1 + 0.1f), new Vector3(d + 0.4f, 0.4f, 0.2f), Mats.StoneDark, 1.2f);
            b.Box(new Vector3(cx, 0.2f, z0 - 0.1f), new Vector3(d + 0.4f, 0.4f, 0.2f), Mats.StoneDark, 1.2f);
            b.Box(new Vector3(x0 - 0.1f, 0.2f, 0f), new Vector3(0.2f, 0.4f, w), Mats.StoneDark, 1.2f);
            foreach (float s in new[] { -1f, 1f }) b.Box(new Vector3(x1 + 0.1f, 0.2f, s * (doorW * 0.5f + pw * 0.5f + 0.15f)), new Vector3(0.2f, 0.4f, pw + 0.3f), Mats.StoneDark, 1.2f);
            // windows on both long walls, outside and inside
            // windows between the frame posts (x -31.5 / -27 / -22.5), clear of the loft edge and the wardrobe corner
            foreach (float wx in new[] { -28.4f, -25.0f, -20.3f })
            {
                Window(b, new Vector3(wx, 3.4f, z0), Vector3.back, 1.1f, 1.0f, null, false, null);
                Window(b, new Vector3(wx, 3.4f, z0 + t), Vector3.forward, 1.1f, 1.0f, null, false, null);
                if (wx > -21f) continue; // north wall: wardrobe corner + sign
                Window(b, new Vector3(wx, 3.4f, z1), Vector3.forward, 1.1f, 1.0f, null, false, null);
                Window(b, new Vector3(wx, 3.4f, z1 - t), Vector3.back, 1.1f, 1.0f, null, false, null);
            }
            // weather vane: rooster on the ridge
            float vy = H + 5.1f;
            b.Cylinder(new Vector3(cx, vy, 0f), 0.04f, 0.04f, 1.5f, 6, Mats.Iron, Mats.Iron);
            b.Box(new Vector3(cx, vy + 1.0f, 0f), new Vector3(1.1f, 0.05f, 0.05f), Mats.Iron, 1f);
            b.Box(new Vector3(cx + 0.05f, vy + 1.55f, 0f), new Vector3(0.6f, 0.34f, 0.04f), Mats.Iron, 1f);
            b.Box(new Vector3(cx + 0.38f, vy + 1.8f, 0f), new Vector3(0.18f, 0.24f, 0.04f), Mats.Iron, 1f);
            b.Box(new Vector3(cx - 0.32f, vy + 1.75f, 0f), new Vector3(0.22f, 0.36f, 0.04f), Mats.Iron, 1f, roll: 25f);

            // ---- interior: floor, frame, hay loft, ladder, stalls, wardrobe corner, lanterns
            b.Box(new Vector3(cx, 0.025f, 0f), new Vector3(d - 2f * t, 0.05f, w - 2f * t), Mats.Floor, 1.2f, skip: 4);
            foreach (float xb in new[] { -31.5f, -27f, -22.5f })
            {
                b.Box(new Vector3(xb, H - 0.25f, 0f), new Vector3(0.3f, 0.3f, w - 2f * t), Mats.WoodDark, 1f);
                foreach (float s in new[] { -1f, 1f })
                    b.Box(new Vector3(xb, H * 0.5f, s * (z1 - t - 0.16f)), new Vector3(0.3f, H, 0.3f), Mats.WoodDark, 1f);
            }
            b.Box(new Vector3(-31.85f, 3.3f, 0f), new Vector3(5.7f, 0.2f, w - 2f * t), Mats.Floor, 1.2f);
            b.Box(new Vector3(-29.05f, 3.12f, 0f), new Vector3(0.26f, 0.32f, w - 2f * t), Mats.WoodDark, 1f);
            b.Box(new Vector3(-29.05f, 4.2f, 0f), new Vector3(0.1f, 0.1f, w - 2f * t), Mats.WoodDark, 1f);
            for (float z = z0 + t + 0.4f; z < z1 - t; z += 1.4f) b.Box(new Vector3(-29.05f, 3.8f, z), new Vector3(0.08f, 0.8f, 0.08f), Mats.WoodDark, 1f);
            foreach (float z in new[] { -3.3f, 3.3f })
            {
                b.Box(new Vector3(-29.1f, 1.6f, z), new Vector3(0.26f, 3.2f, 0.26f), Mats.WoodDark, 1f);
                Solid("LoftPost", g, new Vector3(-29.1f, 1.6f, z), new Vector3(0.26f, 3.2f, 0.26f), Quaternion.identity);
            }
            for (int i = 0; i < 7; i++)
                HayBale(b, null, new Vector3(-33.4f + (i % 2) * 2.0f, 3.4f + 0.45f + (i % 3 == 0 ? 0.9f : 0f), -5.4f + i * 1.8f), (i % 2) * 90f);
            // ladder up to the loft (visual)
            foreach (float z in new[] { -0.35f, 0.35f }) b.Box(new Vector3(-28.55f, 1.85f, z), new Vector3(0.08f, 3.8f, 0.08f), Mats.WoodMid, 1f, roll: 12f);
            for (float y = 0.35f; y < 3.6f; y += 0.4f) b.Box(new Vector3(-28.18f - y * 0.21f, y, 0f), new Vector3(0.06f, 0.06f, 0.78f), Mats.WoodMid, 1f);
            // stalls along the south wall
            foreach (float xp in new[] { -28f, -25f, -22f, -19f })
            {
                b.Box(new Vector3(xp, 0.75f, -5.35f), new Vector3(0.12f, 1.5f, 2.7f), Mats.WoodMid, 1.2f);
                b.Box(new Vector3(xp, 1.55f, -5.35f), new Vector3(0.18f, 0.1f, 2.8f), Mats.WoodDark, 1f);
                Solid("StallWall", g, new Vector3(xp, 0.75f, -5.35f), new Vector3(0.12f, 1.5f, 2.7f), Quaternion.identity);
            }
            foreach (float xs in new[] { -26.5f, -23.5f, -20.5f })
            {
                b.Box(new Vector3(xs, 0.06f, -5.35f), new Vector3(2.85f, 0.04f, 2.65f), Mats.Straw, 1f, skip: 4);
                b.Box(new Vector3(xs, 0.3f, -6.35f), new Vector3(1.5f, 0.5f, 0.5f), Mats.WoodDark, 1f);
                b.Box(new Vector3(xs, 0.5f, -6.35f), new Vector3(1.3f, 0.06f, 0.36f), Mats.Hay, 1f);
            }
            // loose bales under the loft, and by the doors outside
            HayBale(b, g, new Vector3(-30.2f, 0.45f, -5.6f), 90f);
            HayBale(b, g, new Vector3(-30.2f, 1.35f, -5.4f), 84f);
            HayBale(b, g, new Vector3(-33.6f, 0.45f, 4.6f), 0f);
            for (int i = 0; i < 4; i++) HayBale(b, g, new Vector3(x1 + 3.0f + (i % 2) * 1.3f, 0.45f + (i / 2) * 0.9f, -6f - (i % 2) * 0.2f), Rnd(-6, 6));
            HayBale(b, g, new Vector3(-15.4f, 0.45f, 6.9f), 0f);
            // hanging lanterns (always lit inside) + warm unshadowed fill lights so the interior reads
            foreach (float lx in new[] { -21.5f, -30.5f })
            {
                b.Box(new Vector3(lx, H - 0.75f, 0f), new Vector3(0.03f, 0.7f, 0.03f), Mats.Iron, 1f);
                b.Box(new Vector3(lx, H - 1.25f, 0f), new Vector3(0.32f, 0.42f, 0.32f), Mats.LampNight, 1f);
                b.Box(new Vector3(lx, H - 1.01f, 0f), new Vector3(0.4f, 0.06f, 0.4f), Mats.Iron, 1f);
                var lg = new GameObject("Barn Lantern Light");
                lg.transform.SetParent(g, false);
                lg.transform.position = new Vector3(lx, H - 1.6f, 0f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; l.range = 11f; l.intensity = 1.6f; l.color = WorldAssets.Hex("FFD49A"); l.shadows = LightShadows.None;
            }
            // wardrobe corner: rug and mirror (the cabinet itself is Customization's, spawned at the marker)
            b.Box(new Vector3(-20.4f, 0.055f, 5.0f), new Vector3(2.1f, 0.012f, 1.7f), Mats.Rug, 1f);
            b.Box(new Vector3(-18.7f, 1.55f, 6.66f), new Vector3(1.0f, 1.7f, 0.06f), Mats.WoodDark, 1f);
            b.Box(new Vector3(-18.7f, 1.55f, 6.62f), new Vector3(0.8f, 1.5f, 0.02f), WorldAssets.Mat("Mirror", WorldAssets.Hex("C9DCE4"), 0.85f), 1f);
            BakeBuilding(b, "SM_World_Barn", "Barn Building", g, Vector3.zero, 0f);
            SignBoard("WARDROBE", new Vector3(-20.4f, 2.85f, 6.64f), 180f, 1.7f, 0.42f, g, Mats.SignBoard, Ink);
            SignBoard("GRANDPA'S FARM", new Vector3(x1 + 0.12f, 6.45f, 0f), 90f, 4.2f, 0.7f, g, Mats.SignBoard, Ink, thickness: 0.1f);
            AddMarker(MarkerAt("Wardrobe", new Vector3(-20.4f, 0f, 6.33f), 180f, g), MarkerRole.Wardrobe);

            // interior props: workbench + hung tools, barrels, sacks
            var props = Group("Barn Props", g);
            Place(SV, "workbench", new Vector3(-31f, 0f, 5.95f), 180f, 0.95f, props, "box");
            float tz = z1 - t - 0.06f;
            PlaceAt(SV, "tool-shovel", new Vector3(-33.6f, 0.4f, tz), Quaternion.Euler(0f, 0f, 6f), 4.2f, props);
            PlaceAt(SV, "tool-hoe", new Vector3(-33.0f, 0.45f, tz), Quaternion.Euler(0f, 0f, -5f), 4.2f, props);
            PlaceAt(SV, "tool-pickaxe", new Vector3(-29.4f, 1.4f, tz), Quaternion.identity, 3.6f, props);
            PlaceAt(SV, "tool-axe", new Vector3(-28.6f, 1.5f, tz), Quaternion.Euler(0f, 0f, 10f), 3.6f, props);
            Place(SV, "barrel", new Vector3(-33.8f, 0f, -1.6f), 0f, 1.05f, props, "box");
            Place(SV, "barrel", new Vector3(-33.7f, 0f, -0.3f), 30f, 1.05f, props, "box");
            Place(FD, "bag", new Vector3(-34.1f, 0f, 1.2f), 70f, 0.75f, props);
            Place(FD, "bag", new Vector3(-33.6f, 0f, 1.7f), 10f, 0.7f, props);
            Place(SV, "bucket", new Vector3(-25.6f, 0f, -3.6f), 0f, 0.45f, props);

            // yard props outside (clear of the crates at x -12.5..-11.1, z 6.5..7.9)
            Place(FT, "cart", new Vector3(-8.2f, 0, 12.4f), 200f, 1.8f, g, "box");
            Place(SV, "barrel", new Vector3(Barn.x + 10.5f, 0, -8.2f), 0f, 1.1f, g, "box");
            Place(SV, "barrel", new Vector3(Barn.x + 11.4f, 0, -8.8f), 30f, 1.1f, g, "box");
            Place(SV, "box-large", new Vector3(Barn.x + 9.8f, 0, 9.2f), 10f, 0.9f, g, "box");
            Place(FT, "lantern", new Vector3(x1 + 0.6f, 0, 3.75f), 0f, 2.4f, g);
            Place(FT, "lantern", new Vector3(x1 + 0.6f, 0, -3.75f), 0f, 2.4f, g);
        }

        static GameObject MarkerAt(string name, Vector3 pos, float yaw, Transform parent)
        {
            var m = Marker(name, pos, parent);
            m.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return m;
        }

        // ------------------------------------------------------------------ farmhouse (Grandpa's)

        static void FarmhouseArea()
        {
            var g = Group("Farmhouse");
            var small = Cullable(Group("Farmhouse Garden", g), 10f, 55f);
            var spec = new HouseSpec { W = 10f, D = 7.5f, H1 = 3.0f, H2 = 2.7f, Wall = 0, Upper = -1, Roof = 5, Shutter = 1, GableFront = false, Jetty = false, Timber = false, StoneGround = true, Porch = true, FrontWindows = 2, Chimney = true, Pitch = 0.85f };
            PlaceHouse("Farmhouse", spec, Farmhouse, 90f, g, small);
            float gy = GroundY(Farmhouse.x, Farmhouse.z);
            // porch furniture (deck top 0.28 m)
            Place(FT, "stall-bench", new Vector3(-50.3f, gy + 0.28f, 18.9f), 90f, 0.5f, g, "box", snap: false);
            Place(SV, "bucket", new Vector3(-43.6f, gy + 0.28f, 18.7f), 0f, 0.4f, g, snap: false);
            // front garden: picket fence with a gate gap on the path, flowers along it, stone path
            FenceLine(new Vector3(-55f, 0, 13.6f), new Vector3(-48.3f, 0, 13.6f), g, NF, "fence_planks", 0.95f);
            FenceLine(new Vector3(-45.7f, 0, 13.6f), new Vector3(-39f, 0, 13.6f), g, NF, "fence_planks", 0.95f);
            FenceLine(new Vector3(-55f, 0, 13.6f), new Vector3(-55f, 0, 19.6f), g, NF, "fence_planks", 0.95f);
            FenceLine(new Vector3(-39f, 0, 13.6f), new Vector3(-39f, 0, 19.6f), g, NF, "fence_planks", 0.95f);
            for (float x = -54.4f; x < -39.4f; x += 0.55f)
                if (Mathf.Abs(x + 47f) > 1.4f) Place(NF, Pick("flower_yellowA", "flower_purpleA", "flower_redA", "flower_yellowB", "flower_purpleB", "flower_yellowC"), new Vector3(x + Rnd(-0.1f, 0.1f), 0, 14.25f + Rnd(-0.15f, 0.15f)), Rnd(0, 360), Rnd(0.32f, 0.45f), small);
            for (float z = 13.9f; z < 17.6f; z += 0.62f) Place(NF, "path_stone", new Vector3(-47f + Rnd(-0.08f, 0.08f), 0, z), Rnd(-8, 8) + 90f, 0.06f, small);
            // mailbox at the lane
            var mb = new WorldMeshBuilder();
            mb.Box(new Vector3(0, 0.6f, 0), new Vector3(0.1f, 1.2f, 0.1f), Mats.WoodDark, 1f);
            mb.Box(new Vector3(0, 1.32f, 0), new Vector3(0.5f, 0.32f, 0.3f), Mats.Shutter(0), 1f);
            mb.Box(new Vector3(0.12f, 1.62f, 0.16f), new Vector3(0.03f, 0.22f, 0.03f), WorldAssets.Mat("FlagMuted", WorldAssets.Hex("C98A72")), 1f);
            BakeBuilding(mb, "SM_World_Mailbox", "Mailbox", g, G(-44.6f, 12.4f), 0f);
            SignBoard("GRANDPA", G(-44.6f, 12.4f, 0.98f) + new Vector3(0f, 0f, -0.06f), 180f, 0.62f, 0.2f, g, Mats.SignBoard, Ink, thickness: 0.03f);
            // well
            var wb = new WorldMeshBuilder();
            wb.Cylinder(Vector3.zero, 0.95f, 0.95f, 0.85f, 10, Mats.Stone, null, 1.2f, capTop: false);
            wb.Cylinder(new Vector3(0, 0.82f, 0), 1.02f, 1.02f, 0.12f, 10, Mats.StoneDark, Mats.StoneDark, 1.2f, capTop: false);
            var wellWater = WorldAssets.Mat("WellWater", WorldAssets.Hex("2E5E6A"), 0.6f);
            wb.Cylinder(new Vector3(0, 0.55f, 0), 0.8f, 0.8f, 0.01f, 10, wellWater, wellWater);
            foreach (float s in new[] { -1f, 1f }) wb.Box(new Vector3(0, 1.45f, s * 0.95f), new Vector3(0.14f, 1.8f, 0.14f), Mats.WoodDark, 1f);
            wb.Push(new Vector3(0, 2.3f, 0), Quaternion.identity);
            Roof(wb, 1.5f, 2.4f, 0.9f, Mats.Roof(3), Mats.WoodMid);
            wb.Pop();
            wb.Box(new Vector3(0, 1.85f, 0), new Vector3(0.12f, 0.12f, 2.0f), Mats.WoodMid, 1f);
            wb.Box(new Vector3(0, 1.45f, 0), new Vector3(0.02f, 0.8f, 0.02f), Mats.Rope, 1f);
            var well = BakeBuilding(wb, "SM_World_Well", "Well", g, G(-60f, 24f), 0f);
            Solid("Collider", well.transform, new Vector3(0, 0.45f, 0), new Vector3(2.0f, 0.9f, 2.0f), Quaternion.identity);
            Place(SV, "bucket", new Vector3(-58.7f, 0, 23.1f), 0f, 0.42f, g);
            // woodpile, chopping block, axe
            Place(NF, "log_stack", new Vector3(-53.2f, 0, 22.6f), 90f, 1.0f, g, "box");
            Place(NF, "log_stack", new Vector3(-53.2f, 0, 25.4f), 90f, 1.1f, g, "box");
            Place(NF, "stump_round", new Vector3(-54.6f, 0, 20.2f), 0f, 0.45f, g, "box");
            PlaceAt(SV, "tool-axe", G(-54.6f, 20.2f, 0.4f), Quaternion.Euler(20f, 30f, 0f), 2.8f, g);
            // washing line with laundry (combined)
            var lb = new WorldMeshBuilder();
            foreach (float z in new[] { 21.5f, 31f })
            {
                lb.Box(new Vector3(-37.4f, 1.05f, z), new Vector3(0.12f, 2.1f, 0.12f), Mats.WoodMid, 1f);
                lb.Box(new Vector3(-37.4f, 2.05f, z), new Vector3(0.9f, 0.1f, 0.1f), Mats.WoodMid, 1f);
                Solid("LinePost", g, new Vector3(-37.4f, GroundY(-37.4f, z) + 1.05f, z), new Vector3(0.12f, 2.1f, 0.12f), Quaternion.identity);
            }
            float ly = GroundY(-37.4f, 26f);
            foreach (float lx in new[] { -37.75f, -37.05f })
            {
                lb.Box(new Vector3(lx, 2.0f, 26.25f), new Vector3(0.02f, 0.02f, 9.5f), Mats.Rope, 1f);
                for (int i = 0; i < 6; i++)
                {
                    float z = 22.6f + i * 1.45f + Rnd(-0.2f, 0.2f), wdt = Rnd(0.55f, 0.9f), hgt = Rnd(0.6f, 1.0f);
                    lb.Panel(new Vector3(lx, 2.0f - hgt * 0.5f, z), wdt, hgt, Mats.LaundryCols[_rng.Next(Mats.LaundryCols.Length)], 90f + Rnd(-6f, 6f));
                }
            }
            lb.Bake("SM_World_WashingLine", "Washing Line", g, new Vector3(0f, ly, 0f), Quaternion.identity, ShadowCastingMode.On);
            // doghouse with a warning about the goat
            var dh = new WorldMeshBuilder();
            dh.Box(new Vector3(0, 0.45f, 0), new Vector3(1.1f, 0.9f, 1.3f), Mats.WoodMid, 1.2f);
            dh.Push(new Vector3(0, 0.9f, 0), Quaternion.identity);
            Roof(dh, 1.1f, 1.3f, 0.9f, Mats.Roof(0), Mats.WoodMid);
            dh.Pop();
            dh.Box(new Vector3(0.56f, 0.38f, 0), new Vector3(0.04f, 0.6f, 0.5f), WorldAssets.Mat("Interior", WorldAssets.Hex("3A2A20")), 1f);
            var doghouse = BakeBuilding(dh, "SM_World_Doghouse", "Doghouse", g, G(-40.8f, 18.4f), 180f);
            Solid("Collider", doghouse.transform, new Vector3(0, 0.6f, 0), new Vector3(1.1f, 1.2f, 1.3f), Quaternion.identity);
            SignPost("BEWARE\nOF GOAT", new Vector3(-41.2f, 0f, 16.4f), 180f, 0.95f, 0.55f, g, Mats.SignBoard, Ink, postHeight: 0.7f);
            // kitchen garden behind the house
            var kg = Cullable(Group("Kitchen Garden", g), 8f, 60f);
            FenceLine(new Vector3(-55.6f, 0, 27.0f), new Vector3(-55.6f, 0, 31.6f), kg, NF, "fence_simple", 0.7f);
            FenceLine(new Vector3(-55.6f, 0, 31.6f), new Vector3(-49.4f, 0, 31.6f), kg, NF, "fence_simple", 0.7f);
            for (int r = 0; r < 4; r++)
                for (float x = -54.8f; x < -50.2f; x += 0.75f)
                    Place(NF, new[] { "crop_carrot", "crops_leafsStageB", "crop_turnip", "crops_leafsStageA" }[r], new Vector3(x + Rnd(-0.1f, 0.1f), 0, 28.1f + r * 0.85f), Rnd(0, 360), 0.4f, kg);
            // shade trees behind the house
            Place(NF, "tree_oak", new Vector3(-44f, 0, 34.5f), Rnd(0, 360), 8.5f, g, "trunk");
            Place(NF, "tree_default", new Vector3(-58f, 0, 33f), Rnd(0, 360), 7.5f, g, "trunk");
            Place(NF, "tree_fat", new Vector3(-36.5f, 0, 36f), Rnd(0, 360), 7f, g, "trunk");
        }

        // ------------------------------------------------------------------ pens

        static void Pens()
        {
            var pig = Group("Pig Pen");
            // 18 x 12, south gate (gap), north critter hole behind hay (GDD hotspot 7).
            FenceRect(PigPen, 18f, 12f, pig, (0, 9f, 3f), (2, 13f, 1.2f));
            if (TripoGate("PenGate_PigPen", PigPen + new Vector3(-1.5f, 0, -6f), 55f, pig) == null)
                Place(NF, "fence_gate", PigPen + new Vector3(-2.2f, 0, -6.4f), 30f, 1.2f, pig);
            var b = new WorldMeshBuilder();
            HayBale(b, pig, PigPen + new Vector3(-4f, 0.45f, 7.6f), 90f, new Vector3(1.2f, 0.9f, 1.9f)); // hides the hole
            // trough
            b.Box(PigPen + new Vector3(5.5f, 0.28f, -3.6f), new Vector3(2.6f, 0.56f, 0.8f), Mats.WoodDark, 1f);
            b.Box(PigPen + new Vector3(5.5f, 0.5f, -3.6f), new Vector3(2.3f, 0.06f, 0.55f), WorldAssets.Mat("Slop", WorldAssets.Hex("8A7454")), 1f);
            Solid("Trough", pig, PigPen + new Vector3(5.5f, 0.28f, -3.6f), new Vector3(2.6f, 0.56f, 0.8f), Quaternion.identity);
            BakeBuilding(b, "SM_World_PigPenProps", "Pig Pen Props", pig, Vector3.zero, 0f);
            Mud("Mud Wallow", PigPen + new Vector3(-3f, 0f, 1f), new Vector3(11f, 2f, 7.2f), pig, true);
            // A-frame pig hut, open to the west, in the north-east corner
            var hb = new WorldMeshBuilder();
            hb.Box(new Vector3(-1.75f, 0.9f, 0f), new Vector3(0.12f, 1.8f, 3.4f), Mats.WoodMid, 1.2f);
            foreach (float s in new[] { -1f, 1f }) hb.Box(new Vector3(0f, 0.3f, s * 1.7f), new Vector3(3.5f, 0.6f, 0.1f), Mats.WoodMid, 1.2f);
            hb.Push(new Vector3(0f, 0.6f, 0f), Quaternion.Euler(0f, 90f, 0f));
            Roof(hb, 3.4f, 3.5f, 1.05f, Mats.Roof(3), Mats.WoodMid, gableNeg: true, gablePos: false);
            hb.Pop();
            hb.Box(new Vector3(0f, 0.03f, 0f), new Vector3(3.3f, 0.04f, 3.2f), Mats.Straw, 1f, skip: 4);
            var hut = BakeBuilding(hb, "SM_World_PigHut", "Pig Hut", pig, G(PigPen.x + 6f, PigPen.z + 3f), 180f);
            Solid("Back", hut.transform, new Vector3(-1.75f, 0.9f, 0f), new Vector3(0.12f, 1.8f, 3.4f), Quaternion.identity);
            foreach (float s in new[] { -1f, 1f }) Solid("Side", hut.transform, new Vector3(0f, 0.6f, s * 1.7f), new Vector3(3.5f, 1.2f, 0.12f), Quaternion.identity);
            SignPost("PIGS", PigPen + new Vector3(-5.6f, 0f, -6.8f), 180f, 1.1f, 0.42f, pig, Mats.SignBoard, Ink, postHeight: 0.9f);

            var coop = Group("Coop Yard");
            FenceRect(Coop, 15f, 15f, coop, (2, 4f, 2.2f), (1, 10f, 2.2f), (0, 7.5f, 3f));
            var cs = new HouseSpec { W = 4.2f, D = 3.4f, H1 = 2.5f, H2 = 0f, Wall = 0, Roof = 0, Shutter = 0, GableFront = true, Jetty = false, Timber = false, Chimney = false, FrontWindows = 1, FlowerBoxes = false, Pitch = 1.0f };
            var coopHouse = PlaceHouse("Coop", cs, Coop + new Vector3(-3.5f, 0, 3.5f), 0f, coop, null);
            var cb = new WorldMeshBuilder();
            cb.Box(new Vector3(2.35f, 0.32f, 0.9f), new Vector3(1.7f, 0.07f, 0.75f), Mats.WoodLight, 1f, roll: -20f); // ramp
            for (int i = 0; i < 4; i++) cb.Box(new Vector3(2.0f + i * 0.33f, 0.5f - i * 0.12f, 0.9f), new Vector3(0.05f, 0.04f, 0.75f), Mats.WoodDark, 1f);
            foreach (float z in new[] { -0.7f, 0.7f }) // nest boxes on the south wall
                cb.Box(new Vector3(0.2f + z, 1.0f, -2.3f), new Vector3(0.6f, 0.5f, 0.5f), Mats.WoodMid, 1f);
            cb.Bake("SM_World_CoopExtras", "Coop Extras", coopHouse.transform, coopHouse.transform.position, coopHouse.transform.rotation);
            var cprops = Cullable(Group("Coop Props", coop), 8f, 50f);
            for (int i = 0; i < 6; i++) Place(NF, "grass", Coop + new Vector3(Rnd(-6, 6), 0, Rnd(-6, 6)), Rnd(0, 360), 0.4f, cprops);
            Place(FD, "bag", Coop + new Vector3(4.5f, 0, 5f), 20f, 0.8f, cprops);
            Place(SV, "bucket", Coop + new Vector3(4.0f, 0, 4.2f), 0f, 0.45f, cprops);
            TripoGate("PenGate_Coop", Coop + new Vector3(-1.5f, 0, -7.5f), 55f, coop);
            SignPost("CHICKENS", Coop + new Vector3(-5.4f, 0f, 8.2f), 0f, 1.4f, 0.42f, coop, Mats.SignBoard, Ink, postHeight: 0.9f);
        }

        // ------------------------------------------------------------------ pasture (cow / goat / sheep spawners stay open ground)

        static void Pasture()
        {
            var g = Group("Pasture");
            var b = new WorldMeshBuilder();
            // stone water trough
            var tp = G(-1.6f, 26f);
            b.Box(tp + new Vector3(0, 0.3f, 0), new Vector3(2.4f, 0.6f, 0.9f), Mats.Stone, 1.2f);
            b.Box(tp + new Vector3(0, 0.56f, 0), new Vector3(2.1f, 0.02f, 0.62f), WorldAssets.Mat("TroughWater", WorldAssets.Hex("4E8A96"), 0.7f), 1f);
            Solid("Trough", g, tp + new Vector3(0, 0.3f, 0), new Vector3(2.4f, 0.6f, 0.9f), Quaternion.identity);
            // hay rack (sheep feeder)
            var hp = G(-17f, 30f);
            foreach (float s in new[] { -1f, 1f }) b.Box(hp + new Vector3(s * 1.3f, 0.7f, 0), new Vector3(0.12f, 1.4f, 0.12f), Mats.WoodDark, 1f);
            b.Box(hp + new Vector3(0, 0.75f, 0), new Vector3(2.5f, 0.7f, 0.7f), Mats.Hay, 1f);
            for (float x = -1.1f; x <= 1.15f; x += 0.28f) b.Box(hp + new Vector3(x, 0.8f, 0.37f), new Vector3(0.04f, 0.9f, 0.04f), Mats.WoodMid, 1f);
            b.Push(hp + new Vector3(0, 1.4f, 0), Quaternion.identity);
            Roof(b, 1.0f, 2.8f, 0.7f, Mats.Roof(4), Mats.WoodMid);
            b.Pop();
            Solid("HayRack", g, hp + new Vector3(0, 0.7f, 0), new Vector3(2.7f, 1.4f, 0.9f), Quaternion.identity);
            // salt lick on a post
            var sp = G(8.6f, 24.2f);
            b.Box(sp + new Vector3(0, 0.5f, 0), new Vector3(0.14f, 1.0f, 0.14f), Mats.WoodDark, 1f);
            b.Box(sp + new Vector3(0, 1.08f, 0), new Vector3(0.32f, 0.2f, 0.32f), WorldAssets.Mat("SaltLick", WorldAssets.Hex("E9E2D6")), 1f);
            BakeBuilding(b, "SM_World_Pasture", "Pasture Props", g, Vector3.zero, 0f);
            Place(NF, "tree_oak", new Vector3(-13.5f, 0, 34f), 40f, 9f, g, "trunk"); // shade tree for the flock
            SignPost("BEWARE\nOF GOAT", new Vector3(4.2f, 0f, 12.6f), 180f, 0.95f, 0.55f, g, Mats.SignBoard, Ink, postHeight: 0.8f);
        }

        // ------------------------------------------------------------------ fields

        static void Fields()
        {
            CornMaze();
            var veg = Cullable(Group("Veg Rows"), 8f, 75f);
            string[] crops = { "crop_carrot", "crop_turnip", "crops_leafsStageB", "crop_melon" };
            for (int r = 0; r < 11; r++)
            {
                float x = -77.5f + r * 1.55f;
                for (float z = 27.2f; z <= 41f; z += 1.2f) Place(NF, "crops_dirtRow", new Vector3(x, 0f, z), 90f, 0.06f, veg);
                for (float z = 27.3f; z <= 41f; z += 1.0f)
                    Place(NF, crops[(r / 2) % crops.Length], new Vector3(x + Rnd(-0.08f, 0.08f), 0, z + Rnd(-0.15f, 0.15f)), Rnd(0, 360), (r / 2) % 4 == 3 ? 0.62f : Rnd(0.55f, 0.7f), veg);
            }
            FenceRect(new Vector3(-70f, 0, 34f), 18f, 18f, Group("Veg Fence"), (1, 9f, 3f), (0, 4f, 2.5f));
            SignPost("VEG", new Vector3(-58.6f, 0f, 24.6f), 90f, 0.9f, 0.4f, _root, Mats.SignBoard, Ink, postHeight: 0.9f);

            // wheat: mostly the lighter stage B mesh (360 tris) with stage A (720 tris) only on the field edges; culled at 110 m
            var wheat = Cullable(Group("Wheat Field"), 8f, 110f);
            var wheatStalk = WorldAssets.Mat("WheatStalk", WorldAssets.Hex("D3B566"));
            var wheatHead = WorldAssets.Mat("WheatHead", WorldAssets.Hex("E8CF86"));
            for (float x = -107f; x <= -69f; x += 1.42f)
            for (float z = -14f; z <= 17f; z += 1.42f)
            {
                if (Mathf.Abs(z - 1.5f) < 1.2f && x > -100f) continue; // a trampled path through the middle
                bool edge = x < -105f || x > -71f || z < -12.5f || z > 15.5f;
                bool green = edge && ((int)(x * 7 + z * 3) & 1) == 0;
                var w = Place(NF, green ? "crops_wheatStageA" : "crops_wheatStageB", new Vector3(x + Rnd(-0.35f, 0.35f), 0, z + Rnd(-0.35f, 0.35f)), Rnd(0, 360), Rnd(1.0f, 1.25f), wheat);
                // ripe wheat reads golden (crops are the palette's saturation exception), not as tan sticks
                if (w != null && !green) foreach (var r in w.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterials = new[] { wheatStalk, wheatHead };
            }

            var patch = Group("Pumpkin Patch");
            FenceRect(Pumpkins, 16f, 16f, patch, (2, 8f, 3f));
            var pumps = Cullable(Group("Pumpkins", patch), 8f, 80f);
            for (float x = -6f; x <= 6f; x += 2.4f)
            for (float z = -6f; z <= 6f; z += 2.4f)
            {
                if (Mathf.Abs(x) < 2.5f && Mathf.Abs(z) < 2.5f) continue; // prize pumpkin spot
                Place(NF, "crop_pumpkin", Pumpkins + new Vector3(x + Rnd(-0.4f, 0.4f), 0, z + Rnd(-0.4f, 0.4f)), Rnd(0, 360), Rnd(0.5f, 0.8f), pumps);
            }
            SignPost("PUMPKINS", Pumpkins + new Vector3(-1.5f, 0, 8.9f), 0f, 1.4f, 0.42f, patch, Mats.SignBoard, Ink, postHeight: 0.9f);
        }

        static void CornMaze()
        {
            var g = Cullable(Group("Corn Maze"), 10f, 220f);
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
                    // stage C (148 tris) for most stalks, stage D (300 tris, cobs) for one in four
                    Place(NF, ((int)(p.x * 3 + p.z) & 3) == 0 ? "crops_cornStageD" : "crops_cornStageC", p + new Vector3(Rnd(-0.2f, 0.2f), 0, Rnd(-0.2f, 0.2f)), Rnd(0, 360), Rnd(2.3f, 2.7f), g);
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
            SignPost("CORN\nMAZE", new Vector3(ox + 8.4f * cell, 0, oz - 1.6f), 180f, 1.2f, 0.7f, _root, Mats.SignBoard, Ink, postHeight: 1.0f);
        }

        // ------------------------------------------------------------------ windmill

        static void WindmillArea()
        {
            var g = Group("Windmill");
            float gy = GroundY(Windmill.x, Windmill.z);
            var b = new WorldMeshBuilder();
            var plaster = Mats.Wall(0);
            b.Cylinder(new Vector3(0, -0.3f, 0), 4.6f, 4.35f, 1.7f, 8, Mats.Stone, Mats.StoneDark, 1.4f, phase: Mathf.PI / 8f);
            b.Cylinder(new Vector3(0, 1.4f, 0), 4.05f, 2.8f, 10.8f, 8, plaster, null, 2.5f, capTop: false, phase: Mathf.PI / 8f);
            for (int i = 0; i < 2; i++) b.Cylinder(new Vector3(0, 4.2f + i * 4.2f, 0), 4.05f - (2.8f + i * 4.2f) * 0.12f, 4.0f - (2.8f + i * 4.2f) * 0.12f, 0.18f, 8, Mats.WoodDark, null, 1f, capTop: false, phase: Mathf.PI / 8f);
            // gallery deck + rail at the first floor
            b.Cylinder(new Vector3(0, 4.55f, 0), 5.0f, 5.0f, 0.16f, 8, Mats.Floor, Mats.Floor, 1.2f, capTop: true, capBottom: true, phase: Mathf.PI / 8f);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                b.Box(new Vector3(Mathf.Cos(a) * 4.85f, 5.15f, Mathf.Sin(a) * 4.85f), new Vector3(0.08f, 1.05f, 0.08f), Mats.WoodDark, 1f);
                float a1 = (i + 1) * Mathf.PI * 2f / 16f;
                var p0 = new Vector3(Mathf.Cos(a) * 4.85f, 5.65f, Mathf.Sin(a) * 4.85f); var p1 = new Vector3(Mathf.Cos(a1) * 4.85f, 5.65f, Mathf.Sin(a1) * 4.85f);
                b.Push((p0 + p1) * 0.5f, Quaternion.LookRotation(p1 - p0)); b.Box(Vector3.zero, new Vector3(0.07f, 0.07f, (p1 - p0).magnitude), Mats.WoodDark, 1f); b.Pop();
            }
            // cap
            b.Cylinder(new Vector3(0, 12.15f, 0), 3.35f, 3.35f, 0.3f, 8, Mats.WoodDark, null, 1f, capTop: false, phase: Mathf.PI / 8f);
            b.Cylinder(new Vector3(0, 12.4f, 0), 3.4f, 0f, 3.9f, 8, Mats.Roof(3), null, 1.6f, phase: Mathf.PI / 8f);
            // door (east) and small windows
            b.Push(new Vector3(4.42f, 0f, 0f), Quaternion.LookRotation(Vector3.right));
            b.Box(new Vector3(0, 1.15f, 0.04f), new Vector3(1.25f, 2.3f, 0.1f), Mats.DoorWood, 1f);
            b.Box(new Vector3(0, 2.4f, 0.07f), new Vector3(1.55f, 0.2f, 0.14f), Mats.Trim, 1f);
            b.Pop();
            foreach (var (y, a) in new[] { (7.0f, 0f), (9.6f, 135f), (7.4f, 225f) })
            {
                float r = Mathf.Lerp(4.05f, 2.8f, (y - 1.4f) / 10.8f) * Mathf.Cos(Mathf.PI / 8f) + 0.08f;
                var n = Quaternion.Euler(0f, -a, 0f) * Vector3.right;
                Window(b, n * r + Vector3.up * y, n, 0.65f, 0.85f, Mats.Shutter(1), false, null);
            }
            BakeBuilding(b, "SM_World_WindmillBody", "Windmill Body", g, new Vector3(Windmill.x, gy, Windmill.z), 0f);
            // collision: convex frustums (collider-only)
            MeshColliderOnly("Tower Base Collider", WorldAssets.SaveMesh(WorldAssets.Frustum(8, 4.6f, 4.35f, 1.7f), "SM_World_WindmillBase"), g, new Vector3(Windmill.x, gy - 0.3f, Windmill.z));
            MeshColliderOnly("Tower Collider", WorldAssets.SaveMesh(WorldAssets.Frustum(8, 4.05f, 2.8f, 10.8f), "SM_World_WindmillTower"), g, new Vector3(Windmill.x, gy + 1.4f, Windmill.z));
            // sails (spinning hub, lattice arms with cloth)
            var hub = new GameObject("Sails").transform;
            hub.SetParent(g, false);
            hub.position = new Vector3(Windmill.x + 3.3f, gy + 11.2f, Windmill.z);
            hub.rotation = Quaternion.Euler(0f, 90f, 0f);
            var sb = new WorldMeshBuilder();
            for (int i = 0; i < 4; i++)
            {
                sb.Push(Vector3.zero, Quaternion.Euler(0, 0, i * 90f + 15f));
                sb.Box(new Vector3(0, 4.7f, 0), new Vector3(0.3f, 9.2f, 0.25f), Mats.WoodMid, 1f);
                for (float y = 2.0f; y < 9.2f; y += 0.9f) sb.Box(new Vector3(1.05f, y, 0.06f), new Vector3(2.1f, 0.07f, 0.07f), Mats.WoodDark, 1f);
                sb.Box(new Vector3(2.1f, 5.6f, 0.06f), new Vector3(0.07f, 7.2f, 0.07f), Mats.WoodDark, 1f);
                sb.Panel(new Vector3(1.08f, 5.6f, 0.12f), 1.95f, 7.0f, Mats.Canvas, 0f);
                sb.Pop();
            }
            sb.Push(Vector3.zero, Quaternion.Euler(90f, 0f, 0f)); // axle along the spin axis (local Z)
            sb.Cylinder(new Vector3(0, -0.6f, 0), 0.55f, 0.5f, 1.0f, 8, Mats.WoodDark, Mats.WoodDark);
            sb.Pop();
            var sails = sb.Bake("SM_World_WindmillSails", "Sail Arms", hub, hub.position, hub.rotation);
            sails.isStatic = false;
            hub.gameObject.AddComponent<Spinner>().Configure(Vector3.forward, 22f);
            hub.gameObject.isStatic = false;
            var props = Cullable(Group("Mill Props", g), 8f, 60f);
            for (int i = 0; i < 4; i++) Place(FD, "bag", Windmill + new Vector3(5.6f, 0, -2.4f + i * 1.0f), Rnd(0, 360), 0.9f, props);
            Place(SV, "barrel", Windmill + new Vector3(5.2f, 0, 2.6f), 0f, 1.0f, props, "box");
            SignPost("FLOUR MILL", Windmill + new Vector3(7.4f, 0, 3.4f), 90f, 1.6f, 0.42f, g, Mats.SignBoard, Ink, postHeight: 1.0f);
        }

        static void MeshColliderOnly(string name, Mesh mesh, Transform parent, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mesh; mc.convex = true;
            go.isStatic = true;
        }

        // ------------------------------------------------------------------ orchard, hedge, Lost & Found, hay meadow

        static void Orchard()
        {
            var orchard = Group("Orchard");
            var small = Cullable(Group("Orchard Floor", orchard), 10f, 55f);
            for (int i = 0; i < 5; i++)
            for (int j = 0; j < 3; j++)
            {
                var p = new Vector3(3f + i * 11f + Rnd(-1.5f, 1.5f), 0, -78f + j * 10f + Rnd(-1.5f, 1.5f));
                if (NearKeepClear(p.x, p.z, 1f)) p.x += 3f;
                Place(NF, (i + j) % 3 == 0 ? "tree_default" : (i + j) % 3 == 1 ? "tree_oak" : "tree_fat", p, Rnd(0, 360), Rnd(5.5f, 7f), orchard, "trunk");
                for (int k = 0; k < 3; k++) Place(FD, "apple", p + new Vector3(Rnd(-2f, 2f), 0, Rnd(-2f, 2f)), Rnd(0, 360), 0.25f, small);
                if ((i + j) % 2 == 0) Place(NF, Pick("mushroom_red", "mushroom_tanGroup", "grass_large"), p + new Vector3(Rnd(-1.5f, 1.5f), 0, Rnd(-1.5f, 1.5f)), Rnd(0, 360), 0.3f, small);
            }
            Place(FD, "barrel", new Vector3(28f, 0, -54f), 0f, 1.1f, orchard, "box");
            Place(FT, "stall-bench", new Vector3(24f, 0, -54f), 90f, 0.9f, orchard, "box");
            for (int k = 0; k < 3; k++)
            {
                var crate = Place(SV, "box-open", new Vector3(31f + k * 0.8f, 0, -55.2f), Rnd(-10, 10), 0.6f, orchard, "box");
                if (crate != null) for (int a = 0; a < 3; a++) Place(FD, "apple", crate.transform.position + new Vector3(Rnd(-0.15f, 0.15f), 0.45f, Rnd(-0.15f, 0.15f)), Rnd(0, 360), 0.22f, small, snap: false);
            }
            var lb = new WorldMeshBuilder(); // ladder leaning on a tree
            foreach (float z in new[] { -0.3f, 0.3f }) lb.Box(new Vector3(0, 1.7f, z), new Vector3(0.08f, 3.6f, 0.08f), Mats.WoodMid, 1f, roll: 16f);
            for (float y = 0.3f; y < 3.3f; y += 0.4f) lb.Box(new Vector3(-y * 0.29f + 0.47f, y, 0), new Vector3(0.06f, 0.06f, 0.66f), Mats.WoodMid, 1f);
            lb.Bake("SM_World_Ladder", "Ladder", orchard, G(15.2f, -68.4f), Quaternion.Euler(0f, 20f, 0f));
            SignPost("ORCHARD", new Vector3(19.5f, 0f, -53.2f), 0f, 1.3f, 0.4f, orchard, Mats.SignBoard, Ink, postHeight: 0.9f);
        }

        static void BankHedge()
        {
            var hedge = Group("Bank Hedge");
            for (float z = -140f; z <= 100f; z += 2.6f)
            {
                if (Mathf.Abs(z) < 7f) continue; // bridge approach
                if (Mathf.Abs(z + 118f) < 7f) continue; // pile path
                if (((int)((z + 300f) / 2.6f)) % 9 == 0) continue; // bush gaps to hide in / slip through
                Place(NF, ((int)z & 1) == 0 ? "plant_bushLarge" : "plant_bushDetailed", new Vector3(55f + Rnd(-0.8f, 0.8f), 0, z + Rnd(-0.5f, 0.5f)), Rnd(0, 360), Rnd(1.6f, 2.2f), hedge, "box");
            }
        }

        static void LostAndFound()
        {
            var pile = Group("Spit-out Pile");
            SignPost("LOST &\nFOUND", Pile + new Vector3(3.2f, 0, 3.2f), -45f, 1.6f, 0.9f, pile, Mats.SignBoard, Ink, postHeight: 1.1f, twoSided: true);
            SignPost("(mostly)", Pile + new Vector3(3.9f, 0, 2.2f), -45f, 0.8f, 0.26f, pile, Mats.SignBoardDark, Cream, postHeight: 0.6f);
            Place(SV, "box", Pile + new Vector3(-2f, 0, 2f), 20f, 0.6f, pile, "box");
            Place(SV, "barrel-open", Pile + new Vector3(-3f, 0, -1f), 0f, 0.9f, pile, "box");
            Place(NF, "log_stack", Pile + new Vector3(1f, 0, -3.5f), 70f, 0.9f, pile, "box");
            Place(SV, "bucket", Pile + new Vector3(-1.2f, 0, -2.6f), 0f, 0.4f, pile);
            Place(FD, "bag", Pile + new Vector3(-3.4f, 0, 1.4f), 50f, 0.6f, pile);
        }

        /// <summary>Hedgerows (bush lines with gaps) around the patchwork fields, field-corner oaks and a few bales.</summary>
        static void Patchwork()
        {
            var g = Group("Patchwork Fields");
            var hedge = Cullable(Group("Hedgerows", g), 20f, 160f);
            var all = new List<Vector4>(PloughedFields); all.AddRange(HayFields);
            foreach (var f in all)
            {
                var c = new[] { new Vector2(f.x - 2f, f.y - 2f), new Vector2(f.z + 2f, f.y - 2f), new Vector2(f.z + 2f, f.w + 2f), new Vector2(f.x - 2f, f.w + 2f) };
                for (int s = 0; s < 4; s++)
                {
                    Vector2 a = c[s], b = c[(s + 1) % 4];
                    float len = (b - a).magnitude;
                    for (float t = 1.5f; t < len - 1.5f; t += Rnd(2.6f, 3.6f))
                    {
                        if (Mathf.Repeat(t, 22f) < 3.5f) continue; // gaps every ~22 m for critters and players
                        var p = Vector2.Lerp(a, b, t / len);
                        if (Outside(p.x, p.y) > -3f || NearKeepClear(p.x, p.y, 1f) || Blocked2(p.x, p.y)) continue;
                        Place(NF, Pick("plant_bushLarge", "plant_bushDetailed", "plant_bush"), new Vector3(p.x, 0, p.y), Rnd(0, 360), Rnd(1.3f, 2.0f), hedge);
                    }
                }
                var corner = c[_rng.Next(4)];
                if (!NearKeepClear(corner.x, corner.y, 2f) && Outside(corner.x, corner.y) < -6f)
                    Place(NF, Pick("tree_oak", "tree_default", "tree_fat"), new Vector3(corner.x, 0, corner.y), Rnd(0, 360), Rnd(7.5f, 9.5f), g, "trunk");
            }
            var b2 = new WorldMeshBuilder();
            foreach (var p in new[] { new Vector2(-44f, 62f), new Vector2(-38f, 70f), new Vector2(-30f, 58f), new Vector2(-50f, -118f), new Vector2(-40f, -108f) })
                if (!NearKeepClear(p.x, p.y, 2f)) RoundBale(b2, g, G(p.x, p.y) - Vector3.up * 0.05f, Rnd(0, 180));
            BakeBuilding(b2, "SM_World_FieldBales", "Field Bales", g, Vector3.zero, 0f);
        }

        /// <summary>True on roads and built-up farm spots (hedgerows skip them).</summary>
        static bool Blocked2(float x, float z)
        {
            var p = new Vector2(x, z);
            foreach (var r in Roads) if (DistSeg(p, r.a, r.b) < r.w + 1.5f) return true;
            return false;
        }

        static void HayMeadow()
        {
            var g = Group("Hay Meadow");
            var b = new WorldMeshBuilder();
            var spots = new[] { new Vector2(-110f, -60f), new Vector2(-106f, -66f), new Vector2(-101f, -58f), new Vector2(-96f, -64f), new Vector2(-104f, -53f), new Vector2(-98f, -52f) };
            foreach (var s in spots)
            {
                if (NearKeepClear(s.x, s.y, 1.5f)) continue;
                RoundBale(b, g, G(s.x, s.y) - Vector3.up * 0.05f, Rnd(0, 180));
            }
            RoundBale(b, g, G(-107.4f, -61.2f) + Vector3.up * 1.6f, 30f); // one stacked on top
            BakeBuilding(b, "SM_World_HayMeadow", "Round Bales", g, Vector3.zero, 0f);
            Place(FT, "cart-high", new Vector3(-94f, 0, -56f), 120f, 2.0f, g, "box");
        }

        // ------------------------------------------------------------------ farm dressing

        static bool Blocked(float x, float z)
        {
            var p = new Vector2(x, z);
            foreach (var r in Roads) if (DistSeg(p, r.a, r.b) < r.w + 2f) return true;
            if (DistRect(x, z, -46, -14, -4, 14) < 2f) return true;            // barn + yard
            if (DistRect(x, z, -60, 11, -34, 37) < 1f) return true;            // farmhouse + garden + washing line
            if (DistRect(x, z, 5, 26, 27, 42) < 2f) return true;              // pig pen
            if (DistRect(x, z, 7, -45, 25, -27) < 2f) return true;            // coop
            if (DistRect(x, z, -10, 54, 44, 88) < 1f) return true;            // corn maze
            if (DistRect(x, z, -80, 24, -60, 44) < 1f) return true;           // veg
            if (DistRect(x, z, -110, -17, -66, 20) < 1f) return true;         // wheat
            if (DistRect(x, z, -66, -54, -46, -34) < 1f) return true;         // pumpkins
            if ((p - new Vector2(Windmill.x, Windmill.z)).magnitude < 9f) return true;
            if (DistRect(x, z, 0, -82, 52, -54) < 1f) return true;            // orchard
            if (DistRect(x, z, -114, -70, -92, -48) < 1f) return true;        // hay meadow
            if (x > 50f) return true;                                          // bank hedge strip
            if (Outside(x, z) > -16f) return true;                             // edge band (Boundaries)
            if (NearKeepClear(x, z, 1.5f)) return true;
            return false;
        }

        static void FarmDressing()
        {
            var g = Group("Nature Dressing");
            var small = Cullable(Group("Small Dressing"), 12f, 45f);
            // Scattered farm trees, bushes, flowers, rocks, grass tufts (keep clear of roads/fields/spawns).
            for (int k = 0; k < 520; k++)
            {
                float x = Rnd(-118f, 52f), z = Rnd(-140f, 100f);
                if (Blocked(x, z)) continue;
                int kind = _rng.Next(12);
                if (kind < 1) Place(NF, Pick("tree_default", "tree_oak", "tree_detailed", "tree_fat"), new Vector3(x, 0, z), Rnd(0, 360), Rnd(6f, 9f), g, "trunk");
                else if (kind < 3) Place(NF, Pick("plant_bush", "plant_bushSmall", "plant_bushDetailed"), new Vector3(x, 0, z), Rnd(0, 360), Rnd(0.8f, 1.4f), small);
                else if (kind < 7) Place(NF, Pick("flower_yellowA", "flower_purpleA", "flower_yellowB", "flower_purpleB", "flower_redA", "flower_yellowC"), new Vector3(x, 0, z), Rnd(0, 360), Rnd(0.35f, 0.55f), small);
                else if (kind < 8) Place(NF, Pick("rock_smallB", "rock_smallA", "stone_smallA"), new Vector3(x, 0, z), Rnd(0, 360), Rnd(0.4f, 0.9f), small);
                else if (kind < 9) Place(NF, Pick("stump_round", "log", "mushroom_tanGroup"), new Vector3(x, 0, z), Rnd(0, 360), Rnd(0.3f, 0.6f), small);
                else Place(NF, Pick("grass_large", "grass", "grass_leafsLarge"), new Vector3(x, 0, z), Rnd(0, 360), Rnd(0.4f, 0.75f), small);
            }
            // Flower beds along the main road
            for (float x = -8f; x < 52f; x += 3.2f)
                foreach (float s in new[] { -1f, 1f })
                    if (_rng.NextDouble() < 0.6 && !NearKeepClear(x, s * 4.1f)) Place(NF, _rng.NextDouble() < 0.5 ? "flower_yellowC" : "flower_purpleA", new Vector3(x + Rnd(-0.8f, 0.8f), 0, s * Rnd(3.6f, 4.6f)), Rnd(0, 360), 0.45f, small);
            // Tree groves and roadside cover so the spawn view has depth and hiding spots (GDD 11.5.6).
            foreach (var grove in new[] { new Vector2(-8f, 26f), new Vector2(-6f, -26f), new Vector2(36f, 16f), new Vector2(38f, -20f), new Vector2(-26f, 34f), new Vector2(-38f, -26f) })
                for (int k = 0; k < 7; k++)
                {
                    var p = new Vector3(grove.x + Rnd(-6f, 6f), 0, grove.y + Rnd(-5f, 5f));
                    if (NearKeepClear(p.x, p.z, 1.5f) || DistRect(p.x, p.z, -60, 11, -34, 37) < 1f) continue;
                    if (k < 3) Place(NF, k % 2 == 0 ? "tree_oak" : "tree_default", p, Rnd(0, 360), Rnd(6f, 8.5f), g, "trunk");
                    else Place(NF, "plant_bushLarge", p, Rnd(0, 360), Rnd(1.2f, 1.8f), g, "box");
                }
            // Road hay bales (minor obstacles that make a two-person carry wobble) and fences.
            var hb = new WorldMeshBuilder();
            var hay = Group("Road Hay", g);
            foreach (var hp in new[] { new Vector3(14f, 0, 5.5f), new Vector3(26f, 0, -5.8f), new Vector3(-2f, 0, 6f), new Vector3(40f, 0, 6.2f) })
                HayBale(hb, hay, G(hp.x, hp.z, 0.45f), Rnd(-20, 20));
            BakeBuilding(hb, "SM_World_RoadHay", "Road Hay Bales", hay, Vector3.zero, 0f);
            FenceLine(new Vector3(-6f, 0, 3.8f), new Vector3(6f, 0, 3.8f), g);
            FenceLine(new Vector3(9f, 0, -3.8f), new Vector3(24f, 0, -3.8f), g);
            // Fence the road edge near the bridge so the chokepoint reads.
            FenceLine(new Vector3(44f, 0, 3.6f), new Vector3(57.5f, 0, 3.4f), g);
            FenceLine(new Vector3(44f, 0, -3.6f), new Vector3(57.5f, 0, -3.4f), g);
            Mud("Mud Patch (bridge approach)", new Vector3(53f, 0, 4.5f), new Vector3(6.4f, 2f, 4.4f), g);
            Mud("Mud Patch (fork)", new Vector3(-4f, 0, -7f), new Vector3(5.6f, 2f, 4f), g);
            // Fingerpost at the fork, read by the crew walking east from the barn.
            var fp = new Vector3(5.6f, 0f, -4.6f);
            float fy = GroundY(fp.x, fp.z);
            Block("Fingerpost", new Vector3(fp.x, fy + 1.3f, fp.z), new Vector3(0.14f, 2.6f, 0.14f), Mats.WoodDark, g);
            SignBoard("^ MARKET ^", new Vector3(fp.x - 0.09f, fy + 2.25f, fp.z), -90f, 1.5f, 0.32f, g, Mats.SignBoard, Ink);
            SignBoard("< PIGS", new Vector3(fp.x - 0.09f, fy + 1.85f, fp.z + 0.25f), -90f, 1.2f, 0.32f, g, Mats.SignBoard, Ink);
            SignBoard("CHICKENS >", new Vector3(fp.x - 0.09f, fy + 1.45f, fp.z - 0.2f), -90f, 1.4f, 0.32f, g, Mats.SignBoard, Ink);
        }

        // ------------------------------------------------------------------ boundaries: soft natural walls inside the invisible ones

        static void Boundaries()
        {
            var g = Group("Boundaries");
            var bush = Cullable(Group("Edge Undergrowth", g), 14f, 60f);
            // Farm boundary rail fence (with a few broken gaps) a few metres inside the west / north / south edges.
            var fence = Group("Boundary Fence", g);
            void Run(Vector3 a, Vector3 b)
            {
                Vector3 d = b - a; float len = d.magnitude; int n = Mathf.CeilToInt(len / 14f);
                for (int i = 0; i < n; i++)
                {
                    if (_rng.NextDouble() < 0.12) continue; // broken section
                    FenceLine(a + d * (i / (float)n), a + d * ((i + 1) / (float)n), fence);
                }
            }
            Run(new Vector3(-114f, 0, -138f), new Vector3(-114f, 0, 95f));
            Run(new Vector3(-114f, 0, 95f), new Vector3(50f, 0, 95f));
            Run(new Vector3(-114f, 0, -139f), new Vector3(44f, 0, -139f));
            // Edge forest band (trunk colliders) between the fence and the invisible walls, plus undergrowth.
            for (int k = 0; k < 2600; k++)
            {
                float x = Rnd(PlayX0 + 1f, PlayX1 - 1f), z = Rnd(PlayZ0 + 1f, PlayZ1 - 1f);
                float o = Outside(x, z);
                if (o < -15f || o > -1.5f) continue;
                if (Mathf.Abs(x - RiverCentre) < 23f) continue;                        // river + banks
                if ((new Vector2(x, z) - new Vector2(Pile.x, Pile.z)).magnitude < 12f) continue;
                if (NearKeepClear(x, z, 2f)) continue;
                if (DistSeg(new Vector2(x, z), new Vector2(136, 30), new Vector2(136, 104)) < 4f || DistSeg(new Vector2(x, z), new Vector2(136, -30), new Vector2(136, -148)) < 4f) continue;
                if (x > 100f && x < 172f && Mathf.Abs(z) < 64f) continue;                   // town
                float n = Mathf.PerlinNoise(x * 0.07f + 3f, z * 0.07f + 9f);
                if (n < 0.38f) continue;
                if (_rng.NextDouble() < 0.42)
                    Place(NF, Pick("tree_default", "tree_oak", "tree_fat", "tree_tall", "tree_pineRoundA", "tree_pineTallA", "tree_plateau"), new Vector3(x, 0, z), Rnd(0, 360), Rnd(7f, 12f), g, "trunk");
                else
                    Place(NF, Pick("plant_bushLarge", "plant_bushDetailed", "plant_bush", "grass_large", "rock_largeC"), new Vector3(x, 0, z), Rnd(0, 360), Rnd(0.8f, 2.0f), bush);
            }
            // The town lanes end in a fence and a sign instead of an invisible wall.
            foreach (float zEnd in new[] { 99f, -143f })
            {
                FenceLine(new Vector3(132.5f, 0, zEnd), new Vector3(139.5f, 0, zEnd), g, NF, "fence_simpleHigh", 1.2f);
                SignPost("ROAD CLOSED\n(cows)", new Vector3(136f, 0, zEnd + (zEnd > 0 ? -1.2f : 1.2f)), zEnd > 0 ? 180f : 0f, 1.6f, 0.75f, g, Mats.SignBoard, Ink, postHeight: 0.9f);
            }
        }
    }
}
