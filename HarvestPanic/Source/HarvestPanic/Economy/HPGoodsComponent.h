// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/goods-value.md — Contract: Goods Item.
// Attach to any sellable actor. Server-authoritative; BruisePoints/BruiseState/Freshness replicate.
#pragma once

#include "CoreMinimal.h"
#include "Components/ActorComponent.h"
#include "GameplayTagContainer.h"
#include "Economy/HPEconomyTypes.h"
#include "HPGoodsComponent.generated.h"

class UHPGoodsComponent;

DECLARE_DYNAMIC_MULTICAST_DELEGATE_FourParams(FHPOnBruised, UHPGoodsComponent*, Item, float, DeltaBP, int32, DeltaDollars, int32, InstigatorId);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_ThreeParams(FHPOnBruiseStateChanged, UHPGoodsComponent*, Item, EHPBruiseState, OldState, EHPBruiseState, NewState);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_TwoParams(FHPOnSold, UHPGoodsComponent*, Item, int32, FinalValue);

/** One entry of the touch history (blame-ledger TouchWindow = 3 s). */
USTRUCT(BlueprintType)
struct FHPTouchRecord
{
	GENERATED_BODY()
	UPROPERTY(BlueprintReadOnly, Category="Goods") int32 PlayerId = HP_NO_PLAYER;
	UPROPERTY(BlueprintReadOnly, Category="Goods") float ServerTime = 0.f;
};

UCLASS(ClassGroup=(HarvestPanic), meta=(BlueprintSpawnableComponent))
class HARVESTPANIC_API UHPGoodsComponent : public UActorComponent
{
	GENERATED_BODY()

public:
	UHPGoodsComponent();

	// ---------------- Contract: Goods Item (data-asset fields; slice uses presets) ----------------
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Replicated, Category="Goods") FName GoodsId = TEXT("Pig");
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Replicated, Category="Goods") EHPGoodsCategory Category = EHPGoodsCategory::Livestock;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Replicated, Category="Goods") int32 BaseValue = 60;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Goods", meta=(ClampMin=0.25, ClampMax=2.0)) float Fragility = 0.8f;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Goods", meta=(ClampMin=1, ClampMax=8)) int32 CarryHands = 2;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Goods") float MassKg = 60.f;
	/** If true, BeginPlay fills Category/BaseValue/Fragility/CarryHands from the Region 1 table by GoodsId. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Goods") bool bUsePresetForGoodsId = true;

	// ---------------- Contract: Goods Item (runtime, server writes) ----------------
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Goods") float BruisePoints = 0.f;
	UPROPERTY(BlueprintReadOnly, ReplicatedUsing=OnRep_BruiseState, Category="Goods") EHPBruiseState BruiseState = EHPBruiseState::Pristine;
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Goods") float Freshness = 1.f;
	/** Server time the clock started; −1 = not started. */
	UPROPERTY(BlueprintReadOnly, Category="Goods") float FreshnessClockStart = -1.f;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Replicated, Category="Goods") bool bIsAlive = false;
	/** Written by physics-carry via RecordTouch. */
	UPROPERTY(BlueprintReadOnly, Replicated, Category="Goods") int32 LastHandlerId = HP_NO_PLAYER;
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Replicated, Category="Goods") FGameplayTagContainer Tags;
	/** Product of item-scoped Day Modifiers (default 1.0). */
	UPROPERTY(BlueprintReadWrite, Replicated, Category="Goods") float ValueModifierMult = 1.f;

	// ---------------- Events ----------------
	UPROPERTY(BlueprintAssignable, Category="Goods") FHPOnBruised OnBruised;
	UPROPERTY(BlueprintAssignable, Category="Goods") FHPOnBruiseStateChanged OnStateChanged;
	UPROPERTY(BlueprintAssignable, Category="Goods") FHPOnSold OnSold;

	// ---------------- API for other systems ----------------

	/**
	 * physics-carry / hazards: apply one physics hit (server only; ignored on clients).
	 * Impulse in N·s. Instigator = the player pawn/controller/playerstate responsible (nullptr = environment);
	 * when null the touch history (3 s window) attributes blame. Verb lets carry say Dropped/Threw/etc.
	 * Returns the bruise points added.
	 */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Goods")
	float ApplyBruiseHit(float Impulse, AActor* Instigator, EHPBlameVerb Verb = EHPBlameVerb::Bruised);

	/** Direct bruise-point damage (water, creatures). Server only. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Goods")
	float ApplyBruisePoints(float DeltaBP, AActor* Instigator, EHPBlameVerb Verb = EHPBlameVerb::Bruised);

	/** physics-carry: call on grab/throw/drop so blame has a touch history. Server only. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Goods")
	void RecordTouch(AActor* Toucher);

	/** Crops on harvest / products on lay. */
	UFUNCTION(BlueprintCallable, BlueprintAuthorityOnly, Category="Goods")
	void StartFreshnessClock();

	/** Value if sold right now (O = 1.5 when bFillsOrder). Uses GameState demand + dusk bonus. */
	UFUNCTION(BlueprintPure, Category="Goods")
	int32 GetCurrentSaleValue(bool bFillsOrder = false) const;

	/** Players in the touch history within the 3 s TouchWindow, most recent first (max 3). */
	TArray<int32> GetRecentTouchers(float Now) const;

	/** Called by the market when sold (server). Fires OnSold. */
	void NotifySold(int32 FinalValue);

	bool IsSold() const { return bSold; }

	/** Applies a Region 1 preset. */
	UFUNCTION(BlueprintCallable, Category="Goods")
	void ApplyPreset(FName InGoodsId);

	static int32 ResolvePlayerId(const AActor* Actor);

	static constexpr float TouchWindowSeconds = 3.f;

protected:
	virtual void BeginPlay() override;
	virtual void TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction) override;
	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const override;

	UFUNCTION()
	void OnRep_BruiseState(EHPBruiseState OldState);

private:
	TArray<FHPTouchRecord> TouchHistory;
	/** Freshness age accumulates only outside Dusk ("it's cold"). */
	float FreshnessAge = -1.f;
	bool bSold = false;
};
