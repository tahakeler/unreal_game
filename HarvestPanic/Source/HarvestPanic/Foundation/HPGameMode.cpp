// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPGameMode.h"
#include "HPGameState.h"
#include "HPPlayerState.h"

AHPGameMode::AHPGameMode()
{
	GameStateClass = AHPGameState::StaticClass();
	PlayerStateClass = AHPPlayerState::StaticClass();
}
