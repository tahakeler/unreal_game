// Copyright HARVEST PANIC team. All Rights Reserved.
// Movement layer abstraction for critters. The brain only calls MoveTo/Stop/IsValidDestination.
// Today: engine navmesh (UNavigationSystemV1) when nav data exists, else direct steering with
// ground/obstacle traces. Swap point for the AdaptivePathfindingSystem plugin (critter-ai 1):
// subclass and override the virtuals, then set AHPCritter::MoverClass.
#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HPCritterMoverComponent.generated.h"

UCLASS(ClassGroup=(HarvestPanic), meta=(BlueprintSpawnableComponent))
class HARVESTPANIC_API UHPCritterMoverComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHPCritterMoverComponent();

	/** Start moving to Goal at SpeedCmS. Returns false if no route. */
	UFUNCTION(BlueprintCallable, Category="Critter|Move")
	virtual bool MoveTo(const FVector& Goal, float SpeedCmS, float AcceptanceCm = 60.f);

	UFUNCTION(BlueprintCallable, Category="Critter|Move")
	virtual void Stop();

	UFUNCTION(BlueprintPure, Category="Critter|Move")
	bool IsMoving() const { return bMoving; }

	/** Can a critter stand at / reach Point? Projects to navmesh if available (OutPoint = projected). */
	virtual bool IsValidDestination(const FVector& From, const FVector& Point, FVector& OutPoint) const;

	/** Route cost in cm (path length; straight distance in direct mode). <0 if unreachable. */
	virtual float RouteCost(const FVector& From, const FVector& To) const;

	bool HasNavData() const;

	FVector GetGoal() const { return Goal; }
	const TArray<FVector>& GetPathPoints() const { return PathPoints; }
	/** Seconds without meaningful progress while moving. */
	float GetStuckTime() const { return StuckTime; }

	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

protected:
	TArray<FVector> PathPoints;
	int32 PathIndex = 0;
	FVector Goal = FVector::ZeroVector;
	float Acceptance = 60.f;
	float Speed = 300.f;
	bool bMoving = false;
	float StuckTime = 0.f;
	FVector LastPos = FVector::ZeroVector;
};
