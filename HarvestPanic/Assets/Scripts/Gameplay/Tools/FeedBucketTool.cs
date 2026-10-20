using HarvestPanic.Core;
using Unity.Netcode;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Feed bucket (design/gdd/farm-tools.md rule 4): hold Use to shake. Each shake press spends one of
    /// <c>FeedUses</c> and, while held, emits Noise (Source=Tool, loudness 0.4, tag Food) every
    /// <c>FeedShakeInterval</c> s, which critters with a Food Lure response follow. Refills near the barn
    /// (BarnRespawn markers). Throwing feed to place a 20 s lure point is not in this slice.
    /// </summary>
    public sealed class FeedBucketTool : CarryableProp, IUsableTool
    {
        [SerializeField] ToolTuning tuning;
        readonly NetworkVariable<int> _uses = new NetworkVariable<int>(10);
        bool _shaking;
        float _nextShake, _nextRefillCheck;
        ulong _user = PlayerIds.None;
        Transform _visual;

        /// <summary>Uses left.</summary>
        public int UsesLeft => _uses.Value;

        /// <inheritdoc />
        public string UsePrompt => _uses.Value > 0 ? $"RMB: Shake ({_uses.Value} left)" : "Empty - refill at the barn";

        protected override void Awake()
        {
            base.Awake();
            _visual = transform.childCount > 0 ? transform.GetChild(0) : null;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) _uses.Value = tuning != null ? tuning.FeedUses : 10;
        }

        /// <inheritdoc />
        public void ServerUse(bool down, PlayerCarry user)
        {
            if (!down) { _shaking = false; return; }
            if (_uses.Value <= 0) return;
            _uses.Value--;
            _shaking = true;
            _user = user.OwnerClientId;
            _nextShake = 0f;
        }

        public override void OnReleased(ulong clientId)
        {
            base.OnReleased(clientId);
            _shaking = false;
        }

        void Update()
        {
            if (_visual != null) _visual.localRotation = _shaking ? Quaternion.Euler(Mathf.Sin(Time.time * 30f) * 12f, 0f, Mathf.Cos(Time.time * 26f) * 8f) : Quaternion.identity;
            if (!IsServer) return;
            float now = Time.time;
            if (_shaking && now >= _nextShake)
            {
                _nextShake = now + (tuning != null ? tuning.FeedShakeInterval : 0.5f);
                NoiseService.TryEmit(NoiseSource.Tool, tuning != null ? tuning.FeedLoudness : 0.4f, transform.position, NetworkObjectId, _user, NoiseTag.Food);
            }
            if (now >= _nextRefillCheck && tuning != null)
            {
                _nextRefillCheck = now + 1f;
                if (_uses.Value < tuning.FeedUses)
                    foreach (var m in LevelMarker.FindAll(MarkerRole.BarnRespawn))
                        if ((m.transform.position - transform.position).sqrMagnitude <= tuning.FeedRefillRadius * tuning.FeedRefillRadius) { _uses.Value = tuning.FeedUses; break; }
            }
        }
    }
}
