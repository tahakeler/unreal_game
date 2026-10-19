namespace HarvestPanic.Core
{
    /// <summary>How goods were damaged.</summary>
    public enum BruiseKind { Impact, Soaked }

    /// <summary>
    /// Optional companion to <see cref="IGoods"/> for goods that can get soaked (river, rain). Economy may
    /// implement it; callers check <c>goods is IGoodsSoakable</c>. Server only.
    /// </summary>
    public interface IGoodsSoakable
    {
        /// <summary>Soaks the item; <paramref name="instigator"/> is blamed (<see cref="PlayerIds.Nature"/> for weather).</summary>
        void ApplySoak(ulong instigator);
    }
}
