// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/bridge-hazards.md rule 7. Players who enter drown (barn respawn + fee). Goods and
// carryables are pushed downstream and, after 15-25 s, delivered to the spit-out pile (marked Soaked).
// Critters swim to the nearest bank. Flow direction is the actor's +X.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HPRiverVolume.generated.h"

class UBoxComponent;

UCLASS()
class HARVESTPANIC_API AHPRiverVolume : public AActor
{
	GENERATED_BODY()

public:
	AHPRiverVolume();

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="River") TObjectPtr<UBoxComponent> Volume;

	/** World location of the spit-out pile. If unset, the downstream end of the volume is used. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="River") FVector PileLocation = FVector::ZeroVector;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="River") bool bHasPile = false;

	/** Current speed, cm/s (2.5 m/s). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="River|Tuning") float CurrentCmS = 250.f;
	/** Seconds before goods reach the pile (rand between). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="River|Tuning") float SpitOutMinSeconds = 15.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="River|Tuning") float SpitOutMaxSeconds = 25.f;
	/** Seconds a player may be in the water before drowning (a beat to scream). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="River|Tuning") float DrownDelaySeconds = 1.f;
	/** Bruise points of the Soaked hit goods take on entry. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="River|Tuning") float SoakedBruisePoints = 15.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="River|Tuning") float PileScatterCm = 150.f;

	/** Where goods are delivered. */
	FVector GetPileLocation() const;

	virtual void Tick(float DeltaSeconds) override;

private:
	struct FTrack
	{
		double EntryTime = 0.0;
		float Delay = 20.f;
	};
	void HandlePlayer(class AHPCharacter* Player, double Now);
	void HandleCritter(class ACharacter* Critter, double Now);
	void HandleBody(AActor* Body, double Now);
	void DeliverToPile(AActor* Body);

	TMap<TWeakObjectPtr<AActor>, FTrack> Tracks;
	TMap<TWeakObjectPtr<AActor>, double> PlayerEntry;
};
