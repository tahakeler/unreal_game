// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPCharacter.h"
#include "HPCarryComponent.h"

AHPCharacter::AHPCharacter()
{
	bReplicates = true;
	CarryComponent = CreateDefaultSubobject<UHPCarryComponent>(TEXT("CarryComponent"));
}
