using System.Collections.Generic;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.AI
{
    /// <summary>
    /// Active critters plus the single <see cref="INoiseService.OnNoise"/> subscription that fans noises out
    /// to every server-side brain (one delegate instead of one per critter).
    /// </summary>
    public static class CritterRegistry
    {
        static readonly List<CritterBrain> Brains = new List<CritterBrain>();
        static readonly List<CritterBrain> Scratch = new List<CritterBrain>();
        static INoiseService _hooked;
        static bool _waiting;

        /// <summary>All enabled critter brains.</summary>
        public static IReadOnlyList<CritterBrain> All => Brains;

        internal static void Add(CritterBrain b) { if (!Brains.Contains(b)) Brains.Add(b); EnsureNoiseHook(); }
        internal static void Remove(CritterBrain b) => Brains.Remove(b);

        /// <summary>Hooks the current noise service (re-hooks if the provider changed). Cheap; called every brain tick.</summary>
        public static void EnsureNoiseHook()
        {
            if (Services.TryGet(out INoiseService svc))
            {
                if (ReferenceEquals(svc, _hooked)) return;
                if (_hooked != null) _hooked.OnNoise -= Dispatch;
                _hooked = svc;
                _hooked.OnNoise += Dispatch;
                return;
            }
            _hooked = null;
            if (_waiting) return;
            _waiting = true;
            Services.WhenAvailable<INoiseService>(_ => { _waiting = false; EnsureNoiseHook(); });
        }

        /// <summary>Delivers a noise to every authoritative brain (also usable by tests/offline tools).</summary>
        public static void Dispatch(NoiseEvent e)
        {
            Scratch.Clear();
            Scratch.AddRange(Brains);
            foreach (var b in Scratch) if (b != null && b.HasAuthority) b.HearNoise(e);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Brains.Clear();
            _hooked = null;
            _waiting = false;
            CritterFlock.ResetAll();
        }
    }
}
