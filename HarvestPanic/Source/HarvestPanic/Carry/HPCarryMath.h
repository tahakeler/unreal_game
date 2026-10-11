// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/physics-carry.md (Formulas), player-character.md (ragdoll threshold),
//      blame-ledger.md rule 4 (touch shares 0.6/0.3/0.1, TouchWindow 3 s).
// Pure, side-effect-free math so it can be unit tested without a world.
// Every tuning value is passed in by the caller (callers read EditDefaultsOnly properties).
#pragma once

#include "CoreMinimal.h"

/** One entry in an item's touch history (server only). */
struct HARVESTPANIC_API FHPCarryTouchRecord
{
	/** APlayerState::GetPlayerId(); INDEX_NONE = nobody. */
	int32 PlayerId = INDEX_NONE;
	/** Server time in seconds. */
	double Time = 0.0;
};

/** Rolling touch history; newest entry last internally, queried newest-first. */
struct HARVESTPANIC_API FHPTouchHistory
{
	TArray<FHPCarryTouchRecord> Records;

	/** Records a touch. A player touching again moves to the front (newest). Keeps at most MaxKept. */
	void AddTouch(int32 PlayerId, double Now, int32 MaxKept = 8);

	/** Distinct players who touched within Window seconds of Now, newest first, at most MaxCount. */
	TArray<int32> GetRecentToucherIds(double Now, double Window, int32 MaxCount) const;
};

namespace HPCarryMath
{
	/** Per-player load ratio = mass / (hands * KgPerHand), clamped 0..1. 0 hands -> 1 (dragging dead weight). */
	HARVESTPANIC_API float LoadRatio(float MassKg, int32 HandsAttached, float KgPerHand);

	/**
	 * Carry speed multiplier.
	 * GDD: carry_speed = clamp(1 - 0.5 * mass / (hands * KgPerHand), Floor, 1).
	 * Group-carry gate (lead spec): while hands < required the item is dragged and the speed is
	 * additionally capped by hands / required. Result = clamp(min(both), Floor, 1).
	 */
	HARVESTPANIC_API float CarrySpeedMultiplier(float MassKg, int32 HandsAttached, int32 HandsRequired,
		float KgPerHand, float Floor);

	/** GDD: v = lerp(Min, Max, charge) * clamp(RefMass / mass, MassFactorMin, 1) in m/s. */
	HARVESTPANIC_API float ThrowSpeed(float Charge01, float MassKg, float SpeedMin, float SpeedMax,
		float RefMassKg, float MassFactorMin);

	/**
	 * Blame shares for N touchers (newest first), renormalized from BaseShares (e.g. 0.6/0.3/0.1).
	 * Empty result for N == 0 ("Nature").
	 */
	HARVESTPANIC_API TArray<float> TouchShares(int32 NumTouchers, TConstArrayView<float> BaseShares);

	/** GDD player-character: threshold = Base * (1 - LoadFactor*load) * (stamina < TiredStamina ? TiredMult : 1) * Mud. */
	HARVESTPANIC_API float RagdollThreshold(float BaseImpulseNs, float LoadRatio01, float LoadFactor,
		float Stamina, float TiredStamina, float TiredMult, float MudMult);
}
