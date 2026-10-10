// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPCritterSpawner.h"
#include "HPCritter.h"
#include "HPNoiseSubsystem.h"
#include "Components/BillboardComponent.h"
#include "Components/CapsuleComponent.h"
#include "Engine/World.h"
#include "GameFramework/PlayerController.h"
#include "HAL/IConsoleManager.h"

AHPCritterSpawner::AHPCritterSpawner()
{
	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	CritterClass = AHPCritter_Pig::StaticClass();
}

void AHPCritterSpawner::BeginPlay()
{
	Super::BeginPlay();
	if (bSpawnOnBeginPlay && HasAuthority())
	{
		SpawnCritters();
	}
}

TArray<AHPCritter*> AHPCritterSpawner::SpawnCritters()
{
	return SpawnAround(GetWorld(), CritterClass, Count, GetActorLocation(), Radius);
}

TArray<AHPCritter*> AHPCritterSpawner::SpawnAround(UWorld* World, TSubclassOf<AHPCritter> Class, int32 Num, const FVector& Center, float RadiusCm)
{
	TArray<AHPCritter*> Out;
	if (!World || !Class || World->GetNetMode() == NM_Client)
	{
		return Out;
	}
	const float HalfHeight = Class->GetDefaultObject<AHPCritter>()->GetCapsuleComponent()->GetScaledCapsuleHalfHeight();
	for (int32 i = 0; i < Num; ++i)
	{
		const FVector2D R = FMath::RandPointInCircle(RadiusCm);
		FVector Loc = Center + FVector(R, 0.f);
		FHitResult Hit;
		if (World->LineTraceSingleByChannel(Hit, Loc + FVector(0, 0, 500.f), Loc - FVector(0, 0, 1000.f), ECC_Visibility))
		{
			Loc = Hit.ImpactPoint + FVector(0, 0, HalfHeight + 5.f);
		}
		FActorSpawnParameters P;
		P.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AdjustIfPossibleButAlwaysSpawn;
		const FRotator Rot(0.f, FMath::FRandRange(0.f, 360.f), 0.f);
		if (AHPCritter* C = World->SpawnActor<AHPCritter>(Class, Loc, Rot, P))
		{
			Out.Add(C);
		}
	}
	return Out;
}

// ---- Console helpers (dev / visual QA) ---------------------------------------

static FVector HPDevAnchor(UWorld* World, float AheadCm)
{
	if (APlayerController* PC = World->GetFirstPlayerController())
	{
		if (APawn* Pawn = PC->GetPawn())
		{
			return Pawn->GetActorLocation() + Pawn->GetActorForwardVector() * AheadCm;
		}
	}
	return FVector::ZeroVector;
}

static FAutoConsoleCommandWithWorldAndArgs GHPSpawnCrittersCmd(
	TEXT("hp.SpawnCritters"),
	TEXT("hp.SpawnCritters <pigs> <chickens> [radiusM] [aheadM] - spawn critters in front of player 0."),
	FConsoleCommandWithWorldAndArgsDelegate::CreateLambda([](const TArray<FString>& Args, UWorld* World)
	{
		const int32 Pigs = Args.Num() > 0 ? FCString::Atoi(*Args[0]) : 2;
		const int32 Chickens = Args.Num() > 1 ? FCString::Atoi(*Args[1]) : 6;
		const float RadiusCm = (Args.Num() > 2 ? FCString::Atof(*Args[2]) : 4.f) * 100.f;
		const float AheadCm = (Args.Num() > 3 ? FCString::Atof(*Args[3]) : 8.f) * 100.f;
		const FVector C = HPDevAnchor(World, AheadCm);
		AHPCritterSpawner::SpawnAround(World, AHPCritter_Pig::StaticClass(), Pigs, C, RadiusCm);
		AHPCritterSpawner::SpawnAround(World, AHPCritter_Chicken::StaticClass(), Chickens, C, RadiusCm);
	}));

static FAutoConsoleCommandWithWorldAndArgs GHPNoiseCmd(
	TEXT("hp.Noise"),
	TEXT("hp.Noise <loudness> [tag] [aheadM] - emit a Scripted Noise Event at player 0 (+ahead), e.g. hp.Noise 0.9  or  hp.Noise 0.5 Noise.Food 6"),
	FConsoleCommandWithWorldAndArgsDelegate::CreateLambda([](const TArray<FString>& Args, UWorld* World)
	{
		UHPNoiseSubsystem* Noise = World ? World->GetSubsystem<UHPNoiseSubsystem>() : nullptr;
		if (!Noise)
		{
			return;
		}
		FNoiseEvent E;
		E.Source = ENoiseSource::Shout;
		E.Loudness = Args.Num() > 0 ? FCString::Atof(*Args[0]) : 0.9f;
		if (Args.Num() > 1)
		{
			const FGameplayTag Tag = FGameplayTag::RequestGameplayTag(FName(*Args[1]), false);
			if (Tag.IsValid())
			{
				E.Tags.AddTag(Tag);
				E.Source = ENoiseSource::Tool;
			}
		}
		E.Location = HPDevAnchor(World, Args.Num() > 2 ? FCString::Atof(*Args[2]) * 100.f : 0.f);
		if (APlayerController* PC = World->GetFirstPlayerController())
		{
			E.Instigator = PC->GetPawn();
		}
		Noise->Emit(E);
	}));
