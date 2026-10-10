// Copyright HARVEST PANIC team. All Rights Reserved.
// Shared data types for the day loop / economy vertical slice.
// GDD: design/gdd/goods-value.md (Contract: Goods Item), day-loop.md,
//      shared-wallet-economy.md, week-quota.md, blame-ledger.md (Contract: Blame Event).
#pragma once

#include "CoreMinimal.h"
#include "GameplayTagContainer.h"
#include "Engine/NetSerialization.h"
#include "HPEconomyTypes.generated.h"

/** goods-value: Category. */
UENUM(BlueprintType)
enum class EHPGoodsCategory : uint8
{
	Livestock,
	Crop,
	Product, // eggs, milk, wool
	Special
};

/** goods-value: BruiseState derived from BruisePoints. */
UENUM(BlueprintType)
enum class EHPBruiseState : uint8
{
	Pristine,
	Scuffed,
	Bruised,
	Busted,
	Ruined
};

/** day-loop phases (slice: Sundown folded into the end of Workday, Shop folded into Payout). */
UENUM(BlueprintType)
enum class EHPDayPhase : uint8
{
	PreRun,
	Morning,
	Workday,
	Dusk,
	Payout,
	Sunday
};

/** week-quota Sunday outcome. */
UENUM(BlueprintType)
enum class EHPSundayResult : uint8
{
	None,
	Paid,
	Evicted
};

/** blame-ledger EBlameVerb v1.1 (23 verbs; add-only, PascalCase per contract). */
UENUM(BlueprintType)
enum class EHPBlameVerb : uint8
{
	Bruised,
	Dropped,
	Slipped,
	DroppedInRiver,
	Threw,
	Crashed,
	SpilledCargo,
	LetEscape,
	BrokePlank,
	TippedWagon,
	Drowned,
	KnockedOut,
	Stranded,
	YeetedTeammate,
	LostTool,
	LassoedIntoRiver,
	SpookedHerd,
	SummonedCreature,
	Stolen,
	Pocketed,
	LandlordFine,
	Rescue,
	StealBack
};

/** shared-wallet-economy FWalletTxn.Type */
UENUM(BlueprintType)
enum class EHPWalletTxnType : uint8
{
	Sale,
	OrderBonus,
	RespawnFee,
	Repair,
	Fine,
	Upgrade,
	SellBack,
	Quota,
	Theft,
	Refund
};

/** Sentinel for "no player" / "Nature" in PlayerId fields. */
static constexpr int32 HP_NO_PLAYER = -1;

/**
 * Contract: Blame Event (blame-ledger.md). Only the host constructs these.
 * Field names/types mirror the contract; PlayerId = APlayerState::GetPlayerId().
 * ItemId carries the GoodsId of the item (None when no item).
 */
USTRUCT(BlueprintType)
struct HARVESTPANIC_API FBlameEvent
{
	GENERATED_BODY()

	UPROPERTY() uint32 EventId = 0;
	UPROPERTY(BlueprintReadOnly, Category="Blame") float Timestamp = 0.f;
	UPROPERTY() int16 DayIndex = 0;
	/** Who is blamed; empty = "Nature". Max 4. */
	UPROPERTY(BlueprintReadOnly, Category="Blame") TArray<int32> Instigators;
	UPROPERTY(BlueprintReadOnly, Category="Blame") int32 PrimaryInstigator = HP_NO_PLAYER;
	/** Same length as Instigators, sums to 1.0. */
	UPROPERTY(BlueprintReadOnly, Category="Blame") TArray<float> BlameShares;
	UPROPERTY(BlueprintReadOnly, Category="Blame") EHPBlameVerb Verb = EHPBlameVerb::Bruised;
	UPROPERTY(BlueprintReadOnly, Category="Blame") FName ItemId;
	UPROPERTY(BlueprintReadOnly, Category="Blame") FGameplayTag ItemTag;
	/** <= 0 for losses, > 0 only for Rescue/StealBack. */
	UPROPERTY(BlueprintReadOnly, Category="Blame") int32 DollarDelta = 0;
	UPROPERTY(BlueprintReadOnly, Category="Blame") uint8 Severity = 0;
	UPROPERTY() FVector_NetQuantize Location = FVector::ZeroVector;
	UPROPERTY(BlueprintReadOnly, Category="Blame") FGameplayTag CauseTag;
	UPROPERTY(BlueprintReadOnly, Category="Blame") bool bSecret = false;
	/** Merge counter (×n on the receipt). Not part of the wire contract semantics; display only. */
	UPROPERTY(BlueprintReadOnly, Category="Blame") int32 Count = 1;
};

/** day-loop Morning Order line. */
USTRUCT(BlueprintType)
struct HARVESTPANIC_API FHPOrderLine
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Order") FName GoodsId;
	UPROPERTY(BlueprintReadOnly, Category="Order") int32 Quantity = 1;
	UPROPERTY(BlueprintReadOnly, Category="Order") int32 Filled = 0;

	bool IsComplete() const { return Filled >= Quantity; }
};

/** shared-wallet-economy demand state per GoodsId for today. */
USTRUCT(BlueprintType)
struct HARVESTPANIC_API FHPDemandEntry
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Demand") FName GoodsId;
	UPROPERTY(BlueprintReadOnly, Category="Demand") float DayDemand = 1.f;
	UPROPERTY(BlueprintReadOnly, Category="Demand") int32 SoldToday = 0;
};

/** shared-wallet-economy FWalletTxn. */
USTRUCT(BlueprintType)
struct HARVESTPANIC_API FHPWalletTxn
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Wallet") EHPWalletTxnType Type = EHPWalletTxnType::Sale;
	UPROPERTY(BlueprintReadOnly, Category="Wallet") int32 Amount = 0;
	UPROPERTY(BlueprintReadOnly, Category="Wallet") int32 PlayerId = HP_NO_PLAYER;
	UPROPERTY(BlueprintReadOnly, Category="Wallet") FName GoodsId;
	UPROPERTY(BlueprintReadOnly, Category="Wallet") int32 DayIndex = 0;
	UPROPERTY(BlueprintReadOnly, Category="Wallet") float Timestamp = 0.f;
};

/** End-of-day payout data (day-loop Payout + blame-ledger receipt header). */
USTRUCT(BlueprintType)
struct HARVESTPANIC_API FHPDaySummary
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category="Payout") int32 DayIndex = 0;
	UPROPERTY(BlueprintReadOnly, Category="Payout") int32 WeekIndex = 1;
	UPROPERTY(BlueprintReadOnly, Category="Payout") int32 DayOfWeek = 0;
	UPROPERTY(BlueprintReadOnly, Category="Payout") int32 Sales = 0;
	UPROPERTY(BlueprintReadOnly, Category="Payout") int32 ItemsSold = 0;
	UPROPERTY(BlueprintReadOnly, Category="Payout") int32 OrderBonus = 0;
	UPROPERTY(BlueprintReadOnly, Category="Payout") int32 Costs = 0;
	UPROPERTY(BlueprintReadOnly, Category="Payout") int32 Net = 0;
	UPROPERTY(BlueprintReadOnly, Category="Payout") int32 WalletAfter = 0;
	UPROPERTY(BlueprintReadOnly, Category="Payout") bool bOrderComplete = false;
	/** Sunday only. */
	UPROPERTY(BlueprintReadOnly, Category="Payout") EHPSundayResult SundayResult = EHPSundayResult::None;
	UPROPERTY(BlueprintReadOnly, Category="Payout") int32 QuotaDue = 0;
};

/** One printable receipt line (built from Blame Events by UHPEconomyMath::BuildReceipt). */
USTRUCT(BlueprintType)
struct HARVESTPANIC_API FHPReceiptLine
{
	GENERATED_BODY()

	/** HP_NO_PLAYER = Nature block. */
	UPROPERTY(BlueprintReadOnly, Category="Receipt") int32 PlayerId = HP_NO_PLAYER;
	UPROPERTY(BlueprintReadOnly, Category="Receipt") EHPBlameVerb Verb = EHPBlameVerb::Bruised;
	UPROPERTY(BlueprintReadOnly, Category="Receipt") FName ItemId;
	UPROPERTY(BlueprintReadOnly, Category="Receipt") int32 Count = 0;
	UPROPERTY(BlueprintReadOnly, Category="Receipt") int32 Dollars = 0;
	UPROPERTY(BlueprintReadOnly, Category="Receipt") bool bPositive = false;
};
