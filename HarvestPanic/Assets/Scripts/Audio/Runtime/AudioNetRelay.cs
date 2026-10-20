using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Audio
{
    /// <summary>
    /// Forwards server-routed noise cues to clients (noise events are raised on the server only). Host already played
    /// the cue locally, so the RPC targets NotServer (no double play on host). Footsteps go unreliable, the rest reliable.
    /// Spawned by <see cref="AudioNetBootstrapper"/>; prefab listed in Resources/NetworkPrefabs/Audio.
    /// </summary>
    public sealed class AudioNetRelay : NetworkBehaviour
    {
        public static AudioNetRelay Instance { get; private set; }

        /// <summary>Cues received from the server (tests).</summary>
        public int ReceivedCount { get; private set; }

        public override void OnNetworkSpawn() { Instance = this; }

        public override void OnNetworkDespawn() { if (Instance == this) Instance = null; }

        /// <summary>Server: send <paramref name="cue"/> to every non-host client. No-op offline or on clients.</summary>
        public static void Broadcast(in SoundCue cue)
        {
            var r = Instance;
            if (r == null || !r.IsSpawned || !r.IsServer) return;
            int id = AudioEvents.IndexOf(cue.Event);
            if (id < 0) return;
            var gain = (sbyte)Mathf.Clamp(Mathf.RoundToInt(cue.GainDb), -80, 20);
            if (cue.Event == AudioEvents.FootstepDirt) r.CueUnreliableRpc((ushort)id, cue.Position, gain);
            else r.CueRpc((ushort)id, cue.Position, gain);
        }

        [Rpc(SendTo.NotServer)]
        void CueRpc(ushort id, Vector3 pos, sbyte gainDb) => Receive(id, pos, gainDb);

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        void CueUnreliableRpc(ushort id, Vector3 pos, sbyte gainDb) => Receive(id, pos, gainDb);

        void Receive(ushort id, Vector3 pos, sbyte gainDb)
        {
            if (id >= AudioEvents.All.Length) return;
            ReceivedCount++;
            var cue = new SoundCue { Event = AudioEvents.All[id].Name, Position = pos, GainDb = gainDb };
            if (Services.TryGet(out IAudioService svc) && svc is AudioService a) { a.OnCueHeard(cue); a.PlayCue(cue); }
            else if (svc != null) svc.PlayAt(cue.Event, pos, AudioMath.DbToLinear(gainDb));
        }
    }

    /// <summary>
    /// Server hook (same pattern as EconomyBootstrapper): once a NetworkManager is listening as server, spawns the
    /// AudioNetRelay from Resources/NetworkPrefabs/Audio. Hidden, DontDestroyOnLoad, polls twice per second.
    /// </summary>
    public sealed class AudioNetBootstrapper : MonoBehaviour
    {
        public const string ListPath = "NetworkPrefabs/Audio";
        float _next;
        bool _warned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var go = new GameObject("[AudioNetBootstrapper]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<AudioNetBootstrapper>();
        }

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !nm.IsServer || AudioNetRelay.Instance != null) return;
            var prefab = FindRelayPrefab();
            if (prefab == null) { if (!_warned) Debug.LogWarning("[Audio] No AudioNetRelay prefab in Resources/" + ListPath); _warned = true; return; }
            var inst = Instantiate(prefab);
            inst.GetComponent<NetworkObject>().Spawn(false);
        }

        /// <summary>The relay prefab from the Audio network prefab list.</summary>
        public static GameObject FindRelayPrefab()
        {
            var list = Resources.Load<NetworkPrefabsList>(ListPath);
            if (list == null) return null;
            foreach (var p in list.PrefabList)
                if (p.Prefab != null && p.Prefab.GetComponent<AudioNetRelay>() != null) return p.Prefab;
            return null;
        }
    }
}
