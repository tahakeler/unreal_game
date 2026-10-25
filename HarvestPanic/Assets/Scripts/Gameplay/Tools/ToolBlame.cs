using System;
using HarvestPanic.Core;
using UnityEngine;

namespace HarvestPanic.Gameplay
{
    /// <summary>
    /// Server: incident blame events raised by tools and the wagon (blame-ledger verbs LassoedIntoRiver, SpookedHerd,
    /// SpilledCargo, TippedWagon), submitted through <see cref="IEconomyService.EmitBlame"/>. Money is never booked here:
    /// wagon spills book their loss through <see cref="IGoods.ApplyBruise"/> with the wagon verb (one event per item),
    /// and a lassoed item's river soak is booked by the river on the lasser through the goods' touch history. The
    /// ledger's $2 noise floor (blame-ledger rule 3) therefore keeps these $0 incident lines only once Economy treats
    /// LassoedIntoRiver/SpookedHerd as non-economic like YeetedTeammate (open request in docs/qa/tools-progress.md).
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
