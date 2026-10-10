// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPCritterTypes.h"

DEFINE_LOG_CATEGORY(LogHPCritter);

namespace HPCritterTags
{
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(Trait_Skittish, "Trait.Skittish", "Critter flees easily");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(Trait_Greedy, "Trait.Greedy", "Critter loves food lures");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(Trait_Sneaky, "Trait.Sneaky", "Critter uses shortcuts");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(Trait_Dramatic, "Trait.Dramatic", "Critter overreacts");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(Trait_Herd, "Trait.Herd", "Critter moves in groups");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(Trait_Loud, "Trait.Loud", "Critter makes noise");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(Noise_Food, "Noise.Food", "Feed/food noise (lure)");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(Noise_Whistle, "Noise.Whistle", "Short whistle");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(Noise_WhistleLong, "Noise.WhistleLong", "Long whistle (rally)");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(Noise_Squeal, "Noise.Squeal", "Critter squeal when grabbed");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(Lure_FeedBucket, "Lure.FeedBucket", "Feed bucket lure");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(Lure_Whistle, "Lure.Whistle", "Whistle lure");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(Nav_UnderFence, "Nav.UnderFence", "Can squeeze under fences");
	UE_DEFINE_GAMEPLAY_TAG_COMMENT(Comedy_Screams, "Comedy.Screams", "Screams comedically");
}

const TCHAR* LexCritterState(ECritterState State)
{
	switch (State)
	{
	case ECritterState::Idle: return TEXT("Idle");
	case ECritterState::Wander: return TEXT("Wander");
	case ECritterState::Alert: return TEXT("Alert");
	case ECritterState::Flee: return TEXT("Flee");
	case ECritterState::Lured: return TEXT("Lured");
	case ECritterState::Cornered: return TEXT("Cornered");
	case ECritterState::Carried: return TEXT("Carried");
	case ECritterState::Stunned: return TEXT("Stunned");
	case ECritterState::Calm: return TEXT("Calm");
	case ECritterState::Escaped: return TEXT("Escaped");
	}
	return TEXT("?");
}
