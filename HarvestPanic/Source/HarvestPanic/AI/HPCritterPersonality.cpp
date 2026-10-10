// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPCritterPersonality.h"

UHPCritterPersonality::UHPCritterPersonality()
{
	// critter-ai 3.5 defaults
	NoiseTagResponse.Add(HPCritterTags::Noise_Food, { ECritterNoiseResponse::Lure, 0.6f });
	NoiseTagResponse.Add(HPCritterTags::Noise_Whistle, { ECritterNoiseResponse::Flee, 0.8f });
	NoiseTagResponse.Add(HPCritterTags::Noise_WhistleLong, { ECritterNoiseResponse::Rally, 0.f });
}

FPrimaryAssetId UHPCritterPersonality::GetPrimaryAssetId() const
{
	return FPrimaryAssetId(TEXT("CritterPersonality"), Id.IsNone() ? GetFName() : Id);
}

float UHPCritterPersonality::GetNoiseSourceMult(ENoiseSource Source) const
{
	const float* M = NoiseSourceMult.Find(Source);
	return M ? *M : 1.f;
}

bool UHPCritterPersonality::FindNoiseTagResponse(const FGameplayTagContainer& Tags, FGameplayTag& OutTag, FHPNoiseTagResponse& OutResponse) const
{
	bool bFound = false;
	for (const FGameplayTag& Tag : Tags)
	{
		if (const FHPNoiseTagResponse* R = NoiseTagResponse.Find(Tag))
		{
			if (!bFound || R->Strength > OutResponse.Strength)
			{
				OutTag = Tag;
				OutResponse = *R;
				bFound = true;
			}
		}
	}
	return bFound;
}

void UHPCritterPersonality::ApplyPigDefaults(UHPCritterPersonality& P)
{
	P.Id = TEXT("Critter.Pig");
	P.Traits.AddTag(HPCritterTags::Trait_Greedy);
	P.Traits.AddTag(HPCritterTags::Trait_Sneaky);
	P.Traits.AddTag(HPCritterTags::Trait_Dramatic);
	P.MoveSpeed = 280.f;
	P.SprintSpeed = 560.f;
	P.AlertRadius = 10.f;
	P.NoiseSensitivity = 1.0f;
	P.CorneredResponse = ECritterCorneredResponse::Juke;
	P.LureAffinity.Add(HPCritterTags::Lure_FeedBucket, 0.9f);
	// livestock-roster: Noise.Food -> Lure at FeedBucket affinity; whistle short Flee / long Ignore
	P.NoiseTagResponse.Add(HPCritterTags::Noise_Food, { ECritterNoiseResponse::Lure, 0.9f });
	P.NoiseTagResponse.Add(HPCritterTags::Noise_Whistle, { ECritterNoiseResponse::Flee, 0.8f });
	P.NoiseTagResponse.Add(HPCritterTags::Noise_WhistleLong, { ECritterNoiseResponse::Ignore, 0.f });
	P.HerdMode = ECritterHerdMode::None;
	P.SizeClass = ECritterSizeClass::Medium;
	P.Agility = 3.f;
	P.CritterPullN = 500.f;
	P.SecureCarriers = 2;
	P.CarryHands = 2;
	P.StruggleImpulse = 150.f;
	P.ShortcutAccess.AddTag(HPCritterTags::Nav_UnderFence);
	P.GrabNoiseLoudness = 0.7f;
	P.GrabNoiseTag = HPCritterTags::Noise_Squeal;
	P.ComedyFlags.AddTag(HPCritterTags::Comedy_Screams);
}

void UHPCritterPersonality::ApplyChickenDefaults(UHPCritterPersonality& P)
{
	P.Id = TEXT("Critter.Chicken");
	P.Traits.AddTag(HPCritterTags::Trait_Skittish);
	P.Traits.AddTag(HPCritterTags::Trait_Herd);
	P.MoveSpeed = 250.f;
	P.SprintSpeed = 620.f;
	P.AlertRadius = 8.f;
	P.NoiseSensitivity = 1.4f;
	P.CorneredResponse = ECritterCorneredResponse::Juke;
	P.LureAffinity.Add(HPCritterTags::Lure_Whistle, 0.6f);
	P.LureAffinity.Add(HPCritterTags::Lure_FeedBucket, 0.5f);
	P.NoiseTagResponse.Add(HPCritterTags::Noise_Food, { ECritterNoiseResponse::Lure, 0.5f });
	P.NoiseTagResponse.Add(HPCritterTags::Noise_Whistle, { ECritterNoiseResponse::Flee, 0.8f });
	P.NoiseTagResponse.Add(HPCritterTags::Noise_WhistleLong, { ECritterNoiseResponse::Ignore, 0.f });
	P.HerdMode = ECritterHerdMode::Flock;
	P.SizeClass = ECritterSizeClass::Small;
	P.Agility = 4.f;
	P.CritterPullN = 50.f;
	P.SecureCarriers = 1;
	P.CarryHands = 1;
	P.StruggleImpulse = 40.f;
	P.FleeDistance = 10.f;
}

UHPCritterPersonality* UHPCritterPersonality::MakePig(UObject* Outer)
{
	return NewObject<UHPCritterPersonality_Pig>(Outer);
}

UHPCritterPersonality* UHPCritterPersonality::MakeChicken(UObject* Outer)
{
	return NewObject<UHPCritterPersonality_Chicken>(Outer);
}

UHPCritterPersonality_Pig::UHPCritterPersonality_Pig()
{
	ApplyPigDefaults(*this);
}

UHPCritterPersonality_Chicken::UHPCritterPersonality_Chicken()
{
	ApplyChickenDefaults(*this);
}
