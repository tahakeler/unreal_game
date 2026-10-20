using System;

namespace HarvestPanic.Core
{
    /// <summary>
    /// Server-side feed of every bruise hit on any goods (add-only, polish-2), so presentation (Audio's crunch +
    /// register clunk) does not have to find and subscribe to each <see cref="IGoods.OnBruised"/>. Raised by Economy's
    /// Goods right after its own OnBruised. Presentation only.
    /// </summary>
    public static class BruiseFeed
    {
        public static event Action<BruiseReport> OnBruise;

        public static void Raise(in BruiseReport r) => OnBruise?.Invoke(r);
    }
}
