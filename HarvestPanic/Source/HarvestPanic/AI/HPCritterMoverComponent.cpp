// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPCritterMoverComponent.h"
#include "NavigationSystem.h"
#include "NavigationPath.h"
#include "GameFramework/Character.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Engine/World.h"
#include "HPCritterTypes.h"

UHPCritterMoverComponent::UHPCritterMoverComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
}

bool UHPCritterMoverComponent::HasNavData() const
{
	// Nav data must exist AND cover the critter's position; otherwise use direct steering
	// (the template map ships without a NavMeshBoundsVolume).
	const UNavigationSystemV1* Nav = FNavigationSystem::GetCurrent<UNavigationSystemV1>(GetWorld());
	if (!Nav || !Nav->GetDefaultNavDataInstance() || !GetOwner())
	{
		return false;
	}
	FNavLocation Loc;
	return Nav->ProjectPointToNavigation(GetOwner()->GetActorLocation(), Loc, FVector(100.f, 100.f, 250.f));
}

bool UHPCritterMoverComponent::IsValidDestination(const FVector& From, const FVector& Point, FVector& OutPoint) const
{
	OutPoint = Point;
	UWorld* World = GetWorld();
	if (!World) { return false; }
	if (HasNavData())
	{
		const UNavigationSystemV1* Nav = FNavigationSystem::GetCurrent<UNavigationSystemV1>(World);
		FNavLocation Loc;
		if (!Nav->ProjectPointToNavigation(Point, Loc, FVector(150.f, 150.f, 300.f)))
		{
			return false;
		}
		OutPoint = Loc.Location;
		return true;
	}
	// Direct mode: no wall between, and ground below the target.
	FCollisionQueryParams Params(SCENE_QUERY_STAT(CritterMove), false, GetOwner());
	FHitResult Hit;
	const FVector Up(0.f, 0.f, 30.f);
	// Walkable surfaces (ground, ramps) are not obstacles; only steep hits block.
	if (World->LineTraceSingleByChannel(Hit, From + Up, Point + Up, ECC_Visibility, Params) && !Hit.bStartPenetrating && Hit.ImpactNormal.Z < 0.7f)
	{
		UE_LOG(LogHPCritter, Verbose, TEXT("%s: dest blocked by %s n=%s from=%s"), *GetOwner()->GetName(), *GetNameSafe(Hit.GetActor()), *Hit.ImpactNormal.ToString(), *From.ToString());
		return false;
	}
	if (!World->LineTraceSingleByChannel(Hit, Point + FVector(0, 0, 200.f), Point - FVector(0, 0, 400.f), ECC_Visibility, Params))
	{
		return false; // ledge / void
	}
	OutPoint = Hit.ImpactPoint;
	return true;
}

float UHPCritterMoverComponent::RouteCost(const FVector& From, const FVector& To) const
{
	if (HasNavData())
	{
		UNavigationPath* Path = UNavigationSystemV1::FindPathToLocationSynchronously(GetWorld(), From, To, GetOwner());
		if (!Path || !Path->IsValid() || Path->IsPartial())
		{
			return -1.f;
		}
		return Path->GetPathLength();
	}
	FVector Out;
	return IsValidDestination(From, To, Out) ? FVector::Dist(From, To) : -1.f;
}

bool UHPCritterMoverComponent::MoveTo(const FVector& InGoal, float SpeedCmS, float AcceptanceCm)
{
	AActor* Owner = GetOwner();
	if (!Owner) { return false; }
	Goal = InGoal;
	Speed = SpeedCmS;
	Acceptance = AcceptanceCm;
	PathPoints.Reset();
	PathIndex = 0;
	if (HasNavData())
	{
		UNavigationPath* Path = UNavigationSystemV1::FindPathToLocationSynchronously(GetWorld(), Owner->GetActorLocation(), Goal, Owner);
		if (!Path || !Path->IsValid() || Path->PathPoints.Num() < 2)
		{
			bMoving = false;
			return false;
		}
		PathPoints = Path->PathPoints;
		PathIndex = 1;
	}
	else
	{
		PathPoints = { Owner->GetActorLocation(), Goal };
		PathIndex = 1;
	}
	bMoving = true;
	StuckTime = 0.f;
	LastPos = Owner->GetActorLocation();
	if (ACharacter* C = Cast<ACharacter>(Owner))
	{
		C->GetCharacterMovement()->MaxWalkSpeed = Speed;
	}
	return true;
}

void UHPCritterMoverComponent::Stop()
{
	bMoving = false;
	PathPoints.Reset();
	StuckTime = 0.f;
}

void UHPCritterMoverComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
	APawn* Pawn = Cast<APawn>(GetOwner());
	if (!bMoving || !Pawn || !PathPoints.IsValidIndex(PathIndex))
	{
		bMoving = false;
		return;
	}
	const FVector Pos = Pawn->GetActorLocation();
	FVector ToNext = PathPoints[PathIndex] - Pos;
	ToNext.Z = 0.f;
	const bool bLast = PathIndex == PathPoints.Num() - 1;
	if (ToNext.Size() < (bLast ? Acceptance : 40.f))
	{
		if (bLast)
		{
			Stop();
			return;
		}
		++PathIndex;
		return;
	}
	Pawn->AddMovementInput(ToNext.GetSafeNormal());

	const float Moved = FVector::Dist2D(Pos, LastPos);
	StuckTime = (Moved < Speed * DeltaTime * 0.2f) ? StuckTime + DeltaTime : 0.f;
	LastPos = Pos;
}
