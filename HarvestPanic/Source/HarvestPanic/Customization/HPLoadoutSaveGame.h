// Copyright HARVEST PANIC team. All Rights Reserved.
// Local persistence of the player's last-used loadout (slot "HP_Loadout").
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/SaveGame.h"
#include "HPCosmeticTypes.h"
#include "HPLoadoutSaveGame.generated.h"

UCLASS()
class HARVESTPANIC_API UHPLoadoutSaveGame : public USaveGame
{
	GENERATED_BODY()

public:
	static constexpr const TCHAR* SlotName = TEXT("HP_Loadout");
	static constexpr int32 UserIndex = 0;
	static constexpr int32 CurrentVersion = 1;

	UPROPERTY(SaveGame)
	int32 Version = CurrentVersion;

	UPROPERTY(SaveGame)
	FHPCosmeticLoadout Loadout;

	/**
	 * Load the saved loadout (sanitized). First-time players get a random silly
	 * default, which is saved immediately so it stays stable across launches.
	 */
	static FHPCosmeticLoadout LoadOrCreateLocal(bool* bOutWasCreated = nullptr);

	static bool SaveLocal(const FHPCosmeticLoadout& Loadout);
};
