// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPCritterLogic.h"

float FHPCritterLogic::ProximityTerm(float DistM, float AlertRadiusM)
{
	if (AlertRadiusM <= KINDA_SMALL_NUMBER || DistM >= AlertRadiusM)
	{
		return 0.f;
	}
	return FMath::Clamp(1.f - DistM / AlertRadiusM, 0.f, 1.f);
}

float FHPCritterLogic::SourceThreat(float DistM, float AlertRadiusM, bool bSprinting, bool bInSight,
	float NoiseTerm, float DamageTerm, const FHPThreatWeights& W)
{
	const bool bInRadius = DistM < AlertRadiusM;
	const float P = ProximityTerm(DistM, AlertRadiusM);
	const float S = (bSprinting && bInRadius) ? 1.f : 0.f;
	const float V = (bInSight && bInRadius) ? 1.f : 0.f;
	return FMath::Clamp(P * W.Proximity + S * W.Sprint + V * W.Sight + FMath::Max(0.f, NoiseTerm) + FMath::Max(0.f, DamageTerm), 0.f, 1.f);
}

float FHPCritterLogic::PerceivedNoise(float Loudness, float Sensitivity, float SourceMult, float DistM, float RadiusM)
{
	if (RadiusM <= KINDA_SMALL_NUMBER)
	{
		return 0.f;
	}
	const float Falloff = 1.f - DistM / RadiusM;
	return FMath::Max(0.f, Loudness * Sensitivity * SourceMult * Falloff);
}

float FHPCritterLogic::TotalThreat(TConstArrayView<float> SourceThreats, float OtherWeight)
{
	if (SourceThreats.Num() == 0)
	{
		return 0.f;
	}
	float Max = 0.f;
	float Sum = 0.f;
	for (float T : SourceThreats)
	{
		Max = FMath::Max(Max, T);
		Sum += T;
	}
	return FMath::Clamp(Max + OtherWeight * (Sum - Max), 0.f, 1.f);
}

float FHPCritterLogic::LureScore(float Affinity, float DistM, float LureRangeM)
{
	if (LureRangeM <= KINDA_SMALL_NUMBER)
	{
		return 0.f;
	}
	return FMath::Max(0.f, Affinity * (1.f - DistM / LureRangeM));
}

bool FHPCritterLogic::LureWins(float Affinity, float DistM, float Threat, float LureRangeM)
{
	return LureScore(Affinity, DistM, LureRangeM) > Threat * 0.8f;
}

float FHPCritterLogic::BreakFreeChance(int32 SecureCarriers, int32 Carriers)
{
	if (Carriers <= 0 || Carriers >= SecureCarriers)
	{
		return 0.f;
	}
	return FMath::Clamp((SecureCarriers - Carriers) * 0.35f + 0.05f, 0.f, 0.9f);
}

FVector FHPCritterLogic::FleeDirection(const FVector& From, TConstArrayView<FVector> ThreatLocations,
	TConstArrayView<float> Weights, bool* bOutAmbiguous)
{
	if (bOutAmbiguous) { *bOutAmbiguous = false; }
	FVector Away = FVector::ZeroVector;
	FVector Strongest = FVector::ZeroVector;
	float StrongestW = -1.f;
	for (int32 i = 0; i < ThreatLocations.Num(); ++i)
	{
		const float Wt = Weights.IsValidIndex(i) ? Weights[i] : 1.f;
		FVector D = From - ThreatLocations[i];
		D.Z = 0.f;
		const FVector Dir = D.GetSafeNormal();
		Away += Dir * Wt;
		if (Wt > StrongestW)
		{
			StrongestW = Wt;
			Strongest = Dir;
		}
	}
	if (ThreatLocations.Num() == 0)
	{
		return FVector::ZeroVector;
	}
	const float Total = FMath::Max(KINDA_SMALL_NUMBER, [&]{ float S = 0.f; for (int32 i = 0; i < ThreatLocations.Num(); ++i) S += Weights.IsValidIndex(i) ? Weights[i] : 1.f; return S; }());
	if (Away.Size() / Total < 0.2f)
	{
		// Threats on both sides: flee perpendicular to the threat axis (critter-ai 5).
		if (bOutAmbiguous) { *bOutAmbiguous = true; }
		const FVector Perp(-Strongest.Y, Strongest.X, 0.f);
		return Perp.IsNearlyZero() ? FVector::ForwardVector : Perp.GetSafeNormal();
	}
	return Away.GetSafeNormal2D();
}
