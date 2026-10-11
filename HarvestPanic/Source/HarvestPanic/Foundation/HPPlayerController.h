// Copyright HARVEST PANIC team. All Rights Reserved.
// Day-map player controller: shows the in-game frontend root and routes Esc / Gamepad Start to the pause overlay.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/PlayerController.h"
#include "HPPlayerController.generated.h"

UCLASS()
class HARVESTPANIC_API AHPPlayerController : public APlayerController
{
	GENERATED_BODY()

protected:
	virtual void BeginPlay() override;
	virtual void SetupInputComponent() override;

private:
	void OnPausePressed();
};
