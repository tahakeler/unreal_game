// Copyright HARVEST PANIC team. All Rights Reserved.
#include "Economy/HPGoodsComponent.h"
#include "Economy/HPEconomyMath.h"
#include "HPGameState.h"
#include "GameFramework/Pawn.h"
#include "GameFramework/Controller.h"
#include "GameFramework/PlayerState.h"
#include "Engine/World.h"
#include "Net/UnrealNetwork.h"

UHPGoodsComponent::UHPGoodsComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
	PrimaryComponentTick.TickInterval = 1.f; // freshness only needs 1 Hz
	SetIsReplicatedByDefault(true);
}

void UHPGoodsComponent::BeginPlay()
{
	Super::BeginPlay();
	if (bUsePresetForGoodsId)
	{
		ApplyPreset(GoodsId);
	}
	SetComponentTickEnabled(GetOwner() && GetOwner()->HasAuthority());
}

void UHPGoodsComponent::ApplyPreset(FName InGoodsId)
{
	if (const FHPGoodsPreset* P = UHPEconomyMath::FindGoodsPreset(InGoodsId))
	{
		GoodsId = P->GoodsId;
		Category = P->Category;
		BaseValue = P->BaseValue;
		Fragility = P->Fragility;
		CarryHands = P->CarryHands;
		bIsAlive = Category == EHPGoodsCategory::Livestock;
	}
}

void UHPGoodsComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
	if (FreshnessAge < 0.f)
	{
		return;
	}
	const AHPGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHPGameState>() : nullptr;
	if (!GS || GS->Phase != EHPDayPhase::Dusk) // night pauses decay
	{
		FreshnessAge += DeltaTime;
	}
	float Grace, Decay;
	if (UHPEconomyMath::GetFreshnessParams(Category, GoodsId, Grace, Decay))
	{
		Freshness = UHPEconomyMath::Freshness(FreshnessAge, Grace, Decay);
	}
}

void UHPGoodsComponent::StartFreshnessClock()
{
	if (GetOwner() && GetOwner()->HasAuthority() && FreshnessAge < 0.f)
	{
		FreshnessAge = 0.f;
		FreshnessClockStart = GetWorld() ? GetWorld()->GetTimeSeconds() : 0.f;
	}
}

int32 UHPGoodsComponent::ResolvePlayerId(const AActor* Actor)
{
	if (!Actor)
	{
		return HP_NO_PLAYER;
	}
	if (const APlayerState* PS = Cast<APlayerState>(Actor))
	{
		return PS->GetPlayerId();
	}
	if (const APawn* Pawn = Cast<APawn>(Actor))
	{
		return Pawn->GetPlayerState() ? Pawn->GetPlayerState()->GetPlayerId() : HP_NO_PLAYER;
	}
	if (const AController* C = Cast<AController>(Actor))
	{
		return C->PlayerState ? C->PlayerState->GetPlayerId() : HP_NO_PLAYER;
	}
	return ResolvePlayerId(Actor->GetInstigator());
}

void UHPGoodsComponent::RecordTouch(AActor* Toucher)
{
	const int32 Id = ResolvePlayerId(Toucher);
	if (Id == HP_NO_PLAYER || !GetWorld())
	{
		return;
	}
	TouchHistory.RemoveAll([Id](const FHPTouchRecord& R) { return R.PlayerId == Id; });
	TouchHistory.Insert({ Id, static_cast<float>(GetWorld()->GetTimeSeconds()) }, 0);
	TouchHistory.SetNum(FMath::Min(TouchHistory.Num(), 3));
	LastHandlerId = Id;
}

TArray<int32> UHPGoodsComponent::GetRecentTouchers(float Now) const
{
	TArray<int32> Out;
	for (const FHPTouchRecord& R : TouchHistory)
	{
		if (Now - R.ServerTime <= TouchWindowSeconds)
		{
			Out.Add(R.PlayerId);
		}
	}
	return Out;
}

float UHPGoodsComponent::ApplyBruiseHit(float Impulse, AActor* Instigator, EHPBlameVerb Verb)
{
	const float BruiseMult = 1.f; // Day Modifier Goods.BruiseMult hook (UDayModifierSubsystem not in slice)
	return ApplyBruisePoints(UHPEconomyMath::ComputeBruiseDelta(Impulse, Fragility, BruiseMult), Instigator, Verb);
}

float UHPGoodsComponent::ApplyBruisePoints(float DeltaBP, AActor* Instigator, EHPBlameVerb Verb)
{
	if (!GetOwner() || !GetOwner()->HasAuthority() || bSold || DeltaBP <= 0.f)
	{
		return 0.f;
	}
	const EHPBruiseState OldState = BruiseState;
	const float BOld = UHPEconomyMath::BruiseMultiplier(OldState, Category);

	BruisePoints = FMath::Clamp(BruisePoints + DeltaBP, 0.f, 100.f);
	BruiseState = UHPEconomyMath::BruiseStateFromPoints(BruisePoints);
	const float BNew = UHPEconomyMath::BruiseMultiplier(BruiseState, Category);

	AHPGameState* GS = GetWorld()->GetGameState<AHPGameState>();
	const float ExpectedD = GS ? GS->GetDemandFor(GoodsId) : 1.f;
	const int32 DeltaDollars = UHPEconomyMath::BruiseDollarLoss(BaseValue, ExpectedD, BOld, BNew);

	// Attribution: explicit instigator first, then touch history within 3 s; none → Nature.
	const float Now = GetWorld()->GetTimeSeconds();
	TArray<int32> Touchers = GetRecentTouchers(Now);
	const int32 InstId = ResolvePlayerId(Instigator);
	if (InstId != HP_NO_PLAYER)
	{
		Touchers.Remove(InstId);
		Touchers.Insert(InstId, 0);
		Touchers.SetNum(FMath::Min(Touchers.Num(), 3));
	}

	OnBruised.Broadcast(this, DeltaBP, DeltaDollars, Touchers.Num() ? Touchers[0] : HP_NO_PLAYER);
	if (OldState != BruiseState)
	{
		OnStateChanged.Broadcast(this, OldState, BruiseState);
		if (BruiseState == EHPBruiseState::Ruined && Category == EHPGoodsCategory::Livestock)
		{
			Tags.AddTag(FGameplayTag::RequestGameplayTag(TEXT("Goods.Grumpy"), false));
		}
	}
	if (GS && DeltaDollars >= 1)
	{
		GS->EmitBlame(Verb, GoodsId, -DeltaDollars, Touchers, GetOwner()->GetActorLocation());
	}
	return DeltaBP;
}

int32 UHPGoodsComponent::GetCurrentSaleValue(bool bFillsOrder) const
{
	const AHPGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHPGameState>() : nullptr;
	const float D = GS ? GS->GetDemandFor(GoodsId) : 1.f;
	const float PhaseMult = GS ? GS->GetPhaseValueMult() : 1.f;
	return UHPEconomyMath::FinalSaleValue(BaseValue, BruiseState, Category, Freshness, D,
		bFillsOrder ? UHPEconomyMath::OrderBonusMult : 1.f, ValueModifierMult * PhaseMult);
}

void UHPGoodsComponent::NotifySold(int32 FinalValue)
{
	bSold = true;
	OnSold.Broadcast(this, FinalValue);
}

void UHPGoodsComponent::OnRep_BruiseState(EHPBruiseState OldState)
{
	OnStateChanged.Broadcast(this, OldState, BruiseState);
}

void UHPGoodsComponent::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHPGoodsComponent, GoodsId);
	DOREPLIFETIME(UHPGoodsComponent, Category);
	DOREPLIFETIME(UHPGoodsComponent, BaseValue);
	DOREPLIFETIME(UHPGoodsComponent, BruisePoints);
	DOREPLIFETIME(UHPGoodsComponent, BruiseState);
	DOREPLIFETIME(UHPGoodsComponent, Freshness);
	DOREPLIFETIME(UHPGoodsComponent, bIsAlive);
	DOREPLIFETIME(UHPGoodsComponent, LastHandlerId);
	DOREPLIFETIME(UHPGoodsComponent, Tags);
	DOREPLIFETIME(UHPGoodsComponent, ValueModifierMult);
}
