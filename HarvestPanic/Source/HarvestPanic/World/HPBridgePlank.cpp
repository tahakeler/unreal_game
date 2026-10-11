// Copyright HARVEST PANIC team. All Rights Reserved.
#include "World/HPBridgePlank.h"
#include "World/HPHazardMath.h"
#include "HPCharacter.h"
#include "HPCarryComponent.h"
#include "HPGameState.h"
#include "HPNoiseSubsystem.h"
#include "Carry/HPCarryable.h"
#include "Components/BoxComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/World.h"
#include "GameFramework/PlayerState.h"
#include "NativeGameplayTags.h"
#include "Net/UnrealNetwork.h"

UE_DEFINE_GAMEPLAY_TAG_STATIC(TAG_HP_Noise_Creak, "Noise.Creak");
UE_DEFINE_GAMEPLAY_TAG_STATIC(TAG_HP_Noise_PlankBreak, "Noise.PlankBreak");

AHPBridgePlank::AHPBridgePlank()
{
	PrimaryActorTick.bCanEverTick = true;
	bReplicates = true;
	SetNetUpdateFrequency(5.f);
	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));

	LoadSensor = CreateDefaultSubobject<UBoxComponent>(TEXT("LoadSensor"));
	LoadSensor->SetupAttachment(RootComponent);
	LoadSensor->SetCollisionProfileName(TEXT("Trigger"));
	LoadSensor->SetGenerateOverlapEvents(true);
	LoadSensor->SetBoxExtent(FVector(60.f, 120.f, 40.f));
}

void AHPBridgePlank::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHPBridgePlank, SourceActor);
	DOREPLIFETIME(AHPBridgePlank, PlankState);
	DOREPLIFETIME(AHPBridgePlank, Integrity);
}

void AHPBridgePlank::BeginPlay()
{
	Super::BeginPlay();
	CacheMesh();
}

void AHPBridgePlank::BindToMeshActor(AActor* MeshActor)
{
	SourceActor = MeshActor;
	CacheMesh();
	if (MeshActor)
	{
		// The sensor covers the plank's bounds plus a slab above it where people stand.
		FVector Origin, Extent;
		MeshActor->GetActorBounds(false, Origin, Extent);
		LoadSensor->SetWorldRotation(FRotator::ZeroRotator);
		LoadSensor->SetWorldLocation(Origin + FVector(0.f, 0.f, Extent.Z + 40.f));
		LoadSensor->SetBoxExtent(FVector(Extent.X, Extent.Y, 40.f));
	}
}

void AHPBridgePlank::OnRep_Source()
{
	CacheMesh();
}

void AHPBridgePlank::CacheMesh()
{
	if (Mesh || !SourceActor)
	{
		return;
	}
	Mesh = SourceActor->FindComponentByClass<UStaticMeshComponent>();
	if (Mesh)
	{
		Mesh->SetMobility(EComponentMobility::Movable);
		MeshHomeTransform = Mesh->GetComponentTransform();
		bHaveHome = true;
	}
}

float AHPBridgePlank::MeasureLoad(AActor*& OutHeaviestPlayer) const
{
	TArray<AActor*> Overlapping;
	LoadSensor->GetOverlappingActors(Overlapping);
	float Total = 0.f;
	float HeaviestShare = -1.f;
	OutHeaviestPlayer = nullptr;
	for (AActor* A : Overlapping)
	{
		if (!A || A == SourceActor)
		{
			continue;
		}
		float Mass = 0.f;
		float Share = 0.f;
		if (const AHPCharacter* Player = Cast<AHPCharacter>(A))
		{
			Mass = PlayerMassKg;
			Share = Mass;
			if (const AActor* Held = Player->GetCarryComponent()->GetHeldActor())
			{
				if (const IHPCarryable* C = Cast<IHPCarryable>(Held))
				{
					Share += C->GetCarryMassKg() / FMath::Max(1, C->GetHandsAttached() > 0 ? C->GetHandsAttached() / 2 : 1);
				}
			}
			if (Share > HeaviestShare)
			{
				HeaviestShare = Share;
				OutHeaviestPlayer = A;
			}
		}
		else if (const IHPCarryable* Carryable = Cast<IHPCarryable>(A))
		{
			Mass = Carryable->GetCarryMassKg();
		}
		Total += Mass;
	}
	return Total;
}

void AHPBridgePlank::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	// Cosmetic fall on every machine.
	if (FallTime >= 0.f && Mesh)
	{
		FallTime += DeltaSeconds;
		FallVelocity.Z -= 980.f * DeltaSeconds;
		Mesh->AddWorldOffset(FallVelocity * DeltaSeconds);
		Mesh->AddWorldRotation(FRotator(90.f * DeltaSeconds, 0.f, 45.f * DeltaSeconds));
		if (FallTime >= FallSeconds)
		{
			Mesh->SetVisibility(false);
			FallTime = -1.f;
		}
	}

	if (!HasAuthority())
	{
		return;
	}
	SampleTimer += DeltaSeconds;
	if (SampleTimer >= SampleInterval)
	{
		SampleTimer = 0.f;
		ServerSample();
	}
}

void AHPBridgePlank::ServerSample()
{
	if (const AHPGameState* GS = GetWorld()->GetGameState<AHPGameState>())
	{
		// Broken planks are repaired each morning for free (once per day).
		if (GS->Phase == EHPDayPhase::Morning && GS->DayIndex != LastRepairDay)
		{
			LastRepairDay = GS->DayIndex;
			if (PlankState != EHPPlankState::Intact || Integrity < 100.f)
			{
				ServerRepair();
			}
		}
	}
	if (PlankState == EHPPlankState::Broken)
	{
		return;
	}
	AActor* Heaviest = nullptr;
	LastLoadKg = MeasureLoad(Heaviest);
	const float Dps = HPHazardMath::PlankDamagePerSecond(LastLoadKg, SafeLoadKg, DamageRate, PlankState == EHPPlankState::Cracked);
	if (Dps > 0.f)
	{
		ServerApplyDamage(Dps * SampleInterval, Heaviest);
	}
}

bool AHPBridgePlank::ServerApplyDamage(float Damage, AActor* Blamed)
{
	check(HasAuthority());
	if (PlankState == EHPPlankState::Broken || Damage <= 0.f)
	{
		return false;
	}
	Integrity = FMath::Max(0.f, Integrity - Damage);
	if (Integrity <= 0.f)
	{
		PlankState = EHPPlankState::Broken;
		EmitHazardNoise(BreakLoudness, TEXT("Noise.PlankBreak"));
		if (AHPGameState* GS = GetWorld()->GetGameState<AHPGameState>())
		{
			TArray<int32> Who;
			const APawn* BlamedPawn = Cast<APawn>(Blamed);
			if (BlamedPawn && BlamedPawn->GetPlayerState())
			{
				Who.Add(BlamedPawn->GetPlayerState()->GetPlayerId());
			}
			GS->EmitBlame(EHPBlameVerb::BrokePlank, TEXT("Plank"), -PatchCost, Who, GetActorLocation());
		}
		ApplyVisualState(true);
		return true;
	}
	if (Integrity <= CrackIntegrity && PlankState == EHPPlankState::Intact)
	{
		PlankState = EHPPlankState::Cracked;
		EmitHazardNoise(CrackLoudness, TEXT("Noise.Creak"));
		ApplyVisualState(true);
	}
	return false;
}

void AHPBridgePlank::EmitHazardNoise(float Loudness, FName TagName)
{
	if (UHPNoiseSubsystem* Noise = GetWorld()->GetSubsystem<UHPNoiseSubsystem>())
	{
		FNoiseEvent E;
		E.Source = ENoiseSource::Hazard;
		E.Loudness = Loudness;
		E.Location = GetActorLocation();
		E.Instigator = this;
		E.Tags.AddTag(FGameplayTag::RequestGameplayTag(TagName, false));
		Noise->Emit(E);
	}
}

bool AHPBridgePlank::ServerPatch(AActor* By)
{
	if (!HasAuthority() || PlankState != EHPPlankState::Broken)
	{
		return false;
	}
	if (AHPGameState* GS = GetWorld()->GetGameState<AHPGameState>())
	{
		const APawn* Pawn = Cast<APawn>(By);
		const int32 Id = (Pawn && Pawn->GetPlayerState()) ? Pawn->GetPlayerState()->GetPlayerId() : HP_NO_PLAYER;
		GS->AddWalletTxn(EHPWalletTxnType::Repair, -PatchCost, Id, TEXT("Plank"));
	}
	ServerRepair();
	return true;
}

void AHPBridgePlank::ServerRepair()
{
	check(HasAuthority());
	Integrity = 100.f;
	PlankState = EHPPlankState::Intact;
	ApplyVisualState(true);
}

void AHPBridgePlank::OnRep_PlankState()
{
	ApplyVisualState(true);
}

void AHPBridgePlank::ApplyVisualState(bool bJustChanged)
{
	CacheMesh();
	if (!Mesh || (!bJustChanged && VisualState == PlankState))
	{
		return;
	}
	VisualState = PlankState;
	if (PlankState == EHPPlankState::Broken)
	{
		Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision); // everything on it falls
		FallTime = 0.f;
		FallVelocity = FVector(0.f, 0.f, -50.f);
		return;
	}
	// Intact / cracked: restore the home pose (a cracked plank is slightly tilted).
	FallTime = -1.f;
	Mesh->SetVisibility(true);
	Mesh->SetCollisionEnabled(ECollisionEnabled::QueryAndPhysics);
	FTransform T = bHaveHome ? MeshHomeTransform : Mesh->GetComponentTransform();
	if (PlankState == EHPPlankState::Cracked)
	{
		T.SetRotation(T.GetRotation() * FQuat(FRotator(0.f, 0.f, 4.f)));
	}
	Mesh->SetWorldTransform(T);
}
