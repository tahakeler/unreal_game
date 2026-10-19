using System;
using System.Collections.Generic;
using System.Text;

namespace HarvestPanic.Networking
{
    /// <summary>
    /// Server-side cleanup of player-supplied display names. Pure logic (EditMode tested).
    /// Strips control / zero-width / bidi characters and rich-text brackets, collapses whitespace,
    /// clamps length without splitting surrogate pairs and de-duplicates ("Dave", "Dave 2").
    /// </summary>
    public static class PlayerNameSanitizer
    {
        /// <summary>Name used when nothing usable is left.</summary>
        public const string DefaultName = "Farmer";
        /// <summary>Maximum display length in UTF-16 code units.</summary>
        public const int MaxLength = 20;

        /// <summary>Returns a safe, trimmed, length-limited name (never null or empty).</summary>
        /// <example><code>PlayerNameSanitizer.Sanitize("  &lt;b&gt;Dave&lt;/b&gt;\n") == "bDave/b"</code></example>
        public static string Sanitize(string raw, int maxLength = MaxLength)
        {
            if (string.IsNullOrEmpty(raw)) return DefaultName;
            maxLength = Math.Max(1, maxLength);
            var sb = new StringBuilder(raw.Length);
            bool pendingSpace = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (char.IsWhiteSpace(c)) { pendingSpace = sb.Length > 0; continue; }
                if (IsBanned(c)) continue;
                if (char.IsSurrogate(c))
                {
                    // keep only well-formed pairs
                    if (char.IsHighSurrogate(c) && i + 1 < raw.Length && char.IsLowSurrogate(raw[i + 1]))
                    {
                        if (!Append(sb, ref pendingSpace, maxLength, 2)) break;
                        sb.Append(c).Append(raw[i + 1]);
                        i++;
                    }
                    continue;
                }
                if (!Append(sb, ref pendingSpace, maxLength, 1)) break;
                sb.Append(c);
            }
            string s = sb.ToString().Trim();
            return s.Length == 0 ? DefaultName : s;
        }

        static bool Append(StringBuilder sb, ref bool pendingSpace, int max, int width)
        {
            int need = width + (pendingSpace ? 1 : 0);
            if (sb.Length + need > max) return false;
            if (pendingSpace) sb.Append(' ');
            pendingSpace = false;
            return true;
        }

        static bool IsBanned(char c)
        {
            if (char.IsControl(c)) return true;
            if (c == '<' || c == '>') return true;                 // TMP / UI Toolkit rich text
            if (c >= '​' && c <= '‏') return true;       // zero-width, LRM/RLM
            if (c >= '‪' && c <= '‮') return true;       // bidi embedding/override
            if (c >= '⁠' && c <= '⁯') return true;       // word joiner, invisible ops
            if (c == '﻿' || c == '­') return true;       // BOM, soft hyphen
            return false;
        }

        /// <summary>Returns <paramref name="name"/> or "name 2", "name 3"… so it is unique (case-insensitive) in <paramref name="taken"/>.</summary>
        public static string MakeUnique(string name, IEnumerable<string> taken, int maxLength = MaxLength)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (taken != null) foreach (var t in taken) if (t != null) set.Add(t);
            if (!set.Contains(name)) return name;
            for (int n = 2; n < 100; n++)
            {
                string suffix = " " + n;
                string head = name.Length + suffix.Length > maxLength ? name.Substring(0, Math.Max(1, maxLength - suffix.Length)).TrimEnd() : name;
                if (head.Length > 0 && char.IsHighSurrogate(head[head.Length - 1])) head = head.Substring(0, head.Length - 1);
                string candidate = head + suffix;
                if (!set.Contains(candidate)) return candidate;
            }
            return name;
        }
    }
}
