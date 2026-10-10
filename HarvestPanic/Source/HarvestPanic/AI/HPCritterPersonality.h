// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/critter-ai.md section 3.5 "Contract: Critter Personality".
// Values for pig/chicken: design/gdd/livestock-roster.md section 3.2.
// Every species is data: author a UHPCritterPersonality asset (or subclass with C++ defaults,
// as done for the VS pig and chicken below so no .uasset is required).
#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "GameplayTagContainer.h"
#include "HPCritterTypes.h"
#include "HPCritterLogic.h"
#include "HPNoiseSubsystem.h"
#include "HPCritterPersonality.generated.h"

class USoundBase;

USTRUCT(BlueprintType)
struct HARVESTPANIC_API FHPNoiseTagResponse
{
	GENERATED_BODY()

	FHPNoiseTagResponse() = default;
	FHPNoiseTagResponse(ECritterNoiseResponse InResponse, float InStrength) : Response(InResponse), Strength(InStrength) {}

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Critter") ECritterNoiseResponse Response = ECritterNoiseResponse::Ignore;
	/** 0..1 */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Critter", meta=(ClampMin=0, ClampMax=1)) float Strength = 0.f;
};

USTRUCT(BlueprintType)
struct HARVESTPANIC_API FHPGrumpyMods
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Critter") float FleeSpeedMult = 1.5f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Critter") float AggressionAdd = 0.3f;
};

/** Day Modifier hook (critter-ai 3.5): scales a numeric field by name. Applied mult then add. */
USTRUCT(BlueprintType)
struct HARVESTPANIC_API FCritterModifier
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Critter") FName FieldName;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Critter") float Mult = 1.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Critter") float Add = 0.f;
};

UCLASS(BlueprintType)
class HARVESTPANIC_API UHPCritterPersonality : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:
	UHPCritterPersonality();

	virtual FPrimaryAssetId GetPrimaryAssetId() const override;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Identity") FName Id;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Identity") FGameplayTagContainer Traits;

	/** cm/s */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Movement") float MoveSpeed = 300.f;
	/** cm/s */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Movement") float SprintSpeed = 550.f;
	/** cm/s^2 */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Movement") float Acceleration = 1500.f;
	/** m, wander around home */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Movement") float WanderRadius = 6.f;
	/** m, distance per flee leg */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Movement") float FleeDistance = 8.f;

	/** m */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Threat") float AlertRadius = 10.f;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Threat", meta=(ClampMin=0, ClampMax=1)) float AlertThreshold = 0.3f;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Threat", meta=(ClampMin=0, ClampMax=1)) float FleeThreshold = 0.55f;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Threat", meta=(ClampMin=0, ClampMax=1)) float CalmThreshold = 0.15f;
	/** s */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Threat") float CalmTime = 4.f;
	/** s, Alert "tell" before fleeing */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Threat") float AlertTellTime = 0.4f;
	/** cm/s; a threat moving faster than this counts as sprinting (player walk 400 / sprint 650). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Threat") float SprintDetectSpeed = 500.f;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Threat") FHPThreatWeights ThreatWeights;
	/** deg */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Threat") float SightCone = 220.f;

	/** 0..2 */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Noise", meta=(ClampMin=0, ClampMax=2)) float NoiseSensitivity = 1.f;
	/** Missing keys = 1.0 */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Noise") TMap<ENoiseSource, float> NoiseSourceMult;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Noise") TMap<FGameplayTag, FHPNoiseTagResponse> NoiseTagResponse;
	/** Noise emitted when grabbed (pig squeal = Critter 0.7, Noise.Squeal). 0 = silent. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Noise") float GrabNoiseLoudness = 0.f;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Noise") FGameplayTag GrabNoiseTag;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Lure") TMap<FGameplayTag, float> LureAffinity;
	/** s */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Lure") float EatTime = 3.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cornered") ECritterCorneredResponse CorneredResponse = ECritterCorneredResponse::Juke;
	/** N*s */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cornered") float ChargeImpulse = 0.f;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cornered") ECritterSizeClass SizeClass = ECritterSizeClass::Medium;
	/** 0..5 */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cornered", meta=(ClampMin=0, ClampMax=5)) float Agility = 2.f;
	/** N */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Carry") float CritterPullN = 300.f;
	/** Players holding it needed so it cannot break free (1..4). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Carry", meta=(ClampMin=1, ClampMax=4)) int32 SecureCarriers = 1;
	/** Lift hands (Goods Item CarryHands mirror until goods-value lands). carriers = hands/2. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Carry", meta=(ClampMin=1, ClampMax=8)) int32 CarryHands = 2;
	/** N*s */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Carry") float StruggleImpulse = 120.f;
	/** s, random in [Min, Max] */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Carry") float StruggleIntervalMin = 0.5f;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Carry") float StruggleIntervalMax = 1.5f;
	/** s, dizzy after being dropped/thrown */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Carry") float StunTime = 1.0f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Misc") FHPGrumpyMods GrumpyMods;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Misc", meta=(ClampMin=0, ClampMax=1)) float PenEscapeChance = 0.6f;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Group") ECritterHerdMode HerdMode = ECritterHerdMode::None;
	/** m; flock mates within this radius scatter together */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Group") float FlockRadius = 8.f;
	/** s */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Group") float FlockRegroupTime = 15.f;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Misc") FGameplayTagContainer ShortcutAccess;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Misc") ECritterActivePhase ActivePhase = ECritterActivePhase::Day;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Misc", meta=(ClampMin=0, ClampMax=1)) float Aggression = 0.f;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Misc") FGameplayTagContainer StealTargets;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Misc", meta=(ClampMin=0, ClampMax=1)) float Curiosity = 0.2f;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Misc") TSoftObjectPtr<USoundBase> BarkSet;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Misc") FGameplayTagContainer ComedyFlags;

	/** NoiseSourceMult lookup with 1.0 default. */
	float GetNoiseSourceMult(ENoiseSource Source) const;
	/** Highest response among the event's tags; false if none configured. */
	bool FindNoiseTagResponse(const FGameplayTagContainer& Tags, FGameplayTag& OutTag, FHPNoiseTagResponse& OutResponse) const;

	/** Built-in VS defaults (transient objects; no asset needed). */
	static UHPCritterPersonality* MakePig(UObject* Outer = GetTransientPackage());
	static UHPCritterPersonality* MakeChicken(UObject* Outer = GetTransientPackage());
	static void ApplyPigDefaults(UHPCritterPersonality& P);
	static void ApplyChickenDefaults(UHPCritterPersonality& P);
};

/** Pig "Hamlet" (livestock-roster 3.2). Class defaults carry the data. */
UCLASS()
class HARVESTPANIC_API UHPCritterPersonality_Pig : public UHPCritterPersonality
{
	GENERATED_BODY()
public:
	UHPCritterPersonality_Pig();
};

/** Chicken (livestock-roster 3.2). */
UCLASS()
class HARVESTPANIC_API UHPCritterPersonality_Chicken : public UHPCritterPersonality
{
	GENERATED_BODY()
public:
	UHPCritterPersonality_Chicken();
};
