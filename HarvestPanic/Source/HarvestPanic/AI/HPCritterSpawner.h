// Copyright HARVEST PANIC team. All Rights Reserved.
// Spawns N critters of a class in a radius (livestock-roster 3.3 drives counts later via day-loop).
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HPCritterSpawner.generated.h"

class AHPCritter;

UCLASS()
class HARVESTPANIC_API AHPCritterSpawner : public AActor
{
	GENERATED_BODY()

public:
	AHPCritterSpawner();

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Spawner", meta=(ClampMin=0, ClampMax=40))
	int32 Count = 4;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Spawner")
	TSubclassOf<AHPCritter> CritterClass;

	/** cm */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Spawner")
	float Radius = 500.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Spawner")
	bool bSpawnOnBeginPlay = true;

	/** Server only. Returns spawned critters. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Spawner")
	TArray<AHPCritter*> SpawnCritters();

	/** Helper used by the spawner and the hp.SpawnCritters console command. */
	static TArray<AHPCritter*> SpawnAround(UWorld* World, TSubclassOf<AHPCritter> Class, int32 Num, const FVector& Center, float RadiusCm);

protected:
	virtual void BeginPlay() override;
};
