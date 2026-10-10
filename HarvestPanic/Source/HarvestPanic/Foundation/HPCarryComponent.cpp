// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPCarryComponent.h"

UHPCarryComponent::UHPCarryComponent()
{
	PrimaryComponentTick.bCanEverTick = false;
	SetIsReplicatedByDefault(true);
}
