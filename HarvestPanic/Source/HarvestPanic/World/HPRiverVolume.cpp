// Copyright HARVEST PANIC team. All Rights Reserved.
#include "World/HPRiverVolume.h"
#include "World/HPBarnRespawn.h"
#include "AI/HPCritter.h"
#include "Carry/HPCarryable.h"
#include "Economy/HPGoodsComponent.h"
#include "HPCharacter.h"
#include "HPNoiseSubsystem.h"
#include "Components/BoxComponent.h"
#include "Engine/World.h"
#include "GameFramework/CharacterMovementComponent.h"

AHPRiverVolume::AHPRiverVolume()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.TickInterval = 0.05f;
	Volume = CreateDefaultSubobject<UBoxComponent>(TEXT("Volume"));
	Volume->SetBoxExtent(FVector(1000.f, 300.f, 100.f));
	Volume->SetCollisionProfileName(TEXT("Trigger"));
	Volume->SetGenerateOverlapEvents(true);
	RootComponent = Volume;
}

FVector AHPRiverVolume::GetPileLocation() const
{
	return bHasPile ? PileLocation : GetActorLocation() + GetActorForwardVector() * Volume->GetScaledBoxExtent().X;
}

void AHPRiverVolume::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (!HasAuthority())
	{
		return;
	}
	const double Now = GetWorld()->GetTimeSeconds();
	TArray<AActor*> Inside;
	Volume->GetOverlappingActors(Inside);

	TMap<TWeakObjectPtr<AActor>, double> StillInWater;
	for (AActor* A : Inside)
	{
		if (AHPCharacter* Player = Cast<AHPCharacter>(A))
		{
			const double* Entered = PlayerEntry.Find(Player);
			StillInWater.Add(Player, Entered ? *Entered : Now);
			HandlePlayer(Player, StillInWater[Player]);
		}
		else if (AHPCritter* Critter = Cast<AHPCritter>(A))
		{
			HandleCritter(Critter, Now);
		}
		else if (Cast<IHPCarryable>(A))
		{
			HandleBody(A, Now);
		}
	}
	PlayerEntry = MoveTemp(StillInWater);
}

void AHPRiverVolume::HandlePlayer(AHPCharacter* Player, double EnteredAt)
{
	if (GetWorld()->GetTimeSeconds() - EnteredAt >= DrownDelaySeconds)
	{
		UE_LOG(LogTemp, Log, TEXT("[River] %s drowned"), *Player->GetName());
		Player->ServerRespawnAtBarn(EHPBlameVerb::Drowned, true);
		PlayerEntry.Remove(Player);
	}
}

void AHPRiverVolume::HandleCritter(ACharacter* Critter, double Now)
{
	FTrack* Existing = Tracks.Find(Critter);
	if (Existing && Now - Existing->EntryTime < 2.0)
	{
		return;
	}
	FTrack& T = Tracks.FindOrAdd(Critter);
	T.EntryTime = Now;
	if (UHPGoodsComponent* Goods = Critter->FindComponentByClass<UHPGoodsComponent>())
	{
		Goods->ApplyBruisePoints(SoakedBruisePoints, nullptr, EHPBlameVerb::DroppedInRiver);
	}
	// Swim to the nearest bank (sideways across the flow) and run off.
	const FVector Right = GetActorRightVector();
	const float Side = FVector::DotProduct(Critter->GetActorLocation() - GetActorLocation(), Right) >= 0.f ? 1.f : -1.f;
	Critter->GetCharacterMovement()->SetMovementMode(MOVE_Falling);
	Critter->LaunchCharacter(Right * Side * 500.f + FVector(0.f, 0.f, 350.f) + GetActorForwardVector() * CurrentCmS * 0.5f, true, true);
}

void AHPRiverVolume::HandleBody(AActor* Body, double Now)
{
	IHPCarryable* Carryable = Cast<IHPCarryable>(Body);
	UPrimitiveComponent* Prim = Carryable ? Carryable->GetCarryPrimitive() : nullptr;
	if (!Prim)
	{
		return;
	}
	FTrack* Track = Tracks.Find(Body);
	if (!Track)
	{
		FTrack& NewTrack = Tracks.Add(Body);
		NewTrack.EntryTime = Now;
		NewTrack.Delay = FMath::FRandRange(SpitOutMinSeconds, SpitOutMaxSeconds);
		Track = &NewTrack;
		if (UHPGoodsComponent* Goods = Body->FindComponentByClass<UHPGoodsComponent>())
		{
			Goods->ApplyBruisePoints(SoakedBruisePoints, nullptr, EHPBlameVerb::DroppedInRiver);
		}
		UE_LOG(LogTemp, Log, TEXT("[River] %s entered the river (spit-out in %.1fs)"), *Body->GetName(), Track->Delay);
	}
	if (Prim->IsSimulatingPhysics())
	{
		Prim->AddForce(GetActorForwardVector() * CurrentCmS * Prim->GetMass() * 2.f);
	}
	if (Now - Track->EntryTime >= Track->Delay)
	{
		DeliverToPile(Body);
		Tracks.Remove(Body);
	}
}

void AHPRiverVolume::DeliverToPile(AActor* Body)
{
	const FVector2D Scatter = FMath::RandPointInCircle(PileScatterCm);
	const FVector Target = GetPileLocation() + FVector(Scatter, 60.f);
	if (UPrimitiveComponent* Prim = Cast<IHPCarryable>(Body)->GetCarryPrimitive())
	{
		if (Prim->IsSimulatingPhysics())
		{
			Prim->SetPhysicsLinearVelocity(FVector::ZeroVector);
			Prim->SetPhysicsAngularVelocityInDegrees(FVector::ZeroVector);
		}
	}
	Body->SetActorLocation(Target, false, nullptr, ETeleportType::TeleportPhysics);
	UE_LOG(LogTemp, Log, TEXT("[River] %s spat out at the pile"), *Body->GetName());
}
