using System;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>What kind of goods an item is.</summary>
    public enum GoodsCategory { Livestock, Crop, Product, Special }

    /// <summary>Dynamic goods tags.</summary>
    [Flags]
    public enum GoodsTag { None = 0, Golden = 1, Cursed = 2, OrderItem = 4, Pocketed = 8, Stolen = 16, Grumpy = 32 }

    /// <summary>
    /// Static goods data (goods-value Goods Item contract). Author under
    /// <c>Assets/Resources/Data/Goods/</c> so <see cref="ContentCatalog"/> finds it.
    /// </summary>
    [CreateAssetMenu(menuName = "HarvestPanic/Goods Definition", fileName = "Goods_New")]
    public sealed class GoodsDefinition : ScriptableObject, IIdentified
    {
        [field: SerializeField, Tooltip("Unique id, e.g. Livestock.Pig, Crop.PrizePumpkin")]
        public string GoodsId { get; private set; } = "Goods.New";

        [field: SerializeField] public GoodsCategory Category { get; private set; }
        [field: SerializeField, Min(0), Tooltip("Dollars before any multiplier")]
        public int BaseValue { get; private set; } = 10;
        [field: SerializeField, Range(0.25f, 2f), Tooltip("Scales bruise gain")]
        public float Fragility { get; private set; } = 1f;
        [field: SerializeField, Range(1, 8), Tooltip("Hands to lift; carriers = ceil(hands/2)")]
        public int CarryHands { get; private set; } = 1;
        [field: SerializeField, Min(0.01f)] public float MassKg { get; private set; } = 5f;
        [field: SerializeField, Tooltip("Living goods can flee")] public bool IsAlive { get; private set; }
        [field: SerializeField] public GoodsTag DefaultTags { get; private set; }
        [field: SerializeField, Tooltip("Prefab spawned for Goods markers (needs an IGoods + ICarryable)")]
        public GameObject WorldPrefab { get; private set; }

        /// <summary>Catalog key (same as <see cref="GoodsId"/>).</summary>
        public string Id => GoodsId;

        /// <summary>Players needed to lift: <c>ceil(CarryHands / 2)</c>.</summary>
        public int CarriersNeeded => (CarryHands + 1) / 2;
    }
}
