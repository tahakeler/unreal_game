using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HarvestPanic.Core
{
    /// <summary>Result of <see cref="NameFilter.Check"/>.</summary>
    public enum NameCheck : byte { Ok, Empty, TooShort, TooLong, BadCharacters, Profane }

    /// <summary>
    /// Farm Name rules (design/gdd/character-customization.md "Anti-abuse: names"): 3-16 characters of Latin, extended
    /// Latin, digits, space, '-' and '_'; filtered by a data-driven profanity list (DT_NameFilter =
    /// <c>Resources/Data/Social/NameFilter.txt</c>) with leetspeak normalisation; a failed name falls back to "Farmhand #N".
    /// Pure logic, main-thread only (the list loads lazily). Customization's creator checks names as you type; the host
    /// checks every joining name for profanity (Steam persona names are not held to the charset rule). Add-only.
    /// List syntax: one entry per line; <c>#</c> comment; <c>word</c> = banned anywhere (separators ignored, letters may
    /// repeat: "fuuuck"); <c>=word</c> = banned as a whole word only; <c>!word</c> = always allowed (Scunthorpe cases).
    /// </summary>
    public static class NameFilter
    {
        /// <summary>Minimum Farm Name length (after trimming).</summary>
        public const int MinLength = 3;
        /// <summary>Maximum Farm Name length.</summary>
        public const int MaxLength = 16;
        /// <summary>Resources path of the word list (TextAsset).</summary>
        public const string ResourcePath = "Data/Social/NameFilter";

        // Fallback when the data file is missing (kept short; the data file is the real list).
        const string BuiltIn = "fuck\nshit\ncunt\nbitch\nwhore\nslut\nnigger\nnigga\nfaggot\nretard\nasshole\n=ass\n=fag\n=dick\n=cock\n=rape\n!scunthorpe";

        static List<string> _sub, _whole;
        static HashSet<string> _allow;

        /// <summary>"Farmhand #N", the GDD fallback for a rejected name.</summary>
        public static string Fallback(int n) => "Farmhand #" + Mathf.Clamp(n, 1, 99);

        /// <summary>Trims and collapses inner whitespace (the form that is checked and stored).</summary>
        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new StringBuilder(raw.Length);
            bool space = false;
            foreach (char c in raw)
            {
                if (char.IsWhiteSpace(c)) { space = sb.Length > 0; continue; }
                if (space) { sb.Append(' '); space = false; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>True for characters a Farm Name may contain.</summary>
        public static bool IsAllowedChar(char c)
        {
            if (c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9') return true;
            if (c == ' ' || c == '-' || c == '_') return true;
            if (c >= 'À' && c <= 'ÿ') return c != '×' && c != '÷'; // Latin-1 letters (not x / ÷)
            return c >= 'Ā' && c <= 'ɏ';                                     // Latin Extended-A and -B
        }

        /// <summary>Checks a proposed Farm Name. <paramref name="cleaned"/> is the trimmed form that would be stored.</summary>
        public static NameCheck Check(string raw, out string cleaned)
        {
            cleaned = Clean(raw);
            if (cleaned.Length == 0) return NameCheck.Empty;
            foreach (char c in cleaned) if (!IsAllowedChar(c)) return NameCheck.BadCharacters;
            if (cleaned.Length < MinLength) return NameCheck.TooShort;
            if (cleaned.Length > MaxLength) return NameCheck.TooLong;
            return ContainsProfanity(cleaned) ? NameCheck.Profane : NameCheck.Ok;
        }

        /// <summary>Returns the name to use: the cleaned name when it passes, else <see cref="Fallback"/>(n). Empty stays empty.</summary>
        public static string Resolve(string raw, int n)
        {
            var r = Check(raw, out string cleaned);
            return r == NameCheck.Ok ? cleaned : r == NameCheck.Empty ? "" : Fallback(n);
        }

        /// <summary>True when <paramref name="text"/> contains a banned word (after leetspeak and accent folding).</summary>
        public static bool ContainsProfanity(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            EnsureList();
            var tokens = Tokens(text);
            // Substring check per group: tokens of 1-2 letters are glued to their neighbours ("f.u.c.k", "f_uck"), but two
            // real words are not ("Bass Hit" must not read as "basshit").
            var groups = new List<string>();
            var group = new StringBuilder();
            int prevLen = int.MaxValue;
            foreach (var t in tokens)
            {
                if (_allow.Contains(t)) { Flush(groups, group); prevLen = int.MaxValue; continue; }
                foreach (var w in _whole) if (MatchesRuns(t, w, whole: true)) return true;
                if (!(t.Length <= 2 || prevLen <= 2)) Flush(groups, group);
                group.Append(t);
                prevLen = t.Length;
            }
            Flush(groups, group);
            foreach (var g in groups)
                foreach (var w in _sub) if (MatchesRuns(g, w, whole: false)) return true;
            return false;
        }

        static void Flush(List<string> groups, StringBuilder group)
        {
            if (group.Length > 0) groups.Add(group.ToString());
            group.Clear();
        }

        /// <summary>Replaces the word list (tests, mods). Null reloads the data file.</summary>
        public static void SetWordList(string listText)
        {
            _sub = null;
            if (listText != null) Parse(listText);
        }

        static void EnsureList()
        {
            if (_sub != null) return;
            string text = null;
            try { var asset = Resources.Load<TextAsset>(ResourcePath); if (asset != null) text = asset.text; }
            catch (Exception) { /* no Resources outside play/edit (pure tests): fall back */ }
            Parse(string.IsNullOrWhiteSpace(text) ? BuiltIn : text);
        }

        static void Parse(string listText)
        {
            _sub = new List<string>(); _whole = new List<string>(); _allow = new HashSet<string>();
            foreach (var line in listText.Split('\n'))
            {
                string s = line.Trim();
                if (s.Length == 0 || s[0] == '#') continue;
                char mode = s[0];
                if (mode == '=' || mode == '!') s = s.Substring(1);
                string w = Fold(s);
                if (w.Length == 0) continue;
                if (mode == '=') _whole.Add(w); else if (mode == '!') _allow.Add(w); else _sub.Add(w);
            }
        }

        /// <summary>Lower-case letters only: leetspeak digits/symbols mapped to letters, accents folded, others dropped.</summary>
        public static string Fold(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s) { char m = Map(c); if (m != '\0') sb.Append(m); }
            return sb.ToString();
        }

        static List<string> Tokens(string text)
        {
            var list = new List<string>();
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                char m = Map(c);
                if (m != '\0') { sb.Append(m); continue; }
                if (sb.Length > 0) { list.Add(sb.ToString()); sb.Clear(); }
            }
            if (sb.Length > 0) list.Add(sb.ToString());
            return list;
        }

        static char Map(char c)
        {
            switch (c)
            {
                case '0': return 'o';
                case '1': case '!': case '|': return 'i';
                case '3': return 'e';
                case '4': case '@': return 'a';
                case '5': case '$': return 's';
                case '7': case '+': return 't';
                case '8': return 'b';
                case '6': case '9': return 'g';
            }
            char l = char.ToLowerInvariant(c);
            if (l >= 'a' && l <= 'z') return l;
            if (l < 'À' || l > 'ɏ') return '\0';
            return FoldAccent(l);
        }

        static char FoldAccent(char c)
        {
            if ("àáâãäåāăą".IndexOf(c) >= 0) return 'a';
            if ("çćĉċč".IndexOf(c) >= 0) return 'c';
            if ("ďđ".IndexOf(c) >= 0) return 'd';
            if ("èéêëēĕėęě".IndexOf(c) >= 0) return 'e';
            if ("ĝğġģ".IndexOf(c) >= 0) return 'g';
            if ("ĥħ".IndexOf(c) >= 0) return 'h';
            if ("ìíîïĩīĭįı".IndexOf(c) >= 0) return 'i';
            if ("ĵ".IndexOf(c) >= 0) return 'j';
            if ("ķ".IndexOf(c) >= 0) return 'k';
            if ("ĺļľŀł".IndexOf(c) >= 0) return 'l';
            if ("ñńņňŉ".IndexOf(c) >= 0) return 'n';
            if ("òóôõöøōŏő".IndexOf(c) >= 0) return 'o';
            if ("ŕŗř".IndexOf(c) >= 0) return 'r';
            if ("śŝşšß".IndexOf(c) >= 0) return 's';
            if ("ţťŧ".IndexOf(c) >= 0) return 't';
            if ("ùúûüũūŭůűų".IndexOf(c) >= 0) return 'u';
            if ("ŵ".IndexOf(c) >= 0) return 'w';
            if ("ýÿŷ".IndexOf(c) >= 0) return 'y';
            if ("źżž".IndexOf(c) >= 0) return 'z';
            return '\0';
        }

        /// <summary>
        /// Run-length match: every letter run of <paramref name="word"/> must appear in order in <paramref name="text"/>
        /// with at least as many repeats ("fuuuck" matches "fuck", "as" does not match "ass").
        /// </summary>
        static bool MatchesRuns(string text, string word, bool whole)
        {
            var t = Runs(text); var w = Runs(word);
            if (w.Count == 0 || t.Count < w.Count) return false;
            if (whole && t.Count != w.Count) return false;
            for (int start = 0; start + w.Count <= t.Count; start++)
            {
                bool ok = true;
                for (int k = 0; k < w.Count && ok; k++)
                {
                    var tr = t[start + k]; var wr = w[k];
                    ok = tr.c == wr.c && tr.n >= wr.n;
                }
                if (ok) return true;
                if (whole) return false;
            }
            return false;
        }

        static List<(char c, int n)> Runs(string s)
        {
            var r = new List<(char, int)>();
            foreach (char c in s)
            {
                if (r.Count > 0 && r[r.Count - 1].Item1 == c) r[r.Count - 1] = (c, r[r.Count - 1].Item2 + 1);
                else r.Add((c, 1));
            }
            return r;
        }
    }
}
