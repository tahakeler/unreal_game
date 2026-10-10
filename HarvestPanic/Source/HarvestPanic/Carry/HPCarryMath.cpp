// Copyright HARVEST PANIC team. All Rights Reserved.
#include "Carry/HPCarryMath.h"

void FHPTouchHistory::AddTouch(int32 PlayerId, double Now, int32 MaxKept)
{
	if (PlayerId == INDEX_NONE)
	{
		return;
	}
	Records.RemoveAll([PlayerId](const FHPTouchRecord& R) { return R.PlayerId == PlayerId; });
	Records.Add({PlayerId, Now});
	while (Records.Num() > FMath::Max(1, MaxKept))
	{
		Records.RemoveAt(0);
	}
}

TArray<int32> FHPTouchHistory::GetRecentToucherIds(double Now, double Window, int32 MaxCount) const
{
	TArray<int32> Out;
	for (int32 i = Records.Num() - 1; i >= 0 && Out.Num() < MaxCount; --i)
	{
		const FHPTouchRecord& R = Records[i];
		if (Now - R.Time <= Window)
		{
			Out.Add(R.PlayerId);
		}
	}
	return Out;
}

namespace HPCarryMath
{
	float LoadRatio(float MassKg, int32 HandsAttached, float KgPerHand)
	{
		if (HandsAttached <= 0 || KgPerHand <= 0.f)
		{
			return 1.f;
		}
		return FMath::Clamp(MassKg / (HandsAttached * KgPerHand), 0.f, 1.f);
	}

	float CarrySpeedMultiplier(float MassKg, int32 HandsAttached, int32 HandsRequired, float KgPerHand, float Floor)
	{
		if (HandsAttached <= 0)
		{
			return 1.f; // not carrying anything
		}
		const float Gdd = 1.f - 0.5f * (MassKg / (HandsAttached * FMath::Max(KgPerHand, KINDA_SMALL_NUMBER)));
		const float Gate = HandsRequired > 0 ? float(HandsAttached) / float(HandsRequired) : 1.f;
		return FMath::Clamp(FMath::Min(Gdd, Gate), Floor, 1.f);
	}

	float ThrowSpeed(float Charge01, float MassKg, float SpeedMin, float SpeedMax, float RefMassKg, float MassFactorMin)
	{
		const float Charge = FMath::Clamp(Charge01, 0.f, 1.f);
		const float MassFactor = FMath::Clamp(RefMassKg / FMath::Max(MassKg, KINDA_SMALL_NUMBER), MassFactorMin, 1.f);
		return FMath::Lerp(SpeedMin, SpeedMax, Charge) * MassFactor;
	}

	TArray<float> TouchShares(int32 NumTouchers, TConstArrayView<float> BaseShares)
	{
		TArray<float> Out;
		const int32 N = FMath::Min(NumTouchers, BaseShares.Num());
		float Sum = 0.f;
		for (int32 i = 0; i < N; ++i)
		{
			Out.Add(BaseShares[i]);
			Sum += BaseShares[i];
		}
		if (Sum > 0.f)
		{
			for (float& S : Out)
			{
				S /= Sum;
			}
		}
		return Out;
	}

	float RagdollThreshold(float BaseImpulseNs, float LoadRatio01, float LoadFactor, float Stamina,
		float TiredStamina, float TiredMult, float MudMult)
	{
		const float Load = FMath::Clamp(LoadRatio01, 0.f, 1.f);
		return BaseImpulseNs * (1.f - LoadFactor * Load) * (Stamina < TiredStamina ? TiredMult : 1.f) * MudMult;
	}
}
