using System;
using System.Collections.Generic;
using System.Linq;
using HarvestPanic.Core;
using HarvestPanic.World;
using HarvestPanic.World.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HarvestPanic.Tests.EditMode.World
{
    /// <summary>
    /// Layout guards for the built Muddlebury scene (docs/qa/map-progress.md): every marker role is placed, spawn-type
    /// markers stand on open ground (not inside any collider, not floating), the playable area is closed by the world
    /// edges, the wardrobe is inside the barn with room to use it, and the scenery never adds colliders or broken renderers.
    /// </summary>
    public class MuddleburyLayoutTests
    {
        [OneTimeSetUp] public void Open()
        {
            Assume.That(System.IO.File.Exists(BuildMuddlebury.ScenePath), "run BuildMuddlebury.Build first");
            EditorSceneManager.OpenScene(BuildMuddlebury.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
        }

        static readonly MarkerRole[] StandOn = { MarkerRole.PlayerStart, MarkerRole.BarnRespawn, MarkerRole.CritterSpawner, MarkerRole.Goods, MarkerRole.Wardrobe, MarkerRole.RiverPile };

        static bool IsGround(Collider c) => c is TerrainCollider;

        static List<Collider> Blocking(Vector3 p, float radius, LevelMarker self)
        {
            var hits = Physics.OverlapSphere(p, radius, ~0, QueryTriggerInteraction.Ignore);
            return hits.Where(h => !IsGround(h) && !h.transform.IsChildOf(self.transform)).ToList();
        }

        [Test] public void EveryMarkerRoleIsPlaced()
        {
            foreach (MarkerRole role in Enum.GetValues(typeof(MarkerRole)))
                Assert.That(LevelMarker.FindAll(role).Count, Is.GreaterThan(0), $"no {role} marker");
        }

        [Test] public void SpawnMarkersAreNotInsideColliders()
        {
            var bad = new List<string>();
            foreach (var role in StandOn)
            foreach (var m in LevelMarker.FindAll(role))
            {
                var p = m.transform.position;
                foreach (float h in new[] { 0.45f, 1.0f })
                {
                    var hit = Blocking(p + Vector3.up * h, 0.3f, m);
                    if (hit.Count > 0) bad.Add($"{m.name} @ {p} +{h}m overlaps {string.Join(", ", hit.Select(c => c.name + "/" + c.transform.parent?.name))}");
                }
            }
            Assert.That(bad, Is.Empty, string.Join("\n", bad));
        }

        [Test] public void SpawnMarkersStandOnTheGround()
        {
            foreach (var role in StandOn)
            foreach (var m in LevelMarker.FindAll(role))
            {
                var p = m.transform.position;
                Assert.IsTrue(Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore), $"{m.name}: nothing below");
                Assert.That(p.y - hit.point.y, Is.InRange(-0.05f, 0.6f), $"{m.name} floats or is buried ({p.y - hit.point.y:0.00} m)");
            }
        }

        [Test] public void AllMarkersAreInsideThePlayableArea()
        {
            foreach (var m in Object.FindObjectsByType<LevelMarker>(FindObjectsSortMode.None))
            {
                var p = m.transform.position;
                if (m.Role == MarkerRole.River) continue; // the river volume runs the full tile length on purpose
                Assert.That(p.x, Is.InRange(BuildMuddlebury.PlayX0, BuildMuddlebury.PlayX1), m.name);
                Assert.That(p.z, Is.InRange(BuildMuddlebury.PlayZ0, BuildMuddlebury.PlayZ1), m.name);
            }
        }

        [Test] public void WorldEdgesCloseThePlayableArea()
        {
            var edges = new Dictionary<string, BoxCollider>();
            foreach (var n in new[] { "Edge_W", "Edge_E", "Edge_S", "Edge_N" })
            {
                var go = GameObject.Find(n);
                Assert.IsNotNull(go, n);
                var bc = go.GetComponent<BoxCollider>();
                Assert.IsNotNull(bc, n);
                Assert.IsFalse(bc.isTrigger, n);
                Assert.That(bc.bounds.size.y, Is.GreaterThanOrEqualTo(40f), $"{n} too low to stop a jump off a hill");
                edges[n] = bc;
            }
            // the four walls overlap at the corners: no gap a farmer can squeeze through
            Assert.That(edges["Edge_S"].bounds.min.x, Is.LessThanOrEqualTo(edges["Edge_W"].bounds.max.x));
            Assert.That(edges["Edge_S"].bounds.max.x, Is.GreaterThanOrEqualTo(edges["Edge_E"].bounds.min.x));
            Assert.That(edges["Edge_N"].bounds.min.x, Is.LessThanOrEqualTo(edges["Edge_W"].bounds.max.x));
            Assert.That(edges["Edge_N"].bounds.max.x, Is.GreaterThanOrEqualTo(edges["Edge_E"].bounds.min.x));
            Assert.That(edges["Edge_W"].bounds.min.z, Is.LessThanOrEqualTo(edges["Edge_S"].bounds.max.z));
            Assert.That(edges["Edge_W"].bounds.max.z, Is.GreaterThanOrEqualTo(edges["Edge_N"].bounds.min.z));
            // and nothing escapes: horizontal rays from inside always hit something before leaving the rectangle
            var origins = new[] { new Vector2(-11f, 0f), new Vector2(114f, 0f), new Vector2(-86f, 10f), new Vector2(20f, -100f), new Vector2(150f, 60f), new Vector2(-60f, 80f) };
            foreach (var o in origins)
            {
                float y = BuildMuddlebury.GroundY(o.x, o.y) + 1.2f;
                for (int i = 0; i < 72; i++)
                {
                    float a = i * Mathf.PI * 2f / 72f;
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var from = new Vector3(o.x, y, o.y);
                    Assert.IsTrue(Physics.Raycast(from, dir, out var hit, 800f, ~0, QueryTriggerInteraction.Ignore), $"ray from {o} at {i * 5} deg escapes the map");
                    Assert.That(hit.point.x, Is.InRange(BuildMuddlebury.PlayX0 - 3f, BuildMuddlebury.PlayX1 + 3f), $"ray from {o} at {i * 5} deg left through x");
                    Assert.That(hit.point.z, Is.InRange(BuildMuddlebury.PlayZ0 - 3f, BuildMuddlebury.PlayZ1 + 3f), $"ray from {o} at {i * 5} deg left through z");
                }
            }
        }

        [Test] public void WardrobeIsInsideTheBarnWithRoomToUseIt()
        {
            var w = LevelMarker.FindAll(MarkerRole.Wardrobe)[0];
            var p = w.transform.position;
            Assert.That(p.x, Is.InRange(-34.7f, -17.3f), "wardrobe x inside the barn");
            Assert.That(p.z, Is.InRange(-6.7f, 6.7f), "wardrobe z inside the barn");
            // Customization builds a 1.3 x 2.2 x 0.65 cabinet at the marker and an interact zone 1.1 m in front of it.
            var rot = w.transform.rotation;
            var cabinet = Physics.OverlapBox(p + Vector3.up * 1.15f, new Vector3(0.62f, 1.05f, 0.3f), rot, ~0, QueryTriggerInteraction.Ignore).Where(c => !IsGround(c)).ToList();
            Assert.That(cabinet, Is.Empty, "cabinet space blocked by " + string.Join(", ", cabinet.Select(c => c.name)));
            var front = Physics.OverlapBox(p + rot * new Vector3(0f, 1f, 1.2f), new Vector3(0.9f, 0.9f, 0.7f), rot, ~0, QueryTriggerInteraction.Ignore).Where(c => !IsGround(c)).ToList();
            Assert.That(front, Is.Empty, "space in front of the wardrobe blocked by " + string.Join(", ", front.Select(c => c.name)));
            // and the barn is hollow: a ray from the doorway to the wardrobe front is clear
            var door = new Vector3(-16f, 1.2f, 0f);
            var target = p + rot * new Vector3(0f, 1.2f, 1.1f);
            Assert.IsFalse(Physics.Linecast(door, target, out var block, ~0, QueryTriggerInteraction.Ignore), $"doorway -> wardrobe blocked by {block.collider?.name}");
        }

        [Test] public void SceneryTilesHaveNoCollidersAndEveryRendererIsComplete()
        {
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            Assert.That(terrains.Length, Is.EqualTo(9), "3 x 3 terrain grid");
            Assert.That(terrains.Count(t => t.GetComponent<TerrainCollider>() != null), Is.EqualTo(1), "only the playable tile collides");
            Assert.That(terrains.All(t => t.drawInstanced && !t.drawTreesAndFoliage), "instanced terrain, no per-tree terrain draws");
            var forest = Object.FindFirstObjectByType<InstancedForest>();
            Assert.IsNotNull(forest, "scenery forest");
            Assert.That(forest.InstanceCount, Is.GreaterThan(2000), "the bowl reads as forested");
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (r.GetComponent<TextMesh>() != null) continue;
                var mf = r.GetComponent<MeshFilter>();
                Assert.IsNotNull(mf != null ? mf.sharedMesh : null, $"{r.name} has no mesh");
                Assert.That(r.sharedMaterials.All(m => m != null), $"{r.name} has a missing material");
                Assert.That(r.sharedMaterials.Length, Is.GreaterThanOrEqualTo(mf.sharedMesh.subMeshCount), $"{r.name} submeshes without material");
            }
        }

        [Test] public void SignsAreLitAndReadable()
        {
            var texts = Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None);
            Assert.That(texts.Length, Is.GreaterThanOrEqualTo(20), "signage");
            foreach (var t in texts)
            {
                Assert.IsNotNull(t.font, t.name);
                Assert.IsNotNull(t.GetComponent<LitText>(), $"{t.name} would glow unlit at night");
                Assert.That(t.characterSize * t.fontSize * 0.1f, Is.GreaterThan(0.05f), $"{t.name} lettering too small to read");
            }
            Assert.IsNotNull(Object.FindFirstObjectByType<NightWindows>(), "night window swap");
        }
    }
}
