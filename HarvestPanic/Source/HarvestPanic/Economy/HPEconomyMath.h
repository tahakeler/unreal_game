// Copyright HARVEST PANIC team. All Rights Reserved.
// Pure formula library — every number here comes from a GDD Formulas section.
// Kept stateless so it can be unit tested (Tests/HPEconomyTests.cpp).
#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "HPEconomyTypes.h"
#include "HPEconomyMath.generated.h"

/** Static goods definition row (goods-value "Starting value table (Region 1)"). */
struct FHPGoodsPreset
{
	FName GoodsId;
	EHPGoodsCategory Category;
	int32 BaseValue;
	float Fragility;
	int32 CarryHands;
	/** day-loop order base quantity at 4p week 1 (BaseQ_species). 0 = not orderable in the slice. */
	int32 OrderBaseQ;
};

UCLASS()
class HARVESTPANIC_API UHPEconomyMath : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	// ---- goods-value tuning (GDD §4) ----
	static constexpr float ImpulseMin = 400.f;      // I_min N·s
	static constexpr float ImpulseScale = 40.f;     // N·s per BP
	static constexpr float MaxBruisePerHit = 60.f;  // Chaos jitter cap
	static constexpr float ImpulseSolverError = 20000.f;
	static constexpr float OrderBonusMult = 1.5f;   // O

	// ---- shared-wallet-economy ----
	static constexpr int32 StartingBalance = 100;
	static constexpr int32 WalletFloor = -200;
	static constexpr int32 DemandSaturationN = 6;   // N_sat

	// ---- week-quota ----
	static constexpr float QuotaBase = 1200.f;
	static constexpr float QuotaGrowth = 1.35f;
	static constexpr int32 DaysPerWeek = 5;

	/** ΔBP = max(0, I − I_min)/I_scale × Fragility × M_bruise; capped 60; I > 20,000 ignored. */
	UFUNCTION(BlueprintPure, Category="HP|Goods")
	static float ComputeBruiseDelta(float Impulse, float Fragility, float BruiseMult = 1.f);

	/** Pristine <10, Scuffed 10–34, Bruised 35–64, Busted 65–99, Ruined 100. */
	UFUNCTION(BlueprintPure, Category="HP|Goods")
	static EHPBruiseState BruiseStateFromPoints(float BruisePoints);

	/** B(state): 1.00/0.85/0.60/0.30/0.00 (crop/product) or 0.25 (livestock). */
	UFUNCTION(BlueprintPure, Category="HP|Goods")
	static float BruiseMultiplier(EHPBruiseState State, EHPGoodsCategory Category);

	/** F(t) = clamp(1 − max(0, t − T_grace)/T_decay, 0.5, 1.0). */
	UFUNCTION(BlueprintPure, Category="HP|Goods")
	static float Freshness(float AgeSeconds, float GraceSeconds, float DecaySeconds);

	/** Category freshness params (Crops 120/480, Products 180/600). Returns false when the item never decays. */
	static bool GetFreshnessParams(EHPGoodsCategory Category, FName GoodsId, float& OutGrace, float& OutDecay);

	/** V = round(BaseValue × B × F × D × O × ValueModifierMult). */
	UFUNCTION(BlueprintPure, Category="HP|Goods")
	static int32 SaleValue(int32 BaseValue, float B, float F, float D, float O, float ValueModifierMult);

	/** Sale value including the "compost" rule (Ruined crop/product still sells for $1). */
	UFUNCTION(BlueprintPure, Category="HP|Goods")
	static int32 FinalSaleValue(int32 BaseValue, EHPBruiseState State, EHPGoodsCategory Category,
		float F, float D, float O, float ValueModifierMult);

	/** ΔDollars = BaseValue × D_expected × (B_old − B_new), rounded. */
	UFUNCTION(BlueprintPure, Category="HP|Goods")
	static int32 BruiseDollarLoss(int32 BaseValue, float ExpectedDemand, float BOld, float BNew);

	/** D(g,n) = clamp(D_day × 0.95^max(0, n − N_sat) × DemandMult, 0.6, 1.5). n is 1-based. */
	UFUNCTION(BlueprintPure, Category="HP|Economy")
	static float Demand(float DayDemand, int32 UnitIndex1Based, float DemandMult = 1.f);

	/** S_p: 1p 0.45, 2p 0.65, 3p 0.85, 4p 1.00 (clamped to 1..4). */
	UFUNCTION(BlueprintPure, Category="HP|Economy")
	static float PlayerScale(int32 PlayerCount);

	/** Q(w) = round_to_10(1200 × 1.35^(w−1) × S_p × R_mult). */
	UFUNCTION(BlueprintPure, Category="HP|Economy")
	static int32 WeeklyQuota(int32 Week, int32 PlayerCount, float RegionMult = 1.f);

	/** Fee(w) = round_to_5(25 × (1 + 0.25(w − 1)) × RespawnFeeMult). */
	UFUNCTION(BlueprintPure, Category="HP|Economy")
	static int32 RespawnFee(int32 Week, float RespawnFeeMult = 1.f);

	/** OCB = 60 × w. */
	UFUNCTION(BlueprintPure, Category="HP|Economy")
	static int32 OrderCompleteBonus(int32 Week);

	/** q = max(1, round(BaseQ × S_p × (1 + 0.15(w−1)))). */
	UFUNCTION(BlueprintPure, Category="HP|Economy")
	static int32 OrderQuantity(int32 BaseQ, int32 PlayerCount, int32 Week);

	/**
	 * Applies a wallet delta with the −$200 overdraft floor.
	 * Returns the new balance; OutIOU receives the overflow (≥ 0) that becomes "Landlord IOU".
	 */
	UFUNCTION(BlueprintPure, Category="HP|Economy")
	static int32 ApplyWalletDelta(int32 Balance, int32 Amount, int32& OutIOU);

	/** Touch-history shares: last 0.6, previous 0.3, earlier 0.1, renormalized. Index 0 = most recent toucher. */
	UFUNCTION(BlueprintPure, Category="HP|Blame")
	static TArray<float> BlameShares(int32 NumTouchers);

	/** blame-ledger VerbBonus table. */
	UFUNCTION(BlueprintPure, Category="HP|Blame")
	static int32 VerbBonus(EHPBlameVerb Verb);

	/** Severity = clamp(100 × |Δ$| / (WeeklyQuota/5), 0, 100) + VerbBonus, clamped 0–100. */
	UFUNCTION(BlueprintPure, Category="HP|Blame")
	static int32 Severity(int32 DollarDelta, int32 WeeklyQuota, EHPBlameVerb Verb);

	/** Non-economic verbs are kept even under the $2 noise floor. */
	static bool IsNonEconomicVerb(EHPBlameVerb Verb);

	/** Groups day events by PrimaryInstigator then Verb (+item). Negative lines first, positives after, Nature last. */
	static TArray<FHPReceiptLine> BuildReceipt(const TArray<FBlameEvent>& Events);

	/** Receipt line text per blame-ledger templates, e.g. "Dave: dropped Prize Pumpkin ×2". */
	static FString FormatReceiptLine(const FHPReceiptLine& Line, const FString& PlayerName);

	/** "PrizePumpkin" / "Crop.PrizePumpkin" → "Prize Pumpkin". */
	static FString GoodsDisplayName(FName GoodsId);

	/** Region 1 starting value table. */
	static const TArray<FHPGoodsPreset>& GetGoodsPresets();
	static const FHPGoodsPreset* FindGoodsPreset(FName GoodsId);

private:
	static int32 RoundTo(float Value, int32 Step);
};
