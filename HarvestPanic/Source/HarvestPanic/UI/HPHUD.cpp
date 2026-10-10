// Copyright HARVEST PANIC team. All Rights Reserved.
#include "UI/HPHUD.h"
#include "UI/HPDayHUDWidget.h"
#include "UI/HPPayoutWidget.h"
#include "GameFramework/PlayerController.h"

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
