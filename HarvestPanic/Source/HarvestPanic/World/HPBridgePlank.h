// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/bridge-hazards.md rules 2-5. One plank: integrity 100, load sampled every 0.2 s,
// damage above SafeLoad (250 kg), cracks at <= 50, breaks at <= 0 (falls), $30 patch / free morning repair.
// The plank wraps a level-placed mesh actor (BindToMeshActor) so the level agent only places meshes.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HPBridgePlank.generated.h"

class UBoxComponent;
class UStaticMeshComponent;

UENUM(BlueprintType)
enum class EHPPlankState : uint8 { Intact, Cracked, Broken };

UCLASS()
class HARVESTPANIC_API AHPBridgePlank : public AActor
{
	GENERATED_BODY()

public:
	AHPBridgePlank();

	/** Server: take over a level mesh actor (the visible plank). Call right after spawning. */
	void BindToMeshActor(AActor* MeshActor);

	/** Server: pay $30 to put the plank back (mid-day patch kit). Returns true if repaired. */
	bool ServerPatch(AActor* By);
	/** Server: free repair (morning). */
	void ServerRepair();

	UFUNCTION(BlueprintPure, Category="Plank") EHPPlankState GetPlankState() const { return PlankState; }
	UFUNCTION(BlueprintPure, Category="Plank") float GetIntegrity() const { return Integrity; }
	UFUNCTION(BlueprintPure, Category="Plank") float GetLastLoadKg() const { return LastLoadKg; }

	/** Server: apply damage directly (tests, impacts). Returns true if this broke the plank. */
	bool ServerApplyDamage(float Damage, AActor* Blamed);

	// ---- Tuning (bridge-hazards section 7) ----
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Plank|Tuning") float SafeLoadKg = 250.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Plank|Tuning") float DamageRate = 0.2f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Plank|Tuning") float CrackIntegrity = 50.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Plank|Tuning") int32 PatchCost = 30;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Plank|Tuning") float SampleInterval = 0.2f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Plank|Tuning") float PlayerMassKg = 70.f;
	/** Seconds the broken mesh keeps falling before it is hidden. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Plank|Tuning") float FallSeconds = 3.f;
	/** Noise loudness (proximity-voice). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Plank|Tuning") float CrackLoudness = 0.3f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Plank|Tuning") float BreakLoudness = 0.75f;

	virtual void Tick(float DeltaSeconds) override;
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

protected:
	virtual void BeginPlay() override;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Components") TObjectPtr<UBoxComponent> LoadSensor;

	UPROPERTY(ReplicatedUsing=OnRep_Source) TObjectPtr<AActor> SourceActor;
	UPROPERTY(ReplicatedUsing=OnRep_PlankState) EHPPlankState PlankState = EHPPlankState::Intact;
	UPROPERTY(Replicated) float Integrity = 100.f;

	UFUNCTION() void OnRep_Source();
	UFUNCTION() void OnRep_PlankState();

private:
	void CacheMesh();
	void ServerSample();
	float MeasureLoad(AActor*& OutHeaviestPlayer) const;
	void ApplyVisualState(bool bJustChanged);
	void EmitHazardNoise(float Loudness, FName TagName);

	UPROPERTY(Transient) TObjectPtr<UStaticMeshComponent> Mesh;
	FTransform MeshHomeTransform;
	bool bHaveHome = false;
	float SampleTimer = 0.f;
	float LastLoadKg = 0.f;
	float FallTime = -1.f;
	FVector FallVelocity = FVector::ZeroVector;
	int32 LastRepairDay = -1;
	EHPPlankState VisualState = EHPPlankState::Intact;
};
