// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/character-customization.md
// Replicated per-player cosmetics. Lives on the APlayerState (added at runtime by
// UHPCustomizationSubsystem on the server, so PlayerState code is untouched), which
// makes it survive respawns and reach late joiners for free.
#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "HPCosmeticTypes.h"
#include "HPCosmeticsComponent.generated.h"

class APlayerState;
class APawn;
class USkeletalMeshComponent;
class UAnimInstance;
class UTextRenderComponent;

/** Local playback state of a running emote on one body mesh (cosmetic only). */
struct HARVESTPANIC_API FHPEmotePlayback
{
	TWeakObjectPtr<USkeletalMeshComponent> Body;
	TSubclassOf<UAnimInstance> RestoreAnimClass;
	FTransform RestoreRelative;
	EHPEmote Emote = EHPEmote::MAX;
	float Elapsed = 0.f;
	float Duration = 0.f;

	bool IsActive() const { return Emote != EHPEmote::MAX; }
	/** Start (stops any running emote first). bSkipAnim = ragdoll owned by the pawn. */
	void Start(USkeletalMeshComponent* InBody, EHPEmote InEmote, bool bSkipAnim);
	/** Advance procedural layers; returns false once finished (and restored). */
	bool Tick(float DeltaSeconds);
	void Stop();
};

DECLARE_MULTICAST_DELEGATE_OneParam(FHPOnLoadoutChanged, const FHPCosmeticLoadout&);
/** Server-side gameplay hook (integration TODO: Wave -> noise event, Faint -> drop carried item). */
DECLARE_MULTICAST_DELEGATE_TwoParams(FHPOnEmotePlayed, APawn* /*Pawn*/, EHPEmote /*Emote*/);

UCLASS(ClassGroup=(HarvestPanic), meta=(BlueprintSpawnableComponent))
class HARVESTPANIC_API UHPCosmeticsComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHPCosmeticsComponent();

	static UHPCosmeticsComponent* FindForPlayerState(const APlayerState* PS);
	static UHPCosmeticsComponent* FindForPawn(const APawn* Pawn);
	/** Server: add the component if missing. */
	static UHPCosmeticsComponent* EnsureOnPlayerState(APlayerState* PS);

	const FHPCosmeticLoadout& GetLoadout() const { return Loadout; }

	/** Owning client or listen host: send a new loadout (validated on the server). */
	UFUNCTION(BlueprintCallable, Category="Cosmetics")
	void RequestSetLoadout(const FHPCosmeticLoadout& NewLoadout);

	/** Owning client or listen host: play an emote (cooldown + rate limit on the server). */
	UFUNCTION(BlueprintCallable, Category="Cosmetics")
	void RequestEmote(EHPEmote Emote);

	/** Re-apply to whatever pawn is currently owned (respawn, mesh swap by other code). */
	void ApplyToCurrentPawn();

	bool IsEmoting() const { return Playback.IsActive(); }
	float GetCooldownRemaining(EHPEmote Emote) const;

	FHPOnLoadoutChanged OnLoadoutChanged;
	static FHPOnEmotePlayed OnAnyEmotePlayed;

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;
	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;

protected:
	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type Reason) override;

	UFUNCTION(Server, Reliable)
	void Server_SetLoadout(const FHPCosmeticLoadout& NewLoadout);

	UFUNCTION(Server, Reliable)
	void Server_PlayEmote(EHPEmote Emote);

	UFUNCTION(NetMulticast, Unreliable)
	void Multicast_PlayEmote(EHPEmote Emote, bool bRagdollHandled);

	/** Faint changes physics state, so it is reliable (GDD). */
	UFUNCTION(NetMulticast, Reliable)
	void Multicast_PlayEmoteReliable(EHPEmote Emote, bool bRagdollHandled);

	UFUNCTION()
	void OnRep_Loadout();

	UFUNCTION()
	void HandlePawnSet(APlayerState* Player, APawn* NewPawn, APawn* OldPawn);

	UPROPERTY(ReplicatedUsing=OnRep_Loadout)
	FHPCosmeticLoadout Loadout;

private:
	void SetLoadoutAuthority(FHPCosmeticLoadout NewLoadout);
	void PlayEmoteLocal(EHPEmote Emote, bool bRagdollHandled);
	bool TryPawnRagdoll(APawn* Pawn, float Seconds) const;
	bool IsOwnedByLocalPlayer() const;
	APawn* GetOwnedPawn() const;
	void UpdateNameTag(APawn* Pawn);

	FHPEmotePlayback Playback;
	TWeakObjectPtr<APawn> AppliedPawn;
	TWeakObjectPtr<UTextRenderComponent> NameTag;
	FString LastTagName;
	double LastLoadoutChangeTime = -1000.0;
	double LastEmoteTime = -1000.0;
	double EmoteReadyAt[static_cast<int32>(EHPEmote::MAX)] = {};
	float ReapplyTimer = 0.f;
	bool bSentInitialLoadout = false;
};
