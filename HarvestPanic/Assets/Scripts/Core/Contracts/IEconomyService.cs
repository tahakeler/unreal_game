using System;

namespace HarvestPanic.Core
{
    /// <summary>Wallet transaction kinds (shared-wallet-economy).</summary>
    public enum WalletTxnType { Sale, OrderBonus, RespawnFee, Repair, Fine, Upgrade, SellBack, Quota, Theft, Refund }

    /// <summary>One authoritative wallet change.</summary>
    [Serializable]
    public struct WalletTxn
    {
        /// <summary>Kind.</summary>
        public WalletTxnType Type;
        /// <summary>Signed dollars (negative = spend).</summary>
        public int Amount;
        /// <summary>Responsible player or <see cref="PlayerIds.None"/> for a system.</summary>
        public ulong PlayerId;
        /// <summary>Goods id involved, may be empty.</summary>
        public string GoodsId;
        /// <summary>Day within the run.</summary>
        public int DayIndex;
        /// <summary>Server time in seconds.</summary>
        public float Timestamp;
    }

    /// <summary>
    /// Shared wallet, market and blame entry point. Provider: Economy team; callers: Player, AI, World.
    /// All mutating calls are server-only and silently ignored (returning false/0) on clients.
    /// </summary>
    public interface IEconomyService
    {
        /// <summary>Current shared balance in dollars.</summary>
        int Balance { get; }

        /// <summary>True while the market accepts sales.</summary>
        bool IsMarketOpen { get; }

        /// <summary>Raised after every wallet change.</summary>
        event Action<WalletTxn> OnWalletTxn;

        /// <summary>Raised for blame events whose severity qualifies as a disaster (camcorder/audio hook).</summary>
        event Action<BlameEvent> OnDisaster;

        /// <summary>Applies a transaction to the shared wallet.</summary>
        void AddWalletTxn(in WalletTxn txn);

        /// <summary>Charges the respawn fee for <paramref name="playerId"/>; returns dollars charged.</summary>
        int ChargeRespawnFee(ulong playerId, BlameVerb cause);

        /// <summary>Submits a blame event to the ledger (merging, noise floor and severity are the ledger's job).</summary>
        void EmitBlame(in BlameEvent evt);

        /// <summary>Sells an item in the market pen; false when the market is closed or the item is not sellable.</summary>
        bool TrySell(IGoods item, ulong seller, out int finalValue);
    }
}
