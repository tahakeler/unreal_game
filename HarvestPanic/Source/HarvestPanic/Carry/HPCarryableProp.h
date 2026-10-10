// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/physics-carry.md (carryable body, bruise reporting rule 8, touch history),
//      proximity-voice.md (impact Noise Events), player-character.md (impact -> ragdoll).
// Test prop for the vertical slice: an engine BasicShapes mesh with configurable hands/mass.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "Carry/HPCarryable.h"
#include "Carry/HPCarryMath.h"
#include "HPCarryableProp.generated.h"

class UStaticMeshComponent;

UENUM(BlueprintType)
enum class EHPPropShape : uint8 { Cube, Sphere };

/** Payload for a bruise hit (goods-value consumes it). Touchers newest first. */
USTRUCT(BlueprintType)
struct HARVESTPANIC_API FHPBruiseHit
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Carry") TObjectPtr<AActor> Item = nullptr;
	/** N*s */
	UPROPERTY(BlueprintReadOnly, Category="Carry") float ImpulseNs = 0.f;
	UPROPERTY(BlueprintReadOnly, Category="Carry") FVector Location = FVector::ZeroVector;
	/** APlayerState PlayerIds, newest first (index 0 = LastHandlerId). Empty = Nature. */
	UPROPERTY(BlueprintReadOnly, Category="Carry") TArray<int32> TouchHistoryPlayerIds;
	/** Blame shares parallel to TouchHistoryPlayerIds, sums to 1. */
	UPROPERTY(BlueprintReadOnly, Category="Carry") TArray<float> BlameShares;
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FHPOnBruiseHit, const FHPBruiseHit&, Hit);

UCLASS()
class HARVESTPANIC_API AHPCarryableProp : public AActor, public IHPCarryable
{
	GENERATED_BODY()

public:
	AHPCarryableProp();

	// IHPCarryable
	virtual int32 GetCarryHands() const override { return CarryHands; }
	virtual float GetCarryMassKg() const override { return MassKg; }
	virtual UPrimitiveComponent* GetCarryPrimitive() const override;
	virtual void AddCarrierHands(AActor* Carrier, int32 Hands) override;
	virtual void RemoveCarrier(AActor* Carrier) override;
	virtual int32 GetHandsAttached() const override { return HandsAttached; }
	virtual void NotifyTouched(AActor* Toucher, bool bThrown) override;

	/** Server only. Fires for every impact above BruiseImpactMinNs on a held/thrown/recently touched body. */
	UPROPERTY(BlueprintAssignable, Category="Carry")
	FHPOnBruiseHit OnBruiseHit;

	/** Server: current touch history (blame). */
	const FHPTouchHistory& GetTouchHistory() const { return TouchHistory; }

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void OnConstruction(const FTransform& Transform) override;
	virtual void NotifyHit(UPrimitiveComponent* MyComp, AActor* Other, UPrimitiveComponent* OtherComp, bool bSelfMoved,
		FVector HitLocation, FVector HitNormal, FVector NormalImpulse, const FHitResult& Hit) override;

protected:
	virtual void BeginPlay() override;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Components")
	TObjectPtr<UStaticMeshComponent> Mesh;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Carry")
	EHPPropShape Shape = EHPPropShape::Cube;

	/** Hands needed to lift (1, 2, 4, 6, 8). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Carry", meta=(ClampMin=1, ClampMax=8))
	int32 CarryHands = 2;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Carry", meta=(ClampMin=0.1))
	float MassKg = 10.f;

	/** physics-carry BruiseImpactMin (mirror of goods-value I_min). */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning")
	float BruiseImpactMinNs = 400.f;

	/** proximity-voice: > this is a heavy impact (loudness HeavyImpactLoudness), else light. */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning")
	float HeavyImpactNs = 800.f;
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning")
	float LightImpactLoudness = 0.3f;
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning")
	float HeavyImpactLoudness = 0.6f;

	/** blame-ledger TouchWindow (s) and shares. */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning")
	float TouchWindowSeconds = 3.f;
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning")
	TArray<float> TouchShares = {0.6f, 0.3f, 0.1f};

	/** Thrown state lasts until this long after release (GDD: contact or 2 s). */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning")
	float ThrownStateSeconds = 2.f;
	/** Ignore hits on the thrower for this long after release. */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning")
	float ThrowerGraceSeconds = 0.5f;
	/** Minimum time between two impact reports. */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning")
	float ImpactCooldownSeconds = 0.15f;

	UPROPERTY(ReplicatedUsing=OnRep_HandsAttached, BlueprintReadOnly, Category="Carry")
	int32 HandsAttached = 0;

	UFUNCTION()
	void OnRep_HandsAttached();

private:
	void ApplyShape();
	void UpdatePawnCollision();

	/** Server: carrier -> hands. */
	TMap<TWeakObjectPtr<AActor>, int32> Carriers;
	FHPTouchHistory TouchHistory;
	TWeakObjectPtr<AActor> LastThrower;
	double LastThrowTime = -1000.0;
	double LastTouchTime = -1000.0;
	double LastImpactTime = -1000.0;
};
