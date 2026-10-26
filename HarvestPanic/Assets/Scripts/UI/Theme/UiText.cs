using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Frontend string table (localization-ready, hud-menus rule 4). One UTF-8 text file per language in
    /// <c>Resources/Frontend/Strings/UI_Strings_&lt;lang&gt;.txt</c>: <c>key = value</c> lines, <c>#</c> comments, <c>\n</c> for a
    /// line break and <c>{0}</c> placeholders. A missing key in another language falls back to English, then to the key itself.
    /// UXML text that starts with <c>@</c> is a key and is replaced by <see cref="Localize"/> when a panel is built.
    /// </summary>
    public static class UiText
    {
        public const string DefaultLanguage = "en";
        public const string ResourceFolder = "Frontend/Strings/";
        public const char KeyPrefix = '@';

        static Dictionary<string, string> _table, _fallback;
        static string _language = DefaultLanguage;
        static readonly HashSet<string> Warned = new HashSet<string>();
        /// <summary>Original "@key" of each localized element, so a language switch can re-localize.</summary>
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextElement, string> SourceKeys =
            new System.Runtime.CompilerServices.ConditionalWeakTable<TextElement, string>();

        /// <summary>Raised after <see cref="Language"/> changes (screens re-localize).</summary>
        public static event Action Changed;

        /// <summary>Current language code (two letters).</summary>
        public static string Language
        {
            get => _language;
            set
            {
                string v = string.IsNullOrWhiteSpace(value) ? DefaultLanguage : value.Trim().ToLowerInvariant();
                if (v == _language && _table != null) return;
                _language = v; _table = null;
                Changed?.Invoke();
            }
        }

        /// <summary>Every key of the current table (tests, tips).</summary>
        public static IEnumerable<string> Keys { get { Ensure(); return _table.Keys; } }

        public static bool Has(string key) { Ensure(); return key != null && (_table.ContainsKey(key) || _fallback.ContainsKey(key)); }

        /// <summary>The text for <paramref name="key"/> (a leading @ is ignored); the key itself when missing.</summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (key[0] == KeyPrefix) key = key.Substring(1);
            Ensure();
            if (_table.TryGetValue(key, out var v) || _fallback.TryGetValue(key, out v)) return v;
            if (Warned.Add(key)) Debug.LogWarning("[UiText] Missing string '" + key + "'");
            return key;
        }

        /// <summary><see cref="Get"/> then <see cref="string.Format(IFormatProvider, string, object[])"/> (invariant culture).</summary>
        public static string Format(string key, params object[] args)
        {
            string f = Get(key);
            if (args == null || args.Length == 0) return f;
            try { return string.Format(CultureInfo.InvariantCulture, f, args); }
            catch (FormatException) { return f; }
        }

        /// <summary>Keys that share <paramref name="prefix"/>, sorted (e.g. every "tip.").</summary>
        public static List<string> KeysWithPrefix(string prefix)
        {
            Ensure();
            var list = new List<string>();
            foreach (var k in _table.Keys) if (k.StartsWith(prefix, StringComparison.Ordinal)) list.Add(k);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>Replaces every "@key" text (labels, buttons, field labels) under <paramref name="root"/>.</summary>
        public static void Localize(VisualElement root)
        {
            if (root == null) return;
            root.Query<TextElement>().ForEach(t =>
            {
                if (!string.IsNullOrEmpty(t.text) && t.text[0] == KeyPrefix) { SourceKeys.Remove(t); SourceKeys.Add(t, t.text); }
                if (SourceKeys.TryGetValue(t, out var k)) t.text = Get(k);
            });
            root.Query<TextField>().ForEach(f =>
            {
                if (!string.IsNullOrEmpty(f.label) && f.label[0] == KeyPrefix) f.label = Get(f.label);
            });
        }

        /// <summary>Parses a table file. Later duplicates win. Pure (EditMode tests).</summary>
        public static Dictionary<string, string> Parse(string text)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(text)) return d;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                string t = line.TrimStart();
                if (t.Length == 0 || t[0] == '#') continue;
                int eq = t.IndexOf('=');
                if (eq <= 0) continue;
                string key = t.Substring(0, eq).Trim();
                string value = t.Substring(eq + 1).Trim();
                if (key.Length == 0) continue;
                d[key] = Unescape(value);
            }
            return d;
        }

        static string Unescape(string v)
        {
            if (v.IndexOf('\\') < 0) return v;
            var sb = new StringBuilder(v.Length);
            for (int i = 0; i < v.Length; i++)
            {
                char c = v[i];
                if (c == '\\' && i + 1 < v.Length)
                {
                    char n = v[++i];
                    sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n);
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Loads (or reloads) the tables. Tests may inject text with <see cref="UseTable"/>.</summary>
        static void Ensure()
        {
            if (_table != null) return;
            _fallback = Parse(Load(DefaultLanguage));
            _table = _language == DefaultLanguage ? _fallback : Parse(Load(_language));
        }

        static string Load(string lang)
        {
            try
            {
                var asset = Resources.Load<TextAsset>(ResourceFolder + "UI_Strings_" + lang);
                return asset != null ? asset.text : "";
            }
            catch (Exception) { return ""; }
        }

        /// <summary>Test hook: replaces the current table (null reloads from Resources).</summary>
        public static void UseTable(string text)
        {
            if (text == null) { _table = null; return; }
            _fallback = Parse(text); _table = _fallback;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() { _table = null; Changed = null; Warned.Clear(); }
    }
}
