// Copyright HARVEST PANIC team. All Rights Reserved.
// Unit tests for physics-carry / player-character / blame-ledger pure math (Carry/HPCarryMath).
#include "Carry/HPCarryMath.h"
#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

namespace HPCarryTestFixture
{
	// Fixture mirrors the GDD Tuning Knobs (physics-carry, blame-ledger).
	constexpr float KgPerHand = 25.f;
	constexpr float Floor = 0.35f;
	constexpr float ThrowMin = 4.f, ThrowMax = 14.f, RefMass = 10.f, MassFactorMin = 0.3f;
	const float Shares[] = {0.6f, 0.3f, 0.1f};
	constexpr EAutomationTestFlags Flags = EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter;
}
using namespace HPCarryTestFixture;

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPCarrySpeedGddExamplesTest, "HarvestPanic.Carry.SpeedMultiplier.GddExamples", Flags)
bool FHPCarrySpeedGddExamplesTest::RunTest(const FString&)
{
	// 1-hand 5 kg chicken -> 0.9
	TestEqual(TEXT("chicken"), HPCarryMath::CarrySpeedMultiplier(5.f, 1, 1, KgPerHand, Floor), 0.9f, 1e-4f);
	// 4 players on a 300 kg cow (8 hands) -> floor 0.35
	TestEqual(TEXT("cow"), HPCarryMath::CarrySpeedMultiplier(300.f, 8, 8, KgPerHand, Floor), 0.35f, 1e-4f);
	// Not carrying -> 1
	TestEqual(TEXT("empty"), HPCarryMath::CarrySpeedMultiplier(10.f, 0, 2, KgPerHand, Floor), 1.f, 1e-4f);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPCarrySpeedGroupGateTest, "HarvestPanic.Carry.SpeedMultiplier.GroupGateClampsToHandsRatio", Flags)
bool FHPCarrySpeedGroupGateTest::RunTest(const FString&)
{
	// Light 4-hand crate (10 kg): 2 of 4 hands -> dragged at hands/required = 0.5
	TestEqual(TEXT("2 of 4 hands"), HPCarryMath::CarrySpeedMultiplier(10.f, 2, 4, KgPerHand, Floor), 0.5f, 1e-4f);
	// 4 of 4 hands -> GDD formula 1 - 0.5*10/100 = 0.95
	TestEqual(TEXT("4 of 4 hands"), HPCarryMath::CarrySpeedMultiplier(10.f, 4, 4, KgPerHand, Floor), 0.95f, 1e-4f);
	// 2 of 8 hands -> 0.25 clamps to floor
	TestEqual(TEXT("2 of 8 hands"), HPCarryMath::CarrySpeedMultiplier(10.f, 2, 8, KgPerHand, Floor), 0.35f, 1e-4f);
	// More hands never makes it slower
	TestTrue(TEXT("monotonic"), HPCarryMath::CarrySpeedMultiplier(80.f, 6, 4, KgPerHand, Floor)
		>= HPCarryMath::CarrySpeedMultiplier(80.f, 4, 4, KgPerHand, Floor));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPCarryLoadRatioTest, "HarvestPanic.Carry.LoadRatio.ClampsZeroToOne", Flags)
bool FHPCarryLoadRatioTest::RunTest(const FString&)
{
	TestEqual(TEXT("half"), HPCarryMath::LoadRatio(25.f, 2, KgPerHand), 0.5f, 1e-4f);
	TestEqual(TEXT("over"), HPCarryMath::LoadRatio(300.f, 2, KgPerHand), 1.f, 1e-4f);
	TestEqual(TEXT("no hands"), HPCarryMath::LoadRatio(5.f, 0, KgPerHand), 1.f, 1e-4f);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPCarryThrowSpeedTest, "HarvestPanic.Carry.ThrowSpeed.GddExamples", Flags)
bool FHPCarryThrowSpeedTest::RunTest(const FString&)
{
	// 2 kg turnip at full charge = 14 m/s
	TestEqual(TEXT("turnip full"), HPCarryMath::ThrowSpeed(1.f, 2.f, ThrowMin, ThrowMax, RefMass, MassFactorMin), 14.f, 1e-3f);
	// 40 kg pig at full charge = 14 * 0.3 = 4.2 m/s
	TestEqual(TEXT("pig full"), HPCarryMath::ThrowSpeed(1.f, 40.f, ThrowMin, ThrowMax, RefMass, MassFactorMin), 4.2f, 1e-3f);
	// No charge -> 4 m/s for light items
	TestEqual(TEXT("turnip tap"), HPCarryMath::ThrowSpeed(0.f, 2.f, ThrowMin, ThrowMax, RefMass, MassFactorMin), 4.f, 1e-3f);
	// Half charge, 20 kg -> 9 * 0.5 = 4.5
	TestEqual(TEXT("half 20kg"), HPCarryMath::ThrowSpeed(0.5f, 20.f, ThrowMin, ThrowMax, RefMass, MassFactorMin), 4.5f, 1e-3f);
	// Charge is clamped
	TestEqual(TEXT("overcharge"), HPCarryMath::ThrowSpeed(3.f, 2.f, ThrowMin, ThrowMax, RefMass, MassFactorMin), 14.f, 1e-3f);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPCarryTouchSharesTest, "HarvestPanic.Carry.TouchHistory.BlameSharesRenormalize", Flags)
bool FHPCarryTouchSharesTest::RunTest(const FString&)
{
	const TArray<float> Three = HPCarryMath::TouchShares(3, Shares);
	TestEqual(TEXT("3 count"), Three.Num(), 3);
	TestEqual(TEXT("3 newest"), Three[0], 0.6f, 1e-4f);
	TestEqual(TEXT("3 mid"), Three[1], 0.3f, 1e-4f);
	TestEqual(TEXT("3 oldest"), Three[2], 0.1f, 1e-4f);

	const TArray<float> Two = HPCarryMath::TouchShares(2, Shares);
	TestEqual(TEXT("2 newest"), Two[0], 0.6f / 0.9f, 1e-4f);
	TestEqual(TEXT("2 older"), Two[1], 0.3f / 0.9f, 1e-4f);

	const TArray<float> One = HPCarryMath::TouchShares(1, Shares);
	TestEqual(TEXT("1 all"), One[0], 1.f, 1e-4f);

	TestEqual(TEXT("0 = Nature"), HPCarryMath::TouchShares(0, Shares).Num(), 0);
	TestEqual(TEXT("5 capped at 3"), HPCarryMath::TouchShares(5, Shares).Num(), 3);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPCarryTouchHistoryWindowTest, "HarvestPanic.Carry.TouchHistory.NewestFirstWithinWindow", Flags)
bool FHPCarryTouchHistoryWindowTest::RunTest(const FString&)
{
	FHPTouchHistory History;
	History.AddTouch(1, 0.0);   // expires (older than 3 s at t=4)
	History.AddTouch(2, 1.5);
	History.AddTouch(3, 2.0);
	History.AddTouch(2, 3.5);   // re-touch moves player 2 to newest
	History.AddTouch(4, 3.9);

	const TArray<int32> Ids = History.GetRecentToucherIds(4.0, 3.0, 3);
	TestEqual(TEXT("count"), Ids.Num(), 3);
	TestEqual(TEXT("newest"), Ids[0], 4);
	TestEqual(TEXT("second"), Ids[1], 2);
	TestEqual(TEXT("third"), Ids[2], 3);

	TestEqual(TEXT("all expired"), History.GetRecentToucherIds(100.0, 3.0, 3).Num(), 0);
	History.AddTouch(INDEX_NONE, 5.0);
	TestEqual(TEXT("nobody ignored"), History.GetRecentToucherIds(5.0, 3.0, 3)[0], 4);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPCarryRagdollThresholdTest, "HarvestPanic.Character.RagdollThreshold.GddRange", Flags)
bool FHPCarryRagdollThresholdTest::RunTest(const FString&)
{
	TestEqual(TEXT("fresh empty"), HPCarryMath::RagdollThreshold(900.f, 0.f, 0.4f, 100.f, 20.f, 0.7f, 1.f), 900.f, 1e-2f);
	// full load, tired, mud -> 900 * 0.6 * 0.7 * 0.8 = 302.4 (GDD range low end 302)
	TestEqual(TEXT("worst case"), HPCarryMath::RagdollThreshold(900.f, 1.f, 0.4f, 10.f, 20.f, 0.7f, 0.8f), 302.4f, 1e-2f);
	return true;
}

#endif // WITH_DEV_AUTOMATION_TESTS
