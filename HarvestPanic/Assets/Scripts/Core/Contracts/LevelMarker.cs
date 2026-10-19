using System.Collections.Generic;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>
    /// Data component on an empty GameObject that marks where gameplay content goes. The World team places
    /// markers in scenes; spawning teams call <see cref="FindAll"/> and instantiate their own content.
    /// Markers carry no gameplay logic and never reference gameplay assemblies.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelMarker : MonoBehaviour
    {
        [SerializeField] MarkerRole role;
        [SerializeField, Tooltip("CritterSpawner: critter id (Critter.Pig). Goods: goods id (Livestock.Pig).")] string key;
        [SerializeField, Min(1), Tooltip("CritterSpawner: how many to spawn.")] int count = 1;
        [SerializeField, Min(0), Tooltip("BridgePlank: order along the bridge, 0 = west end. PlayerStart: slot 0..3.")] int index;

        /// <summary>What this marker is for.</summary>
        public MarkerRole Role => role;
        /// <summary>Content key (species or goods id); empty for roles that need none.</summary>
        public string Key => key;
        /// <summary>Spawn count.</summary>
        public int Count => count;
        /// <summary>Ordering index.</summary>
        public int Index => index;

        /// <summary>Configures a marker created from code (editor tools, tests).</summary>
        public void Configure(MarkerRole markerRole, string contentKey = null, int spawnCount = 1, int orderIndex = 0)
        {
            role = markerRole;
            key = contentKey;
            count = Mathf.Max(1, spawnCount);
            index = Mathf.Max(0, orderIndex);
        }

        /// <summary>Finds active markers of <paramref name="markerRole"/>, ordered by <see cref="Index"/>.</summary>
        public static List<LevelMarker> FindAll(MarkerRole markerRole)
        {
            var all = FindObjectsByType<LevelMarker>(FindObjectsSortMode.None);
            var result = new List<LevelMarker>();
            foreach (var m in all) if (m.role == markerRole) result.Add(m);
            result.Sort((a, b) => a.index.CompareTo(b.index));
            return result;
        }

#if UNITY_EDITOR
        void Reset() => ApplyTag();
        void OnValidate() => ApplyTag();

        void ApplyTag()
        {
            string tag = MarkerTag.ForRole(role);
            if (!CompareTag(tag)) gameObject.tag = tag;
        }
#endif
    }
}
