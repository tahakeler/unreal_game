using System;
using System.Collections.Generic;
using HarvestPanic.Core;

namespace HarvestPanic.Economy
{
    /// <summary>
    /// Shared wallet rules (shared-wallet-economy §3): integer balance, typed transactions, overdraft floor with
    /// Landlord IOU overflow, theft never below $0, discretionary spends need the cash. Server-side, no Unity state.
    /// </summary>
    public sealed class Wallet
    {
        readonly EconomyTuning _t;
        readonly List<WalletTxn> _history = new List<WalletTxn>();

        /// <summary>Current balance in dollars.</summary>
        public int Balance { get; private set; }
        /// <summary>Overdraft overflow added to this week's Due.</summary>
        public int IouThisWeek { get; private set; }
        /// <summary>Every applied transaction this run (audit: Σ Amount = Balance − start).</summary>
        public IReadOnlyList<WalletTxn> History => _history;
        /// <summary>Balance at run start.</summary>
        public int StartingBalance { get; private set; }

        /// <summary>Raised with the transaction as actually applied (amount may be capped).</summary>
        public event Action<WalletTxn> Applied;
        /// <summary>Raised with the overflow dollars when a cost hits the floor.</summary>
        public event Action<int, WalletTxn> IouAdded;

        public Wallet(EconomyTuning tuning) { _t = tuning; Reset(); }

        /// <summary>New run.</summary>
        public void Reset()
        {
            StartingBalance = Balance = _t.StartingBalance;
            IouThisWeek = 0;
            _history.Clear();
        }

        /// <summary>Save load: restores balance and IOU (history restarts; audit is per session).</summary>
        public void Restore(int balance, int iou)
        {
            _history.Clear();
            StartingBalance = Balance = Math.Max(_t.OverdraftFloor, Math.Min(_t.BalanceCap, balance));
            IouThisWeek = Math.Max(0, iou);
        }

        /// <summary>Sunday settled: IOU cleared.</summary>
        public void ClearIou() => IouThisWeek = 0;

        /// <summary>
        /// Applies <paramref name="txn"/>; returns the applied transaction (Amount may be capped) or false when
        /// rejected (Upgrade without cash, zero amount).
        /// </summary>
        public bool Apply(WalletTxn txn, out WalletTxn applied)
        {
            applied = txn;
            if (txn.Amount == 0) return false;
            long amount = txn.Amount;
            if (amount < 0)
            {
                if (txn.Type == WalletTxnType.Upgrade && Balance + amount < 0) return false;
                if (txn.Type == WalletTxnType.Theft)
                {
                    amount = -Math.Min(-amount, Math.Max(0, Balance));
                    if (amount == 0) return false;
                }
                else if (Balance + amount < _t.OverdraftFloor)
                {
                    int overflow = (int)(_t.OverdraftFloor - (Balance + amount));
                    amount = _t.OverdraftFloor - Balance;
                    IouThisWeek += overflow;
                    IouAdded?.Invoke(overflow, txn);
                }
            }
            else if (Balance + amount > _t.BalanceCap) amount = _t.BalanceCap - Balance;

            applied.Amount = (int)amount;
            Balance += applied.Amount;
            _history.Add(applied);
            Applied?.Invoke(applied);
            return true;
        }
    }
}
