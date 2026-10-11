// Copyright HARVEST PANIC team. All Rights Reserved.
// Self-registering glue for character customization. No other system needs to know
// about it: on the server it adds UHPCosmeticsComponent to every PlayerState; on each
// client it adds a high-priority input mapping context (5-8 emotes, C wardrobe).
#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "Containers/Ticker.h"
#include "HPCosmeticTypes.h"
#include "HPCosmeticsComponent.h"
#include "HPCustomizationSubsystem.generated.h"

class AGameModeBase;
class APlayerController;
class UInputAction;
class UInputMappingContext;
class UEnhancedInputComponent;
class UHPCustomizationWidget;

UCLASS()
class HARVESTPANIC_API UHPCustomizationSubsystem : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;
	virtual void Deinitialize() override;

	/** Open the wardrobe for a local player (in-game: game input is restored on close). */
	UHPCustomizationWidget* OpenWardrobe(APlayerController* PC);

	/** QA: spawn a lineup of 4 farmers + open the widget and take offscreen screenshots. */
	void RunShowcase(UWorld* World, const FString& OutDir, bool bQuitAfter);

	static constexpr int32 InputPriority = 100;

private:
	void HandlePostLogin(AGameModeBase* GameMode, APlayerController* NewPlayer);
	bool HandleTick(float DeltaTime);
	void EnsureServerComponents(UWorld* World);
	void EnsureLocalInput(UWorld* World);
	void BuildInputAssets();

	void OnEmote1() { TriggerEmote(EHPEmote::Wave); }
	void OnEmote2() { TriggerEmote(EHPEmote::Point); }
	void OnEmote3() { TriggerEmote(EHPEmote::Dance); }
	void OnEmote4() { TriggerEmote(EHPEmote::Faint); }
	void OnWardrobeKey();
	void TriggerEmote(EHPEmote Emote);

	FDelegateHandle PostLoginHandle;
	FTSTicker::FDelegateHandle TickHandle;
	float EnsureTimer = 0.f;

	UPROPERTY(Transient) TObjectPtr<UInputMappingContext> MappingContext;
	UPROPERTY(Transient) TArray<TObjectPtr<UInputAction>> EmoteActions;
	UPROPERTY(Transient) TObjectPtr<UInputAction> WardrobeAction;
	UPROPERTY(Transient) TObjectPtr<UEnhancedInputComponent> InputComp;
	TWeakObjectPtr<APlayerController> InputPC;
	TWeakObjectPtr<UHPCustomizationWidget> OpenWidget;

	// Showcase-only state
	TArray<FHPEmotePlayback> ShowcaseEmotes;
};
