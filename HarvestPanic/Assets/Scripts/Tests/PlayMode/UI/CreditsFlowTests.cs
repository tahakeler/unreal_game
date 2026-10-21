using System.Collections;
using HarvestPanic.Core;
using HarvestPanic.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace HarvestPanic.Tests.PlayMode.UI
{
    /// <summary>Main menu -> Credits -> Back, on the real MainMenu scene.</summary>
    public class CreditsFlowTests
    {
        [SetUp]
        public void SetUp()
        {
            SettingsService.UseStore(new MemorySettingsStore());
            Services.Clear();
            Services.Register<ISessionService>(new FakeSessionService { ConnectDelay = 0f, LocalName = "Tester" });
        }

        [TearDown] public void TearDown() { Time.timeScale = 1f; SettingsService.UseStore(null); }

        [UnityTest]
        public IEnumerator Title_Credits_ShowsTeamAndLicences_BackReturns()
        {
            yield return FrontendFlowTests.LoadScene(SceneLoader.MainMenu);
            var menu = Object.FindFirstObjectByType<MainMenuController>();
            Assert.NotNull(menu);
            var btn = menu.Panel.Root.Q<Button>("btn-credits");
            Assert.NotNull(btn, "Credits button on the title screen");
            Assert.AreEqual("Credits", btn.text);

            var credits = menu.OpenCredits();
            yield return null;
            Assert.AreEqual(MenuScreen.Credits, menu.Stack.Current);
            Assert.IsTrue(menu.Panel.Root.Q("credits-screen").ClassListContains("is-active"));
            Assert.IsFalse(menu.Panel.Root.Q("title-screen").ClassListContains("is-active"));
            Assert.Greater(credits.Paragraphs.Count, 10);
            Assert.IsTrue(credits.Contains("TEAM"));
            Assert.IsTrue(credits.Contains("Copyright (c) 2020 Facepunch Studios LTD"));
            Assert.IsTrue(credits.Contains("Permission is hereby granted, free of charge"));
            Assert.IsTrue(credits.Contains("Quaternius"));
            foreach (var p in credits.Paragraphs) Assert.Less(p.text.Length, 8000, "paragraph labels stay small");

            yield return null; yield return null;
            credits.ScrollBy(CreditsPanel.ScrollStep);
            yield return null;
            Assert.Greater(credits.Scroll.scrollOffset.y, 0f, "Down scrolls the credits");

            menu.GoBack();
            yield return null;
            Assert.AreEqual(MenuScreen.Title, menu.Stack.Current);
            Assert.IsFalse(menu.Panel.Root.Q("credits-screen").ClassListContains("is-active"));

            menu.OpenCredits();
            Assert.AreEqual(2, menu.Stack.Depth, "Credits opens again on top of the title");
            menu.GoBack();
            Assert.AreEqual(MenuScreen.Title, menu.Stack.Current);
        }
    }
}
