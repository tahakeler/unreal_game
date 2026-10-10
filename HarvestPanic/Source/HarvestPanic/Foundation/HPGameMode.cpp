// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPGameMode.h"
#include "HPCharacter.h"
#include "HPGameState.h"
#include "HPPlayerState.h"
#include "HarvestPanicPlayerController.h"

AHPGameMode::AHPGameMode()
{
	GameStateClass = AHPGameState::StaticClass();
	PlayerStateClass = AHPPlayerState::StaticClass();
	DefaultPawnClass = AHPCharacter::StaticClass();
	PlayerControllerClass = AHarvestPanicPlayerController::StaticClass();
}
