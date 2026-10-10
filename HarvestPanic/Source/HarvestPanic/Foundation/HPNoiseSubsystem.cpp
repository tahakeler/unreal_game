// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPNoiseSubsystem.h"
#include "Engine/World.h"

float UHPNoiseSubsystem::RadiusForLoudness(float Loudness)
{
	const float L = FMath::Clamp(Loudness, 0.f, 1.f);
	return 4.f + 46.f * FMath::Pow(L, 1.5f);
}

void UHPNoiseSubsystem::Emit(FNoiseEvent Event)
{
	UWorld* World = GetWorld();
	if (!World || World->GetNetMode() == NM_Client)
	{
		return; // contract rule 1: server only
	}
	Event.EventId = FGuid::NewGuid();
	Event.Radius = RadiusForLoudness(Event.Loudness);
	Event.ServerTime = World->GetTimeSeconds();
	OnNoise.Broadcast(Event);
}
