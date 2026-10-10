// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/hud-menus.md (Payout Receipt), blame-ledger.md (receipt rules),
//      week-quota.md (Sunday result). Code-built UMG; visible in Payout and Sunday phases.
#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "HPPayoutWidget.generated.h"

class UVerticalBox;
class UButton;
class UTextBlock;
class AHPGameState;

UCLASS()
class HARVESTPANIC_API UHPPayoutWidget : public UUserWidget
{
	GENERATED_BODY()

protected:
	virtual TSharedRef<SWidget> RebuildWidget() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;

private:
	void BuildTree();
	void RebuildReceipt(const AHPGameState* GS);
	void AddRow(const FString& Left, const FString& Right, const FLinearColor& Color, int32 Size = 16, bool bBold = false);
	void SetMenuInput(bool bMenu);

	UFUNCTION() void OnNextClicked();
	UFUNCTION() void OnRestartClicked();

	UPROPERTY() TObjectPtr<UVerticalBox> ReceiptBox;
	UPROPERTY() TObjectPtr<UButton> NextButton;
	UPROPERTY() TObjectPtr<UTextBlock> NextLabel;
	UPROPERTY() TObjectPtr<UButton> RestartButton;
	UPROPERTY() TObjectPtr<UTextBlock> HostNote;

	/** Phase/day/event-count key so the receipt rebuilds only when data changes. */
	int64 BuiltKey = -1;
	bool bMenuInput = false;
};
