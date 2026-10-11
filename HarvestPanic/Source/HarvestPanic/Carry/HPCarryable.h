// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/physics-carry.md rule 2 (CarryHands, mass) -- minimal Goods-Item-like
// contract for anything UHPCarryComponent can grab. C++ only for the slice.
#pragma once

#include "CoreMinimal.h"
#include "UObject/Interface.h"
#include "Components/PrimitiveComponent.h"
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

	/**
	 * True for bodies that are not physics-simulated (characters such as critters). The carry
	 * component then moves the actor to the hold point itself instead of using a physics handle.
	 */
	virtual bool IsKinematicCarry() const { return false; }

	/** Server: launch the body after a throw. Default sets the simulating primitive's velocity (cm/s). */
	virtual void ApplyThrowVelocity(const FVector& VelocityCmS)
	{
		if (UPrimitiveComponent* Prim = GetCarryPrimitive())
		{
			Prim->SetPhysicsLinearVelocity(VelocityCmS);
		}
	}
};
