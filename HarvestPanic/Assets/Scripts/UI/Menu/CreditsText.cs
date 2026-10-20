using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HarvestPanic.UI
{
    /// <summary>
    /// Credits + third-party licence text. Single source of truth is <c>THIRD_PARTY_LICENSES.md</c> at the repo root:
    /// in the editor it is read from disk every time; for player builds <c>HarvestPanic.UI.EditorTools.CreditsBuildStep</c>
    /// bakes the composed text into <see cref="ResourcePath"/> before the build and writes the same text as
    /// <see cref="LicensesFileName"/> beside the player afterwards. Pure string code so it is EditMode-testable.
    /// </summary>
    public static class CreditsText
    {
        /// <summary>Resources path of the TextAsset baked at build time (folder is gitignored).</summary>
        public const string ResourcePath = "Frontend/Generated/Licenses";
        /// <summary>Project-relative asset path of the baked TextAsset.</summary>
        public const string ResourceAssetPath = "Assets/Resources/Frontend/Generated/Licenses.txt";
        public const string LicensesFileName = "LICENSES.txt";
        public const string SourceFileName = "THIRD_PARTY_LICENSES.md";

        /// <summary>Licence texts that must travel with the binaries (OFL 1.1 condition 2), project-relative.</summary>
        public static readonly string[] BundledLicenceFiles =
        {
            "Assets/Art/UI/Fonts/OFL-LilitaOne.txt",
            "Assets/Art/UI/Fonts/OFL-Nunito.txt",
        };

        /// <summary>Team credit block. Owner: confirm names/roles before Early Access (production/release/early-beta-gate.md).</summary>
        public static readonly string[] TeamLines =
        {
            "A co-op farm. Everything has legs.",
            "",
            "Created by the HARVEST PANIC team",
            "Game direction, design approval and playtesting: Taha (project owner)",
            "Design, programming, art direction, audio and QA: the HARVEST PANIC studio agents (Claude Code by Anthropic), directed by the owner",
            "",
            "Made with Unity. Multiplayer by Netcode for GameObjects with Facepunch.Steamworks.",
            "Thanks to Quaternius, Kenney, Kay Lousberg and the Google Fonts designers for their generous free assets.",
        };

        const string NoText = "Licence information is missing from this build. See LICENSES.txt beside the game.";

        /// <summary>Full credits text (team + third-party + full licence texts).</summary>
        public static string Compose(string licensesMarkdown, IEnumerable<KeyValuePair<string, string>> bundledLicences = null)
        {
            var sb = new StringBuilder();
            sb.Append("HARVEST PANIC - CREDITS AND THIRD-PARTY LICENCES\n");
            sb.Append("=================================================\n\n");
            sb.Append("TEAM\n----\n\n");
            foreach (var l in TeamLines) sb.Append(l).Append('\n');
            sb.Append("\nTHIRD-PARTY ASSETS AND LIBRARIES\n--------------------------------\n\n");
            sb.Append(FromMarkdown(licensesMarkdown ?? ""));
            if (bundledLicences != null)
                foreach (var kv in bundledLicences)
                {
                    if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                    string head = "FULL LICENCE TEXT - " + kv.Key;
                    sb.Append('\n').Append(head).Append('\n').Append(new string('-', head.Length)).Append("\n\n");
                    sb.Append(kv.Value.Replace("\r\n", "\n").Trim()).Append('\n');
                }
            return sb.ToString();
        }

        static readonly Regex Link = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
        static readonly Regex MitCopyright = new Regex(@"\bMIT\s*,\s*(Copyright\s*\(c\)[^|]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex Separator = new Regex(@"^:?-{3,}:?$", RegexOptions.Compiled);

        /// <summary>
        /// Markdown -> plain text: headings become underlined capitals, table rows become bullet lines, inline markup is
        /// stripped, links become "text (url)". The shared MIT body (blockquote after "MIT licence text") is emitted
        /// once per MIT component with that component's copyright line, so each MIT notice ships in full.
        /// </summary>
        public static string FromMarkdown(string md)
        {
            var sb = new StringBuilder();
            var mitComponents = new List<KeyValuePair<string, string>>();
            var mitBody = new List<string>();
            var para = new StringBuilder();
            bool inMit = false, inTable = false, lastBlank = true;
            int mitAt = -1;

            void Blank() { if (!lastBlank) { sb.Append('\n'); lastBlank = true; } }
            void Line(string s) { sb.Append(s).Append('\n'); lastBlank = false; }

            foreach (var raw in (md ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.TrimEnd();
                string t = line.TrimStart();

                if (inMit)
                {
                    if (t.StartsWith(">", StringComparison.Ordinal))
                    {
                        string q = t.Substring(1).Trim();
                        if (q.Length == 0) { if (para.Length > 0) { mitBody.Add(para.ToString()); para.Clear(); } }
                        else { if (para.Length > 0) para.Append(' '); para.Append(Inline(q)); }
                        continue;
                    }
                    if (t.Length == 0 && mitBody.Count == 0 && para.Length == 0) continue;
                    if (para.Length > 0) { mitBody.Add(para.ToString()); para.Clear(); }
                    inMit = false;
                }

                if (t.StartsWith("|", StringComparison.Ordinal))
                {
                    var cells = Cells(t);
                    bool sep = cells.Count > 0 && cells.TrueForAll(c => Separator.IsMatch(c));
                    if (sep) continue;
                    if (!inTable) { Blank(); inTable = true; Line("  " + string.Join(" | ", cells.ConvertAll(Inline))); continue; }
                    Line("  - " + string.Join(" | ", cells.ConvertAll(Inline)));
                    foreach (var c in cells)
                    {
                        var m = MitCopyright.Match(c);
                        if (m.Success) mitComponents.Add(new KeyValuePair<string, string>(Inline(cells[0]), m.Groups[1].Value.Trim()));
                    }
                    continue;
                }
                inTable = false;

                if (t.Length == 0) { Blank(); continue; }

                if (t.StartsWith("MIT licen", StringComparison.OrdinalIgnoreCase)) { inMit = true; mitAt = sb.Length; continue; }

                if (t.StartsWith("#", StringComparison.Ordinal))
                {
                    string h = Inline(t.TrimStart('#').Trim()).ToUpperInvariant();
                    Blank(); Line(h); Line(new string('-', Math.Min(h.Length, 72))); sb.Append('\n'); lastBlank = true;
                    continue;
                }

                if (t.StartsWith(">", StringComparison.Ordinal)) { Line(Inline(t.Substring(1).Trim())); continue; }
                Line(Inline(t));
            }
            if (para.Length > 0) mitBody.Add(para.ToString());

            if (mitBody.Count > 0)
            {
                var mit = new StringBuilder();
                if (mitComponents.Count == 0) mitComponents.Add(new KeyValuePair<string, string>("MIT-licensed components", ""));
                foreach (var kv in mitComponents)
                {
                    string head = "MIT LICENCE - " + kv.Key;
                    mit.Append('\n').Append(head).Append('\n').Append(new string('-', Math.Min(head.Length, 72))).Append("\n\n");
                    if (kv.Value.Length > 0) mit.Append(kv.Value).Append("\n\n");
                    foreach (var p in mitBody) mit.Append(p).Append("\n\n");
                }
                sb.Insert(mitAt < 0 ? sb.Length : mitAt, mit.ToString());
            }
            return sb.ToString().TrimEnd() + "\n";
        }

        static List<string> Cells(string row)
        {
            var parts = row.Trim().Trim('|').Split('|');
            var list = new List<string>(parts.Length);
            foreach (var p in parts) list.Add(p.Trim());
            return list;
        }

        static string Inline(string s)
        {
            s = Link.Replace(s, m => m.Groups[1].Value == m.Groups[2].Value ? m.Groups[2].Value : m.Groups[1].Value + " (" + m.Groups[2].Value + ")");
            return s.Replace("**", "").Replace("`", "").Replace("<br>", " ");
        }

        /// <summary>Repo root for a Unity project folder (the repo keeps the project in HarvestPanic/).</summary>
        public static string RepoRootFor(string projectPath) => Path.GetFullPath(Path.Combine(projectPath, ".."));

        /// <summary>Reads THIRD_PARTY_LICENSES.md and the bundled licence files from disk and composes the text.</summary>
        public static string ComposeFromDisk(string projectPath)
        {
            string md = Path.Combine(RepoRootFor(projectPath), SourceFileName);
            string text = File.Exists(md) ? File.ReadAllText(md) : "";
            var extras = new List<KeyValuePair<string, string>>();
            foreach (var rel in BundledLicenceFiles)
            {
                string p = Path.Combine(projectPath, rel);
                if (File.Exists(p)) extras.Add(new KeyValuePair<string, string>(Path.GetFileNameWithoutExtension(p), File.ReadAllText(p)));
            }
            return Compose(text, extras);
        }

        /// <summary>The text the Credits screen shows (editor: live from disk; player: baked TextAsset).</summary>
        public static string Load()
        {
#if UNITY_EDITOR
            return ComposeFromDisk(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
#else
            var ta = Resources.Load<TextAsset>(ResourcePath);
            return ta != null && !string.IsNullOrEmpty(ta.text) ? ta.text : NoText;
#endif
        }
    }
}
