// HARVEST PANIC - small C++ widget building blocks with TFunction callbacks and visible focus.
#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "Components/Button.h"
#include "Components/Slider.h"
#include "HPWidgets.generated.h"

class UTextBlock;
class UVerticalBox;
class UHPFrontendUISubsystem;

/** Button that shows a highlight while it has keyboard/gamepad focus and calls a native lambda. */
UCLASS()
class HARVESTPANIC_API UHPButton : public UButton
{
	GENERATED_BODY()
public:
	UHPButton();
	TFunction<void()> OnHPClick;
	UPROPERTY() TObjectPtr<UTextBlock> Label;

	void SetLabel(const FText& Text);
	/** Called each tick by the owning panel to mirror focus into the hovered look. */
	void RefreshFocusVisual();

private:
	UFUNCTION() void HandleClicked();
	bool bShowingFocus = false;
};

/** Slider that calls a native lambda. */
UCLASS()
class HARVESTPANIC_API UHPSlider : public USlider
{
	GENERATED_BODY()
public:
	UHPSlider();
	TFunction<void(float)> OnHPChanged;
private:
	UFUNCTION() void HandleChanged(float V);
};

/**
 * Base for every frontend screen. Builds its tree in code (no .uasset), handles Back
 * (Esc / gamepad B / Start), first-focus, and a short fade-in.
 * Kept separate from game state on purpose: subclasses read from GameState/GameInstance and send
 * requests back through them. Shaped so a later port to UCommonActivatableWidget is mechanical.
 */
UCLASS(Abstract)
class HARVESTPANIC_API UHPMenuPanel : public UUserWidget
{
	GENERATED_BODY()

public:
	UPROPERTY() TWeakObjectPtr<UHPFrontendUISubsystem> Owner;

	/** Return true if handled. */
	TFunction<bool()> OnBackRequested;

	/** Gives focus to the first enabled button. */
	void FocusFirst();

	/** Back key (Esc / gamepad B), routed by the owning PlayerController. Return true if handled. */
	virtual bool HandleBackKey() { return OnBackRequested && OnBackRequested(); }
	/** Other keys routed by the PlayerController while this panel is active (R, B...). */
	virtual bool HandleKey(const FKey& Key) { return false; }

protected:
	virtual TSharedRef<SWidget> RebuildWidget() override;
	virtual void NativeConstruct() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;

	/** Subclasses build their tree here; WidgetTree is valid. */
	virtual void BuildTree() PURE_VIRTUAL(UHPMenuPanel::BuildTree, );

	/** Root layout: optional dim backdrop + centered warm card. Returns the card's content box. */
	UVerticalBox* MakeCard(const FText& Title, float Width, bool bDimBackground = true, const FText& Subtitle = FText::GetEmpty());
	/** Adds a full-width button to a vertical box. */
	UHPButton* AddButton(UVerticalBox* Box, const FText& Text, TFunction<void()> OnClick);
	class UHPGameInstance* GetHPGameInstance() const;

	UHPButton* MakeButton(const FText& Text, TFunction<void()> OnClick, int32 FontSize = 0);
	UHPSlider* MakeSliderRow(UVerticalBox* Parent, const FText& Label, float Min, float Max, float Step, float Value, TFunction<FText(float)> Format, TFunction<void(float)> OnChange);
	UHPButton* MakeToggleRow(UVerticalBox* Parent, const FText& Label, TFunction<bool()> Get, TFunction<void(bool)> Set, const FText& OnText, const FText& OffText);

	UPROPERTY() TArray<TObjectPtr<UHPButton>> Buttons;
	float FadeAlpha = 0.f;
	bool bBuilt = false;
};
