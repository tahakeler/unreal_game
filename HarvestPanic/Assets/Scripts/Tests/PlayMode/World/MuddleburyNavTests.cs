using System.Collections;
using HarvestPanic.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HarvestPanic.Tests.PlayMode.World
{
    /// <summary>
    /// NavMesh reachability on the built Muddlebury (critters use NavMeshAgents; the humanoid agent also stands in for a
    /// walking farmer): every critter spawn can reach the Sale Pen over the bridge, the crew can walk barn -> market at the
    /// GDD 11.5.5 distances, the Lost &amp; Found walk of shame leads back to the bridge, and the barn interior is open.
    /// </summary>
    public class MuddleburyNavTests
    {
        const string ScenePath = "Assets/Scenes/Muddlebury.unity";

        IEnumerator Load()
        {
#if UNITY_EDITOR
            var op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone) yield return null;
            yield return null;
#else
            Assert.Ignore("Editor-only scene loading");
            yield break;
#endif
        }

        static Vector3 OnMesh(Vector3 p, string what)
        {
            Assert.IsTrue(NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas), $"{what} {p} is off the navmesh");
            return hit.position;
        }

        static float PathLength(Vector3 from, Vector3 to, string what)
        {
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path), $"{what}: no path");
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, $"{what}: path {path.status}");
            float len = 0f;
            for (int i = 1; i < path.corners.Length; i++) len += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            Debug.Log($"[MapNav] {what}: {len:0} m (walk {len / 4f:0} s, sprint {len / 6.5f:0} s)");
            return len;
        }

        static Vector3 PenCentre() => OnMesh(LevelMarker.FindAll(MarkerRole.MarketPen)[0].transform.position, "Sale Pen");

        [UnityTest] public IEnumerator EveryCritterSpawnReachesTheSalePen()
        {
            yield return Load();
            var pen = PenCentre();
            var spawners = LevelMarker.FindAll(MarkerRole.CritterSpawner);
            Assert.That(spawners.Count, Is.GreaterThanOrEqualTo(5));
            foreach (var s in spawners)
            {
                float len = PathLength(OnMesh(s.transform.position, s.Key), pen, s.Key + " -> Sale Pen");
                Assert.That(len, Is.InRange(90f, 260f), $"{s.Key} -> Sale Pen is {len:0} m");
            }
        }

        [UnityTest] public IEnumerator CrewWalksBarnToMarketAtDocDistances()
        {
            yield return Load();
            var pen = PenCentre();
            var start = OnMesh(LevelMarker.FindAll(MarkerRole.PlayerStart)[0].transform.position, "PlayerStart 0");
            float spawnToPen = PathLength(start, pen, "spawn -> Sale Pen");
            Assert.That(spawnToPen, Is.InRange(110f, 145f), $"spawn -> Sale Pen {spawnToPen:0} m (GDD 11.5.5: ~125 m, rule 70-140 to the chokepoint)");
            var respawn = OnMesh(LevelMarker.FindAll(MarkerRole.BarnRespawn)[0].transform.position, "BarnRespawn");
            PathLength(respawn, pen, "barn respawn -> Sale Pen");
            var pumpkin = OnMesh(new Vector3(-56f, 0f, -44f), "pumpkin patch");
            float pumpkinRun = PathLength(pumpkin, pen, "pumpkin patch -> Sale Pen");
            Assert.That(pumpkinRun, Is.InRange(160f, 240f), $"pumpkin run {pumpkinRun:0} m (GDD ~195 m)");
            var hall = OnMesh(new Vector3(160f, 0f, 0f), "town hall forecourt");
            PathLength(pen, hall, "pen -> town hall");
        }

        [UnityTest] public IEnumerator LostAndFoundLeadsBackToTheBridge()
        {
            yield return Load();
            var pile = OnMesh(LevelMarker.FindAll(MarkerRole.RiverPile)[0].transform.position, "RiverPile");
            var planks = LevelMarker.FindAll(MarkerRole.BridgePlank);
            var west = OnMesh(planks[0].transform.position + new Vector3(-3f, 0.2f, 0f), "bridge west end");
            float len = PathLength(pile, west, "Lost & Found -> bridge");
            Assert.That(len, Is.InRange(110f, 200f), $"walk of shame {len:0} m (GDD ~150 m)");
        }

        [UnityTest] public IEnumerator BarnInteriorAndWardrobeAreReachable()
        {
            yield return Load();
            var start = OnMesh(LevelMarker.FindAll(MarkerRole.PlayerStart)[0].transform.position, "PlayerStart 0");
            var w = LevelMarker.FindAll(MarkerRole.Wardrobe)[0].transform;
            var front = OnMesh(w.position + w.forward * 1.3f, "wardrobe front");
            float len = PathLength(start, front, "spawn -> wardrobe");
            Assert.That(len, Is.LessThan(25f), $"wardrobe is {len:0} m from the spawn");
            var loftSide = OnMesh(new Vector3(-31f, 0f, 0f), "barn west end");
            PathLength(start, loftSide, "spawn -> back of the barn");
        }
    }
}
