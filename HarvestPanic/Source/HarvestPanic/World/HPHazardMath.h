// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/bridge-hazards.md section 4 (plank damage). Pure functions so they are unit-testable.
#pragma once

#include "CoreMinimal.h"

namespace HPHazardMath
{
	/**
	 * damage_per_s = max(0, load - SafeLoad * SafeLoadMult) * DamageRate * (1 + 0.5 * cracked) * BreakMult.
	 * @param bCracked true once integrity <= the crack threshold.
	 */
	inline float PlankDamagePerSecond(float LoadKg, float SafeLoadKg, float DamageRate, bool bCracked, float SafeLoadMult = 1.f, float BreakMult = 1.f)
	{
		return FMath::Max(0.f, LoadKg - SafeLoadKg * SafeLoadMult) * DamageRate * (bCracked ? 1.5f : 1.f) * BreakMult;
	}
}
