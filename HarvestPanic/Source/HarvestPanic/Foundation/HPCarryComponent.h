// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/physics-carry.md (grab, carry, throw, tethers; server-authoritative,
//      Chaos Predictive Interpolation per session-networking). Stub only.
#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HPCarryComponent.generated.h"

UCLASS(ClassGroup=(HarvestPanic), meta=(BlueprintSpawnableComponent))
class HARVESTPANIC_API UHPCarryComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHPCarryComponent();
};
