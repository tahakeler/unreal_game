// Copyright HARVEST PANIC team. All Rights Reserved.
#include "UI/HPHUD.h"
#include "UI/HPDayHUDWidget.h"
#include "UI/HPPayoutWidget.h"
#include "GameFramework/PlayerController.h"
#include "HPGameState.h"
#include "Engine/World.h"

void AHPHUD::BeginPlay()
{
	Super::BeginPlay();
	APlayerController* PC = GetOwningPlayerController();
	if (!PC || !PC->IsLocalController())
	{
		return;
	}
	DayWidget = CreateWidget<UHPDayHUDWidget>(PC, UHPDayHUDWidget::StaticClass());
	if (DayWidget)
	{
		DayWidget->AddToViewport(10);
	}
	PayoutWidget = CreateWidget<UHPPayoutWidget>(PC, UHPPayoutWidget::StaticClass());
	if (PayoutWidget)
	{
		PayoutWidget->AddToViewport(20);
	}
}

void AHPHUD::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	const AHPGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHPGameState>() : nullptr;
	if (!GS)
	{
		return;
	}
	// Widgets can't toggle themselves back on (collapsed widgets don't tick), so the HUD owns visibility.
	const bool bDay = GS->Phase == EHPDayPhase::Morning || GS->Phase == EHPDayPhase::Workday || GS->Phase == EHPDayPhase::Dusk;
	const bool bPayout = GS->Phase == EHPDayPhase::Payout || GS->Phase == EHPDayPhase::Sunday;
	if (DayWidget)
	{
		DayWidget->SetVisibility(bDay ? ESlateVisibility::HitTestInvisible : ESlateVisibility::Collapsed);
	}
	if (PayoutWidget)
	{
		PayoutWidget->SetVisibility(bPayout ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);
		PayoutWidget->SetMenuInput(bPayout);
	}
}
