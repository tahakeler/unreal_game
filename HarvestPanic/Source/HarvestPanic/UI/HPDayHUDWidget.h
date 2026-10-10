// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/hud-menus.md — day HUD: wallet + quota progress (top-center),
// day clock/phase (top-right), Morning Order card (left). Built in code via WidgetTree.
#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "HPDayHUDWidget.generated.h"

class UTextBlock;
class UProgressBar;
class UVerticalBox;

UCLASS()
class HARVESTPANIC_API UHPDayHUDWidget : public UUserWidget
{
	GENERATED_BODY()

protected:
	virtual TSharedRef<SWidget> RebuildWidget() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;

private:
	void BuildTree();

	UPROPERTY() TObjectPtr<UTextBlock> WalletText;
	UPROPERTY() TObjectPtr<UTextBlock> QuotaText;
	UPROPERTY() TObjectPtr<UProgressBar> QuotaBar;
	UPROPERTY() TObjectPtr<UTextBlock> DayText;
	UPROPERTY() TObjectPtr<UTextBlock> PhaseText;
	UPROPERTY() TObjectPtr<UTextBlock> ClockText;
	UPROPERTY() TObjectPtr<UTextBlock> OrderBonusText;
	UPROPERTY() TArray<TObjectPtr<UTextBlock>> OrderLineTexts;
};
