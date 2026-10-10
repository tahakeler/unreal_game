// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/hud-menus.md — owns the local player's day HUD and payout receipt widgets.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/HUD.h"
#include "HPHUD.generated.h"

class UHPDayHUDWidget;
class UHPPayoutWidget;

UCLASS()
class HARVESTPANIC_API AHPHUD : public AHUD
{
	GENERATED_BODY()

protected:
	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;

private:
	UPROPERTY() TObjectPtr<UHPDayHUDWidget> DayWidget;
	UPROPERTY() TObjectPtr<UHPPayoutWidget> PayoutWidget;
};
