using System;

namespace HarvestPanic.Economy
{
    /// <summary>Sunday visit beats (week-quota Landlord Sunday, landlord-events comedy beats).</summary>
    public enum LandlordVisitState : byte { Walking, Greeting, Counting, AwaitVerdict, Verdict, Done }

    /// <summary>Verdict the visit acts out.</summary>
    public enum LandlordVerdict : byte { None, Paid, PaidExact, Collateral, Evicted }

    /// <summary>
    /// Pure timeline of the Landlord's Sunday walk-in. Explicit transition table:
    /// Walking --arrived--> Greeting --t--> Counting --t--> AwaitVerdict --verdict--> Verdict --t--> Done.
    /// A Collateral verdict loops back to AwaitVerdict (the crew is selling), then resolves to Paid/Evicted.
    /// The MonoBehaviour only moves the figure and shows <see cref="Line"/>.
    /// </summary>
    public sealed class LandlordVisitScript
    {
        readonly float _greet, _count, _verdictHold;
        float _t;

        public LandlordVisitState State { get; private set; } = LandlordVisitState.Walking;
        public LandlordVerdict Verdict { get; private set; }
        /// <summary>Speech-bubble line for the current beat (null = no bubble).</summary>
        public string Line { get; private set; }
        /// <summary>Stamp text shown with the verdict ("PAID" / "EVICTED"), else null.</summary>
        public string Stamp { get; private set; }
        /// <summary>Raised on every state change (old, new).</summary>
        public event Action<LandlordVisitState, LandlordVisitState> Changed;

        public LandlordVisitScript(float greetSeconds, float countSeconds, float verdictSeconds)
        {
            _greet = greetSeconds; _count = countSeconds; _verdictHold = verdictSeconds;
        }

        /// <summary>The figure reached the farmhouse door.</summary>
        public void Arrived() { if (State == LandlordVisitState.Walking) Go(LandlordVisitState.Greeting, LandlordText.SundayArrive); }

        /// <summary>Feeds the authoritative verdict (may arrive at any time; applied after counting).</summary>
        public void SetVerdict(LandlordVerdict v)
        {
            if (v == LandlordVerdict.None || v == Verdict) return;
            if (Verdict != LandlordVerdict.None && Verdict != LandlordVerdict.Collateral) return; // final verdicts are final
            Verdict = v;
            if (State == LandlordVisitState.AwaitVerdict || (State == LandlordVisitState.Verdict && v != LandlordVerdict.Collateral)) EnterVerdict();
        }

        public void Tick(float dt)
        {
            _t += Math.Max(0f, dt);
            switch (State)
            {
                case LandlordVisitState.Greeting when _t >= _greet: Go(LandlordVisitState.Counting, LandlordText.SundayCount); break;
                case LandlordVisitState.Counting when _t >= _count:
                    if (Verdict != LandlordVerdict.None) EnterVerdict(); else Go(LandlordVisitState.AwaitVerdict, LandlordText.SundayCount);
                    break;
                case LandlordVisitState.Verdict when _t >= _verdictHold && Verdict != LandlordVerdict.Collateral: Go(LandlordVisitState.Done, Line); break;
            }
        }

        void EnterVerdict()
        {
            switch (Verdict)
            {
                case LandlordVerdict.Paid: Stamp = "PAID"; Go(LandlordVisitState.Verdict, LandlordText.SundayPaid); break;
                case LandlordVerdict.PaidExact: Stamp = "PAID"; Go(LandlordVisitState.Verdict, LandlordText.SundayExact); break;
                case LandlordVerdict.Collateral: Stamp = null; Go(LandlordVisitState.Verdict, LandlordText.SundayShort); break;
                case LandlordVerdict.Evicted: Stamp = "EVICTED"; Go(LandlordVisitState.Verdict, LandlordText.SundayEvicted); break;
            }
        }

        void Go(LandlordVisitState to, string line)
        {
            var from = State;
            State = to; Line = line; _t = 0f;
            Changed?.Invoke(from, to);
        }
    }
}
