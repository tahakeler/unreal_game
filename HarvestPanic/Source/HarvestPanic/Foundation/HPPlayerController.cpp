// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPPlayerController.h"
#include "Frontend/HPFrontendUISubsystem.h"
#include "GameFramework/PlayerInput.h"

void AHPPlayerController::BeginPlay()
{
	Super::BeginPlay();
	if (IsLocalController())
	{
		if (UHPFrontendUISubsystem* UI = UHPFrontendUISubsystem::Get(this))
		{
			UI->ShowRoot(EHPMenuState::InGame);
		}
	}
}

void AHPPlayerController::SetupInputComponent()
{
	Super::SetupInputComponent();
	if (InputComponent)
	{
		InputComponent->BindKey(EKeys::Escape, IE_Pressed, this, &AHPPlayerController::OnPausePressed).bExecuteWhenPaused = true;
		InputComponent->BindKey(EKeys::Gamepad_Special_Right, IE_Pressed, this, &AHPPlayerController::OnPausePressed);
	}
}

void AHPPlayerController::OnPausePressed()
{
	if (UHPFrontendUISubsystem* UI = UHPFrontendUISubsystem::Get(this))
	{
		UI->HandleBackKey();
	}
}
