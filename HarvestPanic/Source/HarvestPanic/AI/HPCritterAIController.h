// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/critter-ai.md 3.1-3.4, 4, 5. The critter brain: a compact 10 Hz state machine
// (Idle/Wander, Alert, Flee, Lured, Cornered, Carried, Stunned, Calm). All tuning comes from the
// UHPCritterPersonality. Debug draw: hp.CritterDebug 1.
// NOTE: GDD names a StateTree (ST_Critter). This C++ machine is the VS stand-in; its states map 1:1
// and the pure decision math lives in FHPCritterLogic so a StateTree can reuse it.
#pragma once

#include "CoreMinimal.h"
#include "AIController.h"
#include "HPCritterTypes.h"
#include "HPCritterAIController.generated.h"

class AHPCritter;
class UHPCritterPersonality;
struct FNoiseEvent;

USTRUCT()
struct FHPNoiseThreat
{
	GENERATED_BODY()

	FVector Location = FVector::ZeroVector;
	TWeakObjectPtr<AActor> Instigator;
	float Value = 0.f;
};

UCLASS()
class HARVESTPANIC_API AHPCritterAIController : public AAIController
{
	GENERATED_BODY()

public:
	AHPCritterAIController();

	virtual void Tick(float DeltaSeconds) override;

	/** Flock scatter from a mate: skip the Alert tell and flee radially from FlockCenter. */
	void Scatter(const FVector& FlockCenter, const FVector& ThreatLocation);

	/** Called by the critter when released/thrown. */
	void EnterStunned(float Seconds);

	/** Damage threat term D (0.6, decays 0.3/s). */
	UFUNCTION(BlueprintCallable, Category="Critter")
	void NotifyDamaged(AActor* Source);

	/** Place a lure directly (e.g. feed bucket item without a noise). */
	UFUNCTION(BlueprintCallable, Category="Critter")
	void SetLure(const FVector& Location, float Affinity);

	UFUNCTION(BlueprintPure, Category="Critter")
	float GetCurrentThreat() const { return CurrentThreat; }

protected:
	virtual void OnPossess(APawn* InPawn) override;
	virtual void OnUnPossess() override;

	void HandleNoise(const FNoiseEvent& Event);
	void SetBrainState(ECritterState NewState);

	/** Gathers sources, fills ThreatLocs/Weights and returns total threat. */
	float EvaluateThreat(float Dt);
	bool PickFleePoint(bool& bOutCornered);
	bool PickWanderPoint();
	void TickCornered();
	void StartCornered();
	void TriggerFlockScatter();
	int32 CountThreatsWithin(float RadiusCm) const;
	void DrawDebug() const;

	AHPCritter* Critter() const;
	const UHPCritterPersonality* Personality() const;
	double Now() const;

	FDelegateHandle NoiseHandle;
	TArray<FHPNoiseThreat> NoiseThreats;
	TArray<FVector> ThreatLocs;
	TArray<float> ThreatWeights;
	TArray<FVector> PlayerLocs;
	float CurrentThreat = 0.f;
	float DamageThreat = 0.f;
	TWeakObjectPtr<AActor> DamageSource;

	bool bLureActive = false;
	FVector LureLocation = FVector::ZeroVector;
	float LureAffinityValue = 0.f;
	double LureExpireTime = 0.0;
	float EatTimer = 0.f;

	double StateEnterTime = 0.0;
	double NextFleeQueryTime = 0.0;
	double StateEndTime = 0.0;
	float LowThreatTime = 0.f;
	float IdleWait = 1.f;
	FVector FleeGoal = FVector::ZeroVector;
	bool bScatterPending = false;
	FVector ScatterDir = FVector::ZeroVector;
	float AccumDt = 0.f;
};
