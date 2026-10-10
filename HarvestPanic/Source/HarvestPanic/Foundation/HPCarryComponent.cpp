// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPCarryComponent.h"
#include "Carry/HPCarryable.h"
#include "Carry/HPCarryMath.h"
#include "Components/PrimitiveComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/World.h"
#include "GameFramework/Character.h"
#include "GameFramework/Controller.h"
#include "Net/UnrealNetwork.h"
#include "PhysicsEngine/PhysicsHandleComponent.h"

UHPCarryComponent::UHPCarryComponent()
{
	PrimaryComponentTick.bCanEverTick = true;
	PrimaryComponentTick.TickGroup = TG_PrePhysics;
	SetIsReplicatedByDefault(true);
}

void UHPCarryComponent::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(UHPCarryComponent, HeldActor);
	DOREPLIFETIME(UHPCarryComponent, SpeedMultiplier);
	DOREPLIFETIME(UHPCarryComponent, LoadRatio);
}

void UHPCarryComponent::BeginPlay()
{
	Super::BeginPlay();
	if (GetOwner()->HasAuthority())
	{
		Handle = NewObject<UPhysicsHandleComponent>(GetOwner(), TEXT("CarryHandle"));
		Handle->bSoftLinearConstraint = true;
		Handle->bSoftAngularConstraint = true;
		Handle->SetLinearStiffness(HandStiffness);
		Handle->SetLinearDamping(HandDamping);
		Handle->SetAngularStiffness(HandStiffness);
		Handle->SetAngularDamping(HandDamping);
		Handle->SetInterpolationSpeed(HandInterpSpeed);
		Handle->RegisterComponent();
	}
}

void UHPCarryComponent::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	if (GetOwner()->HasAuthority() && HeldActor)
	{
		ServerRelease(false);
	}
	Super::EndPlay(EndPlayReason);
}

IHPCarryable* UHPCarryComponent::GetHeldCarryable() const
{
	return Cast<IHPCarryable>(HeldActor.Get());
}

void UHPCarryComponent::GetView(FVector& OutStart, FVector& OutDir) const
{
	const APawn* Pawn = Cast<APawn>(GetOwner());
	FRotator Rot = GetOwner()->GetActorRotation();
	FVector Loc = GetOwner()->GetActorLocation();
	if (Pawn && Pawn->GetController())
	{
		Pawn->GetController()->GetPlayerViewPoint(Loc, Rot);
	}
	OutStart = Loc;
	OutDir = Rot.Vector();
}

FVector UHPCarryComponent::GetHoldLocation() const
{
	const APawn* Pawn = Cast<APawn>(GetOwner());
	const FVector Eye = Pawn ? Pawn->GetPawnViewLocation() : GetOwner()->GetActorLocation();
	FRotator Aim = Pawn ? Pawn->GetBaseAimRotation() : GetOwner()->GetActorRotation();
	Aim.Pitch = FMath::Clamp(FRotator::NormalizeAxis(Aim.Pitch), -HoldPitchLimit, HoldPitchLimit);
	return Eye + Aim.Vector() * HoldForwardCm + FVector::UpVector * HoldUpCm;
}

AActor* UHPCarryComponent::TraceAimTarget(const FVector& ViewStart, const FVector& ViewDir) const
{
	FCollisionQueryParams Params(SCENE_QUERY_STAT(HPGrab), false, GetOwner());
	FCollisionObjectQueryParams Objects;
	Objects.AddObjectTypesToQuery(ECC_PhysicsBody);
	Objects.AddObjectTypesToQuery(ECC_WorldDynamic);
	TArray<FHitResult> Hits;
	GetWorld()->SweepMultiByObjectType(Hits, ViewStart, ViewStart + ViewDir.GetSafeNormal() * GrabRangeCm, FQuat::Identity,
		Objects, FCollisionShape::MakeSphere(GrabSweepRadiusCm), Params);
	Hits.Sort([](const FHitResult& A, const FHitResult& B) { return A.Distance < B.Distance; });
	for (const FHitResult& Hit : Hits)
	{
		if (Cast<IHPCarryable>(Hit.GetActor()))
		{
			return Hit.GetActor();
		}
	}
	return nullptr;
}

AActor* UHPCarryComponent::TraceAimTargetFromView() const
{
	FVector Start, Dir;
	GetView(Start, Dir);
	return TraceAimTarget(Start, Dir);
}

float UHPCarryComponent::GetThrowCharge() const
{
	if (ThrowChargeStart < 0.0 || !GetWorld())
	{
		return 0.f;
	}
	return FMath::Clamp(float(GetWorld()->GetTimeSeconds() - ThrowChargeStart) / FMath::Max(ThrowChargeSeconds, 0.01f), 0.f, 1.f);
}

// ---------------- Intents ----------------

void UHPCarryComponent::RequestGrabOrDrop()
{
	FVector Start, Dir;
	GetView(Start, Dir);
	if (GetOwner()->HasAuthority())
	{
		DoGrabOrDrop(Start, Dir);
	}
	else
	{
		Server_GrabOrDrop(Start, Dir);
	}
}

void UHPCarryComponent::BeginThrowCharge()
{
	if (HeldActor)
	{
		ThrowChargeStart = GetWorld()->GetTimeSeconds();
	}
}

void UHPCarryComponent::ReleaseThrow()
{
	if (ThrowChargeStart < 0.0)
	{
		return;
	}
	const float Charge = GetThrowCharge();
	ThrowChargeStart = -1.0;
	FVector Start, Dir;
	GetView(Start, Dir);
	if (GetOwner()->HasAuthority())
	{
		DoThrow(Charge, Dir);
	}
	else
	{
		Server_Throw(Charge, Dir);
	}
}

void UHPCarryComponent::Server_GrabOrDrop_Implementation(FVector_NetQuantize ViewStart, FVector_NetQuantizeNormal ViewDir)
{
	DoGrabOrDrop(ViewStart, ViewDir);
}

void UHPCarryComponent::Server_Throw_Implementation(float Charge, FVector_NetQuantizeNormal ViewDir)
{
	DoThrow(FMath::Clamp(Charge, 0.f, 1.f), ViewDir);
}

// ---------------- Server ----------------

void UHPCarryComponent::DoGrabOrDrop(const FVector& ViewStart, const FVector& ViewDir)
{
	if (HeldActor)
	{
		ServerRelease(false);
		return;
	}
	// Validate the client's center-screen ray against the server pawn's eyes.
	const APawn* Pawn = Cast<APawn>(GetOwner());
	const FVector Eye = Pawn ? Pawn->GetPawnViewLocation() : GetOwner()->GetActorLocation();
	const FVector Start = FVector::Dist(ViewStart, Eye) <= MaxViewStartErrorCm ? ViewStart : Eye;
	ServerGrabActor(TraceAimTarget(Start, ViewDir));
}

bool UHPCarryComponent::ServerGrabActor(AActor* Target)
{
	check(GetOwner()->HasAuthority());
	IHPCarryable* Carryable = Cast<IHPCarryable>(Target);
	if (!Carryable || HeldActor || !Handle)
	{
		return false;
	}
	UPrimitiveComponent* Prim = Carryable->GetCarryPrimitive();
	if (!Prim || !Prim->IsSimulatingPhysics())
	{
		return false;
	}
	const int32 Hands = FMath::Min(HandsPerPlayer, Carryable->GetCarryHands());
	Handle->GrabComponentAtLocationWithRotation(Prim, NAME_None, Prim->GetComponentLocation(), Prim->GetComponentRotation());
	HeldActor = Target;
	Carryable->AddCarrierHands(GetOwner(), Hands);
	Carryable->NotifyTouched(GetOwner(), false);
	UpdateServerHold();
	OnHeldChanged.Broadcast(HeldActor);
	return true;
}

void UHPCarryComponent::ServerRelease(bool bThrown)
{
	check(GetOwner()->HasAuthority());
	if (Handle)
	{
		Handle->ReleaseComponent();
	}
	if (IHPCarryable* Carryable = GetHeldCarryable())
	{
		Carryable->RemoveCarrier(GetOwner());
		Carryable->NotifyTouched(GetOwner(), bThrown); // a drop counts as the dropper's fault
	}
	HeldActor = nullptr;
	SpeedMultiplier = 1.f;
	LoadRatio = 0.f;
	ThrowChargeStart = -1.0;
	OnHeldChanged.Broadcast(nullptr);
}

void UHPCarryComponent::DoThrow(float Charge, const FVector& ViewDir)
{
	IHPCarryable* Carryable = GetHeldCarryable();
	if (!Carryable)
	{
		return;
	}
	const float Mass = Carryable->GetCarryMassKg();
	const int32 Mine = FMath::Min(HandsPerPlayer, Carryable->GetCarryHands());
	const bool bSolo = Carryable->GetHandsAttached() <= Mine;
	// Multi-hand items: only a solo heave of a light enough body.
	if (Carryable->GetCarryHands() > HandsPerPlayer && !(bSolo && Mass <= HeaveMaxMassKg))
	{
		ServerRelease(false);
		return;
	}
	UPrimitiveComponent* Prim = Carryable->GetCarryPrimitive();
	ServerRelease(true);
	if (Prim)
	{
		const float SpeedMs = HPCarryMath::ThrowSpeed(Charge, Mass, ThrowSpeedMin, ThrowSpeedMax, ThrowRefMassKg, ThrowMassFactorMin);
		const FVector Dir = (FVector(ViewDir.X, ViewDir.Y, FMath::Max(ViewDir.Z, 0.f)).GetSafeNormal() + FVector(0, 0, ThrowLoftZ)).GetSafeNormal();
		Prim->SetPhysicsLinearVelocity(Dir * SpeedMs * 100.f);
	}
}

void UHPCarryComponent::UpdateServerHold()
{
	IHPCarryable* Carryable = GetHeldCarryable();
	if (!Carryable || !Handle)
	{
		return;
	}
	UPrimitiveComponent* Prim = Carryable->GetCarryPrimitive();
	const int32 Total = Carryable->GetHandsAttached();
	const int32 Required = Carryable->GetCarryHands();
	const float Mass = Carryable->GetCarryMassKg();

	FVector Target = GetHoldLocation();
	if (Total < Required && Prim)
	{
		Target.Z = Prim->GetComponentLocation().Z; // not enough hands: drag along the ground
	}
	if (Prim && FVector::Dist(Prim->GetComponentLocation(), Target) > SlipDistanceCm)
	{
		ServerRelease(false); // slip
		return;
	}
	Handle->SetTargetLocationAndRotation(Target, GetOwner()->GetActorRotation());

	SpeedMultiplier = HPCarryMath::CarrySpeedMultiplier(Mass, Total, Required, KgPerHand, SpeedFloor);
	LoadRatio = HPCarryMath::LoadRatio(Mass, Total, KgPerHand);
}

void UHPCarryComponent::TickComponent(float DeltaTime, ELevelTick TickType, FActorComponentTickFunction* ThisTickFunction)
{
	Super::TickComponent(DeltaTime, TickType, ThisTickFunction);
	if (GetOwner()->HasAuthority() && HeldActor)
	{
		if (!IsValid(HeldActor))
		{
			ServerRelease(false);
			return;
		}
		UpdateServerHold();
	}
}

void UHPCarryComponent::OnRep_HeldActor()
{
	if (!HeldActor)
	{
		ThrowChargeStart = -1.0;
	}
	OnHeldChanged.Broadcast(HeldActor);
}
