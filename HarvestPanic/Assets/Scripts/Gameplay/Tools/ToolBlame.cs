using System;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Server: blame events raised by tools and the wagon (blame-ledger verbs LassoedIntoRiver, SpookedHerd,
    /// SpilledCargo, TippedWagon). Dollar losses are booked by the goods' own bruise/soak events; these name the
    /// culprit of the incident (receipt line, camcorder disaster, stinger).
    /// </summary>
    public static class ToolBlame
    {
        /// <summary>Raised on the server for every tool/wagon blame event emitted (tests, debug).</summary>
        public static event Action<BlameEvent> Emitted;

        /// <summary>
        /// Emits one event blaming <paramref name="who"/> (Nature when not a player). Returns false when no economy is
        /// running (dev scenes).
        /// </summary>
        public static bool Emit(BlameVerb verb, ulong who, string itemId, string itemTag, Vector3 at, string cause, int dollars = 0)
        {
            bool player = PlayerIds.IsPlayer(who);
            var e = new BlameEvent
            {
                Timestamp = Time.timeSinceLevelLoad,
                Instigators = player ? new[] { who } : Array.Empty<ulong>(),
                BlameShares = player ? new[] { 1f } : Array.Empty<float>(),
                PrimaryInstigator = player ? who : PlayerIds.None,
                Verb = verb,
                ItemId = itemId ?? string.Empty,
                ItemTag = itemTag ?? string.Empty,
                DollarDelta = Mathf.Min(0, dollars),
                Location = at,
                CauseTag = cause ?? string.Empty,
            };
            Emitted?.Invoke(e);
            if (!Services.TryGet(out IEconomyService eco)) return false;
            eco.EmitBlame(e);
            return true;
        }
    }
}
