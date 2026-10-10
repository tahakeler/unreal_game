// Copyright Epic Games, Inc. All Rights Reserved.

using UnrealBuildTool;

public class HarvestPanic : ModuleRules
{
	public HarvestPanic(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicDependencyModuleNames.AddRange(new string[] {
			"Core",
			"CoreUObject",
			"Engine",
			"InputCore",
			"EnhancedInput",
			"AIModule",
			"StateTreeModule",
			"GameplayStateTreeModule",
			"UMG",
			"Slate",
			"SlateCore",
			"GameplayTags",
			"NetCore"
		});

		PrivateDependencyModuleNames.AddRange(new string[] { "OnlineSubsystem", "OnlineSubsystemUtils" });

		PublicIncludePaths.AddRange(new string[] {
			"HarvestPanic",
			"HarvestPanic/Foundation",
			"HarvestPanic/Variant_Platforming",
			"HarvestPanic/Variant_Platforming/Animation",
			"HarvestPanic/Variant_Combat",
			"HarvestPanic/Variant_Combat/AI",
			"HarvestPanic/Variant_Combat/Animation",
			"HarvestPanic/Variant_Combat/Gameplay",
			"HarvestPanic/Variant_Combat/Interfaces",
			"HarvestPanic/Variant_Combat/UI",
			"HarvestPanic/Variant_SideScrolling",
			"HarvestPanic/Variant_SideScrolling/AI",
			"HarvestPanic/Variant_SideScrolling/Gameplay",
			"HarvestPanic/Variant_SideScrolling/Interfaces",
			"HarvestPanic/Variant_SideScrolling/UI"
		});

		// Uncomment if you are using Slate UI
		// PrivateDependencyModuleNames.AddRange(new string[] { "Slate", "SlateCore" });

		// Uncomment if you are using online features
		// PrivateDependencyModuleNames.Add("OnlineSubsystem");

		// To include OnlineSubsystemSteam, add it to the plugins section in your uproject file with the Enabled attribute set to true
	}
}
