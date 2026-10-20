using System.Collections.Generic;
using HarvestPanic.Core;
using HarvestPanic.Weather;
using NUnit.Framework;
using UnityEngine;

namespace HarvestPanic.Tests.EditMode.Weather
{
    /// <summary>weather-twists §3.3 deck rules, §8.2 determinism, card -> modifier mapping.</summary>
    public class WeatherEditModeTests
    {
        [Test]
        public void SameSeed_SameSequence_DifferentSeed_Differs()
        {
            var a = new List<WeatherKind>(); var b = new List<WeatherKind>(); var c = new List<WeatherKind>();
            for (int d = 0; d < 40; d++) { a.Add(WeatherDeck.Roll(1234, d)); b.Add(WeatherDeck.Roll(1234, d)); c.Add(WeatherDeck.Roll(98765, d)); }
            CollectionAssert.AreEqual(a, b);
            CollectionAssert.AreNotEqual(a, c);
        }

        [Test]
        public void DeckRules_NoStormOrFogDay1_NoThreeInARow()
        {
            for (int seed = 1; seed <= 500; seed++)
            {
                var d0 = WeatherDeck.Roll(seed, 0);
                Assert.That(d0 != WeatherKind.Storm && d0 != WeatherKind.Fog, $"seed {seed} day 1 = {d0}");
                var p2 = WeatherDeck.Roll(seed, 0); var p1 = WeatherDeck.Roll(seed, 1);
                for (int d = 2; d < 15; d++)
                {
                    var k = WeatherDeck.Roll(seed, d);
                    Assert.IsFalse(k == p1 && k == p2, $"seed {seed} repeats {k} three days");
                    p2 = p1; p1 = k;
                }
            }
        }

        [Test]
        public void Weights_RoughlyMatchDeck()
        {
            var n = new int[WeatherDeck.KindCount];
            for (int seed = 1; seed <= 3000; seed++) n[(int)WeatherDeck.Roll(seed, 3)]++;
            for (int i = 0; i < n.Length; i++) Assert.Greater(n[i], 100, ((WeatherKind)i).ToString());
            Assert.Greater(n[(int)WeatherKind.Clear], n[(int)WeatherKind.Storm]);
            var only = new float[] { 0, 1, 0, 0, 0, 0 };
            Assert.AreEqual(WeatherKind.Rain, WeatherDeck.Roll(5, 4, only));
        }

        [Test]
        public void EveryCard_HasModifierAsset_WithExpectedEffects()
        {
            Content.DayModifiers.Invalidate();
            for (int i = 0; i < WeatherDeck.KindCount; i++)
            {
                var k = (WeatherKind)i;
                Assert.IsTrue(Content.DayModifiers.TryGet(WeatherDeck.ModifierId(k), out var m), k.ToString());
                Assert.AreEqual(DayModifierCategory.Weather, m.Category);
                Assert.AreEqual(DayModifierStackRule.ReplaceGroup, m.StackRule);
                Assert.AreEqual("Group.Weather", m.GroupTag);
                CollectionAssert.Contains(m.GrantTags, WeatherDeck.WorldTag(k));
                Assert.IsFalse(string.IsNullOrEmpty(m.CardText));
            }
            Assert.AreEqual(45f, Effect(WeatherKind.Rain, DayParams.WeatherSoakSeconds));
            Assert.Less(Effect(WeatherKind.Rain, DayParams.MudSpeedMult), 1f);
            Assert.Greater(Effect(WeatherKind.Rain, DayParams.RiverCurrentMult), 1f);
            Assert.That(Effect(WeatherKind.Wind, DayParams.WeatherWindSpeed), Is.InRange(4f, 12f));
            Assert.AreEqual(22f, Effect(WeatherKind.Fog, DayParams.WeatherFogDistance));
            Assert.Less(Effect(WeatherKind.Storm, DayParams.BridgeSafeLoadMult), Effect(WeatherKind.Rain, DayParams.BridgeSafeLoadMult));
            Assert.IsTrue(Content.DayModifiers.TryGet(WeatherDeck.ModifierId(WeatherKind.Clear), out var clear));
            Assert.AreEqual(0, clear.Effects.Length);
        }

        static float Effect(WeatherKind k, string p)
        {
            Content.DayModifiers.TryGet(WeatherDeck.ModifierId(k), out var m);
            foreach (var e in m.Effects) if (e.ParamName == p) return e.Value;
            Assert.Fail($"{k} has no {p}");
            return 0f;
        }

        [Test]
        public void WindForce_MatchesGddExample()
        {
            // 10 m/s on a 0.6 m^2 pumpkin ~ 36 N
            Assert.AreEqual(36f, WeatherDirector.WindForce(100f, 0.6f), 0.01f);
        }

        [Test]
        public void Lighting_PhasesWarmBrightOrange_AndWeatherDarkens()
        {
            var morning = WeatherLook.ForPhase(DayClockPhase.Morning, 0f);
            var noon = WeatherLook.ForPhase(DayClockPhase.Workday, 0.5f);
            var dusk = WeatherLook.ForPhase(DayClockPhase.Dusk, 0f);
            Assert.Greater(noon.SunIntensity, morning.SunIntensity);
            Assert.Greater(noon.SunPitch, morning.SunPitch);
            Assert.Less(dusk.SunPitch, morning.SunPitch);
            Assert.Greater(dusk.SunColor.r - dusk.SunColor.b, noon.SunColor.r - noon.SunColor.b, "dusk is orange");
            var rain = WeatherLook.WithWeather(noon, WeatherKind.Rain, 0f);
            Assert.Less(rain.SunIntensity, noon.SunIntensity);
            Assert.Less(rain.FogEnd, noon.FogEnd);
            var fog = WeatherLook.WithWeather(noon, WeatherKind.Fog, 22f);
            Assert.AreEqual(22f, fog.FogEnd);
        }

        [Test]
        public void Parse_And_Forecast()
        {
            Assert.IsTrue(WeatherDeck.TryParse("rain", out var k)); Assert.AreEqual(WeatherKind.Rain, k);
            Assert.IsFalse(WeatherDeck.TryParse("snow", out _));
            for (int i = 0; i < WeatherDeck.KindCount; i++) Assert.IsNotEmpty(WeatherDeck.Forecast((WeatherKind)i));
        }
    }
}
