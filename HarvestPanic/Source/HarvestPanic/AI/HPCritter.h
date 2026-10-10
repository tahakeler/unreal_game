// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/critter-ai.md (body side), livestock-roster.md (pig, chicken).
// The critter pawn: placeholder visuals + wobble, carry hooks, struggle/break-free.
// The brain lives in AHPCritterAIController. Server-authoritative; State replicates for visuals.
//
// Carry integration (physics-carry, owned by another system): the carry component should call
//   OnGrabbed(Carrier) when a player takes hold, OnReleased(Carrier) when that player lets go,
//   read GetCarryHands()/GetSecureCarriers(), and bind OnStruggle (apply impulse to holders)
//   and OnBreakFree (critter released itself; detach it). See also IsCarryable().
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "HPCritterTypes.h"
#include "HPCritter.generated.h"

class UHPCritterPersonality;
class UHPCritterMoverComponent;
class UStaticMeshComponent;
class UMaterialInterface;
class UStaticMesh;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_ThreeParams(FOnCritterStruggle, AHPCritter*, Critter, FVector, ImpulseDirection, float, ImpulseNs);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnCritterBreakFree, AHPCritter*, Critter);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_ThreeParams(FOnCritterStateChanged, AHPCritter*, Critter, ECritterState, OldState, ECritterState, NewState);

UCLASS(Abstract)
class HARVESTPANIC_API AHPCritter : public ACharacter
{
	GENERATED_BODY()

public:
	AHPCritter();

	// ---- Personality ------------------------------------------------------
	/** Asset override; if null the PersonalityClass CDO is used. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Critter")
	TObjectPtr<UHPCritterPersonality> PersonalityAsset;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Critter")
	TSubclassOf<UHPCritterPersonality> PersonalityClass;

	UFUNCTION(BlueprintPure, Category="Critter")
	const UHPCritterPersonality* GetPersonality() const;

	// ---- State --------------------------------------------------------------
	UFUNCTION(BlueprintPure, Category="Critter")
	ECritterState GetCritterState() const { return State; }

	/** Server. Logs every transition (rules/ai-code). */
	void SetCritterState(ECritterState NewState);

	UPROPERTY(BlueprintAssignable, Category="Critter")
	FOnCritterStateChanged OnStateChanged;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Critter")
	FVector HomeLocation = FVector::ZeroVector;

	// ---- Carry hooks (for UHPCarryComponent / IHPCarryable) -------------------
	/** A player took hold. First grab enters Carried and emits the grab noise (pig squeal). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Critter|Carry")
	void OnGrabbed(AActor* By);

	/** A holder let go (By == nullptr releases all). Last release -> Stunned, then Flee. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Critter|Carry")
	void OnReleased(AActor* By = nullptr);

	UFUNCTION(BlueprintPure, Category="Critter|Carry")
	int32 GetCarryHands() const;

	UFUNCTION(BlueprintPure, Category="Critter|Carry")
	int32 GetSecureCarriers() const;

	UFUNCTION(BlueprintPure, Category="Critter|Carry")
	int32 GetNumCarriers() const { return Carriers.Num(); }

	UFUNCTION(BlueprintPure, Category="Critter|Carry")
	bool IsCarried() const { return Carriers.Num() > 0; }

	UFUNCTION(BlueprintPure, Category="Critter|Carry")
	bool IsCarryable() const { return State != ECritterState::Escaped; }

	/** Each struggle interval: wriggle impulse to apply to holders. */
	UPROPERTY(BlueprintAssignable, Category="Critter|Carry")
	FOnCritterStruggle OnStruggle;

	/** Broke the grip; critter already released itself (carry system should detach). */
	UPROPERTY(BlueprintAssignable, Category="Critter|Carry")
	FOnCritterBreakFree OnBreakFree;

	UHPCritterMoverComponent* GetMover() const { return Mover; }

	virtual void Tick(float DeltaSeconds) override;
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

protected:
	virtual void BeginPlay() override;
	void ApplyBodyColor(UStaticMeshComponent* Comp, const FLinearColor& Color);
	void UpdateWobble(float DeltaSeconds);
	void TickStruggle(float DeltaSeconds);
	void EmitGrabNoise(AActor* By);

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Critter")
	TObjectPtr<UHPCritterMoverComponent> Mover;

	/** Placeholder body (engine BasicShapes). Swap Mesh/Material freely. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Critter|Visual")
	TObjectPtr<UStaticMeshComponent> Body;

	/** Snout / beak accent. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Critter|Visual")
	TObjectPtr<UStaticMeshComponent> Accent;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Critter|Visual")
	TObjectPtr<UStaticMeshComponent> Head;

	UPROPERTY(EditAnywhere, Category="Critter|Visual") FLinearColor BodyColor = FLinearColor::White;
	UPROPERTY(EditAnywhere, Category="Critter|Visual") FLinearColor AccentColor = FLinearColor::White;
	UPROPERTY(EditAnywhere, Category="Critter|Visual") float WobbleFrequency = 14.f;
	UPROPERTY(EditAnywhere, Category="Critter|Visual") float WobbleRollDeg = 8.f;
	UPROPERTY(EditAnywhere, Category="Critter|Visual") float WobbleBobCm = 6.f;

	UPROPERTY(Transient) TObjectPtr<UMaterialInterface> BaseMaterial;

	UPROPERTY(Replicated)
	ECritterState State = ECritterState::Idle;

	TArray<TWeakObjectPtr<AActor>> Carriers;
	float StruggleTimer = 1.f;
	float WobblePhase = 0.f;
	FVector BodyBaseLoc = FVector::ZeroVector;
	FRotator BodyBaseRot = FRotator::ZeroRotator;
};

UCLASS()
class HARVESTPANIC_API AHPCritter_Pig : public AHPCritter
{
	GENERATED_BODY()
public:
	AHPCritter_Pig();
};

UCLASS()
class HARVESTPANIC_API AHPCritter_Chicken : public AHPCritter
{
	GENERATED_BODY()
public:
	AHPCritter_Chicken();
};
