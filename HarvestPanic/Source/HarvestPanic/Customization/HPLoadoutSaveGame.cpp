// Copyright HARVEST PANIC team. All Rights Reserved.

#include "HPLoadoutSaveGame.h"
#include "HPCosmeticCatalog.h"
#include "Kismet/GameplayStatics.h"

FHPCosmeticLoadout UHPLoadoutSaveGame::LoadOrCreateLocal(bool* bOutWasCreated)
{
	const UHPCosmeticCatalog* Cat = UHPCosmeticCatalog::Get();
	if (bOutWasCreated) { *bOutWasCreated = false; }

	if (UGameplayStatics::DoesSaveGameExist(SlotName, UserIndex))
	{
		if (UHPLoadoutSaveGame* Save = Cast<UHPLoadoutSaveGame>(UGameplayStatics::LoadGameFromSlot(SlotName, UserIndex)))
		{
			FHPCosmeticLoadout L = Save->Loadout;
			Cat->SanitizeLoadout(L);
			return L;
		}
	}

	FRandomStream Stream(static_cast<int32>(FDateTime::Now().GetTicks() & 0x7fffffff));
	const FHPCosmeticLoadout L = Cat->MakeRandomLoadout(Stream);
	SaveLocal(L);
	if (bOutWasCreated) { *bOutWasCreated = true; }
	return L;
}

bool UHPLoadoutSaveGame::SaveLocal(const FHPCosmeticLoadout& Loadout)
{
	UHPLoadoutSaveGame* Save = Cast<UHPLoadoutSaveGame>(UGameplayStatics::CreateSaveGameObject(UHPLoadoutSaveGame::StaticClass()));
	if (!Save) { return false; }
	Save->Loadout = Loadout;
	UHPCosmeticCatalog::Get()->SanitizeLoadout(Save->Loadout);
	return UGameplayStatics::SaveGameToSlot(Save, SlotName, UserIndex);
}
