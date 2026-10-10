// Copyright HARVEST PANIC team. All Rights Reserved.
// Dev-only visual QA: "hp.CritterShowcase [quit]" builds a flat arena under player 0 (the template map
// in this repo has no floor/nav content), spawns pigs + chickens, shouts at t=3s, takes HighResShots
// and optionally quits. Used for production/qa/evidence screenshots.
#include "CoreMinimal.h"

#if !UE_BUILD_SHIPPING
#include "HPCritter.h"
#include "HPCritterSpawner.h"
#include "HPNoiseSubsystem.h"
#include "Camera/CameraActor.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/Engine.h"
#include "Engine/StaticMesh.h"
#include "Engine/StaticMeshActor.h"
#include "Engine/World.h"
#include "GameFramework/PlayerController.h"
#include "HAL/IConsoleManager.h"
#include "TimerManager.h"
#include "EngineUtils.h"
#include "HPCritterMoverComponent.h"
#include "GameFramework/CharacterMovementComponent.h"

static void HPShowcaseRun(UWorld* World, bool bQuit)
{
	APlayerController* PC = World->GetFirstPlayerController();
	APawn* Pawn = PC ? PC->GetPawn() : nullptr;
	const FVector Center = Pawn ? Pawn->GetActorLocation() : FVector(0, 0, 200);
	FVector Floor = Center - FVector(0, 0, 150.f);
	{
		// Sit flush on any existing ground collision (template meshes may be missing but still collide).
		FHitResult Hit;
		FCollisionQueryParams Q(SCENE_QUERY_STAT(HPShowcase), false, Pawn);
		if (World->LineTraceSingleByChannel(Hit, Center, Center - FVector(0, 0, 5000.f), ECC_Visibility, Q))
		{
			Floor = Hit.ImpactPoint + FVector(0, 0, 2.f);
		}
	}

	FActorSpawnParameters SP;
	SP.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	if (AStaticMeshActor* Ground = World->SpawnActor<AStaticMeshActor>(Floor, FRotator::ZeroRotator, SP))
	{
		Ground->SetMobility(EComponentMobility::Movable);
		Ground->GetStaticMeshComponent()->SetStaticMesh(LoadObject<UStaticMesh>(nullptr, TEXT("/Engine/BasicShapes/Cube.Cube")));
		Ground->GetStaticMeshComponent()->SetMaterial(0, LoadObject<UMaterialInterface>(nullptr, TEXT("/Engine/EngineMaterials/DefaultMaterial.DefaultMaterial")));
		Ground->SetActorScale3D(FVector(60.f, 60.f, 1.f));
		Ground->SetActorLocation(Floor - FVector(0, 0, 50.f));
	}
	if (Pawn)
	{
		Pawn->SetActorLocation(Floor + FVector(0, 0, 100.f));
	}
	const FVector Herd = Floor + FVector(600.f, 0.f, 0.f);
	AHPCritterSpawner::SpawnAround(World, AHPCritter_Pig::StaticClass(), 3, Herd + FVector(0, 0, 100.f), 350.f);
	AHPCritterSpawner::SpawnAround(World, AHPCritter_Chicken::StaticClass(), 8, Herd + FVector(0, 300.f, 100.f), 250.f);

	if (ACameraActor* Cam = World->SpawnActor<ACameraActor>(Herd + FVector(-650.f, -450.f, 380.f), FRotator::ZeroRotator, SP))
	{
		Cam->SetActorRotation((Herd - Cam->GetActorLocation()).Rotation());
		if (PC) { PC->SetViewTarget(Cam); }
	}
	IConsoleManager::Get().FindConsoleVariable(TEXT("hp.CritterDebug"))->Set(1);

	FTimerHandle H1, H2, H3, H4, H5;
	World->GetTimerManager().SetTimer(H1, [World]()
	{
		if (APlayerController* P = World->GetFirstPlayerController()) { P->ConsoleCommand(TEXT("HighResShot 1"), false); }
	}, 2.5f, false);
	World->GetTimerManager().SetTimer(H2, [World, Herd]()
	{
		if (UHPNoiseSubsystem* Noise = World->GetSubsystem<UHPNoiseSubsystem>())
		{
			FNoiseEvent E;
			E.Source = ENoiseSource::Shout;
			E.Loudness = 0.9f;
			E.Location = Herd - FVector(300.f, 0, 0);
			Noise->Emit(E);
		}
	}, 3.0f, false);
	World->GetTimerManager().SetTimer(H3, [World]()
	{
		if (APlayerController* P = World->GetFirstPlayerController()) { P->ConsoleCommand(TEXT("HighResShot 1"), false); }
	}, 4.2f, false);
	World->GetTimerManager().SetTimer(H4, [World, Herd]()
	{
		// Smoke report + grab test: player grabs the first pig (squeal should alert others).
		AHPCritter* FirstPig = nullptr;
		for (TActorIterator<AHPCritter> It(World); It; ++It)
		{
			UE_LOG(LogHPCritter, Log, TEXT("[Showcase] %s state=%s dist_from_herd=%.0fcm moving=%d nav=%d mode=%d vel=%.0f z=%.0f"), *It->GetName(),
				LexCritterState(It->GetCritterState()), FVector::Dist2D(It->GetActorLocation(), Herd),
				It->GetMover()->IsMoving(), It->GetMover()->HasNavData(), (int32)It->GetCharacterMovement()->MovementMode.GetValue(), It->GetVelocity().Size(), It->GetActorLocation().Z);
			if (!FirstPig && It->IsA<AHPCritter_Pig>()) { FirstPig = *It; }
		}
		APlayerController* PC = World->GetFirstPlayerController();
		if (FirstPig && PC && PC->GetPawn())
		{
			FirstPig->OnGrabbed(PC->GetPawn());
			UE_LOG(LogHPCritter, Log, TEXT("[Showcase] grabbed %s carriers=%d secure=%d"), *FirstPig->GetName(), FirstPig->GetNumCarriers(), FirstPig->GetSecureCarriers());
		}
	}, 6.0f, false);
	if (bQuit)
	{
		World->GetTimerManager().SetTimer(H5, [World]()
		{
			GEngine->Exec(World, TEXT("quit"));
		}, 12.0f, false);
	}
}

static FAutoConsoleCommandWithWorldAndArgs GHPCritterShowcaseCmd(
	TEXT("hp.CritterShowcase"),
	TEXT("hp.CritterShowcase [quit] - dev arena + pigs/chickens + shout + 2 HighResShots."),
	FConsoleCommandWithWorldAndArgsDelegate::CreateLambda([](const TArray<FString>& Args, UWorld* World)
	{
		if (World)
		{
			HPShowcaseRun(World, Args.Num() > 0 && Args[0].Equals(TEXT("quit"), ESearchCase::IgnoreCase));
		}
	}));
#endif
