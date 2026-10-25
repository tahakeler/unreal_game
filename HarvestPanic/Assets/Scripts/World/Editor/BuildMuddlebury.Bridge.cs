using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HarvestPanic.World.Editor
{
    public static partial class BuildMuddlebury
    {
        // ------------------------------------------------------------------ bridge (gameplay: 40 m, 20 x 2 m planks, 0.7 m sag)

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
                    Rope(pa, pb, 0.07f, Mats.Rope, g);
                }
                for (int i = 0; i <= PlankCount; i += 2)
                {
                    float t = i / (float)PlankCount;
                    float x = RiverWest + i * PlankLength;
                    var top = new Vector3(x, PlankMath.Sag(t, BridgeSag * 0.85f) + 1.05f, z);
                    var bottom = new Vector3(x, PlankMath.Sag(t, BridgeSag), z);
                    Rope(bottom, top, 0.035f, Mats.Rope, g);
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

        // ------------------------------------------------------------------ bridge approaches (dressing only)

        static void BridgeArea()
        {
            var g = Group("Bridge Approaches");
            // Stone-and-timber footings under the posts on both banks: anchor the bridge visually (below the deck, out of the walk line).
            var mb = new WorldMeshBuilder();
            foreach (float x in new[] { RiverWest - 1.2f, RiverEast + 1.2f })
            foreach (float side in new[] { -1f, 1f })
            {
                float z = side * (PlankWidth / 2f + 0.15f);
                float gy = GroundY(x, z);
                mb.Box(new Vector3(x, gy - 0.6f, z), new Vector3(1.1f, 1.6f, 1.1f), Mats.StoneDark, 1.2f);
            }
            foreach (float x in new[] { RiverWest - 0.4f, RiverEast + 0.4f })
            {
                float gy = Mathf.Min(GroundY(x, 0f), -0.3f);
                mb.Box(new Vector3(x, gy - 0.9f, 0f), new Vector3(1.6f, 2.0f, PlankWidth + 1.4f), Mats.Stone, 1.2f); // bank seat under the deck ends
            }
            BakeBuilding(mb, "SM_World_BridgeFootings", "Bridge Footings", g, Vector3.zero, 0f);
            // Rocks tumbling down the banks either side of the deck (they sit on the steep slope, below the walk line).
            foreach (float bx in new[] { RiverWest + 0.5f, RiverEast - 0.5f })
            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i < 4; i++)
                {
                    float z = side * Rnd(2.8f, 6.5f), x = bx + (bx < RiverCentre ? Rnd(-1f, 2.5f) : Rnd(-2.5f, 1f));
                    Place(FT, Pick("rock-large", "rock-wide", "rock-small"), new Vector3(x, -0.2f, z), Rnd(0, 360), Rnd(0.8f, 1.6f), g);
                }
            // Lanterns on both ends (the east one is also a night lantern-post spot).
            foreach (var p in new[] { new Vector3(RiverWest - 2.6f, 0, 2.6f), new Vector3(RiverEast + 2.6f, 0, -2.6f) })
                Place(FT, "lantern", p, 0f, 2.6f, g);
            // Warning sign at the west end, facing the farm (players arrive from the west).
            SignPost("ONE AT A\nTIME!", new Vector3(RiverWest - 3.2f, 0f, -3.1f), -90f, 1.5f, 0.9f, g, Mats.SignBoard, WorldAssets.Hex("2B2118"));
            SignPost("MARKET >", new Vector3(RiverWest - 5f, 0f, 3.3f), -90f, 1.4f, 0.5f, g, Mats.SignBoard, WorldAssets.Hex("2B2118"), postHeight: 1.6f);
            // East landing: welcome board facing the bridge.
            SignPost("MUDDLEBURY\nMARKET", new Vector3(RiverEast + 3.6f, 0f, 3.4f), -90f, 1.7f, 0.95f, g, Mats.SignBoardDark, WorldAssets.Hex("F4E9CF"));
            // The river path north and south of the bridge on the farm side keeps its fences; a few sacks/crates at the west
            // end tell the "staging area" story (players pile goods here before the crossing).
            Place(SV, "box-large", new Vector3(54.5f, 0, -5.4f), 12f, 0.9f, g, "box");
            Place(FD, "bag", new Vector3(55.6f, 0, -5.9f), 40f, 0.7f, g);
            Place(SV, "barrel", new Vector3(53.4f, 0, -6.2f), 0f, 1.0f, g, "box");
        }
    }
}
