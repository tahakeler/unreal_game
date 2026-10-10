// Copyright HARVEST PANIC team. All Rights Reserved.
#include "Carry/HPCarryableProp.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "GameFramework/Pawn.h"
#include "GameFramework/PlayerState.h"
#include "Net/UnrealNetwork.h"
#include "UObject/ConstructorHelpers.h"
#include "HPCharacter.h"
#include "HPNoiseSubsystem.h"

namespace
{
	int32 PlayerIdOf(const AActor* Actor)
	{
		const APawn* Pawn = Cast<APawn>(Actor);
		const APlayerState* PS = Pawn ? Pawn->GetPlayerState() : nullptr;
		return PS ? PS->GetPlayerId() : INDEX_NONE;
	}

	/** Engine impulses are kg*cm/s; GDD uses N*s (kg*m/s). */
	constexpr float CmToM = 0.01f;
}

AHPCarryableProp::AHPCarryableProp()
{
	bReplicates = true;
	SetReplicatingMovement(true);

	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	SetRootComponent(Mesh);
	Mesh->SetCollisionProfileName(UCollisionProfile::PhysicsActor_ProfileName);
	Mesh->SetSimulatePhysics(true);
	Mesh->SetNotifyRigidBodyCollision(true);
	Mesh->SetRelativeScale3D(FVector(0.5f));

	static ConstructorHelpers::FObjectFinder<UStaticMesh> CubeMesh(TEXT("/Engine/BasicShapes/Cube.Cube"));
	if (CubeMesh.Succeeded())
	{
		Mesh->SetStaticMesh(CubeMesh.Object);
	}
}

UPrimitiveComponent* AHPCarryableProp::GetCarryPrimitive() const
{
	return Mesh;
}

void AHPCarryableProp::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	ApplyShape();
}

void AHPCarryableProp::BeginPlay()
{
	Super::BeginPlay();
	ApplyShape();
}

void AHPCarryableProp::ApplyShape()
{
	if (Shape == EHPPropShape::Sphere)
	{
		if (UStaticMesh* Sphere = LoadObject<UStaticMesh>(nullptr, TEXT("/Engine/BasicShapes/Sphere.Sphere")))
		{
			Mesh->SetStaticMesh(Sphere);
		}
	}
	Mesh->SetMassOverrideInKg(NAME_None, MassKg, true);
}

void AHPCarryableProp::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHPCarryableProp, HandsAttached);
}

void AHPCarryableProp::AddCarrierHands(AActor* Carrier, int32 Hands)
{
	check(HasAuthority());
	Carriers.FindOrAdd(Carrier) = Hands;
	HandsAttached = 0;
	for (const TPair<TWeakObjectPtr<AActor>, int32>& Pair : Carriers)
	{
		HandsAttached += Pair.Key.IsValid() ? Pair.Value : 0;
	}
	UpdatePawnCollision();
}

void AHPCarryableProp::RemoveCarrier(AActor* Carrier)
{
	check(HasAuthority());
	Carriers.Remove(Carrier);
	HandsAttached = 0;
	for (const TPair<TWeakObjectPtr<AActor>, int32>& Pair : Carriers)
	{
		HandsAttached += Pair.Key.IsValid() ? Pair.Value : 0;
	}
	UpdatePawnCollision();
}

void AHPCarryableProp::OnRep_HandsAttached()
{
	UpdatePawnCollision();
}

void AHPCarryableProp::UpdatePawnCollision()
{
	// Held bodies ignore pawns so carriers are not shoved by their own load.
	Mesh->SetCollisionResponseToChannel(ECC_Pawn, HandsAttached > 0 ? ECR_Ignore : ECR_Block);
}

void AHPCarryableProp::NotifyTouched(AActor* Toucher, bool bThrown)
{
	if (!HasAuthority())
	{
		return;
	}
	const double Now = GetWorld()->GetTimeSeconds();
	TouchHistory.AddTouch(PlayerIdOf(Toucher), Now);
	LastTouchTime = Now;
	if (bThrown)
	{
		LastThrower = Toucher;
		LastThrowTime = Now;
	}
}

void AHPCarryableProp::NotifyHit(UPrimitiveComponent* MyComp, AActor* Other, UPrimitiveComponent* OtherComp,
	bool bSelfMoved, FVector HitLocation, FVector HitNormal, FVector NormalImpulse, const FHitResult& Hit)
{
	Super::NotifyHit(MyComp, Other, OtherComp, bSelfMoved, HitLocation, HitNormal, NormalImpulse, Hit);

	UWorld* World = GetWorld();
	if (!HasAuthority() || !World)
	{
		return;
	}
	const double Now = World->GetTimeSeconds();
	if (Now - LastImpactTime < ImpactCooldownSeconds)
	{
		return;
	}

	// Contact impulse; against kinematic pawns fall back to momentum along the normal.
	float ImpulseNs = NormalImpulse.Size() * CmToM;
	if (AHPCharacter* HitCharacter = Cast<AHPCharacter>(Other))
	{
		const float Closing = FMath::Abs(FVector::DotProduct(Mesh->GetPhysicsLinearVelocity(), HitNormal)) * CmToM;
		ImpulseNs = FMath::Max(ImpulseNs, MassKg * Closing);
		const bool bThrowerGrace = LastThrower.Get() == HitCharacter && (Now - LastThrowTime) < ThrowerGraceSeconds;
		if (!bThrowerGrace && ImpulseNs > 0.f)
		{
			const TArray<int32> Ids = TouchHistory.GetRecentToucherIds(Now, TouchWindowSeconds, 1);
			HitCharacter->ReceiveImpactImpulse(ImpulseNs, Ids.Num() > 0 ? Ids[0] : INDEX_NONE);
		}
	}

	if (ImpulseNs <= BruiseImpactMinNs)
	{
		return;
	}
	LastImpactTime = Now;

	// Heavy-impact Noise Event (proximity-voice table).
	if (UHPNoiseSubsystem* Noise = World->GetSubsystem<UHPNoiseSubsystem>())
	{
		FNoiseEvent Event;
		Event.Source = ENoiseSource::Impact;
		Event.Loudness = ImpulseNs > HeavyImpactNs ? HeavyImpactLoudness : LightImpactLoudness;
		Event.Location = HitLocation;
		Event.Instigator = this;
		Noise->Emit(Event);
	}

	// Bruise only applies to bodies that are held, thrown, or were just handled (drop = dropper's fault).
	const bool bHandled = HandsAttached > 0 || (Now - LastThrowTime) < ThrownStateSeconds
		|| (Now - LastTouchTime) < TouchWindowSeconds;
	if (!bHandled)
	{
		return;
	}
	FHPBruiseHit Bruise;
	Bruise.Item = this;
	Bruise.ImpulseNs = ImpulseNs;
	Bruise.Location = HitLocation;
	Bruise.TouchHistoryPlayerIds = TouchHistory.GetRecentToucherIds(Now, TouchWindowSeconds, TouchShares.Num());
	Bruise.BlameShares = HPCarryMath::TouchShares(Bruise.TouchHistoryPlayerIds.Num(), TouchShares);
	OnBruiseHit.Broadcast(Bruise);
}
