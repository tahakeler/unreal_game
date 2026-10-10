// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/player-character.md (movement, Shout emote -> Noise Event 0.8, quiet-walk)
// Built on the template AHarvestPanicCharacter (camera + Enhanced Input). Stub only.
#pragma once

#include "CoreMinimal.h"
#include "HarvestPanicCharacter.h"
#include "HPCharacter.generated.h"

class UHPCarryComponent;

UCLASS()
class HARVESTPANIC_API AHPCharacter : public AHarvestPanicCharacter
{
	GENERATED_BODY()

public:
	AHPCharacter();

	UHPCarryComponent* GetCarryComponent() const { return CarryComponent; }

protected:
	/** physics-carry: grab / hold / throw. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Components")
	TObjectPtr<UHPCarryComponent> CarryComponent;
};
