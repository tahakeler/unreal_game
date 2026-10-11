// Copyright HARVEST PANIC team. All Rights Reserved.
#include "World/HPBarnRespawn.h"
#include "Components/ArrowComponent.h"
#include "Engine/World.h"
#include "EngineUtils.h"

AHPBarnRespawn::AHPBarnRespawn()
{
	Arrow = CreateDefaultSubobject<UArrowComponent>(TEXT("Arrow"));
	RootComponent = Arrow;
	Arrow->ArrowSize = 2.f;
}

FTransform AHPBarnRespawn::GetRespawnTransform(int32 SlotIndex) const
{
	const float Angle = FMath::DegreesToRadians(90.f * (FMath::Abs(SlotIndex) % 4));
	const FVector Offset = GetActorRotation().RotateVector(FVector(FMath::Cos(Angle), FMath::Sin(Angle), 0.f) * SlotRadiusCm);
	return FTransform(GetActorRotation(), GetActorLocation() + Offset + FVector(0.f, 0.f, 100.f));
}

AHPBarnRespawn* AHPBarnRespawn::Find(const UWorld* World)
{
	if (!World)
	{
		return nullptr;
	}
	TActorIterator<AHPBarnRespawn> It(const_cast<UWorld*>(World));
	return It ? *It : nullptr;
}
