using HarvestPanic.AI.Night;
using NUnit.Framework;

namespace HarvestPanic.Tests.EditMode.Night
{
    /// <summary>
    /// bot-week-report #5: foxes appeared to grow +1 per night (0,1,2,2,2 | W2 3). GDD 8.9 / unity-contracts "Night phase":
    /// the concurrent fox CAP grows by week (2 +1/week, max 5) while the budget B = 10 + 4*DayIndex + 3*PlayersOutside
    /// grows by day, so early-week nights are budget-limited and fill up to the weekly cap. This pins that behaviour.
    /// </summary>
    public class NightFoxEscalationTests
    {
        static int AffordableFoxes(int dayIndex, int playersOutside, NightTuning t)
        {
            int week = NightMath.Week(dayIndex, t.DaysPerWeek);
            int left = NightMath.Budget(dayIndex, playersOutside, t) - NightMath.StitchesCap(week, t) * t.StitchesCost;
            return System.Math.Min(NightMath.FoxCap(week, t), System.Math.Max(0, left / t.FoxCost));
        }

        [Test] public void FoxCap_IsWeekly_NotNightly()
        {
            var t = new NightTuning();
            for (int d = 0; d < t.DaysPerWeek; d++) Assert.AreEqual(2, NightMath.FoxCap(NightMath.Week(d, t.DaysPerWeek), t), "week 1 day " + d);
            for (int d = 5; d < 10; d++) Assert.AreEqual(3, NightMath.FoxCap(NightMath.Week(d, t.DaysPerWeek), t), "week 2 day " + d);
            Assert.AreEqual(5, NightMath.FoxCap(10, t), "capped at FoxCapMax");
        }

        [Test] public void BotWeekFoxCounts_AreBudgetFillingTheWeeklyCap()
        {
            var t = new NightTuning();
            // idle host inside the barn zone: 0 players outside (as in the bot run)
            Assert.AreEqual(new[] { 0, 1, 2, 2, 2, 3 }, new[] { AffordableFoxes(0, 0, t), AffordableFoxes(1, 0, t), AffordableFoxes(2, 0, t), AffordableFoxes(3, 0, t), AffordableFoxes(4, 0, t), AffordableFoxes(5, 0, t) });
            for (int d = 1; d < 25; d++)
            {
                int w = NightMath.Week(d, t.DaysPerWeek), wPrev = NightMath.Week(d - 1, t.DaysPerWeek);
                if (w == wPrev) Assert.AreEqual(NightMath.FoxCap(wPrev, t), NightMath.FoxCap(w, t), "cap must not change mid-week (day " + d + ")");
            }
        }
    }
}
