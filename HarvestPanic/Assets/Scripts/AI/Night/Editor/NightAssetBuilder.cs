using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace HarvestPanic.AI.Night.Editor
{
    /// <summary>Builds Prefabs/Night (director, Mr. Stitches, fox) and Resources/NetworkPrefabs/Night. -executeMethod HarvestPanic.AI.Night.Editor.NightAssetBuilder.Build</summary>
    public static class NightAssetBuilder
    {
        const string Dir = "Assets/Prefabs/Night";

        [MenuItem("HarvestPanic/Night/Build Assets")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Night");
            var stitches = Creature("MrStitches", go => { go.AddComponent<MrStitches>(); var c = go.AddComponent<CapsuleCollider>(); c.isTrigger = true; c.center = Vector3.up * 1.3f; c.height = 2.6f; c.radius = 0.5f; });
            var fox = Creature("NightFox", go => { go.AddComponent<NightFox>(); var c = go.AddComponent<CapsuleCollider>(); c.isTrigger = true; c.center = Vector3.up * 0.4f; c.height = 0.9f; c.radius = 0.3f; c.direction = 2; });

            var d = new GameObject("NightDirector");
            d.AddComponent<NetworkObject>();
            var dir = d.AddComponent<NightDirector>();
            var pres = d.AddComponent<NightPresenter>();
            var so = new SerializedObject(dir);
            so.FindProperty("stitchesPrefab").objectReferenceValue = stitches;
            so.FindProperty("foxPrefab").objectReferenceValue = fox;
            so.ApplyModifiedPropertiesWithoutUndo();
            var lantern = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Environment/KenneyFantasyTown/lantern.fbx");
            var ps = new SerializedObject(pres); ps.FindProperty("lanternPostPrefab").objectReferenceValue = lantern; ps.ApplyModifiedPropertiesWithoutUndo();
            var director = PrefabUtility.SaveAsPrefabAsset(d, Dir + "/NightDirector.prefab");
            Object.DestroyImmediate(d);

            const string listPath = "Assets/Resources/NetworkPrefabs/Night.asset";
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(listPath);
            if (list == null) { list = ScriptableObject.CreateInstance<NetworkPrefabsList>(); AssetDatabase.CreateAsset(list, listPath); }
            foreach (var p in new List<NetworkPrefab>(list.PrefabList)) list.Remove(p);
            foreach (var p in new[] { director, stitches, fox }) list.Add(new NetworkPrefab { Prefab = p });
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            Debug.Log("[Night] assets built");
        }

        static GameObject Creature(string name, System.Action<GameObject> setup)
        {
            var go = new GameObject(name) { layer = 6 };
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            setup(go);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Dir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
