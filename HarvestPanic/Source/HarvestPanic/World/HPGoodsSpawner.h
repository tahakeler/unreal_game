// Copyright HARVEST PANIC team. All Rights Reserved.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "World/HPGoodsProp.h"
#include "HPGoodsSpawner.generated.h"

/** Spawns Prize Pumpkins (4 hands) or crates at its location. */
UCLASS()
class HARVESTPANIC_API AHPGoodsSpawner : public AActor
{
	GENERATED_BODY()

public:
	AHPGoodsSpawner();

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Spawner") EHPGoodsKind Kind = EHPGoodsKind::Crate;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Spawner", meta=(ClampMin=0, ClampMax=20)) int32 Count = 1;
	/** cm; items are spread over a ring when Count > 1. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Spawner") float SpreadCm = 120.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Spawner") bool bSpawnOnBeginPlay = true;

	/** Server: spawn the goods. Returns the spawned props. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Spawner")
	TArray<AHPGoodsProp*> SpawnGoods();

protected:
	virtual void BeginPlay() override;
};
