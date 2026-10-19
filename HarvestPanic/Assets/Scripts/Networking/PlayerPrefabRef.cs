using UnityEngine;

namespace HarvestPanic.Networking
{
    /// <summary>
    /// Which prefab the session spawns for each player. Asset: Resources/Data/Networking/PlayerPrefabRef.asset.
    /// The Player team points <see cref="prefab"/> at its player prefab (must have a NetworkObject and be in
    /// one of the Resources/NetworkPrefabs lists; if it is not, Networking registers it at startup).
    /// Ships with a placeholder capsule (Prefabs/Networking/PlaceholderPlayer.prefab).
    /// </summary>
    [CreateAssetMenu(menuName = "Harvest Panic/Networking/Player Prefab Ref", fileName = "PlayerPrefabRef")]
    public sealed class PlayerPrefabRef : ScriptableObject
    {
        public const string ResourcePath = "Data/Networking/PlayerPrefabRef";

        [Tooltip("Root must have a NetworkObject. Spawned with SpawnAsPlayerObject at a PlayerStart marker.")]
        public GameObject prefab;

        public static PlayerPrefabRef Load() => Resources.Load<PlayerPrefabRef>(ResourcePath);
    }
}
