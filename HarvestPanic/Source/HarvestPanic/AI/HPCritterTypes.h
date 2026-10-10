// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/critter-ai.md -- shared enums, native gameplay tags and log category.
#pragma once

#include "CoreMinimal.h"
#include "NativeGameplayTags.h"
#include "HPCritterTypes.generated.h"

HARVESTPANIC_API DECLARE_LOG_CATEGORY_EXTERN(LogHPCritter, Log, All);

namespace HPCritterTags
{
	HARVESTPANIC_API UE_DECLARE_GAMEPLAY_TAG_EXTERN(Trait_Skittish);
	HARVESTPANIC_API UE_DECLARE_GAMEPLAY_TAG_EXTERN(Trait_Greedy);
	HARVESTPANIC_API UE_DECLARE_GAMEPLAY_TAG_EXTERN(Trait_Sneaky);
	HARVESTPANIC_API UE_DECLARE_GAMEPLAY_TAG_EXTERN(Trait_Dramatic);
	HARVESTPANIC_API UE_DECLARE_GAMEPLAY_TAG_EXTERN(Trait_Herd);
	HARVESTPANIC_API UE_DECLARE_GAMEPLAY_TAG_EXTERN(Trait_Loud);
	HARVESTPANIC_API UE_DECLARE_GAMEPLAY_TAG_EXTERN(Noise_Food);
	HARVESTPANIC_API UE_DECLARE_GAMEPLAY_TAG_EXTERN(Noise_Whistle);
	HARVESTPANIC_API UE_DECLARE_GAMEPLAY_TAG_EXTERN(Noise_WhistleLong);
	HARVESTPANIC_API UE_DECLARE_GAMEPLAY_TAG_EXTERN(Noise_Squeal);
	HARVESTPANIC_API UE_DECLARE_GAMEPLAY_TAG_EXTERN(Lure_FeedBucket);
	HARVESTPANIC_API UE_DECLARE_GAMEPLAY_TAG_EXTERN(Lure_Whistle);
	HARVESTPANIC_API UE_DECLARE_GAMEPLAY_TAG_EXTERN(Nav_UnderFence);
	HARVESTPANIC_API UE_DECLARE_GAMEPLAY_TAG_EXTERN(Comedy_Screams);
}

UENUM(BlueprintType)
enum class ECritterState : uint8
{
	Idle, Wander, Alert, Flee, Lured, Cornered, Carried, Stunned, Calm, Escaped
};

UENUM(BlueprintType)
enum class ECritterCorneredResponse : uint8 { Freeze, Charge, Juke, PlayDead, Burrow, Teleport };

UENUM(BlueprintType)
enum class ECritterSizeClass : uint8 { Tiny, Small, Medium, Large, Huge };

UENUM(BlueprintType)
enum class ECritterHerdMode : uint8 { None, Flock, Follow, Guard };

UENUM(BlueprintType)
enum class ECritterActivePhase : uint8 { Day, Night, Both };

UENUM(BlueprintType)
enum class ECritterNoiseResponse : uint8 { Lure, Flee, Rally, Ignore };

HARVESTPANIC_API const TCHAR* LexCritterState(ECritterState State);
