// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPGameState.h"
#include "Economy/HPEconomyMath.h"
#include "Economy/HPGoodsComponent.h"
#include "GameFramework/PlayerState.h"
#include "GameFramework/Pawn.h"
#include "Engine/World.h"
#include "Net/UnrealNetwork.h"

AHPGameState::AHPGameState()
{
	WalletBalance = UHPEconomyMath::StartingBalance;
}

void AHPGameState::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHPGameState, WalletBalance);
	DOREPLIFETIME(AHPGameState, WeekIOU);
	DOREPLIFETIME(AHPGameState, Phase);
	DOREPLIFETIME(AHPGameState, PhaseStartTime);
	DOREPLIFETIME(AHPGameState, PhaseEndTime);
	DOREPLIFETIME(AHPGameState, DayIndex);
	DOREPLIFETIME(AHPGameState, DayOfWeek);
	DOREPLIFETIME(AHPGameState, WeekIndex);
	DOREPLIFETIME(AHPGameState, WeeklyQuota);
	DOREPLIFETIME(AHPGameState, QuotaPlayerCount);
	DOREPLIFETIME(AHPGameState, OrderLines);
	DOREPLIFETIME(AHPGameState, Demand);
	DOREPLIFETIME(AHPGameState, DaySales);
	DOREPLIFETIME(AHPGameState, DayItemsSold);
	DOREPLIFETIME(AHPGameState, DayCosts);
	DOREPLIFETIME(AHPGameState, DayBlameEvents);
	DOREPLIFETIME(AHPGameState, LastSummary);
}

// ---------------- Queries ----------------

float AHPGameState::GetPhaseSecondsRemaining() const
{
	if (PhaseEndTime <= PhaseStartTime)
	{
		return 0.f;
	}
	return FMath::Max(0.f, PhaseEndTime - static_cast<float>(GetServerWorldTimeSeconds()));
}

bool AHPGameState::IsMarketOpen() const
{
	return Phase == EHPDayPhase::Workday || Phase == EHPDayPhase::Dusk;
}

float AHPGameState::GetPhaseValueMult() const
{
	return Phase == EHPDayPhase::Dusk ? 1.25f : 1.f; // Day.DuskBonus base
}

float AHPGameState::GetDemandFor(FName GoodsId) const
{
	const FHPDemandEntry* E = Demand.FindByPredicate([&](const FHPDemandEntry& D) { return D.GoodsId == GoodsId; });
	const float DayD = E ? E->DayDemand : 1.f;
	const int32 Sold = E ? E->SoldToday : 0;
	return UHPEconomyMath::Demand(DayD, Sold + 1, 1.f /* Market.DemandMult */);
}

float AHPGameState::GetDayTime() const
{
	return GetWorld() ? static_cast<float>(GetWorld()->GetTimeSeconds()) - DayStartTime : 0.f;
}

FString AHPGameState::GetPlayerNameById(int32 PlayerId) const
{
	if (PlayerId == HP_NO_PLAYER)
	{
		return TEXT("Nature");
	}
	for (const APlayerState* PS : PlayerArray)
	{
		if (PS && PS->GetPlayerId() == PlayerId)
		{
			return PS->GetPlayerName();
		}
	}
	return FString::Printf(TEXT("Farmhand #%d (fled the farm)"), PlayerId);
}

FHPDemandEntry* AHPGameState::FindDemand(FName GoodsId)
{
	return Demand.FindByPredicate([&](const FHPDemandEntry& D) { return D.GoodsId == GoodsId; });
}

// ---------------- Server API ----------------

int32 AHPGameState::AddWalletTxn(EHPWalletTxnType Type, int32 Amount, int32 PlayerId, FName GoodsId)
{
	if (!HasAuthority())
	{
		return 0;
	}
	int32 IOU = 0;
	const int32 Old = WalletBalance;
	WalletBalance = UHPEconomyMath::ApplyWalletDelta(WalletBalance, Amount, IOU);
	WeekIOU += IOU;
	const int32 Applied = WalletBalance - Old;

	FHPWalletTxn& T = DayTxns.AddDefaulted_GetRef();
	T.Type = Type;
	T.Amount = Applied;
	T.PlayerId = PlayerId;
	T.GoodsId = GoodsId;
	T.DayIndex = DayIndex;
	T.Timestamp = GetDayTime();

	if (Amount < 0)
	{
		DayCosts += -Amount; // the receipt shows the full fee; overflow is tracked as IOU
	}
	return Applied;
}

void AHPGameState::RecordBlameEvent(FBlameEvent E)
{
	if (!HasAuthority())
	{
		return; // only the host emits
	}
	// Rule 3: noise floor.
	if (FMath::Abs(E.DollarDelta) < 2 && !UHPEconomyMath::IsNonEconomicVerb(E.Verb))
	{
		return;
	}
	E.Timestamp = GetDayTime();
	E.DayIndex = static_cast<int16>(DayIndex);
	if (E.Instigators.Num() > 0 && E.PrimaryInstigator == HP_NO_PLAYER)
	{
		E.PrimaryInstigator = E.Instigators[0];
	}

	// Rule 2: merge repeated damage on the same item by the same instigator within 1.5 s.
	for (int32 i = DayBlameEvents.Num() - 1; i >= 0; --i)
	{
		FBlameEvent& Prev = DayBlameEvents[i];
		if (E.Timestamp - Prev.Timestamp > 1.5f)
		{
			break;
		}
		if (Prev.Verb == E.Verb && Prev.ItemId == E.ItemId && Prev.PrimaryInstigator == E.PrimaryInstigator
			&& (Prev.DollarDelta > 0) == (E.DollarDelta > 0))
		{
			Prev.DollarDelta += E.DollarDelta;
			Prev.Timestamp = E.Timestamp;
			Prev.Severity = static_cast<uint8>(UHPEconomyMath::Severity(Prev.DollarDelta, WeeklyQuota, Prev.Verb));
			if (Prev.Severity >= 60)
			{
				OnDisaster.Broadcast(Prev);
			}
			return;
		}
	}

	E.EventId = NextEventId++;
	E.Severity = static_cast<uint8>(UHPEconomyMath::Severity(E.DollarDelta, WeeklyQuota, E.Verb));
	DayBlameEvents.Add(E);
	if (E.Severity >= 60 || E.Verb == EHPBlameVerb::DroppedInRiver || E.Verb == EHPBlameVerb::TippedWagon)
	{
		OnDisaster.Broadcast(E);
	}
}

void AHPGameState::EmitBlame(EHPBlameVerb Verb, FName ItemId, int32 DollarDelta, const TArray<int32>& Touchers, FVector Location)
{
	FBlameEvent E;
	E.Verb = Verb;
	E.ItemId = ItemId;
	E.DollarDelta = DollarDelta;
	E.Location = Location;
	for (int32 i = 0; i < FMath::Min(Touchers.Num(), 3); ++i)
	{
		E.Instigators.Add(Touchers[i]);
	}
	E.BlameShares = UHPEconomyMath::BlameShares(E.Instigators.Num());
	E.PrimaryInstigator = E.Instigators.Num() ? E.Instigators[0] : HP_NO_PLAYER;
	RecordBlameEvent(MoveTemp(E));
}

int32 AHPGameState::SellGoods(UHPGoodsComponent* Goods)
{
	if (!HasAuthority() || !Goods || Goods->IsSold() || !IsMarketOpen())
	{
		return -1;
	}
	FHPOrderLine* Line = OrderLines.FindByPredicate([&](const FHPOrderLine& L)
	{
		return L.GoodsId == Goods->GoodsId && !L.IsComplete();
	});
	const int32 Value = Goods->GetCurrentSaleValue(Line != nullptr); // demand for unit n+1 is read before increment
	if (Line)
	{
		++Line->Filled;
	}
	FHPDemandEntry* D = FindDemand(Goods->GoodsId);
	if (!D)
	{
		D = &Demand.AddDefaulted_GetRef();
		D->GoodsId = Goods->GoodsId;
	}
	++D->SoldToday;

	AddWalletTxn(EHPWalletTxnType::Sale, Value, Goods->LastHandlerId, Goods->GoodsId);
	DaySales += Value;
	++DayItemsSold;
	Goods->NotifySold(Value);
	OnGoodsSold.Broadcast(Goods, Value, Line != nullptr, Goods->LastHandlerId);
	return Value;
}

int32 AHPGameState::ChargeRespawnFee(APlayerState* Player, EHPBlameVerb Verb)
{
	const int32 Fee = UHPEconomyMath::RespawnFee(WeekIndex);
	const int32 Id = Player ? Player->GetPlayerId() : HP_NO_PLAYER;
	AddWalletTxn(EHPWalletTxnType::RespawnFee, -Fee, Id);
	TArray<int32> Who;
	if (Id != HP_NO_PLAYER) Who.Add(Id);
	EmitBlame(Verb, NAME_None, -Fee, Who, Player && Player->GetPawn() ? Player->GetPawn()->GetActorLocation() : FVector::ZeroVector);
	return Fee;
}

void AHPGameState::ServerSetPhase(EHPDayPhase NewPhase, float Duration)
{
	Phase = NewPhase;
	PhaseStartTime = static_cast<float>(GetServerWorldTimeSeconds());
	PhaseEndTime = Duration > 0.f ? PhaseStartTime + Duration : PhaseStartTime;
}

void AHPGameState::ServerSetWeek(int32 InWeek, int32 PlayerCount)
{
	WeekIndex = FMath::Max(1, InWeek);
	QuotaPlayerCount = FMath::Clamp(PlayerCount, 1, 4);
	WeeklyQuota = UHPEconomyMath::WeeklyQuota(WeekIndex, QuotaPlayerCount);
	WeekIOU = 0;
}

void AHPGameState::ServerBeginDay(int32 InDayIndex, int32 InDayOfWeek, int32 InWeek, int32 Seed)
{
	DayIndex = InDayIndex;
	DayOfWeek = InDayOfWeek;
	WeekIndex = InWeek;
	DayStartTime = GetWorld() ? static_cast<float>(GetWorld()->GetTimeSeconds()) : 0.f;
	DaySales = 0;
	DayItemsSold = 0;
	DayCosts = 0;
	DayBlameEvents.Reset();
	DayTxns.Reset();

	FRandomStream Rng(Seed);

	// Demand: each GoodsId rolls D_day in [0.8, 1.2].
	Demand.Reset();
	for (const FHPGoodsPreset& P : UHPEconomyMath::GetGoodsPresets())
	{
		FHPDemandEntry& D = Demand.AddDefaulted_GetRef();
		D.GoodsId = P.GoodsId;
		D.DayDemand = FMath::RoundToFloat(Rng.FRandRange(0.8f, 1.2f) * 100.f) / 100.f;
	}

	// Morning Order: 3 lines of distinct orderable species.
	TArray<const FHPGoodsPreset*> Pool;
	for (const FHPGoodsPreset& P : UHPEconomyMath::GetGoodsPresets())
	{
		if (P.OrderBaseQ > 0) Pool.Add(&P);
	}
	OrderLines.Reset();
	for (int32 i = 0; i < 3 && Pool.Num() > 0; ++i)
	{
		const int32 Pick = Rng.RandRange(0, Pool.Num() - 1);
		FHPOrderLine& L = OrderLines.AddDefaulted_GetRef();
		L.GoodsId = Pool[Pick]->GoodsId;
		L.Quantity = UHPEconomyMath::OrderQuantity(Pool[Pick]->OrderBaseQ, QuotaPlayerCount, WeekIndex);
		Pool.RemoveAt(Pick);
	}
}

void AHPGameState::ServerResetRun()
{
	WalletBalance = UHPEconomyMath::StartingBalance;
	WeekIOU = 0;
	NextEventId = 1;
	LastSummary = FHPDaySummary();
}
