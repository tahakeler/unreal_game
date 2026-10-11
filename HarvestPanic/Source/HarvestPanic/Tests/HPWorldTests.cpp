// Copyright HARVEST PANIC team. All Rights Reserved.
// Tests: marker tag parsing (docs/engine/marker-contract.md) and bridge-hazards plank damage formula.
#include "World/HPMarkerResolver.h"
#include "World/HPHazardMath.h"
#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

namespace HPWorldTest
{
	constexpr EAutomationTestFlags Flags = EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPMarkerParseTest, "HarvestPanic.World.MarkerTags.Parse", HPWorldTest::Flags)
bool FHPMarkerParseTest::RunTest(const FString&)
{
	int32 N = 0;
	TestTrue(TEXT("pig"), UHPMarkerResolver::ParseTag(TEXT("HP_CritterSpawner_Pig:7"), N) == EHPMarkerKind::CritterSpawnerPig);
	TestEqual(TEXT("pig count"), N, 7);
	TestTrue(TEXT("chicken default"), UHPMarkerResolver::ParseTag(TEXT("HP_CritterSpawner_Chicken"), N, 3) == EHPMarkerKind::CritterSpawnerChicken);
	TestEqual(TEXT("default count"), N, 3);
	TestTrue(TEXT("bad count"), UHPMarkerResolver::ParseTag(TEXT("HP_CritterSpawner_Pig:abc"), N, 2) == EHPMarkerKind::CritterSpawnerPig);
	TestEqual(TEXT("bad count -> default"), N, 2);
	TestTrue(TEXT("unrelated"), UHPMarkerResolver::ParseTag(TEXT("Foo"), N) == EHPMarkerKind::Unknown);
	TestTrue(TEXT("unknown HP"), UHPMarkerResolver::ParseTag(TEXT("HP_Nope"), N) == EHPMarkerKind::Unknown);
	TestTrue(TEXT("river"), UHPMarkerResolver::ParseTag(TEXT("HP_River"), N) == EHPMarkerKind::River);
	TestTrue(TEXT("pile"), UHPMarkerResolver::ParseTag(TEXT("HP_RiverPile"), N) == EHPMarkerKind::RiverPile);
	TestTrue(TEXT("wardrobe"), UHPMarkerResolver::ParseTag(TEXT("HP_Wardrobe"), N) == EHPMarkerKind::Wardrobe);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPPlankDamageTest, "HarvestPanic.World.Bridge.PlankDamage", HPWorldTest::Flags)
bool FHPPlankDamageTest::RunTest(const FString&)
{
	// GDD: 290 kg on a plank -> (290-250)*0.2 = 8 dmg/s.
	TestEqual(TEXT("cow carry"), HPHazardMath::PlankDamagePerSecond(290.f, 250.f, 0.2f, false), 8.f, 1e-3f);
	// Loaded wagon 260 kg -> 2 dmg/s.
	TestEqual(TEXT("wagon"), HPHazardMath::PlankDamagePerSecond(260.f, 250.f, 0.2f, false), 2.f, 1e-3f);
	// Lone player never damages.
	TestEqual(TEXT("solo"), HPHazardMath::PlankDamagePerSecond(70.f, 250.f, 0.2f, false), 0.f, 1e-3f);
	// Cracked planks take x1.5.
	TestEqual(TEXT("cracked"), HPHazardMath::PlankDamagePerSecond(290.f, 250.f, 0.2f, true), 12.f, 1e-3f);
	// Storm SafeLoad x0.7 -> 175.
	TestEqual(TEXT("storm"), HPHazardMath::PlankDamagePerSecond(275.f, 250.f, 0.2f, false, 0.7f), 20.f, 1e-3f);
	return true;
}

#endif
