// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/player-character.md (barn respawn) / bridge-hazards.md (players respawn at the barn).
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HPBarnRespawn.generated.h"

class UArrowComponent;

/** Where drowned / knocked-out players reappear. Several slots are spread on a ring around the actor. */
UCLASS()
class HARVESTPANIC_API AHPBarnRespawn : public AActor
{
	GENERATED_BODY()

public:
	AHPBarnRespawn();

	/** Ring radius (cm) the player slots are spread on. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Barn") float SlotRadiusCm = 120.f;

	/** Transform for the Nth player (wraps at 4). Z is lifted by the capsule half height. */
	UFUNCTION(BlueprintPure, Category="Barn")
	FTransform GetRespawnTransform(int32 SlotIndex) const;

	/** The barn respawn point of a world (first found), or null. */
	static AHPBarnRespawn* Find(const UWorld* World);

private:
	UPROPERTY(VisibleAnywhere) TObjectPtr<UArrowComponent> Arrow;
};
