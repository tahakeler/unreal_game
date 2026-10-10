// Copyright HARVEST PANIC team. All Rights Reserved.
// Formula tests for goods-value, shared-wallet-economy, week-quota, blame-ledger, day-loop.
#include "Misc/AutomationTest.h"
#include "Economy/HPEconomyMath.h"

#if WITH_DEV_AUTOMATION_TESTS

static constexpr EAutomationTestFlags HPTestFlags = EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter;

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FEconomySaleValueFormulaTest, "HarvestPanic.Economy.SaleValue.MatchesGddFormula", HPTestFlags)
bool FEconomySaleValueFormulaTest::RunTest(const FString& Parameters)
{
	// Arrange: pristine pig, neutral demand, no order
	// Act / Assert
	TestEqual(TEXT("pristine pig = base"), UHPEconomyMath::SaleValue(60, 1.f, 1.f, 1.f, 1.f, 1.f), 60);
	TestEqual(TEXT("scuffed pig on order, D=1.1: 60*.85*1.1*1.5 = 84.15 -> 84"),
		UHPEconomyMath::SaleValue(60, 0.85f, 1.f, 1.1f, 1.5f, 1.f), 84);
	TestEqual(TEXT("max: pumpkin x1.5 D x1.5 O x1.25 dusk = 843.75 -> 844"),
		UHPEconomyMath::SaleValue(300, 1.f, 1.f, 1.5f, 1.5f, 1.25f), 844);
	TestEqual(TEXT("ruined crop sells as compost for $1"),
		UHPEconomyMath::FinalSaleValue(300, EHPBruiseState::Ruined, EHPGoodsCategory::Crop, 1.f, 1.f, 1.f, 1.f), 1);
	TestEqual(TEXT("ruined livestock floored at 25%"),
		UHPEconomyMath::FinalSaleValue(60, EHPBruiseState::Ruined, EHPGoodsCategory::Livestock, 1.f, 1.f, 1.f, 1.f), 15);
	TestEqual(TEXT("egg at freshness floor 0.5"),
		UHPEconomyMath::FinalSaleValue(5, EHPBruiseState::Pristine, EHPGoodsCategory::Product, 0.5f, 1.f, 1.f, 1.f), 3);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FEconomyBruiseDeltaTest, "HarvestPanic.Economy.Bruise.DeltaMatchesGddExamples", HPTestFlags)
bool FEconomyBruiseDeltaTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("pig shoulder drop I=1200 -> 16 BP"), UHPEconomyMath::ComputeBruiseDelta(1200.f, 0.8f), 16.f);
	TestEqual(TEXT("below I_min does nothing"), UHPEconomyMath::ComputeBruiseDelta(399.f, 2.f), 0.f);
	TestEqual(TEXT("pumpkin off bridge I=3000 capped at 60 per hit"), UHPEconomyMath::ComputeBruiseDelta(3000.f, 1.5f), 60.f);
	TestEqual(TEXT("solver spike > 20000 ignored"), UHPEconomyMath::ComputeBruiseDelta(25000.f, 1.f), 0.f);
	TestEqual(TEXT("bruise mult 2x"), UHPEconomyMath::ComputeBruiseDelta(800.f, 1.f, 2.f), 20.f);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FEconomyBruiseTiersTest, "HarvestPanic.Economy.Bruise.TierThresholdsAndMultipliers", HPTestFlags)
bool FEconomyBruiseTiersTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("9.99 Pristine"), UHPEconomyMath::BruiseStateFromPoints(9.99f), EHPBruiseState::Pristine);
	TestEqual(TEXT("10 Scuffed"), UHPEconomyMath::BruiseStateFromPoints(10.f), EHPBruiseState::Scuffed);
	TestEqual(TEXT("34.9 Scuffed"), UHPEconomyMath::BruiseStateFromPoints(34.9f), EHPBruiseState::Scuffed);
	TestEqual(TEXT("35 Bruised"), UHPEconomyMath::BruiseStateFromPoints(35.f), EHPBruiseState::Bruised);
	TestEqual(TEXT("65 Busted"), UHPEconomyMath::BruiseStateFromPoints(65.f), EHPBruiseState::Busted);
	TestEqual(TEXT("99.9 Busted"), UHPEconomyMath::BruiseStateFromPoints(99.9f), EHPBruiseState::Busted);
	TestEqual(TEXT("100 Ruined"), UHPEconomyMath::BruiseStateFromPoints(100.f), EHPBruiseState::Ruined);
	TestEqual(TEXT("B scuffed"), UHPEconomyMath::BruiseMultiplier(EHPBruiseState::Scuffed, EHPGoodsCategory::Crop), 0.85f);
	TestEqual(TEXT("B bruised"), UHPEconomyMath::BruiseMultiplier(EHPBruiseState::Bruised, EHPGoodsCategory::Crop), 0.60f);
	TestEqual(TEXT("B busted"), UHPEconomyMath::BruiseMultiplier(EHPBruiseState::Busted, EHPGoodsCategory::Crop), 0.30f);
	TestEqual(TEXT("B ruined crop"), UHPEconomyMath::BruiseMultiplier(EHPBruiseState::Ruined, EHPGoodsCategory::Crop), 0.f);
	TestEqual(TEXT("B ruined livestock"), UHPEconomyMath::BruiseMultiplier(EHPBruiseState::Ruined, EHPGoodsCategory::Livestock), 0.25f);
	TestEqual(TEXT("blame $ pig pristine->scuffed at D 1.0 = 9"), UHPEconomyMath::BruiseDollarLoss(60, 1.f, 1.f, 0.85f), 9);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FEconomyFreshnessTest, "HarvestPanic.Economy.Freshness.GraceThenLinearToFloor", HPTestFlags)
bool FEconomyFreshnessTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("crop within grace"), UHPEconomyMath::Freshness(120.f, 120.f, 480.f), 1.f);
	TestEqual(TEXT("crop 240s after grace = 0.5"), UHPEconomyMath::Freshness(360.f, 120.f, 480.f), 0.5f);
	TestEqual(TEXT("crop 120s after grace = 0.75"), UHPEconomyMath::Freshness(240.f, 120.f, 480.f), 0.75f);
	TestEqual(TEXT("floor 0.5 at 10 min"), UHPEconomyMath::Freshness(600.f, 120.f, 480.f), 0.5f);
	TestEqual(TEXT("not started"), UHPEconomyMath::Freshness(-1.f, 120.f, 480.f), 1.f);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FEconomyQuotaCurveTest, "HarvestPanic.Economy.Quota.MatchesWeekQuotaTable", HPTestFlags)
bool FEconomyQuotaCurveTest::RunTest(const FString& Parameters)
{
	// week-quota.md table, rows w=1..5, columns 4p/3p/2p/1p
	const int32 Expected[5][4] = {
		{ 1200, 1020, 780, 540 },
		{ 1620, 1380, 1050, 730 },
		{ 2190, 1860, 1420, 980 },
		{ 2950, 2510, 1920, 1330 },
		{ 3990, 3390, 2590, 1790 },
	};
	for (int32 W = 1; W <= 5; ++W)
	{
		for (int32 P = 4; P >= 1; --P)
		{
			TestEqual(*FString::Printf(TEXT("Q(w=%d, %dp)"), W, P), UHPEconomyMath::WeeklyQuota(W, P), Expected[W - 1][4 - P]);
		}
	}
	TestEqual(TEXT("Region 2 mult 1.2"), UHPEconomyMath::WeeklyQuota(1, 4, 1.2f), 1440);
	TestEqual(TEXT("5+ players clamp to 4p"), UHPEconomyMath::WeeklyQuota(1, 6), 1200);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FEconomyFeesAndBonusTest, "HarvestPanic.Economy.Wallet.RespawnFeeAndOrderBonus", HPTestFlags)
bool FEconomyFeesAndBonusTest::RunTest(const FString& Parameters)
{
	const int32 Fees[6] = { 25, 30, 40, 45, 50, 55 };
	for (int32 W = 1; W <= 6; ++W)
	{
		TestEqual(*FString::Printf(TEXT("Fee(w=%d)"), W), UHPEconomyMath::RespawnFee(W), Fees[W - 1]);
	}
	TestEqual(TEXT("OCB week 3"), UHPEconomyMath::OrderCompleteBonus(3), 180);
	TestEqual(TEXT("order qty pigs 4p w1"), UHPEconomyMath::OrderQuantity(3, 4, 1), 3);
	TestEqual(TEXT("order qty eggs 1p w1 = round(2.7)"), UHPEconomyMath::OrderQuantity(6, 1, 1), 3);
	TestEqual(TEXT("order qty min 1"), UHPEconomyMath::OrderQuantity(1, 1, 1), 1);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FEconomyWalletFloorTest, "HarvestPanic.Economy.Wallet.FloorAtMinus200WithIOU", HPTestFlags)
bool FEconomyWalletFloorTest::RunTest(const FString& Parameters)
{
	int32 IOU = -1;
	TestEqual(TEXT("normal fee"), UHPEconomyMath::ApplyWalletDelta(100, -40, IOU), 60);
	TestEqual(TEXT("no IOU"), IOU, 0);
	TestEqual(TEXT("overdraft to -150 allowed"), UHPEconomyMath::ApplyWalletDelta(-110, -40, IOU), -150);
	TestEqual(TEXT("fee capped at floor"), UHPEconomyMath::ApplyWalletDelta(-180, -55, IOU), -200);
	TestEqual(TEXT("excess -> IOU"), IOU, 35);
	TestEqual(TEXT("at floor, whole fee -> IOU"), UHPEconomyMath::ApplyWalletDelta(-200, -25, IOU), -200);
	TestEqual(TEXT("IOU 25"), IOU, 25);
	TestEqual(TEXT("income from below zero"), UHPEconomyMath::ApplyWalletDelta(-200, 300, IOU), 100);
	TestEqual(TEXT("income no IOU"), IOU, 0);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FEconomyDemandTest, "HarvestPanic.Economy.Demand.SaturationAfterSixUnits", HPTestFlags)
bool FEconomyDemandTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("unit 6 full demand"), UHPEconomyMath::Demand(1.f, 6), 1.f);
	TestEqual(TEXT("unit 7 x0.95"), UHPEconomyMath::Demand(1.f, 7), 0.95f, 1e-4f);
	TestEqual(TEXT("floor 0.6"), UHPEconomyMath::Demand(0.8f, 40), 0.6f);
	TestEqual(TEXT("cap 1.5"), UHPEconomyMath::Demand(1.2f, 1, 2.f), 1.5f);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FBlameSharesTest, "HarvestPanic.Blame.Shares.RenormalizedTouchHistory", HPTestFlags)
bool FBlameSharesTest::RunTest(const FString& Parameters)
{
	const TArray<float> Three = UHPEconomyMath::BlameShares(3);
	TestEqual(TEXT("3 touchers"), Three.Num(), 3);
	TestEqual(TEXT("last 0.6"), Three[0], 0.6f, 1e-5f);
	TestEqual(TEXT("prev 0.3"), Three[1], 0.3f, 1e-5f);
	TestEqual(TEXT("earlier 0.1"), Three[2], 0.1f, 1e-5f);
	const TArray<float> Two = UHPEconomyMath::BlameShares(2);
	TestEqual(TEXT("2: 2/3"), Two[0], 2.f / 3.f, 1e-5f);
	TestEqual(TEXT("2: 1/3"), Two[1], 1.f / 3.f, 1e-5f);
	TestEqual(TEXT("1: 1.0"), UHPEconomyMath::BlameShares(1)[0], 1.f);
	TestEqual(TEXT("0: Nature (empty)"), UHPEconomyMath::BlameShares(0).Num(), 0);
	TestEqual(TEXT("5 touchers capped at 3"), UHPEconomyMath::BlameShares(5).Num(), 3);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FBlameSeverityTest, "HarvestPanic.Blame.Severity.MatchesGddExample", HPTestFlags)
bool FBlameSeverityTest::RunTest(const FString& Parameters)
{
	TestEqual(TEXT("$150 cow into river, W1 4p = 62.5+25 -> 88"),
		UHPEconomyMath::Severity(-150, 1200, EHPBlameVerb::DroppedInRiver), 88);
	TestEqual(TEXT("clamped at 100"), UHPEconomyMath::Severity(-1000, 1200, EHPBlameVerb::TippedWagon), 100);
	TestEqual(TEXT("small bruise"), UHPEconomyMath::Severity(-12, 1200, EHPBlameVerb::Bruised), 5);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FBlameReceiptTest, "HarvestPanic.Blame.Receipt.GroupsByPlayerThenVerbNatureLast", HPTestFlags)
bool FBlameReceiptTest::RunTest(const FString& Parameters)
{
	// Arrange
	auto Make = [](int32 Who, EHPBlameVerb Verb, FName Item, int32 Dollars)
	{
		FBlameEvent E;
		if (Who >= 0) { E.Instigators.Add(Who); E.PrimaryInstigator = Who; }
		E.Verb = Verb; E.ItemId = Item; E.DollarDelta = Dollars;
		return E;
	};
	TArray<FBlameEvent> Events = {
		Make(-1, EHPBlameVerb::DroppedInRiver, TEXT("Pig"), -60),
		Make(7, EHPBlameVerb::Dropped, TEXT("PrizePumpkin"), -90),
		Make(7, EHPBlameVerb::Rescue, TEXT("Pig"), 20),
		Make(7, EHPBlameVerb::Dropped, TEXT("PrizePumpkin"), -30),
	};
	// Act
	const TArray<FHPReceiptLine> Lines = UHPEconomyMath::BuildReceipt(Events);
	// Assert
	TestEqual(TEXT("3 grouped lines"), Lines.Num(), 3);
	TestEqual(TEXT("player first"), Lines[0].PlayerId, 7);
	TestEqual(TEXT("dropped x2"), Lines[0].Count, 2);
	TestEqual(TEXT("dropped total"), Lines[0].Dollars, -120);
	TestTrue(TEXT("positive after negative"), Lines[1].bPositive);
	TestEqual(TEXT("nature last"), Lines[2].PlayerId, HP_NO_PLAYER);
	TestEqual(TEXT("template"), UHPEconomyMath::FormatReceiptLine(Lines[0], TEXT("Dave")), FString(TEXT("Dave: dropped Prize Pumpkin x2")));
	return true;
}

#endif
