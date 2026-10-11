// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/player-character.md (movement, stamina, ragdoll/KO/revive, shout -> Noise 0.8),
//      physics-carry.md (input for grab/throw), farm-tools.md (net + feed bucket only).
// Built on the template AHarvestPanicCharacter (camera boom). Input actions and the mapping
// context are created at runtime in C++ so no .uasset is required.
//
// Body state machine (server-authoritative, replicated):
//   Normal  --impulse >= threshold-------------------> Ragdoll
//   Normal/Ragdoll --impulse >= KOMult*threshold
//                    or RagdollsForKO ragdolls in RagdollKOWindow --> KnockedOut
//   Ragdoll --settled for GetUpSettleSeconds + Jump---> Normal
//   KnockedOut --KODuration elapsed or revive hold---> Normal
#pragma once

#include "CoreMinimal.h"
#include "HarvestPanicCharacter.h"
#include "HPCharacter.generated.h"

class UHPCarryComponent;
class UInputAction;
class UInputMappingContext;
class UCameraComponent;
class UPoseableMeshComponent;
class UCanvas;
class UMeshComponent;
class USkeletalMeshComponent;
enum class EHPBlameVerb : uint8;
class APlayerController;

UENUM(BlueprintType)
enum class EHPBodyState : uint8 { Normal, Ragdoll, KnockedOut };

UENUM(BlueprintType)
enum class EHPTool : uint8 { None, Net, FeedBucket };

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FHPOnPawnReady, AHPCharacter*, Pawn);

UCLASS()
class HARVESTPANIC_API AHPCharacter : public AHarvestPanicCharacter
{
	GENERATED_BODY()

public:
	AHPCharacter();

	/**
	 * Cosmetics hook (customization finds these via reflection). Full-body third-person mannequin
	 * (what other players see). Never null after construction.
	 */
	UFUNCTION(BlueprintPure, Category="Cosmetics") USkeletalMeshComponent* GetBodyMesh3P() const;
	/**
	 * Owner-only first-person arms. Returned as UMeshComponent (base of both) because the arms are a procedurally
	 * posed UPoseableMeshComponent (not a USkeletalMeshComponent); SetSkinnedAssetAndUpdate / SetMaterial work on it.
	 */
	UFUNCTION(BlueprintPure, Category="Cosmetics") UMeshComponent* GetArmsMesh1P() const;
	/** True once BeginPlay finished and OnPawnReady fired (late binders should check this first). */
	UFUNCTION(BlueprintPure, Category="Cosmetics") bool IsPawnReady() const { return bPawnReady; }
	/** Fired on every machine at the end of BeginPlay, when body and arms meshes exist. */
	UPROPERTY(BlueprintAssignable, Category="Cosmetics") FHPOnPawnReady OnPawnReady;

	/**
	 * Server: charge the barn respawn fee (optional), drop what is held, get up and move to the barn.
	 * Used by drowning and the end of a knockout.
	 */
	void ServerRespawnAtBarn(EHPBlameVerb Verb, bool bChargeFee);

	/**
	 * Emote hook (customization, found via reflection): server-side real ragdoll for Seconds, dropping
	 * anything held, then recover through the normal get-up path. Ignored on clients / when already down.
	 */
	UFUNCTION() void HPEmoteFaint(float Seconds);

	/** Debug / input hook: patch the nearest broken bridge plank for $30. */
	UFUNCTION(Exec) void HPPatch();

	UHPCarryComponent* GetCarryComponent() const { return CarryComponent; }

	/** Server: an impact (N*s) hit this player. Applies the ragdoll / KO rules. */
	void ReceiveImpactImpulse(float ImpulseNs, int32 InstigatorPlayerId);

	UFUNCTION(BlueprintPure, Category="Character") EHPBodyState GetBodyState() const { return BodyState; }
	UFUNCTION(BlueprintPure, Category="Character") float GetStamina() const { return Stamina; }
	UFUNCTION(BlueprintPure, Category="Character") float GetCurrentRagdollThreshold() const;

	/** Server: revive from KO (teammate hold completed or timer). */
	void ServerRecover();

	/** Debug: console `HPImpact 1000` applies an impact to the local player (server-side). */
	UFUNCTION(Exec) void HPImpact(float ImpulseNs);
	/** Debug: console `HPGrab` = press grab (E). */
	UFUNCTION(Exec) void HPGrab();
	/** Debug: log FP arm state. */
	UFUNCTION(Exec) void HPDumpArms();
	/** Debug/QA: set view pitch/yaw (deterministic screenshots). */
	UFUNCTION(Exec) void HPSetView(float Pitch, float Yaw);
	/** Debug/QA: `HPRunLater 5 Shot showui` runs a console command after N seconds (screenshots). */
	UFUNCTION(Exec) void HPRunLater(float Seconds, const FString& Command);

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void Tick(float DeltaSeconds) override;
	/** Look input scaled by the player's sensitivity / invert-Y setting. */
	virtual void DoLook(float Yaw, float Pitch) override;
	/** Pull sensitivity, invert Y and FOV from UHPGameUserSettings (frontend). */
	void ApplyUserSettings(const class UHPGameUserSettings& Settings);

protected:
	virtual void BeginPlay() override;
	virtual void SetupPlayerInputComponent(UInputComponent* PlayerInputComponent) override;
	virtual void NotifyControllerChanged() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;

	// ---- First-person presentation (local player only; others see the full body) ----
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Components")
	TObjectPtr<UCameraComponent> FirstPersonCamera;
	/** Mannequin copy posed procedurally so only the arms reach into view (OnlyOwnerSee). */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Components")
	TObjectPtr<UPoseableMeshComponent> FirstPersonArms;

	UPROPERTY(EditDefaultsOnly, Category="Tuning|Camera") float EyeHeightCm = 64.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Camera") float BaseFOV = 90.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Camera") float SprintFOVKick = 8.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Camera") float FOVInterpSpeed = 8.f;
	/** Head bob amplitude (cm) at walk speed and cycle frequency (Hz). */
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Camera") float HeadBobAmplitude = 1.6f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Camera") float HeadBobFrequency = 1.8f;
	/** Impact shake: degrees at trauma 1, decay per second; impulse (N*s) that gives trauma 1. */
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Camera") float ShakeMaxDegrees = 4.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Camera") float ShakeDecayPerSec = 2.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Camera") float ShakeFullImpulseNs = 900.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Camera") FName HeadSocket = TEXT("head");
	/** Arms mesh offset from the camera (chest behind the near plane). */
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Camera") FVector ArmsOffset = FVector(-8.f, 0.f, -146.f);
	/** Idle hand point relative to the camera (x fwd, y right, z up), cm. */
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Camera") FVector IdleHandOffset = FVector(60.f, 20.f, -22.f);
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Camera") float ArmInterpSpeed = 12.f;

	/** physics-carry: grab / hold / throw. */
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Components")
	TObjectPtr<UHPCarryComponent> CarryComponent;

	// ---- Movement (cm/s; GDD 4.0 / 6.5 / 2.0 m/s) ----
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Movement") float WalkSpeed = 400.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Movement") float SprintSpeed = 650.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Movement") float CrouchSpeed = 200.f;
	/** Jump apex ~1.1 m with default gravity. */
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Movement") float JumpZVelocity = 465.f;

	// ---- Stamina ----
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Stamina") float StaminaMax = 100.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Stamina") float SprintDrainPerSec = 15.f;
	/** carry_load_drain = CarryDrainPerSec * load_ratio. */
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Stamina") float CarryDrainPerSec = 8.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Stamina") float StaminaRegenPerSec = 25.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Stamina") float StaminaRegenDelay = 1.f;
	/** After hitting 0, sprint is blocked until this much stamina. */
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Stamina") float SprintResumeStamina = 30.f;

	// ---- Ragdoll / KO ----
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Ragdoll") float RagdollImpulseNs = 900.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Ragdoll") float RagdollLoadFactor = 0.4f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Ragdoll") float TiredStamina = 20.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Ragdoll") float TiredThresholdMult = 0.7f;
	/** KO when impulse >= KOImpulseMult * threshold. */
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Ragdoll") float KOImpulseMult = 2.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Ragdoll") int32 RagdollsForKO = 3;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Ragdoll") float RagdollKOWindow = 20.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Ragdoll") float KODuration = 8.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Ragdoll") float ReviveHoldSeconds = 1.5f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Ragdoll") float ReviveRangeCm = 200.f;
	/** Can get up once pelvis speed < this (cm/s) for GetUpSettleSeconds. */
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Ragdoll") float GetUpMaxSpeed = 50.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Ragdoll") float GetUpSettleSeconds = 0.5f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Ragdoll") FName PelvisBone = TEXT("pelvis");

	// ---- Noise ----
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Noise") float HollerLoudness = 0.8f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Noise") float HollerCooldown = 2.f;

	// ---- Tools (farm-tools: net + feed bucket) ----
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Tools") float NetReachCm = 150.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Tools") float NetArcDegrees = 90.f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Tools") float NetCooldown = 1.f;
	/** Only carryables needing at most this many hands fit in the net. */
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Tools") int32 NetMaxCarryHands = 1;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Tools") float FeedLoudness = 0.4f;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Tools") int32 FeedUsesPerBucket = 10;
	UPROPERTY(EditDefaultsOnly, Category="Tuning|Tools") float FeedCooldown = 0.25f;

	// ---- Replicated state ----
	UPROPERTY(ReplicatedUsing=OnRep_BodyState, BlueprintReadOnly, Category="Character") EHPBodyState BodyState = EHPBodyState::Normal;
	UPROPERTY(Replicated, BlueprintReadOnly, Category="Character") float Stamina = 100.f;
	UPROPERTY(Replicated, BlueprintReadOnly, Category="Character") bool bWantsSprint = false;
	UPROPERTY(Replicated, BlueprintReadOnly, Category="Character") bool bSprintExhausted = false;
	UPROPERTY(Replicated, BlueprintReadOnly, Category="Tools") EHPTool ActiveTool = EHPTool::Net;
	UPROPERTY(Replicated, BlueprintReadOnly, Category="Tools") int32 FeedUsesLeft = 10;

	UFUNCTION() void OnRep_BodyState();

	UFUNCTION(Server, Reliable) void Server_PatchPlank();
	UFUNCTION(Server, Reliable) void Server_SetSprint(bool bSprint);
	UFUNCTION(Server, Reliable) void Server_Holler();
	UFUNCTION(Server, Reliable) void Server_UseTool();
	UFUNCTION(Server, Reliable) void Server_SelectTool(EHPTool Tool);
	UFUNCTION(Server, Reliable) void Server_GetUp();
	UFUNCTION(Server, Reliable) void Server_BeginRevive(AHPCharacter* Target);
	UFUNCTION(Server, Reliable) void Server_CancelRevive();
	UFUNCTION(Server, Reliable) void Server_DebugImpact(float ImpulseNs);
	UFUNCTION(Client, Unreliable) void Client_ImpactShake(float Trauma01);

private:
	void BuildInput();
	void ApplyBodyState();
	void SetBodyState(EHPBodyState NewState);
	void TickStamina(float DeltaSeconds);
	void TickBodyState(float DeltaSeconds);
	void UpdateMovementSpeed();
	AHPCharacter* FindReviveTarget() const;

	// Input handlers
	void OnSprintStarted();
	void OnSprintCompleted();
	void OnCrouchToggle();
	void OnGrabStarted();
	void OnGrabCompleted();
	void OnThrowStarted();
	void OnThrowCompleted();
	void OnHoller();
	void OnUseTool();
	void OnSelectNet();
	void OnSelectFeed();
	void OnJumpStarted();

	void TickFirstPerson(float DeltaSeconds);
	void PoseArm(FName UpperArm, FName LowerArm, const FVector& WorldTarget, float Side);
	void DrawCrosshair(UCanvas* Canvas, APlayerController* PC);
	FDelegateHandle DebugDrawHandle;
	float ShakeTrauma = 0.f;
	float BobPhase = 0.f;
	FVector LeftHandTarget = FVector::ZeroVector;
	FVector RightHandTarget = FVector::ZeroVector;

	UPROPERTY(Transient) TObjectPtr<UInputMappingContext> RuntimeIMC;
	UPROPERTY(Transient) TObjectPtr<UInputAction> IA_Move;
	UPROPERTY(Transient) TObjectPtr<UInputAction> IA_Look;
	UPROPERTY(Transient) TObjectPtr<UInputAction> IA_Jump;
	UPROPERTY(Transient) TObjectPtr<UInputAction> IA_Sprint;
	UPROPERTY(Transient) TObjectPtr<UInputAction> IA_Crouch;
	UPROPERTY(Transient) TObjectPtr<UInputAction> IA_Grab;
	UPROPERTY(Transient) TObjectPtr<UInputAction> IA_Throw;
	UPROPERTY(Transient) TObjectPtr<UInputAction> IA_Holler;
	UPROPERTY(Transient) TObjectPtr<UInputAction> IA_Tool;
	UPROPERTY(Transient) TObjectPtr<UInputAction> IA_ToolNet;
	UPROPERTY(Transient) TObjectPtr<UInputAction> IA_ToolFeed;

	bool bPawnReady = false;
	float LookSensitivity = 1.f;
	bool bInvertLookY = false;
	FDelegateHandle SettingsHandle;

	// Server-side bookkeeping
	TArray<double> RecentRagdollTimes;
	double BodyStateStartTime = 0.0;
	double LastSprintTime = -1000.0;
	double LastHollerTime = -1000.0;
	double LastToolTime = -1000.0;
	float SettledSeconds = 0.f;
	TWeakObjectPtr<AHPCharacter> ReviveTarget;
	double ReviveStartTime = 0.0;
	FTransform MeshRelativeTransform;
};
