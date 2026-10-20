using System.Collections.Generic;

namespace HarvestPanic.Networking
{
    /// <summary>
    /// Server-side chat validation (pure). Lines are cleaned like names (no control / zero-width / rich-text chars,
    /// whitespace collapsed), capped at <see cref="MaxLength"/>, and each sender gets a token bucket of
    /// <see cref="BurstSize"/> lines refilling one every <see cref="RefillSeconds"/> s.
    /// </summary>
    public sealed class ChatRules
    {
        public const int MaxLength = 120;
        public const int BurstSize = 5;
        public const float RefillSeconds = 1.5f;

        readonly Dictionary<ulong, (float tokens, float last)> _buckets = new Dictionary<ulong, (float, float)>();

        /// <summary>Cleaned line, or null when nothing printable is left.</summary>
        public static string Clean(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = PlayerNameSanitizer.Sanitize(raw, MaxLength);
            return s == PlayerNameSanitizer.DefaultName && raw.Trim() != PlayerNameSanitizer.DefaultName ? null : s;
        }

        /// <summary>True if <paramref name="sender"/> may send a line at time <paramref name="now"/> (consumes a token).</summary>
        public bool Allow(ulong sender, float now)
        {
            if (!_buckets.TryGetValue(sender, out var b)) b = (BurstSize, now);
            float tokens = System.Math.Min(BurstSize, b.tokens + (now - b.last) / RefillSeconds);
            bool ok = tokens >= 1f;
            _buckets[sender] = (ok ? tokens - 1f : tokens, now);
            return ok;
        }

        public void Forget(ulong sender) => _buckets.Remove(sender);
        public void Clear() => _buckets.Clear();
    }
}
