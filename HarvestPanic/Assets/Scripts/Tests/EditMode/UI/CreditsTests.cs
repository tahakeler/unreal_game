using System.Collections.Generic;
using System.IO;
using HarvestPanic.UI;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.UI
{
    /// <summary>Credits text generated from THIRD_PARTY_LICENSES.md (release readiness, GDD 18.7 item 9).</summary>
    public class CreditsTests
    {
        static string ProjectPath => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string RepoMd => Path.Combine(CreditsText.RepoRootFor(ProjectPath), CreditsText.SourceFileName);

        [Test]
        public void RealLicencesFile_ComposesFullCredits()
        {
            Assert.IsTrue(File.Exists(RepoMd), "THIRD_PARTY_LICENSES.md at repo root");
            string t = CreditsText.ComposeFromDisk(ProjectPath);
            foreach (var must in new[]
                     {
                         "TEAM", "Quaternius", "Kenney", "Kay Lousberg", "Lilita One", "Nunito", "Tripo",
                         "Facepunch.Steamworks", "MIT LICENCE - Facepunch.Steamworks", "Copyright (c) 2020 Facepunch Studios LTD",
                         "Copyright (c) 2021 Unity Technologies", "Permission is hereby granted, free of charge",
                         "THE SOFTWARE IS PROVIDED \"AS IS\"", "SIL OPEN FONT LICENSE", "https://poly.pizza/m/u35l6uP5vj",
                     })
                StringAssert.Contains(must, t, must);
            StringAssert.DoesNotContain("**", t);
            StringAssert.DoesNotContain("|---", t);
            StringAssert.DoesNotContain("> Permission", t);
        }

        [Test]
        public void MitBody_IsEmittedInFull_OncePerMitComponent()
        {
            string md = "## Libs\n\n| Component | Licence |\n|---|---|\n| LibA | MIT, Copyright (c) 2020 A Ltd |\n| LibB | MIT, Copyright (c) 2021 B Inc |\n| LibC | Zlib |\n\n" +
                        "MIT licence text (applies to both MIT rows):\n\n> Permission is hereby granted,\n> free of charge.\n>\n> THE SOFTWARE IS PROVIDED \"AS IS\".\n\nAfter.\n";
            string t = CreditsText.FromMarkdown(md);
            Assert.AreEqual(2, Count(t, "Permission is hereby granted, free of charge."), t);
            Assert.AreEqual(2, Count(t, "THE SOFTWARE IS PROVIDED \"AS IS\"."), t);
            StringAssert.Contains("MIT LICENCE - LibA\n", t);
            StringAssert.Contains("Copyright (c) 2020 A Ltd", t);
            StringAssert.Contains("MIT LICENCE - LibB\n", t);
            StringAssert.Contains("Copyright (c) 2021 B Inc", t);
            StringAssert.Contains("  - LibC | Zlib", t);
            StringAssert.Contains("After.", t);
            Assert.Less(t.IndexOf("MIT LICENCE - LibA"), t.IndexOf("After."), "MIT block stays where the md put it");
        }

        [Test]
        public void Markdown_HeadingsTablesLinks_ArePlainText()
        {
            string t = CreditsText.FromMarkdown("# Title **bold**\nSee [site](https://x.y) and `code`.\n\n| A | B |\n|:--|--:|\n| 1 | 2 |\n");
            StringAssert.Contains("TITLE BOLD\n----------\n", t);
            StringAssert.Contains("See site (https://x.y) and code.", t);
            StringAssert.Contains("  A | B\n  - 1 | 2\n", t);
        }

        [Test]
        public void Compose_AppendsBundledLicences_AndSurvivesMissingInput()
        {
            string t = CreditsText.Compose(null, new[] { new KeyValuePair<string, string>("OFL-Test", "SIL Open Font License 1.1 body"), new KeyValuePair<string, string>("Empty", " ") });
            StringAssert.Contains("FULL LICENCE TEXT - OFL-Test", t);
            StringAssert.DoesNotContain("FULL LICENCE TEXT - Empty", t);
            StringAssert.Contains("TEAM", t);
        }

        [Test]
        public void BundledLicenceFiles_ExistInProject()
        {
            foreach (var rel in CreditsText.BundledLicenceFiles)
                Assert.IsTrue(File.Exists(Path.Combine(ProjectPath, rel)), rel);
        }

        static int Count(string s, string sub)
        {
            int n = 0, i = 0;
            while ((i = s.IndexOf(sub, i, System.StringComparison.Ordinal)) >= 0) { n++; i += sub.Length; }
            return n;
        }
    }
}
