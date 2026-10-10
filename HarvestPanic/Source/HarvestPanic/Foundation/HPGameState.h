// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/session-networking.md (replicated run state visible to all clients),
//      shared-wallet-economy.md (wallet), day-loop.md (phase/clock/order), week-quota.md,
//      blame-ledger.md (day Blame Event log + receipt data).
// All writes are server-only; clients read replicated state (HUD polls it).
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameStateBase.h"
#include "Economy/HPEconomyTypes.h"
#include "HPGameState.generated.h"

class UHPGoodsComponent;
class APlayerState;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FHPOnDisaster, const FBlameEvent&, Event);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_FourParams(FHPOnGoodsSold, UHPGoodsComponent*, Item, int32, Value, bool, bFilledOrder, int32, SellerId);

UCLASS()
class HARVESTPANIC_API AHPGameState : public AGameStateBase
{
	GENERATED_BODY()

public:
	AHPGameState();

	// ---------------- Replicated run state ----------------
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Wallet") int32 WalletBalance = 100;
	/** Landlord IOU (overdraft overflow) added to this week's quota. */
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Wallet") int32 WeekIOU = 0;

	UPROPERTY(BlueprintReadOnly, Replicated, Category="Day") EHPDayPhase Phase = EHPDayPhase::PreRun;
	/** Server world time (GetServerWorldTimeSeconds) when the phase started / ends. End <= Start = untimed. */
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Day") float PhaseStartTime = 0.f;
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Day") float PhaseEndTime = 0.f;
	/** Run-wide 0-based day counter (Blame Event DayIndex). */
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Day") int32 DayIndex = 0;
	/** 0..4 = Mon..Fri. */
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Day") int32 DayOfWeek = 0;
	/** 1-based week number w. */
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Day") int32 WeekIndex = 1;

	UPROPERTY(BlueprintReadOnly, Replicated, Category="Quota") int32 WeeklyQuota = 1200;
	/** Player count sampled Monday morning (quota fixed for the week). */
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Quota") int32 QuotaPlayerCount = 4;

	UPROPERTY(BlueprintReadOnly, Replicated, Category="Order") TArray<FHPOrderLine> OrderLines;
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Market") TArray<FHPDemandEntry> Demand;

	UPROPERTY(BlueprintReadOnly, Replicated, Category="Day") int32 DaySales = 0;
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Day") int32 DayItemsSold = 0;
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Day") int32 DayCosts = 0;

	/** Today's Blame Events (blame-ledger Recording state). */
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Blame") TArray<FBlameEvent> DayBlameEvents;
	/** Last end-of-day / Sunday summary for the Payout screen. */
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Payout") FHPDaySummary LastSummary;

	UPROPERTY(BlueprintAssignable, Category="Blame") FHPOnDisaster OnDisaster;
	UPROPERTY(BlueprintAssignable, Category="Market") FHPOnGoodsSold OnGoodsSold;

	// ---------------- Queries (any machine) ----------------
	UFUNCTION(BlueprintPure, Category="Day") float GetPhaseSecondsRemaining() const;
	UFUNCTION(BlueprintPure, Category="Day") bool IsMarketOpen() const;
	/** Day.DuskBonus applied to ValueModifierMult during Dusk. */
	UFUNCTION(BlueprintPure, Category="Day") float GetPhaseValueMult() const;
	/** Demand for the next unit of GoodsId sold today (D(g, n+1)). */
	UFUNCTION(BlueprintPure, Category="Market") float GetDemandFor(FName GoodsId) const;
	UFUNCTION(BlueprintPure, Category="Quota") int32 GetQuotaDue() const { return WeeklyQuota + WeekIOU; }
	/** Seconds since the day (Morning) started, for Blame Event timestamps. */
	float GetDayTime() const;
	/** "Dave" / "Nature" etc. */
	FString GetPlayerNameById(int32 PlayerId) const;

	// ---------------- Server API (other systems call these) ----------------

	/**
	 * Single entry point for every wallet change (shared-wallet-economy). Negative amounts respect the −$200 floor;
	 * overflow becomes WeekIOU. Returns the amount actually applied.
	 */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Wallet")
	int32 AddWalletTxn(EHPWalletTxnType Type, int32 Amount, int32 PlayerId = -1, FName GoodsId = NAME_None);

	/** Record a Blame Event (host only). Fills EventId/Timestamp/DayIndex/Severity; merges & noise-filters per contract. */
	void RecordBlameEvent(FBlameEvent Event);

	/** Convenience emitter: touchers most-recent-first; shares computed 0.6/0.3/0.1. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Blame")
	void EmitBlame(EHPBlameVerb Verb, FName ItemId, int32 DollarDelta, const TArray<int32>& TouchersRecentFirst, FVector Location);

	/** Market acceptance: computes V, credits wallet, fills order lines, fires OnSold. Returns the sale value (−1 if rejected). */
	int32 SellGoods(UHPGoodsComponent* Goods);

	/** player-character: barn respawn fee for this week (also logs Blame verb Drowned/KnockedOut/Stranded). */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Wallet")
	int32 ChargeRespawnFee(APlayerState* Player, EHPBlameVerb Verb = EHPBlameVerb::Drowned);

	// ---- used by AHPGameMode ----
	void ServerSetPhase(EHPDayPhase NewPhase, float DurationSeconds);
	void ServerBeginDay(int32 InDayIndex, int32 InDayOfWeek, int32 InWeek, int32 Seed);
	void ServerSetWeek(int32 InWeek, int32 PlayerCount);
	void ServerResetRun();
	void ServerSetSummary(const FHPDaySummary& Summary) { LastSummary = Summary; }

protected:
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

private:
	FHPDemandEntry* FindDemand(FName GoodsId);
	uint32 NextEventId = 1;
	float DayStartTime = 0.f;
	/** Server-side txn log (not replicated). */
	TArray<FHPWalletTxn> DayTxns;
};
