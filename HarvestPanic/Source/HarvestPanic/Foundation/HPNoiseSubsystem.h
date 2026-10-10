// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/proximity-voice.md -- owns the "Noise Event" contract.
// Rules: created only on the server via Emit(); delivered via AI Perception hearing
// and the OnNoise multicast. Radius = 4 + 46 * Loudness^1.5 m (* env multipliers).
// Stub only: rate limiting, env_mult and perception reporting are not implemented yet.
#pragma once

#include "CoreMinimal.h"
#include "GameplayTagContainer.h"
#include "Subsystems/WorldSubsystem.h"
#include "HPNoiseSubsystem.generated.h"

class APlayerState;

UENUM(BlueprintType)
enum class ENoiseSource : uint8
{
	Voice, Shout, Footstep, Impact, Tool, Hazard, Critter, Crop, Vehicle, Scripted
};

USTRUCT(BlueprintType)
struct HARVESTPANIC_API FNoiseEvent
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Noise") FGuid EventId;
	UPROPERTY(BlueprintReadWrite, Category="Noise") ENoiseSource Source = ENoiseSource::Scripted;
	/** 0..1 normalized. */
	UPROPERTY(BlueprintReadWrite, Category="Noise") float Loudness = 0.f;
	/** Metres; derived by the subsystem from Loudness. */
	UPROPERTY(BlueprintReadOnly, Category="Noise") float Radius = 0.f;
	UPROPERTY(BlueprintReadWrite, Category="Noise") FVector Location = FVector::ZeroVector;
	UPROPERTY(BlueprintReadWrite, Category="Noise") TObjectPtr<AActor> Instigator = nullptr;
	UPROPERTY(BlueprintReadWrite, Category="Noise") TObjectPtr<APlayerState> BlamedPlayer = nullptr;
	UPROPERTY(BlueprintReadWrite, Category="Noise") FGameplayTagContainer Tags;
	/** Seconds, server clock. */
	UPROPERTY(BlueprintReadOnly, Category="Noise") double ServerTime = 0.0;
};

DECLARE_MULTICAST_DELEGATE_OneParam(FOnNoiseEvent, const FNoiseEvent&);

UCLASS()
class HARVESTPANIC_API UHPNoiseSubsystem : public UWorldSubsystem
{
	GENERATED_BODY()

public:
	/** Server only. Fills EventId/Radius/ServerTime and broadcasts OnNoise. */
	void Emit(FNoiseEvent Event);

	/** Contract rule 3 (without env_mult). */
	static float RadiusForLoudness(float Loudness);

	/** For non-AI consumers (blame-ledger, camcorder-replay, ...). */
	FOnNoiseEvent OnNoise;
};
