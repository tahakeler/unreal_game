// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/character-customization.md (replication, emotes 5-8, Faint 2 s / 5 s cooldown).

#include "HPCosmeticsComponent.h"
#include "HPCosmeticCatalog.h"
#include "HPCosmeticApplier.h"
#include "HPLoadoutSaveGame.h"
#include "HarvestPanic.h"
#include "GameFramework/PlayerState.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/Pawn.h"
#include "Camera/PlayerCameraManager.h"
#include "Components/SkeletalMeshComponent.h"
#include "Components/TextRenderComponent.h"
#include "Animation/AnimSequenceBase.h"
#include "Animation/AnimSequence.h"
#include "Animation/AnimInstance.h"
#include "Animation/AnimSingleNodeInstance.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "Engine/Engine.h"
#include "UObject/UnrealType.h"

FHPOnEmotePlayed UHPCosmeticsComponent::OnAnyEmotePlayed;

// ------------------------------------------------------------------ emote playback

void FHPEmotePlayback::Start(USkeletalMeshComponent* InBody, EHPEmote InEmote, bool bSkipAnim)
{
	Stop();
	const FHPEmoteDef* Def = UHPCosmeticCatalog::Get()->GetEmote(InEmote);
	if (!InBody || !Def) { return; }

	Body = InBody;
	Emote = InEmote;
	Elapsed = 0.f;
	Duration = FMath::Max(0.1f, Def->Duration);
	RestoreRelative = InBody->GetRelativeTransform();
	RestoreAnimClass = InBody->GetAnimClass();

	if (bSkipAnim) { return; }
	if (UAnimSequenceBase* Anim = Def->Anim.LoadSynchronous())
	{
		// Template death/attack anims carry root translation; lock it so the body stays in its capsule.
		if (UAnimSequence* Seq = Cast<UAnimSequence>(Anim))
		{
			Seq->bEnableRootMotion = true;
			Seq->RootMotionRootLock = ERootMotionRootLock::RefPose;
		}
		// Single-node playback works with any anim blueprint (no slot needed); the ABP is restored after.
		InBody->PlayAnimation(Anim, Def->bLoopAnim && !Def->bHoldLastFrame);
		InBody->SetPlayRate(Def->PlayRate);
	}
	// No anim available -> procedural layers below still sell the emote.
}

bool FHPEmotePlayback::Tick(float Dt)
{
	if (!IsActive()) { return false; }
	USkeletalMeshComponent* B = Body.Get();
	const FHPEmoteDef* Def = UHPCosmeticCatalog::Get()->GetEmote(Emote);
	if (!B || !Def) { Stop(); return false; }

	Elapsed += Dt;
	if (Elapsed >= Duration) { Stop(); return false; }

	const float Phase = 2.f * PI * Def->BobFrequency * Elapsed;
	// Ease the spin in/out so Dance does not snap back to the start yaw.
	const float SpinYaw = Def->SpinDegPerSec * Elapsed;
	const float Wobble = Def->WobbleDegrees * FMath::Sin(Phase);
	const float Bob = Def->BobAmplitude * FMath::Abs(FMath::Sin(Phase));
	if (Def->SpinDegPerSec != 0.f || Def->WobbleDegrees != 0.f || Def->BobAmplitude != 0.f)
	{
		FTransform T = RestoreRelative;
		T.SetRotation(FQuat(FRotator(0.f, SpinYaw, Wobble)) * RestoreRelative.GetRotation());
		T.AddToTranslation(FVector(0.f, 0.f, Bob));
		B->SetRelativeTransform(T);
	}
	return true;
}

void FHPEmotePlayback::Stop()
{
	if (USkeletalMeshComponent* B = Body.Get())
	{
		B->SetRelativeTransform(RestoreRelative);
		if (B->GetAnimationMode() == EAnimationMode::AnimationSingleNode && RestoreAnimClass)
		{
			B->SetAnimationMode(EAnimationMode::AnimationBlueprint);
			B->SetAnimInstanceClass(RestoreAnimClass);
		}
	}
	Body.Reset();
	Emote = EHPEmote::MAX;
}

// ------------------------------------------------------------------ component

UHPCosmeticsComponent::UHPCosmeticsComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
	PrimaryComponentTick.TickInterval = 0.f;
	SetIsReplicatedByDefault(true);
}

UHPCosmeticsComponent* UHPCosmeticsComponent::FindForPlayerState(const APlayerState* PS)
{
	return PS ? PS->FindComponentByClass<UHPCosmeticsComponent>() : nullptr;
}

UHPCosmeticsComponent* UHPCosmeticsComponent::FindForPawn(const APawn* Pawn)
{
	return Pawn ? FindForPlayerState(Pawn->GetPlayerState()) : nullptr;
}

UHPCosmeticsComponent* UHPCosmeticsComponent::EnsureOnPlayerState(APlayerState* PS)
{
	if (!PS || !PS->HasAuthority() || PS->IsActorBeingDestroyed()) { return nullptr; }
	if (UHPCosmeticsComponent* Existing = FindForPlayerState(PS)) { return Existing; }
	UHPCosmeticsComponent* Comp = NewObject<UHPCosmeticsComponent>(PS, TEXT("HPCosmetics"));
	Comp->SetIsReplicated(true);
	PS->AddInstanceComponent(Comp);
	Comp->RegisterComponent();
	UE_LOG(LogHarvestPanic, Log, TEXT("[Customization] added cosmetics component to %s"), *PS->GetPlayerName());
	return Comp;
}

void UHPCosmeticsComponent::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHPCosmeticsComponent, Loadout);
}

APawn* UHPCosmeticsComponent::GetOwnedPawn() const
{
	const APlayerState* PS = Cast<APlayerState>(GetOwner());
	return PS ? PS->GetPawn() : nullptr;
}

bool UHPCosmeticsComponent::IsOwnedByLocalPlayer() const
{
	const APlayerState* PS = Cast<APlayerState>(GetOwner());
	const APlayerController* PC = PS ? PS->GetPlayerController() : nullptr;
	return PC && PC->IsLocalController();
}

void UHPCosmeticsComponent::BeginPlay()
{
	Super::BeginPlay();
	if (APlayerState* PS = Cast<APlayerState>(GetOwner()))
	{
		PS->OnPawnSet.AddUniqueDynamic(this, &UHPCosmeticsComponent::HandlePawnSet);
	}
	if (GetOwner()->HasAuthority())
	{
		// Until the owner sends theirs, show a valid random farmer instead of a naked default.
		FRandomStream Stream(GetTypeHash(GetOwner()->GetName()));
		Loadout = UHPCosmeticCatalog::Get()->MakeRandomLoadout(Stream);
	}
	ApplyToCurrentPawn();
}

void UHPCosmeticsComponent::EndPlay(const EEndPlayReason::Type Reason)
{
	Playback.Stop();
	if (APlayerState* PS = Cast<APlayerState>(GetOwner()))
	{
		PS->OnPawnSet.RemoveDynamic(this, &UHPCosmeticsComponent::HandlePawnSet);
	}
	Super::EndPlay(Reason);
}

void UHPCosmeticsComponent::HandlePawnSet(APlayerState* Player, APawn* NewPawn, APawn* OldPawn)
{
	Playback.Stop();
	ApplyToCurrentPawn();
}

void UHPCosmeticsComponent::ApplyToCurrentPawn()
{
	if (GetNetMode() == NM_DedicatedServer) { return; } // purely visual
	APawn* Pawn = GetOwnedPawn();
	AppliedPawn = Pawn;
	if (!Pawn) { return; }
	FHPCosmeticApplier::ApplyToPawn(Pawn, Loadout);
	LastTagName.Reset();
	UpdateNameTag(Pawn);
}

void UHPCosmeticsComponent::UpdateNameTag(APawn* Pawn)
{
	const APlayerState* PS = Cast<APlayerState>(GetOwner());
	if (!Pawn || !PS) { return; }
	const UHPCosmeticCatalog* Cat = UHPCosmeticCatalog::Get();
	const FString Name = PS->GetPlayerName();
	if (!NameTag.IsValid() || NameTag->GetOwner() != Pawn || Name != LastTagName)
	{
		NameTag = FHPCosmeticApplier::EnsureNameTag(Pawn, Name, Cat->GetPaletteColor(Loadout.NameTagColor), Cat->NameTagHeight, Cat->NameTagWorldSize);
		LastTagName = Name;
	}
}

void UHPCosmeticsComponent::OnRep_Loadout()
{
	ApplyToCurrentPawn();
	OnLoadoutChanged.Broadcast(Loadout);
}

void UHPCosmeticsComponent::SetLoadoutAuthority(FHPCosmeticLoadout NewLoadout)
{
	UHPCosmeticCatalog::Get()->SanitizeLoadout(NewLoadout); // server owns validation (unlocks, ranges)
	Loadout = NewLoadout;
	ApplyToCurrentPawn(); // listen host does not get OnRep
	OnLoadoutChanged.Broadcast(Loadout);
	GetOwner()->ForceNetUpdate();
}

void UHPCosmeticsComponent::RequestSetLoadout(const FHPCosmeticLoadout& NewLoadout)
{
	if (GetOwner()->HasAuthority())
	{
		SetLoadoutAuthority(NewLoadout);
	}
	else
	{
		Server_SetLoadout(NewLoadout);
	}
}

void UHPCosmeticsComponent::Server_SetLoadout_Implementation(const FHPCosmeticLoadout& NewLoadout)
{
	const double Now = GetWorld()->GetTimeSeconds();
	if (Now - LastLoadoutChangeTime < UHPCosmeticCatalog::Get()->LoadoutChangeMinInterval)
	{
		return; // spam guard; the client re-sends on the next Confirm
	}
	LastLoadoutChangeTime = Now;
	SetLoadoutAuthority(NewLoadout);
}

float UHPCosmeticsComponent::GetCooldownRemaining(EHPEmote Emote) const
{
	const int32 Idx = static_cast<int32>(Emote);
	if (Idx < 0 || Idx >= static_cast<int32>(EHPEmote::MAX) || !GetWorld()) { return 0.f; }
	return FMath::Max(0.f, static_cast<float>(EmoteReadyAt[Idx] - GetWorld()->GetTimeSeconds()));
}

void UHPCosmeticsComponent::RequestEmote(EHPEmote Emote)
{
	if (GetCooldownRemaining(Emote) > 0.f) { return; } // client-side guard; server re-checks
	Server_PlayEmote(Emote); // on the listen host this runs locally
}

bool UHPCosmeticsComponent::TryPawnRagdoll(APawn* Pawn, float Seconds) const
{
	// Decoupled: use the character's ragdoll if it exposes one (see docs/customization.md).
	static const FName Candidates[] = { TEXT("HPEmoteFaint"), TEXT("StartEmoteRagdoll"), TEXT("StartRagdoll") };
	for (const FName& Name : Candidates)
	{
		UFunction* Fn = Pawn ? Pawn->FindFunction(Name) : nullptr;
		if (!Fn) { continue; }
		TArray<uint8, TInlineAllocator<64>> Params;
		Params.SetNumZeroed(FMath::Max<int32>(Fn->ParmsSize, 8));
		for (TFieldIterator<FProperty> It(Fn); It && It->HasAnyPropertyFlags(CPF_Parm); ++It)
		{
			if (FFloatProperty* F = CastField<FFloatProperty>(*It))
			{
				F->SetPropertyValue_InContainer(Params.GetData(), Seconds);
				break;
			}
		}
		Pawn->ProcessEvent(Fn, Params.GetData());
		return true;
	}
	return false;
}

void UHPCosmeticsComponent::Server_PlayEmote_Implementation(EHPEmote Emote)
{
	const FHPEmoteDef* Def = UHPCosmeticCatalog::Get()->GetEmote(Emote);
	APawn* Pawn = GetOwnedPawn();
	if (!Def || !Pawn) { return; }

	const double Now = GetWorld()->GetTimeSeconds();
	const int32 Idx = static_cast<int32>(Emote);
	if (Now - LastEmoteTime < UHPCosmeticCatalog::Get()->EmoteMinInterval || Now < EmoteReadyAt[Idx])
	{
		return;
	}
	LastEmoteTime = Now;
	EmoteReadyAt[Idx] = Now + Def->Cooldown;

	const bool bRagdoll = Def->bRagdoll && TryPawnRagdoll(Pawn, Def->Duration);
	OnAnyEmotePlayed.Broadcast(Pawn, Emote); // TODO(integration): Wave noise, Faint drops carried item
	if (Def->bRagdoll)
	{
		Multicast_PlayEmoteReliable(Emote, bRagdoll);
	}
	else
	{
		Multicast_PlayEmote(Emote, bRagdoll);
	}
}

void UHPCosmeticsComponent::Multicast_PlayEmote_Implementation(EHPEmote Emote, bool bRagdollHandled)
{
	PlayEmoteLocal(Emote, bRagdollHandled);
}

void UHPCosmeticsComponent::Multicast_PlayEmoteReliable_Implementation(EHPEmote Emote, bool bRagdollHandled)
{
	PlayEmoteLocal(Emote, bRagdollHandled);
}

void UHPCosmeticsComponent::PlayEmoteLocal(EHPEmote Emote, bool bRagdollHandled)
{
	const FHPEmoteDef* Def = UHPCosmeticCatalog::Get()->GetEmote(Emote);
	if (Def && GetWorld())
	{
		const int32 Idx = static_cast<int32>(Emote);
		EmoteReadyAt[Idx] = FMath::Max(EmoteReadyAt[Idx], GetWorld()->GetTimeSeconds() + Def->Cooldown);
	}
	if (GetNetMode() == NM_DedicatedServer) { return; }
	Playback.Start(FHPCosmeticApplier::FindBody(GetOwnedPawn()), Emote, bRagdollHandled);
}

void UHPCosmeticsComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
	if (GetNetMode() == NM_DedicatedServer) { return; }

	// Owner pushes its saved loadout once (listen host or remote client).
	if (!bSentInitialLoadout && IsOwnedByLocalPlayer())
	{
		bSentInitialLoadout = true;
		RequestSetLoadout(UHPLoadoutSaveGame::LoadOrCreateLocal());
	}

	Playback.Tick(DeltaTime);

	// Cheap safety net: respawns / pawns that rebuild their mesh after possession.
	ReapplyTimer -= DeltaTime;
	if (ReapplyTimer <= 0.f)
	{
		ReapplyTimer = 0.5f;
		APawn* Pawn = GetOwnedPawn();
		if (Pawn != AppliedPawn.Get())
		{
			ApplyToCurrentPawn();
		}
		else if (Pawn)
		{
			UpdateNameTag(Pawn);
		}
	}

	if (UTextRenderComponent* Tag = NameTag.Get())
	{
		if (const APlayerController* PC = GEngine ? GEngine->GetFirstLocalPlayerController(GetWorld()) : nullptr)
		{
			if (PC->PlayerCameraManager)
			{
				FHPCosmeticApplier::FaceNameTag(Tag, PC->PlayerCameraManager->GetCameraLocation());
			}
		}
	}
}
