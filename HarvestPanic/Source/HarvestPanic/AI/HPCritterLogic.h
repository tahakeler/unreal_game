// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/critter-ai.md section 4 (Formulas). Pure, stateless, unit-tested.
// All distances in metres unless suffixed Cm.
#pragma once

#include "CoreMinimal.h"
#include "HPCritterLogic.generated.h"

/** Threat term weights (critter-ai 4). Data-tunable via the personality asset. */
USTRUCT(BlueprintType)
struct HARVESTPANIC_API FHPThreatWeights
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Threat") float Proximity = 0.5f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Threat") float Sprint = 0.2f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Threat") float Sight = 0.15f;
	/** Weight of non-max sources in the total. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Threat") float OtherSources = 0.25f;
};

struct HARVESTPANIC_API FHPCritterLogic
{
	/** P = 1 - d/AlertRadius, 0 beyond radius. */
	static float ProximityTerm(float DistM, float AlertRadiusM);

	/** T_src = clamp(P*w_prox + S*w_sprint + V*w_sight + N + D, 0, 1). Sprint/sight only count inside AlertRadius. */
	static float SourceThreat(float DistM, float AlertRadiusM, bool bSprinting, bool bInSight,
		float NoiseTerm, float DamageTerm, const FHPThreatWeights& W = FHPThreatWeights());

	/** N = Loudness * Sensitivity * SourceMult * (1 - d/Radius), floored at 0. */
	static float PerceivedNoise(float Loudness, float Sensitivity, float SourceMult, float DistM, float RadiusM);

	/** T = max(T_src) + w_other * sum(others), clamped 0..1. */
	static float TotalThreat(TConstArrayView<float> SourceThreats, float OtherWeight = 0.25f);

	/** LureAffinity * (1 - d/15m), floored at 0. */
	static float LureScore(float Affinity, float DistM, float LureRangeM = 15.f);

	/** Lure wins if LureScore > T * 0.8. */
	static bool LureWins(float Affinity, float DistM, float Threat, float LureRangeM = 15.f);

	/**
	 * P_break = clamp((Secure - carriers) * 0.35 + 0.05, 0, 0.9) per struggle interval.
	 * Returns 0 when Carriers == 0 (not carried) or Carriers >= SecureCarriers (GDD AC5: "by 2 players, never").
	 */
	static float BreakFreeChance(int32 SecureCarriers, int32 Carriers);

	/**
	 * Unit 2D direction away from the weighted threat centroid. If threats surround the critter
	 * (centroid ~ on top of it) returns a direction perpendicular to the strongest threat axis
	 * and sets bOutAmbiguous.
	 */
	static FVector FleeDirection(const FVector& From, TConstArrayView<FVector> ThreatLocations,
		TConstArrayView<float> Weights, bool* bOutAmbiguous = nullptr);

	/** Linear decay toward 0. */
	static float Decay(float Value, float RatePerSec, float DeltaSec) { return FMath::Max(0.f, Value - RatePerSec * DeltaSec); }
};
