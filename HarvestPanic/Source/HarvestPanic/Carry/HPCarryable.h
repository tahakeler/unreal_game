// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/physics-carry.md rule 2 (CarryHands, mass) -- minimal Goods-Item-like
// contract for anything UHPCarryComponent can grab. C++ only for the slice.
#pragma once

#include "CoreMinimal.h"
#include "UObject/Interface.h"
#include "HPCarryable.generated.h"

class UPrimitiveComponent;

UINTERFACE(MinimalAPI, meta=(CannotImplementInterfaceInBlueprint))
class UHPCarryable : public UInterface
{
	GENERATED_BODY()
};

class HARVESTPANIC_API IHPCarryable
{
	GENERATED_BODY()

public:
	/** Hands needed to lift (1, 2, 4, 6, 8). Each player supplies up to 2. */
	virtual int32 GetCarryHands() const = 0;
	/** Mass used by carry/throw formulas, kg. */
	virtual float GetCarryMassKg() const = 0;
	/** The simulating body that gets grabbed. */
	virtual UPrimitiveComponent* GetCarryPrimitive() const = 0;

	/** Server: a player attached / detached hands. Implementations track the total. */
	virtual void AddCarrierHands(AActor* Carrier, int32 Hands) = 0;
	virtual void RemoveCarrier(AActor* Carrier) = 0;
	/** Total hands currently attached (replicated by the implementation). */
	virtual int32 GetHandsAttached() const = 0;

	/** Server: touch history hook (grab, drop, throw). */
	virtual void NotifyTouched(AActor* Toucher, bool bThrown) = 0;
};
