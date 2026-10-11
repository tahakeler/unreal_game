// Copyright HARVEST PANIC team. All Rights Reserved.
// Automation: HarvestPanic.Customization.*  (loadout validation, serialization, catalog defaults)

#include "Misc/AutomationTest.h"
#include "Customization/HPCosmeticCatalog.h"
#include "Customization/HPLoadoutSaveGame.h"
#include "Kismet/GameplayStatics.h"
#include "Serialization/BitWriter.h"
#include "Serialization/BitReader.h"
#include "Serialization/MemoryWriter.h"
#include "Serialization/MemoryReader.h"
#include "Serialization/ObjectAndNameAsStringProxyArchive.h"

#if WITH_DEV_AUTOMATION_TESTS

namespace HPCustTest
{
	static UHPCosmeticCatalog* MakeCatalog(bool bUnlockAll)
	{
		UHPCosmeticCatalog* C = NewObject<UHPCosmeticCatalog>();
		C->bUnlockAllCosmetics = bUnlockAll;
		return C;
	}
	static FHPCosmeticLoadout Sample()
	{
		FHPCosmeticLoadout L;
		L.BodyPreset = EHPBodyPreset::Quinn; L.PrimaryColor = 14; L.SecondaryColor = 19; L.AccentColor = 21;
		L.SkinTone = 7; L.Hat = 108; L.Face = 203; L.Back = 302; L.NameTagColor = 23;
		return L;
	}
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPCatalogDefaultsTest, "HarvestPanic.Customization.CatalogDefaults",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
bool FHPCatalogDefaultsTest::RunTest(const FString&)
{
	const UHPCosmeticCatalog* C = GetDefault<UHPCosmeticCatalog>();
	TestEqual(TEXT("24-swatch palette"), C->Palette.Num(), 24);
	TestEqual(TEXT("12 base + 2 fantasy skins"), C->SkinTones.Num(), 14);
	TestEqual(TEXT("4 emotes"), C->Emotes.Num(), 4);
	TestTrue(TEXT("unlock-all defaults ON for VS"), C->bUnlockAllCosmetics);
	TestTrue(TEXT(">=10 hats"), C->GetItemsForSlot(EHPCosmeticSlot::Hat, false).Num() >= 10);
	TestTrue(TEXT(">=3 face"), C->GetItemsForSlot(EHPCosmeticSlot::Face, false).Num() >= 3);
	TestTrue(TEXT(">=2 back"), C->GetItemsForSlot(EHPCosmeticSlot::Back, false).Num() >= 2);
	const TCHAR* RequiredHats[] = { TEXT("StrawHat"), TEXT("TruckerCap"), TEXT("BucketHelmet"), TEXT("PumpkinHead"), TEXT("CowboyHat"),
		TEXT("ChefToque"), TEXT("FlowerCrown"), TEXT("TrafficCone"), TEXT("VikingHorns"), TEXT("GrandpasFedora") };
	for (const TCHAR* H : RequiredHats)
	{
		const FHPCosmeticItem* I = C->FindItemByName(H);
		TestTrue(FString::Printf(TEXT("hat %s exists"), H), I && I->Slot == EHPCosmeticSlot::Hat && I->Parts.Num() > 0);
	}
	TestNotNull(TEXT("Moustache"), C->FindItemByName(TEXT("Moustache")));
	TestNotNull(TEXT("Goggles"), C->FindItemByName(TEXT("Goggles")));
	TestNotNull(TEXT("Sunglasses"), C->FindItemByName(TEXT("Sunglasses")));
	TestNotNull(TEXT("Backpack Basket"), C->FindItemByName(TEXT("BackpackBasket")));
	TestNotNull(TEXT("Cape"), C->FindItemByName(TEXT("Cape")));

	TSet<uint16> Ids;
	for (const FHPCosmeticItem& I : C->Items)
	{
		TestTrue(TEXT("NetId non-zero"), I.NetId != 0);
		TestFalse(FString::Printf(TEXT("NetId %d unique"), I.NetId), Ids.Contains(I.NetId));
		Ids.Add(I.NetId);
	}
	const FHPEmoteDef* Faint = C->GetEmote(EHPEmote::Faint);
	TestTrue(TEXT("Faint 2s / 5s cooldown / ragdoll"), Faint && FMath::IsNearlyEqual(Faint->Duration, 2.f) && FMath::IsNearlyEqual(Faint->Cooldown, 5.f) && Faint->bRagdoll);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPLoadoutValidationTest, "HarvestPanic.Customization.LoadoutValidation",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
bool FHPLoadoutValidationTest::RunTest(const FString&)
{
	UHPCosmeticCatalog* All = HPCustTest::MakeCatalog(true);
	UHPCosmeticCatalog* Gated = HPCustTest::MakeCatalog(false);

	TestTrue(TEXT("sample valid with unlock-all"), All->IsLoadoutValid(HPCustTest::Sample()));
	TestTrue(TEXT("default struct valid"), All->IsLoadoutValid(FHPCosmeticLoadout()));

	// Locked hat (Traffic Cone is a Letter unlock) rejected when the gate is off.
	FHPCosmeticLoadout L = HPCustTest::Sample();
	TestFalse(TEXT("locked hat invalid when gated"), Gated->IsLoadoutValid(L));
	TestTrue(TEXT("sanitize changes it"), Gated->SanitizeLoadout(L));
	TestEqual(TEXT("locked hat -> None"), (int32)L.Hat, 0);
	TestEqual(TEXT("unlocked face kept"), (int32)L.Face, 203);

	// Fantasy skin locked when gated.
	FHPCosmeticLoadout Skin; Skin.SkinTone = 12;
	TestFalse(TEXT("fantasy skin gated"), Gated->IsLoadoutValid(Skin));
	TestTrue(TEXT("fantasy skin ok unlock-all"), All->IsLoadoutValid(Skin));

	// Garbage from a malicious client.
	FHPCosmeticLoadout Bad;
	Bad.BodyPreset = static_cast<EHPBodyPreset>(77); Bad.PrimaryColor = 200; Bad.SecondaryColor = 24; Bad.AccentColor = 255;
	Bad.SkinTone = 99; Bad.NameTagColor = 30; Bad.Hat = 201 /*face item in hat slot*/; Bad.Face = 9999; Bad.Back = 101;
	TestTrue(TEXT("garbage sanitized"), All->SanitizeLoadout(Bad));
	TestTrue(TEXT("now valid"), All->IsLoadoutValid(Bad));
	TestEqual(TEXT("wrong-slot item cleared"), (int32)Bad.Hat, 0);
	TestEqual(TEXT("unknown id cleared"), (int32)Bad.Face, 0);
	TestEqual(TEXT("back hat cleared"), (int32)Bad.Back, 0);
	TestTrue(TEXT("body clamped"), Bad.BodyPreset == EHPBodyPreset::Manny);
	TestFalse(TEXT("idempotent"), All->SanitizeLoadout(Bad));

	// Random loadouts are always valid, also under the gate.
	FRandomStream S(1234);
	for (int32 i = 0; i < 300; ++i)
	{
		const FHPCosmeticLoadout R = Gated->MakeRandomLoadout(S);
		if (!Gated->IsLoadoutValid(R)) { AddError(FString::Printf(TEXT("random invalid: %s"), *R.ToDebugString())); break; }
		if (R.PrimaryColor == R.SecondaryColor) { AddError(TEXT("random primary == secondary")); break; }
	}
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPLoadoutSerializationTest, "HarvestPanic.Customization.SerializationRoundTrip",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::EngineFilter)
bool FHPLoadoutSerializationTest::RunTest(const FString&)
{
	const FHPCosmeticLoadout In = HPCustTest::Sample();

	// 1) Network: custom NetSerialize, compact payload.
	{
		FBitWriter W(0, true);
		FHPCosmeticLoadout Copy = In; bool bOk = false;
		Copy.NetSerialize(W, nullptr, bOk);
		TestTrue(TEXT("net write ok"), bOk);
		TestTrue(TEXT("payload <= 32 bytes"), W.GetNumBytes() <= 32);
		TestEqual(TEXT("payload is 12 bytes"), (int32)W.GetNumBytes(), 12);
		FBitReader R(W.GetData(), W.GetNumBits());
		FHPCosmeticLoadout Out; bool bOk2 = false;
		Out.NetSerialize(R, nullptr, bOk2);
		TestTrue(TEXT("net read ok"), bOk2);
		TestTrue(TEXT("net round trip equal"), Out == In);
	}
	// 2) Tagged property serialization (what USaveGame uses).
	{
		TArray<uint8> Bytes;
		FMemoryWriter MW(Bytes, true);
		FObjectAndNameAsStringProxyArchive WA(MW, false);
		FHPCosmeticLoadout Copy = In;
		FHPCosmeticLoadout::StaticStruct()->SerializeItem(WA, &Copy, nullptr);
		FMemoryReader MR(Bytes, true);
		FObjectAndNameAsStringProxyArchive RA(MR, false);
		FHPCosmeticLoadout Out;
		FHPCosmeticLoadout::StaticStruct()->SerializeItem(RA, &Out, nullptr);
		TestTrue(TEXT("tagged round trip equal"), Out == In);
	}
	// 3) SaveGame object through memory (same path as slot "HP_Loadout", without touching disk).
	{
		UHPLoadoutSaveGame* Save = NewObject<UHPLoadoutSaveGame>();
		Save->Loadout = In;
		TArray<uint8> Bytes;
		TestTrue(TEXT("save to memory"), UGameplayStatics::SaveGameToMemory(Save, Bytes));
		UHPLoadoutSaveGame* Loaded = Cast<UHPLoadoutSaveGame>(UGameplayStatics::LoadGameFromMemory(Bytes));
		TestTrue(TEXT("savegame round trip equal"), Loaded && Loaded->Loadout == In && Loaded->Version == UHPLoadoutSaveGame::CurrentVersion);
	}
	return true;
}

#endif
