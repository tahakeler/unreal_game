using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.World
{
    /// <summary>
    /// In-scene NetworkObject on the bridge root: server writes plank states into a NetworkList, clients apply them
    /// (reliable, on change). Planks themselves stay plain MonoBehaviours so they run offline too.
    /// </summary>
    [RequireComponent(typeof(BridgeController))]
    public sealed class BridgeNetSync : NetworkBehaviour
    {
        readonly NetworkList<byte> _states = new NetworkList<byte>();
        BridgeController _bridge;

        void Awake() => _bridge = GetComponent<BridgeController>();

        public override void OnNetworkSpawn()
        {
            var planks = _bridge.Planks;
            if (IsServer)
            {
                _states.Clear();
                for (int i = 0; i < planks.Count; i++)
                {
                    _states.Add((byte)planks[i].State);
                    int idx = i;
                    planks[i].StateChanged += (p, s) => { if (idx < _states.Count) _states[idx] = (byte)s; };
                }
            }
            else
            {
                _states.OnListChanged += _ => ApplyAll();
                ApplyAll();
            }
        }

        void ApplyAll()
        {
            var planks = _bridge.Planks;
            for (int i = 0; i < planks.Count && i < _states.Count; i++)
            {
                var s = (PlankState)_states[i];
                planks[i].ApplyReplicatedState(s, s == PlankState.Broken ? 0f : s == PlankState.Cracked ? 50f : 100f);
            }
        }
    }
}
