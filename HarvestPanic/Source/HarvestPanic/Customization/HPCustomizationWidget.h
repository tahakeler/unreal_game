// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/character-customization.md (wardrobe UI).
// Pure C++ UMG wardrobe (no .uasset). Embeddable by the frontend via
// StaticLoadClass(UUserWidget::StaticClass(), nullptr, TEXT("/Script/HarvestPanic.HPCustomizationWidget")).
#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "HPCosmeticTypes.h"
#include "HPCustomizationWidget.generated.h"

class UButton;
class UTextBlock;
class UImage;
class UWidgetSwitcher;
class UPanelWidget;
class UUniformGridPanel;
class AHPCustomizationPreview;
class UHPCustomizationWidget;

UENUM()
enum class EHPWardrobeAction : uint8
{
	Tab, BodyPrev, BodyNext, Skin, ColorTarget, Swatch, Item, ItemPrev, ItemNext, Randomize, Confirm, Cancel, SpinLeft, SpinRight
};

/** Routes a UButton click (dynamic delegate, no payload) back to the widget with an action + args. */
UCLASS()
class HARVESTPANIC_API UHPWardrobeClickProxy : public UObject
{
	GENERATED_BODY()
public:
	TWeakObjectPtr<UHPCustomizationWidget> Owner;
	EHPWardrobeAction Action = EHPWardrobeAction::Tab;
	int32 Arg = 0;
	int32 Arg2 = 0;
	UFUNCTION() void HandleClicked();
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FHPOnWardrobeConfirmed, const FHPCosmeticLoadout&, Loadout);
DECLARE_DYNAMIC_MULTICAST_DELEGATE(FHPOnWardrobeClosed);

UCLASS()
class HARVESTPANIC_API UHPCustomizationWidget : public UUserWidget
{
	GENERATED_BODY()

public:
	/** Fired on Confirm (after saving + sending to the server). */
	UPROPERTY(BlueprintAssignable, Category="Wardrobe") FHPOnWardrobeConfirmed OnConfirmed;
	/** Fired when the widget closes itself (Confirm or Cancel). */
	UPROPERTY(BlueprintAssignable, Category="Wardrobe") FHPOnWardrobeClosed OnClosed;

	/** Remove from parent on Confirm/Cancel (frontend may set false and manage itself). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Wardrobe") bool bCloseOnConfirm = true;
	/** Set by the in-world wardrobe: restore Game-only input + hide cursor on close. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Wardrobe") bool bRestoreGameInputOnClose = false;

	UFUNCTION(BlueprintCallable, Category="Wardrobe") void SetWorkingLoadout(const FHPCosmeticLoadout& InLoadout);
	UFUNCTION(BlueprintPure, Category="Wardrobe") FHPCosmeticLoadout GetWorkingLoadout() const { return Working; }
	UFUNCTION(BlueprintCallable, Category="Wardrobe") void Randomize();
	UFUNCTION(BlueprintCallable, Category="Wardrobe") void Confirm();
	UFUNCTION(BlueprintCallable, Category="Wardrobe") void Cancel();
	void SelectTab(int32 Index);

	void HandleAction(EHPWardrobeAction Action, int32 Arg, int32 Arg2);

protected:
	virtual void NativeOnInitialized() override;
	virtual void NativeConstruct() override;
	virtual void NativeDestruct() override;

private:
	void BuildUI();
	UButton* MakeButton(UPanelWidget* Parent, const FText& Label, EHPWardrobeAction Action, int32 Arg, int32 Arg2, FLinearColor Color, float FontSize, UTextBlock** OutText = nullptr);
	UTextBlock* MakeText(UPanelWidget* Parent, const FText& Text, float Size, FLinearColor Color = FLinearColor::White);
	UWidget* BuildBodyPage();
	UWidget* BuildColorsPage();
	UWidget* BuildSlotPage(EHPCosmeticSlot Slot);
	UWidget* BuildTagPage();
	UUniformGridPanel* BuildSwatchGrid(UPanelWidget* Parent, const TArray<struct FHPNamedColor>& Colors, EHPWardrobeAction Action, int32 Columns, TArray<TObjectPtr<UButton>>& OutButtons);
	void Refresh();
	void CycleItem(EHPCosmeticSlot Slot, int32 Dir);
	void Close();
	uint8& ColorTargetRef();

	FHPCosmeticLoadout Working;
	int32 ActiveTab = 0;
	int32 ColorTarget = 0; // 0 overalls, 1 shirt, 2 accent
	bool bBuilt = false;

	UPROPERTY(Transient) TArray<TObjectPtr<UHPWardrobeClickProxy>> Proxies;
	UPROPERTY(Transient) TObjectPtr<AHPCustomizationPreview> Preview;
	UPROPERTY(Transient) TObjectPtr<UImage> PreviewImage;
	UPROPERTY(Transient) TObjectPtr<UWidgetSwitcher> Pages;
	UPROPERTY(Transient) TArray<TObjectPtr<UButton>> TabButtons;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> BodyText;
	UPROPERTY(Transient) TArray<TObjectPtr<UButton>> SkinButtons;
	UPROPERTY(Transient) TArray<TObjectPtr<UButton>> ColorTargetButtons;
	UPROPERTY(Transient) TArray<TObjectPtr<UButton>> SwatchButtons;
	UPROPERTY(Transient) TArray<TObjectPtr<UButton>> TagButtons;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> TagPreviewText;
	UPROPERTY(Transient) TObjectPtr<UTextBlock> SummaryText;
	UPROPERTY(Transient) TArray<TObjectPtr<UTextBlock>> SlotNameText;
	/** Per slot: item buttons and the NetId they select. */
	UPROPERTY(Transient) TArray<TObjectPtr<UButton>> ItemButtons;
	TArray<TPair<EHPCosmeticSlot, uint16>> ItemButtonIds;
};
