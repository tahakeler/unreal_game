// Copyright HARVEST PANIC team. All Rights Reserved.
#include "Economy/HPEconomyMath.h"

float UHPEconomyMath::ComputeBruiseDelta(float Impulse, float Fragility, float BruiseMult)
{
	if (Impulse > ImpulseSolverError)
	{
		return 0.f; // solver error spike
	}
	const float Raw = FMath::Max(0.f, Impulse - ImpulseMin) / ImpulseScale * Fragility * BruiseMult;
	return FMath::Min(Raw, MaxBruisePerHit);
}

EHPBruiseState UHPEconomyMath::BruiseStateFromPoints(float BP)
{
	if (BP < 10.f) return EHPBruiseState::Pristine;
	if (BP < 35.f) return EHPBruiseState::Scuffed;
	if (BP < 65.f) return EHPBruiseState::Bruised;
	if (BP < 100.f) return EHPBruiseState::Busted;
	return EHPBruiseState::Ruined;
}

float UHPEconomyMath::BruiseMultiplier(EHPBruiseState State, EHPGoodsCategory Category)
{
	switch (State)
	{
	case EHPBruiseState::Pristine: return 1.00f;
	case EHPBruiseState::Scuffed:  return 0.85f;
	case EHPBruiseState::Bruised:  return 0.60f;
	case EHPBruiseState::Busted:   return 0.30f;
	case EHPBruiseState::Ruined:   return Category == EHPGoodsCategory::Livestock ? 0.25f : 0.f;
	}
	return 1.f;
}

float UHPEconomyMath::Freshness(float Age, float Grace, float Decay)
{
	if (Age < 0.f || Decay <= 0.f)
	{
		return 1.f; // clock not started
	}
	return FMath::Clamp(1.f - FMath::Max(0.f, Age - Grace) / Decay, 0.5f, 1.f);
}

bool UHPEconomyMath::GetFreshnessParams(EHPGoodsCategory Category, FName GoodsId, float& OutGrace, float& OutDecay)
{
	switch (Category)
	{
	case EHPGoodsCategory::Crop:    OutGrace = 120.f; OutDecay = 480.f; return true;
	case EHPGoodsCategory::Product: OutGrace = 180.f; OutDecay = 600.f; return true;
	default: break; // Livestock never decays; Golden Egg (Special) never decays
	}
	OutGrace = 0.f;
	OutDecay = 0.f;
	return false;
}

int32 UHPEconomyMath::SaleValue(int32 BaseValue, float B, float F, float D, float O, float VMM)
{
	return FMath::RoundToInt(static_cast<float>(BaseValue) * B * F * D * O * VMM);
}

int32 UHPEconomyMath::FinalSaleValue(int32 BaseValue, EHPBruiseState State, EHPGoodsCategory Category,
	float F, float D, float O, float VMM)
{
	if (State == EHPBruiseState::Ruined && Category != EHPGoodsCategory::Livestock)
	{
		return 1; // compost
	}
	return SaleValue(BaseValue, BruiseMultiplier(State, Category), F, D, O, VMM);
}

int32 UHPEconomyMath::BruiseDollarLoss(int32 BaseValue, float ExpectedDemand, float BOld, float BNew)
{
	return FMath::RoundToInt(static_cast<float>(BaseValue) * ExpectedDemand * (BOld - BNew));
}

float UHPEconomyMath::Demand(float DayDemand, int32 N, float DemandMult)
{
	const int32 Excess = FMath::Max(0, N - DemandSaturationN);
	return FMath::Clamp(DayDemand * FMath::Pow(0.95f, static_cast<float>(Excess)) * DemandMult, 0.6f, 1.5f);
}

float UHPEconomyMath::PlayerScale(int32 PlayerCount)
{
	switch (FMath::Clamp(PlayerCount, 1, 4))
	{
	case 1: return 0.45f;
	case 2: return 0.65f;
	case 3: return 0.85f;
	default: return 1.0f;
	}
}

int32 UHPEconomyMath::RoundTo(float Value, int32 Step)
{
	return FMath::RoundToInt(Value / static_cast<float>(Step)) * Step;
}

int32 UHPEconomyMath::WeeklyQuota(int32 Week, int32 PlayerCount, float RegionMult)
{
	const int32 W = FMath::Max(1, Week);
	const double Q = QuotaBase * FMath::Pow(static_cast<double>(QuotaGrowth), static_cast<double>(W - 1))
		* PlayerScale(PlayerCount) * RegionMult;
	return RoundTo(static_cast<float>(Q), 10);
}

int32 UHPEconomyMath::RespawnFee(int32 Week, float Mult)
{
	const int32 W = FMath::Max(1, Week);
	return RoundTo(25.f * (1.f + 0.25f * (W - 1)) * Mult, 5);
}

int32 UHPEconomyMath::OrderCompleteBonus(int32 Week)
{
	return 60 * FMath::Max(1, Week);
}

int32 UHPEconomyMath::OrderQuantity(int32 BaseQ, int32 PlayerCount, int32 Week)
{
	const int32 W = FMath::Max(1, Week);
	return FMath::Max(1, FMath::RoundToInt(BaseQ * PlayerScale(PlayerCount) * (1.f + 0.15f * (W - 1))));
}

int32 UHPEconomyMath::ApplyWalletDelta(int32 Balance, int32 Amount, int32& OutIOU)
{
	OutIOU = 0;
	const int64 NewBalance = static_cast<int64>(Balance) + Amount;
	if (Amount < 0 && NewBalance < WalletFloor)
	{
		// Fee capped at the floor; the excess becomes Landlord IOU added to this week's quota.
		const int32 Capped = FMath::Min(Balance, WalletFloor); // never moves an already-sub-floor balance
		OutIOU = static_cast<int32>(Capped - NewBalance);
		return Capped;
	}
	return static_cast<int32>(NewBalance);
}

TArray<float> UHPEconomyMath::BlameShares(int32 NumTouchers)
{
	static const float Weights[3] = { 0.6f, 0.3f, 0.1f };
	TArray<float> Out;
	const int32 N = FMath::Clamp(NumTouchers, 0, 3);
	float Sum = 0.f;
	for (int32 i = 0; i < N; ++i)
	{
		Sum += Weights[i];
	}
	for (int32 i = 0; i < N; ++i)
	{
		Out.Add(Weights[i] / Sum);
	}
	return Out;
}

int32 UHPEconomyMath::VerbBonus(EHPBlameVerb Verb)
{
	switch (Verb)
	{
	case EHPBlameVerb::DroppedInRiver:   return 25;
	case EHPBlameVerb::TippedWagon:      return 30;
	case EHPBlameVerb::SummonedCreature: return 20;
	case EHPBlameVerb::KnockedOut:
	case EHPBlameVerb::Drowned:
	case EHPBlameVerb::Stranded:         return 10;
	case EHPBlameVerb::YeetedTeammate:   return 15;
	default:                             return 0;
	}
}

int32 UHPEconomyMath::Severity(int32 DollarDelta, int32 WeeklyQuota, EHPBlameVerb Verb)
{
	const float Share = FMath::Max(1.f, WeeklyQuota / static_cast<float>(DaysPerWeek));
	const float Base = FMath::Clamp(100.f * FMath::Abs(DollarDelta) / Share, 0.f, 100.f);
	return FMath::Clamp(FMath::RoundToInt(Base) + VerbBonus(Verb), 0, 100);
}

bool UHPEconomyMath::IsNonEconomicVerb(EHPBlameVerb Verb)
{
	return Verb == EHPBlameVerb::KnockedOut || Verb == EHPBlameVerb::Drowned || Verb == EHPBlameVerb::Stranded
		|| Verb == EHPBlameVerb::SummonedCreature || Verb == EHPBlameVerb::YeetedTeammate;
}

TArray<FHPReceiptLine> UHPEconomyMath::BuildReceipt(const TArray<FBlameEvent>& Events)
{
	TArray<FHPReceiptLine> Lines;
	for (const FBlameEvent& E : Events)
	{
		if (E.bSecret)
		{
			continue; // revealed on Sunday only
		}
		const int32 Who = E.Instigators.Num() > 0 ? E.PrimaryInstigator : HP_NO_PLAYER;
		FHPReceiptLine* Found = Lines.FindByPredicate([&](const FHPReceiptLine& L)
		{
			return L.PlayerId == Who && L.Verb == E.Verb && L.ItemId == E.ItemId;
		});
		if (!Found)
		{
			Found = &Lines.AddDefaulted_GetRef();
			Found->PlayerId = Who;
			Found->Verb = E.Verb;
			Found->ItemId = E.ItemId;
			Found->bPositive = E.DollarDelta > 0;
		}
		Found->Count += FMath::Max(1, E.Count);
		Found->Dollars += E.DollarDelta;
	}
	// Players in order of first appearance; within a player negatives (biggest loss first) then positives; Nature last.
	TArray<int32> PlayerOrder;
	for (const FHPReceiptLine& L : Lines)
	{
		if (L.PlayerId != HP_NO_PLAYER) PlayerOrder.AddUnique(L.PlayerId);
	}
	PlayerOrder.Add(HP_NO_PLAYER);
	Lines.StableSort([&](const FHPReceiptLine& A, const FHPReceiptLine& B)
	{
		const int32 IA = PlayerOrder.IndexOfByKey(A.PlayerId);
		const int32 IB = PlayerOrder.IndexOfByKey(B.PlayerId);
		if (IA != IB) return IA < IB;
		if (A.bPositive != B.bPositive) return !A.bPositive;
		return A.Dollars < B.Dollars;
	});
	return Lines;
}

FString UHPEconomyMath::GoodsDisplayName(FName GoodsId)
{
	if (GoodsId.IsNone())
	{
		return TEXT("something");
	}
	FString S = GoodsId.ToString();
	int32 Dot;
	if (S.FindLastChar(TEXT('.'), Dot))
	{
		S.RightChopInline(Dot + 1);
	}
	return FName::NameToDisplayString(S, false);
}

FString UHPEconomyMath::FormatReceiptLine(const FHPReceiptLine& L, const FString& P)
{
	const FString Item = GoodsDisplayName(L.ItemId);
	const FString N = L.Count > 1 ? FString::Printf(TEXT(" x%d"), L.Count) : FString();
	switch (L.Verb)
	{
	case EHPBlameVerb::Bruised:          return FString::Printf(TEXT("%s: bruised %s%s"), *P, *Item, *N);
	case EHPBlameVerb::Dropped:          return FString::Printf(TEXT("%s: dropped %s%s"), *P, *Item, *N);
	case EHPBlameVerb::Slipped:          return FString::Printf(TEXT("%s: lost grip on %s%s"), *P, *Item, *N);
	case EHPBlameVerb::DroppedInRiver:   return FString::Printf(TEXT("%s: dropped %s into river%s"), *P, *Item, *N);
	case EHPBlameVerb::Threw:            return FString::Printf(TEXT("%s: yeeted %s%s"), *P, *Item, *N);
	case EHPBlameVerb::Crashed:          return FString::Printf(TEXT("%s: crashed with %s aboard%s"), *P, *Item, *N);
	case EHPBlameVerb::SpilledCargo:     return FString::Printf(TEXT("%s: bounced %s out of the wagon%s"), *P, *Item, *N);
	case EHPBlameVerb::LetEscape:        return FString::Printf(TEXT("%s: let %s escape%s"), *P, *Item, *N);
	case EHPBlameVerb::BrokePlank:       return FString::Printf(TEXT("%s: broke a plank (again)%s"), *P, *N);
	case EHPBlameVerb::TippedWagon:      return FString::Printf(TEXT("%s: tipped the wagon%s"), *P, *N);
	case EHPBlameVerb::Drowned:          return FString::Printf(TEXT("%s: drowned (respawn fee)%s"), *P, *N);
	case EHPBlameVerb::KnockedOut:       return FString::Printf(TEXT("%s: got knocked out (respawn fee)%s"), *P, *N);
	case EHPBlameVerb::Stranded:         return FString::Printf(TEXT("%s: lost in the dark (respawn fee)%s"), *P, *N);
	case EHPBlameVerb::YeetedTeammate:   return FString::Printf(TEXT("%s: threw a teammate into the river%s"), *P, *N);
	case EHPBlameVerb::LostTool:         return FString::Printf(TEXT("%s: lost the %s%s"), *P, *Item, *N);
	case EHPBlameVerb::LassoedIntoRiver: return FString::Printf(TEXT("%s: lassoed %s into the river%s"), *P, *Item, *N);
	case EHPBlameVerb::SpookedHerd:      return FString::Printf(TEXT("%s: screamed, animals fled%s"), *P, *N);
	case EHPBlameVerb::SummonedCreature: return FString::Printf(TEXT("%s: woke something in the dark%s"), *P, *N);
	case EHPBlameVerb::Stolen:           return FString::Printf(TEXT("%s: left %s unguarded%s"), *P, *Item, *N);
	case EHPBlameVerb::Pocketed:         return FString::Printf(TEXT("%s: ...nothing to see here"), *P);
	case EHPBlameVerb::LandlordFine:     return FString::Printf(TEXT("%s: failed hat inspection%s"), *P, *N);
	case EHPBlameVerb::Rescue:           return FString::Printf(TEXT("%s: saved %s%s"), *P, *Item, *N);
	case EHPBlameVerb::StealBack:        return FString::Printf(TEXT("%s: stole %s back%s"), *P, *Item, *N);
	}
	return P;
}

const TArray<FHPGoodsPreset>& UHPEconomyMath::GetGoodsPresets()
{
	// goods-value.md "Starting value table (Region 1)"; OrderBaseQ from day-loop example ("3 Pigs · 6 Eggs · 1 Prize Pumpkin").
	static const TArray<FHPGoodsPreset> Presets = {
		{ TEXT("Chicken"),      EHPGoodsCategory::Livestock, 15,  0.6f, 1, 4 },
		{ TEXT("Egg"),          EHPGoodsCategory::Product,   5,   2.0f, 1, 6 },
		{ TEXT("Pig"),          EHPGoodsCategory::Livestock, 60,  0.8f, 2, 3 },
		{ TEXT("PrizePumpkin"), EHPGoodsCategory::Crop,      300, 1.5f, 8, 1 },
		{ TEXT("GoldenEgg"),    EHPGoodsCategory::Special,   150, 2.0f, 1, 0 },
		{ TEXT("Goat"),         EHPGoodsCategory::Livestock, 70,  0.5f, 2, 0 },
		{ TEXT("Cow"),          EHPGoodsCategory::Livestock, 150, 0.7f, 6, 0 },
		{ TEXT("Bull"),         EHPGoodsCategory::Livestock, 200, 0.4f, 6, 0 },
		{ TEXT("Turnip"),       EHPGoodsCategory::Crop,      10,  1.0f, 1, 0 },
		{ TEXT("Mandrake"),     EHPGoodsCategory::Crop,      45,  1.2f, 1, 0 },
	};
	return Presets;
}

const FHPGoodsPreset* UHPEconomyMath::FindGoodsPreset(FName GoodsId)
{
	return GetGoodsPresets().FindByPredicate([&](const FHPGoodsPreset& P) { return P.GoodsId == GoodsId; });
}
