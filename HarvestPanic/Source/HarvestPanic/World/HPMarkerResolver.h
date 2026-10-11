// Copyright HARVEST PANIC team. All Rights Reserved.
// Marker contract (docs/engine/marker-contract.md): the level places plain actors carrying these
// actor Tags; on BeginPlay (server) this subsystem spawns the gameplay actor at the marker's transform.
// The level never references gameplay classes.
#pragma once

#include "CoreMinimal.h"
#include "Subsystems/WorldSubsystem.h"
#include "HPMarkerResolver.generated.h"

/** The marker kinds of the contract. */
enum class EHPMarkerKind : uint8
{
	Unknown, PlayerStart, MarketPen, CritterSpawnerPig, CritterSpawnerChicken, GoodsPrizePumpkin, GoodsCrate,
	BridgePlank, River, RiverPile, BarnRespawn
};

UCLASS()
class HARVESTPANIC_API UHPMarkerResolver : public UWorldSubsystem
{
	GENERATED_BODY()

public:
	virtual bool ShouldCreateSubsystem(UObject* Outer) const override;
	virtual void OnWorldBeginPlay(UWorld& InWorld) override;

	/**
	 * Parse "HP_Name" or "HP_Name:<count>". Returns the kind (Unknown if not a marker tag);
	 * OutCount is the integer after the colon, or DefaultCount when absent / invalid.
	 */
	static EHPMarkerKind ParseTag(FName Tag, int32& OutCount, int32 DefaultCount = 1);

	/** Server: scan the world and spawn gameplay actors for every unprocessed marker. Returns actors spawned. */
	int32 ResolveMarkers();

	/** Actors this resolver spawned (for tests / smoke). */
	const TArray<TWeakObjectPtr<AActor>>& GetSpawned() const { return Spawned; }

	/** Marker actors tagged HP_PlayerStart that are not APlayerStart (used by AHPGameMode). */
	static void CollectPlayerStartMarkers(const UWorld* World, TArray<AActor*>& OutMarkers);

private:
	/** Half-extent (cm) of the marker: its UBoxComponent if present, else 50 cm x actor scale. */
	static FVector MarkerHalfExtent(const AActor* Marker);

	void SpawnForMarker(AActor* Marker, EHPMarkerKind Kind, int32 Count);
	void SpawnCritterSpawner(AActor* Marker, UClass* CritterClass, int32 Count);

	TSet<TWeakObjectPtr<AActor>> Processed;
	TArray<TWeakObjectPtr<AActor>> Spawned;
	TArray<FVector> PileLocations;
	TArray<TWeakObjectPtr<class AHPRiverVolume>> Rivers;
};
