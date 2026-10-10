// Copyright HARVEST PANIC team. All Rights Reserved.
// Unit tests for design/gdd/critter-ai.md section 4 formulas (pure logic, no world).
#include "Misc/AutomationTest.h"
#include "AI/HPCritterLogic.h"
#include "AI/HPCritterPersonality.h"
#include "HPNoiseSubsystem.h"

#if WITH_DEV_AUTOMATION_TESTS

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPCritterThreatFormulaTest, "HarvestPanic.AI.Critter.ThreatFormula",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
bool FHPCritterThreatFormulaTest::RunTest(const FString& Parameters)
{
	// GDD example: pig, player 4 m away sprinting in view -> 0.3 + 0.2 + 0.15 = 0.65 -> Flee
	const float T = FHPCritterLogic::SourceThreat(4.f, 10.f, true, true, 0.f, 0.f);
	TestEqual(TEXT("GDD worked example"), T, 0.65f, 1e-4f);
	const UHPCritterPersonality* Pig = GetDefault<UHPCritterPersonality_Pig>();
	TestTrue(TEXT("Example exceeds pig FleeThreshold"), T >= Pig->FleeThreshold);

	TestEqual(TEXT("Beyond radius = 0"), FHPCritterLogic::SourceThreat(12.f, 10.f, true, true, 0.f, 0.f), 0.f);
	TestEqual(TEXT("At radius edge P=0"), FHPCritterLogic::ProximityTerm(10.f, 10.f), 0.f);
	TestEqual(TEXT("Adjacent P=1"), FHPCritterLogic::ProximityTerm(0.f, 10.f), 1.f);
	TestEqual(TEXT("Clamped to 1"), FHPCritterLogic::SourceThreat(0.f, 10.f, true, true, 0.8f, 0.6f), 1.f);
	TestEqual(TEXT("Walk 8 m unseen"), FHPCritterLogic::SourceThreat(8.f, 10.f, false, false, 0.f, 0.f), 0.1f, 1e-4f);

	// Total = max + 0.25 * sum(others)
	const float Arr[] = { 0.4f, 0.2f, 0.2f };
	TestEqual(TEXT("Total threat"), FHPCritterLogic::TotalThreat(Arr), 0.5f, 1e-4f);
	TestEqual(TEXT("Empty total"), FHPCritterLogic::TotalThreat(TConstArrayView<float>()), 0.f);
	const float Big[] = { 0.9f, 0.9f };
	TestEqual(TEXT("Total clamps"), FHPCritterLogic::TotalThreat(Big), 1.f);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPCritterNoiseTest, "HarvestPanic.AI.Critter.PerceivedNoise",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
bool FHPCritterNoiseTest::RunTest(const FString& Parameters)
{
	// N = L * Sens * SrcMult * (1 - d/R)
	TestEqual(TEXT("Formula"), FHPCritterLogic::PerceivedNoise(0.8f, 1.f, 1.f, 10.f, 40.f), 0.6f, 1e-4f);
	TestEqual(TEXT("Outside radius floored"), FHPCritterLogic::PerceivedNoise(0.8f, 1.f, 1.f, 50.f, 40.f), 0.f);
	TestEqual(TEXT("Zero radius"), FHPCritterLogic::PerceivedNoise(0.8f, 1.f, 1.f, 0.f, 0.f), 0.f);

	// AC2: shout (0.8) within 10 m sends critters to Flee; walk footstep (0.10) does not.
	const UHPCritterPersonality* Pig = GetDefault<UHPCritterPersonality_Pig>();
	const UHPCritterPersonality* Chicken = GetDefault<UHPCritterPersonality_Chicken>();
	const float ShoutR = UHPNoiseSubsystem::RadiusForLoudness(0.8f);
	const float PigShout = FHPCritterLogic::PerceivedNoise(0.8f, Pig->NoiseSensitivity, Pig->GetNoiseSourceMult(ENoiseSource::Shout), 10.f, ShoutR);
	TestTrue(TEXT("Shout at 10 m makes pig flee"), FHPCritterLogic::SourceThreat(10.f, Pig->AlertRadius, false, false, PigShout, 0.f) >= Pig->FleeThreshold);
	const float StepR = UHPNoiseSubsystem::RadiusForLoudness(0.1f);
	const float ChickStep = FHPCritterLogic::PerceivedNoise(0.1f, Chicken->NoiseSensitivity, 1.f, 1.f, StepR);
	TestTrue(TEXT("Footstep 1 m from chicken does not flee it"), ChickStep < Chicken->FleeThreshold);
	TestTrue(TEXT("Chicken more sensitive than pig"), Chicken->NoiseSensitivity > Pig->NoiseSensitivity);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPCritterLureTest, "HarvestPanic.AI.Critter.LureVsThreat",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
bool FHPCritterLureTest::RunTest(const FString& Parameters)
{
	// Lure wins if Affinity * (1 - d/15) > T * 0.8
	TestEqual(TEXT("Lure score"), FHPCritterLogic::LureScore(0.9f, 7.5f), 0.45f, 1e-4f);
	TestTrue(TEXT("Calm pig, bucket 8 m: lured"), FHPCritterLogic::LureWins(0.9f, 8.f, 0.f));
	TestFalse(TEXT("Panicked pig ignores bucket"), FHPCritterLogic::LureWins(0.9f, 8.f, 0.65f));
	TestTrue(TEXT("Mild threat, close bucket"), FHPCritterLogic::LureWins(0.9f, 2.f, 0.3f));
	TestFalse(TEXT("Out of lure range"), FHPCritterLogic::LureWins(0.9f, 16.f, 0.f));
	TestFalse(TEXT("Equality is not a win"), FHPCritterLogic::LureWins(0.5f, 0.f, 0.625f));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPCritterBreakFreeTest, "HarvestPanic.AI.Critter.BreakFreeOdds",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
bool FHPCritterBreakFreeTest::RunTest(const FString& Parameters)
{
	// P = clamp((Secure - carriers) * 0.35 + 0.05, 0, 0.9)
	TestEqual(TEXT("Pig (secure 2) by 1 = 40%"), FHPCritterLogic::BreakFreeChance(2, 1), 0.4f, 1e-4f);
	TestEqual(TEXT("Pig by 2 = never (AC5)"), FHPCritterLogic::BreakFreeChance(2, 2), 0.f);
	TestEqual(TEXT("Over-secured = never"), FHPCritterLogic::BreakFreeChance(2, 3), 0.f);
	TestEqual(TEXT("Not carried = 0"), FHPCritterLogic::BreakFreeChance(2, 0), 0.f);
	TestEqual(TEXT("Bull (3) by 1 = 75%"), FHPCritterLogic::BreakFreeChance(3, 1), 0.75f, 1e-4f);
	TestEqual(TEXT("Clamp 0.9"), FHPCritterLogic::BreakFreeChance(4, 1), 0.9f, 1e-4f);
	TestEqual(TEXT("Chicken (1) by 1 = never"), FHPCritterLogic::BreakFreeChance(1, 1), 0.f);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPCritterFleeDirTest, "HarvestPanic.AI.Critter.FleeDirection",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
bool FHPCritterFleeDirTest::RunTest(const FString& Parameters)
{
	const FVector Threats1[] = { FVector(-100, 0, 0) };
	const float W1[] = { 1.f };
	bool bAmb = true;
	const FVector D1 = FHPCritterLogic::FleeDirection(FVector::ZeroVector, Threats1, W1, &bAmb);
	TestTrue(TEXT("Flees +X away from threat"), D1.Equals(FVector(1, 0, 0), 1e-3f));
	TestFalse(TEXT("Not ambiguous"), bAmb);

	const FVector Threats2[] = { FVector(-100, 0, 0), FVector(100, 0, 0) };
	const float W2[] = { 1.f, 1.f };
	const FVector D2 = FHPCritterLogic::FleeDirection(FVector::ZeroVector, Threats2, W2, &bAmb);
	TestTrue(TEXT("Both sides -> ambiguous"), bAmb);
	TestTrue(TEXT("Both sides -> perpendicular"), FMath::Abs(D2.X) < 1e-3f && FMath::IsNearlyEqual(FMath::Abs(D2.Y), 1.f, 1e-3f));
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPCritterPersonalityTest, "HarvestPanic.AI.Critter.PersonalityDefaults",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
bool FHPCritterPersonalityTest::RunTest(const FString& Parameters)
{
	const UHPCritterPersonality* Pig = GetDefault<UHPCritterPersonality_Pig>();
	TestEqual(TEXT("Pig sprint"), Pig->SprintSpeed, 560.f);
	TestEqual(TEXT("Pig secure carriers"), Pig->SecureCarriers, 2);
	TestEqual(TEXT("Pig squeal loudness"), Pig->GrabNoiseLoudness, 0.7f);
	const UHPCritterPersonality* Chicken = GetDefault<UHPCritterPersonality_Chicken>();
	TestTrue(TEXT("Chicken flocks"), Chicken->HerdMode == ECritterHerdMode::Flock);
	TestEqual(TEXT("Chicken alert radius"), Chicken->AlertRadius, 8.f);
	TestEqual(TEXT("Unset source mult = 1"), Pig->GetNoiseSourceMult(ENoiseSource::Vehicle), 1.f);
	return true;
}

#endif // WITH_DEV_AUTOMATION_TESTS
