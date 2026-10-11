// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPCritter.h"
#include "HPCritterPersonality.h"
#include "HPCritterMoverComponent.h"
#include "HPCritterAIController.h"
#include "HPNoiseSubsystem.h"
#include "Economy/HPGoodsComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/StaticMeshComponent.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/PlayerState.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "Net/UnrealNetwork.h"
#include "UObject/ConstructorHelpers.h"

namespace
{
	UStaticMeshComponent* MakePart(AHPCritter* Owner, FName Name, USceneComponent* Parent)
	{
		UStaticMeshComponent* C = Owner->CreateDefaultSubobject<UStaticMeshComponent>(Name);
		C->SetupAttachment(Parent);
		C->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		C->SetGenerateOverlapEvents(false);
		C->SetCanEverAffectNavigation(false);
		return C;
	}
}

AHPCritter::AHPCritter()
{
	PrimaryActorTick.bCanEverTick = true;
	bReplicates = true;
	AIControllerClass = AHPCritterAIController::StaticClass();
	AutoPossessAI = EAutoPossessAI::PlacedInWorldOrSpawned;
	bUseControllerRotationYaw = false;

	UCharacterMovementComponent* Move = GetCharacterMovement();
	Move->bOrientRotationToMovement = true;
	Move->RotationRate = FRotator(0.f, 720.f, 0.f);
	Move->bUseRVOAvoidance = true;
	Move->AvoidanceConsiderationRadius = 200.f;

	Mover = CreateDefaultSubobject<UHPCritterMoverComponent>(TEXT("Mover"));
	Goods = CreateDefaultSubobject<UHPGoodsComponent>(TEXT("Goods"));
	Goods->bIsAlive = true;

	Body = MakePart(this, TEXT("Body"), GetCapsuleComponent());
	Head = MakePart(this, TEXT("Head"), Body);
	Accent = MakePart(this, TEXT("Accent"), Body);

	static ConstructorHelpers::FObjectFinder<UMaterialInterface> Mat(TEXT("/Engine/BasicShapes/BasicShapeMaterial.BasicShapeMaterial"));
	BaseMaterial = Mat.Object;
}

const UHPCritterPersonality* AHPCritter::GetPersonality() const
{
	if (PersonalityAsset)
	{
		return PersonalityAsset;
	}
	return PersonalityClass ? PersonalityClass->GetDefaultObject<UHPCritterPersonality>() : GetDefault<UHPCritterPersonality>();
}

void AHPCritter::BeginPlay()
{
	Super::BeginPlay();
	HomeLocation = GetActorLocation();
	BodyBaseLoc = Body->GetRelativeLocation();
	BodyBaseRot = Body->GetRelativeRotation();
	ApplyBodyColor(Body, BodyColor);
	ApplyBodyColor(Head, BodyColor);
	ApplyBodyColor(Accent, AccentColor);
	if (HasAuthority() && Goods && !GetGoodsId().IsNone())
	{
		Goods->ApplyPreset(GetGoodsId());
		Goods->bIsAlive = true;
		Goods->MassKg = GetBodyMassKg();
	}
	if (const UHPCritterPersonality* P = GetPersonality())
	{
		GetCharacterMovement()->MaxWalkSpeed = P->MoveSpeed;
		GetCharacterMovement()->MaxAcceleration = P->Acceleration;
	}
}

void AHPCritter::ApplyBodyColor(UStaticMeshComponent* Comp, const FLinearColor& Color)
{
	if (!Comp || !BaseMaterial || !Comp->GetStaticMesh())
	{
		return;
	}
	UMaterialInstanceDynamic* MID = UMaterialInstanceDynamic::Create(BaseMaterial, this);
	MID->SetVectorParameterValue(TEXT("Color"), Color);
	Comp->SetMaterial(0, MID);
}

void AHPCritter::SetCritterState(ECritterState NewState)
{
	if (NewState == State)
	{
		return;
	}
	const ECritterState Old = State;
	State = NewState;
	UE_LOG(LogHPCritter, Log, TEXT("%s: %s -> %s"), *GetName(), LexCritterState(Old), LexCritterState(NewState));
	OnStateChanged.Broadcast(this, Old, NewState);
}

int32 AHPCritter::GetCarryHands() const
{
	const UHPCritterPersonality* P = GetPersonality();
	return P ? P->CarryHands : 2;
}

int32 AHPCritter::GetSecureCarriers() const
{
	const UHPCritterPersonality* P = GetPersonality();
	return P ? P->SecureCarriers : 1;
}

void AHPCritter::OnGrabbed(AActor* By)
{
	if (!HasAuthority() || !By)
	{
		return;
	}
	Carriers.RemoveAll([](const TWeakObjectPtr<AActor>& A) { return !A.IsValid(); });
	if (Carriers.Contains(By))
	{
		return;
	}
	const bool bFirst = Carriers.Num() == 0;
	Carriers.Add(By);
	if (bFirst)
	{
		GetCapsuleComponent()->SetCollisionResponseToChannel(ECC_Pawn, ECR_Ignore); // do not shove the holder
		Mover->Stop();
		GetCharacterMovement()->StopMovementImmediately();
		GetCharacterMovement()->DisableMovement();
		const UHPCritterPersonality* P = GetPersonality();
		StruggleTimer = P ? FMath::FRandRange(P->StruggleIntervalMin, P->StruggleIntervalMax) : 1.f;
		SetCritterState(ECritterState::Carried);
		EmitGrabNoise(By);
	}
}

void AHPCritter::OnReleased(AActor* By)
{
	if (!HasAuthority())
	{
		return;
	}
	if (By)
	{
		Carriers.Remove(By);
	}
	else
	{
		Carriers.Reset();
	}
	Carriers.RemoveAll([](const TWeakObjectPtr<AActor>& A) { return !A.IsValid(); });
	if (Carriers.Num() == 0)
	{
		GetCapsuleComponent()->SetCollisionResponseToChannel(ECC_Pawn, ECR_Block);
	}
	if (Carriers.Num() == 0 && State == ECritterState::Carried)
	{
		GetCharacterMovement()->SetMovementMode(MOVE_Falling);
		if (AHPCritterAIController* AI = Cast<AHPCritterAIController>(GetController()))
		{
			const UHPCritterPersonality* P = GetPersonality();
			AI->EnterStunned(P ? P->StunTime : 1.f);
		}
		else
		{
			SetCritterState(ECritterState::Stunned);
		}
	}
}

void AHPCritter::EmitGrabNoise(AActor* By)
{
	const UHPCritterPersonality* P = GetPersonality();
	UHPNoiseSubsystem* Noise = GetWorld() ? GetWorld()->GetSubsystem<UHPNoiseSubsystem>() : nullptr;
	if (!P || !Noise || P->GrabNoiseLoudness <= 0.f)
	{
		return;
	}
	FNoiseEvent E;
	E.Source = ENoiseSource::Critter;
	E.Loudness = P->GrabNoiseLoudness;
	E.Location = GetActorLocation();
	E.Instigator = this;
	if (const APawn* Pawn = Cast<APawn>(By))
	{
		E.BlamedPlayer = Pawn->GetPlayerState();
	}
	if (P->GrabNoiseTag.IsValid())
	{
		E.Tags.AddTag(P->GrabNoiseTag);
	}
	Noise->Emit(E);
}

void AHPCritter::TickStruggle(float DeltaSeconds)
{
	if (!HasAuthority() || State != ECritterState::Carried)
	{
		return;
	}
	StruggleTimer -= DeltaSeconds;
	if (StruggleTimer > 0.f)
	{
		return;
	}
	const UHPCritterPersonality* P = GetPersonality();
	if (!P)
	{
		return;
	}
	StruggleTimer = FMath::FRandRange(P->StruggleIntervalMin, P->StruggleIntervalMax);
	const FVector Dir = FVector(FMath::FRandRange(-1.f, 1.f), FMath::FRandRange(-1.f, 1.f), 0.4f).GetSafeNormal();
	OnStruggle.Broadcast(this, Dir, P->StruggleImpulse);
	ShoveCarriers(Dir, P->StruggleImpulse);

	const float Chance = FHPCritterLogic::BreakFreeChance(P->SecureCarriers, Carriers.Num());
	if (Chance > 0.f && FMath::FRand() < Chance)
	{
		UE_LOG(LogHPCritter, Log, TEXT("%s broke free (p=%.2f, carriers=%d)"), *GetName(), Chance, Carriers.Num());
		OnBreakFree.Broadcast(this);
		OnReleased(nullptr);
		LaunchCharacter(Dir * 300.f + FVector(0, 0, 250.f), true, true);
	}
}

void AHPCritter::ShoveCarriers(const FVector& Direction, float ImpulseNs)
{
	for (const TWeakObjectPtr<AActor>& Weak : Carriers)
	{
		if (ACharacter* Holder = Cast<ACharacter>(Weak.Get()))
		{
			// N*s -> kg*cm/s; the holder is ~100 kg so this is roughly ImpulseNs cm/s of wobble.
			Holder->GetCharacterMovement()->AddImpulse(Direction.GetSafeNormal2D() * ImpulseNs * 100.f);
		}
	}
}

// ---- IHPCarryable --------------------------------------------------------------

float AHPCritter::GetCarryMassKg() const
{
	return Goods ? Goods->MassKg : 10.f;
}

UPrimitiveComponent* AHPCritter::GetCarryPrimitive() const
{
	return GetCapsuleComponent();
}

void AHPCritter::AddCarrierHands(AActor* Carrier, int32 Hands)
{
	if (!HasAuthority() || !IsCarryable())
	{
		return;
	}
	CarrierHands.FindOrAdd(Carrier) = Hands;
	OnGrabbed(Carrier);
}

void AHPCritter::RemoveCarrier(AActor* Carrier)
{
	if (!HasAuthority())
	{
		return;
	}
	CarrierHands.Remove(Carrier);
	OnReleased(Carrier);
}

int32 AHPCritter::GetHandsAttached() const
{
	int32 Total = 0;
	for (const TPair<TWeakObjectPtr<AActor>, int32>& Pair : CarrierHands)
	{
		Total += (Pair.Key.IsValid() && Carriers.Contains(Pair.Key)) ? Pair.Value : 0;
	}
	return Total;
}

void AHPCritter::NotifyTouched(AActor* Toucher, bool bThrown)
{
	if (HasAuthority() && Goods)
	{
		Goods->RecordTouch(Toucher);
	}
}

void AHPCritter::ApplyThrowVelocity(const FVector& VelocityCmS)
{
	GetCharacterMovement()->SetMovementMode(MOVE_Falling);
	LaunchCharacter(VelocityCmS, true, true);
}

void AHPCritter::UpdateWobble(float DeltaSeconds)
{
	const UHPCritterPersonality* P = GetPersonality();
	const float MaxSpeed = P ? FMath::Max(1.f, P->SprintSpeed) : 500.f;
	float Ratio = FMath::Clamp(GetVelocity().Size2D() / MaxSpeed, 0.f, 1.f);
	float Freq = WobbleFrequency;
	if (State == ECritterState::Carried) { Ratio = 1.f; Freq *= 1.8f; }
	if (State == ECritterState::Alert) { Ratio = FMath::Max(Ratio, 0.5f); Freq *= 2.f; }
	WobblePhase += DeltaSeconds * Freq;
	const float S = FMath::Sin(WobblePhase);
	FRotator Rot = BodyBaseRot;
	Rot.Roll += S * WobbleRollDeg * Ratio;
	Rot.Pitch += (State == ECritterState::Stunned ? 15.f * FMath::Sin(WobblePhase * 0.5f) : 0.f);
	if (State == ECritterState::Cornered && P && P->CorneredResponse == ECritterCorneredResponse::PlayDead)
	{
		Rot.Roll = 90.f;
	}
	Body->SetRelativeLocationAndRotation(BodyBaseLoc + FVector(0, 0, FMath::Abs(S) * WobbleBobCm * Ratio), Rot);
}

void AHPCritter::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	UpdateWobble(DeltaSeconds);
	TickStruggle(DeltaSeconds);
}

void AHPCritter::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHPCritter, State);
}

// ---- Species ---------------------------------------------------------------

AHPCritter_Pig::AHPCritter_Pig()
{
	PersonalityClass = UHPCritterPersonality_Pig::StaticClass();
	GetCapsuleComponent()->InitCapsuleSize(42.f, 42.f);
	BodyColor = FLinearColor(1.f, 0.45f, 0.6f);
	AccentColor = FLinearColor(0.85f, 0.3f, 0.45f);
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Sphere(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cyl(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	Body->SetStaticMesh(Sphere.Object);
	Body->SetRelativeScale3D(FVector(1.0f, 0.72f, 0.65f));
	Body->SetRelativeLocation(FVector(0.f, 0.f, -10.f));
	Head->SetStaticMesh(Sphere.Object);
	Head->SetRelativeScale3D(FVector(0.45f, 0.6f, 0.65f)); // relative to body
	Head->SetRelativeLocation(FVector(45.f, 0.f, 10.f));
	Accent->SetStaticMesh(Cyl.Object);
	Accent->SetRelativeScale3D(FVector(0.2f, 0.25f, 0.12f));
	Accent->SetRelativeRotation(FRotator(90.f, 0.f, 0.f));
	Accent->SetRelativeLocation(FVector(68.f, 0.f, 8.f));
}

AHPCritter_Chicken::AHPCritter_Chicken()
{
	PersonalityClass = UHPCritterPersonality_Chicken::StaticClass();
	GetCapsuleComponent()->InitCapsuleSize(18.f, 22.f);
	BodyColor = FLinearColor(0.95f, 0.95f, 0.92f);
	AccentColor = FLinearColor(1.f, 0.55f, 0.05f);
	WobbleFrequency = 22.f;
	WobbleBobCm = 4.f;
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Sphere(TEXT("/Engine/BasicShapes/Sphere.Sphere"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cone(TEXT("/Engine/BasicShapes/Cone.Cone"));
	Body->SetStaticMesh(Sphere.Object);
	Body->SetRelativeScale3D(FVector(0.38f, 0.3f, 0.32f));
	Body->SetRelativeLocation(FVector(0.f, 0.f, -6.f));
	Head->SetStaticMesh(Sphere.Object);
	Head->SetRelativeScale3D(FVector(0.5f, 0.6f, 0.6f));
	Head->SetRelativeLocation(FVector(35.f, 0.f, 45.f));
	Accent->SetStaticMesh(Cone.Object);
	Accent->SetRelativeScale3D(FVector(0.15f, 0.2f, 0.25f));
	Accent->SetRelativeRotation(FRotator(-90.f, 0.f, 0.f));
	Accent->SetRelativeLocation(FVector(68.f, 0.f, 45.f));
}
