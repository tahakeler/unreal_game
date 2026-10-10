// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPGameMode.h"
#include "HPCharacter.h"
#include "HPGameState.h"
#include "HPPlayerState.h"
#include "GameFramework/PlayerController.h"

AHPGameMode::AHPGameMode()
{
	GameStateClass = AHPGameState::StaticClass();
	PlayerStateClass = AHPPlayerState::StaticClass();
	DefaultPawnClass = AHPCharacter::StaticClass();
	PlayerControllerClass = APlayerController::StaticClass(); // template PC is abstract
}
