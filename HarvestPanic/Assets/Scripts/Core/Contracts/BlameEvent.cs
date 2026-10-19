using System;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>
    /// The 23 blame verbs (blame-ledger v1.1). Add-only; never renumber. Emitters must use exactly these names.
    /// </summary>
    public enum BlameVerb
    {
        Bruised, Dropped, Slipped, DroppedInRiver, Threw, Crashed, SpilledCargo, LetEscape,
        BrokePlank, TippedWagon, Drowned, KnockedOut, Stranded, YeetedTeammate, LostTool,
        LassoedIntoRiver, SpookedHerd, SummonedCreature, Stolen, Pocketed, LandlordFine,
        Rescue, StealBack
    }

    /// <summary>
    /// One costly (or heroic) incident. Constructed on the host only and submitted through
    /// <see cref="IEconomyService.EmitBlame"/>; clients never build these.
    /// </summary>
    [Serializable]
    public struct BlameEvent
    {
        /// <summary>Host-assigned, monotonic per session.</summary>
        public uint EventId;
        /// <summary>Server seconds since day start.</summary>
        public float Timestamp;
        /// <summary>Day within the run, 0-based.</summary>
        public short DayIndex;
        /// <summary>Blamed players (max 4); empty means "Nature".</summary>
        public ulong[] Instigators;
        /// <summary>Largest share; ties go to the earliest toucher. <see cref="PlayerIds.None"/> for Nature.</summary>
        public ulong PrimaryInstigator;
        /// <summary>Same length as <see cref="Instigators"/>, sums to 1.</summary>
        public float[] BlameShares;
        /// <summary>What happened.</summary>
        public BlameVerb Verb;
        /// <summary>Goods id of the item involved (empty when none).</summary>
        public string ItemId;
        /// <summary>Descriptive tag, e.g. Goods.Livestock.Pig, Tool.Lasso, Bridge.Plank.</summary>
        public string ItemTag;
        /// <summary>At most 0 for losses; positive only for Rescue/StealBack.</summary>
        public int DollarDelta;
        /// <summary>0..100 computed by the ledger.</summary>
        public byte Severity;
        /// <summary>World position of the incident.</summary>
        public Vector3 Location;
        /// <summary>Optional context such as Weather.Wind, Critter.Bull, Hazard.River.</summary>
        public string CauseTag;
        /// <summary>Hidden until Sunday (golden egg).</summary>
        public bool IsSecret;
    }
}
