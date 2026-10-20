using System;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>Bruise tier derived from bruise points.</summary>
    public enum BruiseState { Pristine, Scuffed, Bruised, Busted, Ruined }

    /// <summary>Result of a bruise hit, published by <see cref="IGoods.OnBruised"/>.</summary>
    public struct BruiseReport
    {
        /// <summary>The bruised item.</summary>
        public IGoods Item;
        /// <summary>Bruise points added.</summary>
        public float DeltaPoints;
        /// <summary>Value lost in whole dollars (>= 0).</summary>
        public int DeltaDollars;
        /// <summary>Player blamed (<see cref="PlayerIds.None"/> for nature).</summary>
        public ulong InstigatorId;
        /// <summary>Verb that describes the cause.</summary>
        public BlameVerb Verb;
    }

    /// <summary>
    /// Runtime view of a sellable object (component on goods prefabs). Only the implementation
    /// mutates bruise/freshness; every other system reads or calls these methods.
    /// </summary>
    public interface IGoods
    {
        /// <summary>The item's transform.</summary>
        Transform GoodsTransform { get; }
        /// <summary>Static definition (base value, hands, mass...).</summary>
        GoodsDefinition Definition { get; }
        /// <summary>Convenience for <c>Definition.GoodsId</c>.</summary>
        string GoodsId { get; }
        /// <summary>Accumulated bruise points 0..100.</summary>
        float BruisePoints { get; }
        /// <summary>Tier derived from <see cref="BruisePoints"/>.</summary>
        BruiseState BruiseState { get; }
        /// <summary>Freshness 0.5..1.0 (1.0 for livestock).</summary>
        float Freshness { get; }
        /// <summary>Current sale value in dollars after bruise, freshness and active modifiers.</summary>
        int CurrentValue { get; }
        /// <summary>Last player to grab, throw or drop it.</summary>
        ulong LastHandlerId { get; }
        /// <summary>Dynamic tags (golden, cursed, order item...).</summary>
        GoodsTag Tags { get; }

        /// <summary>Raised after each bruise hit.</summary>
        event Action<BruiseReport> OnBruised;

        /// <summary>
        /// Server only. Applies an impact in DESIGN units (callers convert raw Unity N*s with <c>ImpactUnits.ToDesign</c> first) and emits the resulting blame through the economy service.
        /// </summary>
        void ApplyBruise(float impulse, ulong instigator, BlameVerb verb);

        /// <summary>Records that <paramref name="clientId"/> touched the item now (feeds the 3 s touch history).</summary>
        void RecordTouch(ulong clientId);
    }
}
