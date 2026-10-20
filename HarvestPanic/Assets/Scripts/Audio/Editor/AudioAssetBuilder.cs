using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace HarvestPanic.Audio.Editor
{
    /// <summary>
    /// Builds Prefabs/Audio/AudioNetRelay.prefab and Resources/NetworkPrefabs/Audio.asset, and applies audio import
    /// settings (music/ambience streamed, short SFX decompress-on-load). Batch: -executeMethod HarvestPanic.Audio.Editor.AudioAssetBuilder.BuildAll
    /// </summary>
    public static class AudioAssetBuilder
    {
        const string PrefabPath = "Assets/Prefabs/Audio/AudioNetRelay.prefab";
        const string ListPath = "Assets/Resources/NetworkPrefabs/Audio.asset";
        const string ClipRoot = "Assets/Audio/Resources/Audio";

        [MenuItem("HarvestPanic/Audio/Build Audio Assets")]
        public static void BuildAll()
        {
            BuildRelay();
            ApplyImportSettings();
            AssetDatabase.SaveAssets();
        }

        public static void BuildRelay()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                var go = new GameObject("AudioNetRelay");
                go.AddComponent<NetworkObject>();
                go.AddComponent<AudioNetRelay>();
                prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Object.DestroyImmediate(go);
            }
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(ListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, ListPath);
            }
            if (!list.Contains(prefab)) list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
        }

        public static void ApplyImportSettings()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ClipRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter imp)) continue;
                string folder = Path.GetFileName(Path.GetDirectoryName(path));
                bool longLoop = folder.StartsWith("MUS_") || folder.StartsWith("AMB_");
                var s = imp.defaultSampleSettings;
                var want = longLoop ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                if (s.loadType == want && imp.forceToMono == !longLoop) continue;
                s.loadType = want;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = longLoop ? 0.6f : 0.7f;
                imp.defaultSampleSettings = s;
                imp.forceToMono = !longLoop;
                imp.SaveAndReimport();
            }
        }
    }
}
