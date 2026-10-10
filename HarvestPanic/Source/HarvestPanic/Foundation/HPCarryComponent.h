// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/physics-carry.md (grab, group carry, throw, drop, touch history).
// Server-authoritative: clients send intent RPCs; the server owns the physics handle,
// the held reference and the speed multiplier, which replicate to everyone.
// Not yet: tethers/ropes, per-hand slots (L/R), critter struggle, Chaos predictive interpolation.
#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HPCarryComponent.generated.h"

class UPhysicsHandleComponent;
class IHPCarryable;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FHPOnHeldChanged, AActor*, NewHeld);

UCLASS(ClassGroup=(HarvestPanic), meta=(BlueprintSpawnableComponent))
class HARVESTPANIC_API UHPCarryComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHPCarryComponent();

	// ---- Local-player intents (safe to call on owning client or server) ----
	UFUNCTION(BlueprintCallable, Category="Carry") void RequestGrabOrDrop();
	UFUNCTION(BlueprintCallable, Category="Carry") void BeginThrowCharge();
	UFUNCTION(BlueprintCallable, Category="Carry") void ReleaseThrow();
	/** Server: grab a specific carryable (used by the net tool). */
	bool ServerGrabActor(AActor* Target);
	/** Server: release whatever is held; bFling keeps the body's current velocity (ragdoll). */
	void ServerRelease(bool bThrown);

	UFUNCTION(BlueprintPure, Category="Carry") bool IsHolding() const { return HeldActor != nullptr; }
	UFUNCTION(BlueprintPure, Category="Carry") AActor* GetHeldActor() const { return HeldActor; }
	/** Replicated; applied to MaxWalkSpeed by AHPCharacter. */
	UFUNCTION(BlueprintPure, Category="Carry") float GetSpeedMultiplier() const { return SpeedMultiplier; }
	/** Replicated per-player load ratio 0..1 (stamina drain, ragdoll threshold). */
	UFUNCTION(BlueprintPure, Category="Carry") float GetLoadRatio() const { return LoadRatio; }
	/** Center-screen trace for a carryable (prompt + grab). Works on any machine. */
	AActor* TraceAimTarget(const FVector& ViewStart, const FVector& ViewDir) const;
	/** Uses the owner's own view point. */
	AActor* TraceAimTargetFromView() const;
	/** World hold point in front of the eyes (server drives the handle to it; FP arms reach to it). */
	FVector GetHoldLocation() const;
	/** Local: 0..1 while the throw button is held. */
	UFUNCTION(BlueprintPure, Category="Carry") float GetThrowCharge() const;

	UPROPERTY(BlueprintAssignable, Category="Carry") FHPOnHeldChanged OnHeldChanged;

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

protected:
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	// ---- Tuning (physics-carry Tuning Knobs) ----
	/** Grab reach beyond the character, cm (GDD 1.8 m). */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float GrabRangeCm = 180.f;
	/** Sphere radius of the grab sweep, cm. */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float GrabSweepRadiusCm = 30.f;
	/** Hands one player supplies. */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") int32 HandsPerPlayer = 2;
	/** kg one hand can carry before slowing (formula constant 25). */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float KgPerHand = 25.f;
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float SpeedFloor = 0.35f;
	/** Hold point relative to the eyes along the aim, cm (first-person). */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float HoldForwardCm = 110.f;
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float HoldUpCm = -25.f;
	/** Max pitch (deg) used for the hold point, so looking straight down doesn't bury the item. */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float HoldPitchLimit = 45.f;
	/** Server rejects client view starts further than this from the pawn's eyes, cm. */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float MaxViewStartErrorCm = 150.f;
	/** Spring-damped hands (wobbly on purpose). */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float HandStiffness = 1500.f;
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float HandDamping = 120.f;
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float HandInterpSpeed = 25.f;
	/** Break the grip if the body ends up this far from the hold point, cm. */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float SlipDistanceCm = 250.f;
	/** Throw (m/s): lerp(Min, Max, charge) * clamp(ThrowRefMassKg / mass, ThrowMassFactorMin, 1). */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float ThrowSpeedMin = 4.f;
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float ThrowSpeedMax = 14.f;
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float ThrowChargeSeconds = 1.f;
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float ThrowRefMassKg = 10.f;
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float ThrowMassFactorMin = 0.3f;
	/** Upward bias added to the throw direction. */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float ThrowLoftZ = 0.25f;
	/** A solo player may heave a multi-hand item only up to this mass (kg). */
	UPROPERTY(EditDefaultsOnly, Category="Carry|Tuning") float HeaveMaxMassKg = 40.f;

	// ---- Replicated state ----
	UPROPERTY(ReplicatedUsing=OnRep_HeldActor) TObjectPtr<AActor> HeldActor;
	UPROPERTY(Replicated) float SpeedMultiplier = 1.f;
	UPROPERTY(Replicated) float LoadRatio = 0.f;

	UFUNCTION() void OnRep_HeldActor();

	UFUNCTION(Server, Reliable) void Server_GrabOrDrop(FVector_NetQuantize ViewStart, FVector_NetQuantizeNormal ViewDir);
	UFUNCTION(Server, Reliable) void Server_Throw(float Charge, FVector_NetQuantizeNormal ViewDir);

private:
	void DoGrabOrDrop(const FVector& ViewStart, const FVector& ViewDir);
	void DoThrow(float Charge, const FVector& ViewDir);
	void GetView(FVector& OutStart, FVector& OutDir) const;
	void UpdateServerHold();
	IHPCarryable* GetHeldCarryable() const;

	UPROPERTY(Transient) TObjectPtr<UPhysicsHandleComponent> Handle;
	double ThrowChargeStart = -1.0;
};
