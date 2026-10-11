// Copyright HARVEST PANIC team. All Rights Reserved.

#include "HPWardrobe.h"
#include "HPCosmeticCatalog.h"
#include "Components/SphereComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Components/TextRenderComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "Engine/Engine.h"
#include "GameFramework/Pawn.h"
#include "GameFramework/PlayerController.h"
#include "Camera/PlayerCameraManager.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "UObject/ConstructorHelpers.h"

AHPWardrobe::AHPWardrobe()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.TickInterval = 0.1f;

	Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	SetRootComponent(Root);

	Trigger = CreateDefaultSubobject<USphereComponent>(TEXT("Trigger"));
	Trigger->SetupAttachment(Root);
	Trigger->SetRelativeLocation(FVector(80, 0, 100));
	Trigger->SetCollisionProfileName(TEXT("OverlapAllDynamic"));
	Trigger->SetGenerateOverlapEvents(true);

	static ConstructorHelpers::FObjectFinder<UStaticMesh> Cube(TEXT("/Engine/BasicShapes/Cube.Cube"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> Hat(TEXT("/Game/Emberlight/Meshes/Props/SM_Strawhat.SM_Strawhat"));

	Cabinet = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Cabinet"));
	Cabinet->SetupAttachment(Root);
	Cabinet->SetStaticMesh(Cube.Object);
	Cabinet->SetRelativeLocation(FVector(0, 0, 105));
	Cabinet->SetRelativeScale3D(FVector(0.6f, 1.2f, 2.1f));

	DoorL = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("DoorL"));
	DoorL->SetupAttachment(Root);
	DoorL->SetStaticMesh(Cube.Object);
	DoorL->SetRelativeLocation(FVector(31, -29, 105));
	DoorL->SetRelativeScale3D(FVector(0.03f, 0.56f, 1.95f));
	DoorL->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	DoorR = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("DoorR"));
	DoorR->SetupAttachment(Root);
	DoorR->SetStaticMesh(Cube.Object);
	DoorR->SetRelativeLocation(FVector(31, 29, 105));
	DoorR->SetRelativeScale3D(FVector(0.03f, 0.56f, 1.95f));
	DoorR->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	TopHat = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("TopHat"));
	TopHat->SetupAttachment(Root);
	if (Hat.Succeeded()) { TopHat->SetStaticMesh(Hat.Object); }
	TopHat->SetRelativeLocation(FVector(0, 0, 212));
	TopHat->SetRelativeScale3D(FVector(1.6f));
	TopHat->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	Sign = CreateDefaultSubobject<UTextRenderComponent>(TEXT("Sign"));
	Sign->SetupAttachment(Root);
	Sign->SetRelativeLocation(FVector(34, 0, 196));
	Sign->SetHorizontalAlignment(EHTA_Center);
	Sign->SetWorldSize(16.f);
	Sign->SetText(NSLOCTEXT("HPWardrobe", "Sign", "WARDROBE"));
	Sign->SetTextRenderColor(FColor(255, 215, 120));

	Prompt = CreateDefaultSubobject<UTextRenderComponent>(TEXT("Prompt"));
	Prompt->SetupAttachment(Root);
	Prompt->SetRelativeLocation(FVector(60, 0, 240));
	Prompt->SetHorizontalAlignment(EHTA_Center);
	Prompt->SetWorldSize(20.f);
	Prompt->SetTextRenderColor(FColor::White);
	Prompt->SetHiddenInGame(true);
}

void AHPWardrobe::OnConstruction(const FTransform& Transform)
{
	Super::OnConstruction(Transform);
	Trigger->SetSphereRadius(InteractRadius);
	Prompt->SetText(PromptText);
}

void AHPWardrobe::BeginPlay()
{
	Super::BeginPlay();
	Trigger->SetSphereRadius(InteractRadius);
	Prompt->SetText(PromptText);
	const UHPCosmeticCatalog* Cat = UHPCosmeticCatalog::Get();
	if (UMaterialInterface* Base = Cat->PartTintMaterial.LoadSynchronous())
	{
		auto Tint = [&](UStaticMeshComponent* C, FColor Col)
		{
			UMaterialInstanceDynamic* MID = UMaterialInstanceDynamic::Create(Base, C);
			MID->SetVectorParameterValue(Cat->PartTintParam, FLinearColor(Col));
			C->SetMaterial(0, MID);
		};
		Tint(Cabinet, FColor(110, 62, 34));
		Tint(DoorL, FColor(150, 40, 30));
		Tint(DoorR, FColor(150, 40, 30));
	}
}

bool AHPWardrobe::IsPawnInRange(const APawn* Pawn) const
{
	return Pawn && Trigger && Trigger->IsOverlappingActor(Pawn);
}

void AHPWardrobe::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	// Prompt is local-only: shown when the local pawn is in the trigger, turned toward the camera.
	const APlayerController* PC = GEngine ? GEngine->GetFirstLocalPlayerController(GetWorld()) : nullptr;
	const bool bShow = PC && IsPawnInRange(PC->GetPawn());
	Prompt->SetHiddenInGame(!bShow);
	if (bShow && PC->PlayerCameraManager)
	{
		const FVector To = PC->PlayerCameraManager->GetCameraLocation() - Prompt->GetComponentLocation();
		Prompt->SetWorldRotation(FRotator(0, To.Rotation().Yaw, 0));
	}
}
