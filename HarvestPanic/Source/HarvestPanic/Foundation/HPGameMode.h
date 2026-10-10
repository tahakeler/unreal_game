// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/session-networking.md (listen-server authority, max 4 players, join/drop)
// Stub only; no gameplay logic yet.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "HPGameMode.generated.h"

UCLASS()
class HARVESTPANIC_API AHPGameMode : public AGameModeBase
{
	GENERATED_BODY()

public:
	AHPGameMode();

	/** session-networking Tuning Knob: MaxPlayers (2-4). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Session", meta=(ClampMin=1, ClampMax=4))
	int32 MaxPlayers = 4;
};
