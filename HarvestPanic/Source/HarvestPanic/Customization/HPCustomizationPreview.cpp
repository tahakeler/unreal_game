// Copyright HARVEST PANIC team. All Rights Reserved.

#include "HPCustomizationPreview.h"
#include "HPCosmeticApplier.h"
#include "HPCosmeticCatalog.h"
#include "Components/SkeletalMeshComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Components/SceneCaptureComponent2D.h"
#include "Components/PointLightComponent.h"
#include "Engine/TextureRenderTarget2D.h"
#include "Engine/StaticMesh.h"
#include "Engine/SkeletalMesh.h"
#include "Animation/AnimSequenceBase.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "UObject/ConstructorHelpers.h"

AHPCustomizationPreview::AHPCustomizationPreview()
{
	PrimaryActorTick.bCanEverTick = true;
	bReplicates = false;
	SetCanBeDamaged(false);

	Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	SetRootComponent(Root);
	Turntable = CreateDefaultSubobject<USceneComponent>(TEXT("Turntable"));
	Turntable->SetupAttachment(Root);

	Body = CreateDefaultSubobject<USkeletalMeshComponent>(TEXT("Body"));
	Body->SetupAttachment(Turntable);
	// Mannequin faces +Y; rotate so it faces the capture at +X.
	Body->SetRelativeRotation(FRotator(0, -90, 0));
	Body->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Body->VisibilityBasedAnimTickOption = EVisibilityBasedAnimTickOption::AlwaysTickPoseAndRefreshBones;

	static ConstructorHelpers::FObjectFinder<UStaticMesh> CubeMesh(TEXT("/Engine/BasicShapes/Cube.Cube"));
	static ConstructorHelpers::FObjectFinder<UStaticMesh> CylMesh(TEXT("/Engine/BasicShapes/Cylinder.Cylinder"));
	Backdrop = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Backdrop"));
	Backdrop->SetupAttachment(Root);
	Backdrop->SetStaticMesh(CubeMesh.Object);
	Backdrop->SetRelativeLocation(FVector(-150, 0, 100));
	Backdrop->SetRelativeScale3D(FVector(0.1f, 8.f, 8.f));
	Backdrop->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Floor = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Floor"));
	Floor->SetupAttachment(Root);
	Floor->SetStaticMesh(CylMesh.Object);
	Floor->SetRelativeLocation(FVector(0, 0, -4));
	Floor->SetRelativeScale3D(FVector(4.f, 4.f, 0.08f));
	Floor->SetCollisionEnabled(ECollisionEnabled::NoCollision);

	KeyLight = CreateDefaultSubobject<UPointLightComponent>(TEXT("KeyLight"));
	KeyLight->SetupAttachment(Root);
	KeyLight->SetRelativeLocation(FVector(220, 140, 230));
	KeyLight->SetIntensityUnits(ELightUnits::Candelas);
	KeyLight->SetIntensity(60.f);
	KeyLight->SetAttenuationRadius(1200.f);
	KeyLight->SetLightColor(FLinearColor(1.f, 0.92f, 0.8f));
	FillLight = CreateDefaultSubobject<UPointLightComponent>(TEXT("FillLight"));
	FillLight->SetupAttachment(Root);
	FillLight->SetRelativeLocation(FVector(180, -220, 120));
	FillLight->SetIntensityUnits(ELightUnits::Candelas);
	FillLight->SetIntensity(25.f);
	FillLight->SetAttenuationRadius(1200.f);
	FillLight->SetLightColor(FLinearColor(0.75f, 0.85f, 1.f));
	FillLight->SetCastShadows(false);

	Capture = CreateDefaultSubobject<USceneCaptureComponent2D>(TEXT("Capture"));
	Capture->SetupAttachment(Root);
	Capture->SetRelativeLocation(FVector(310, 0, 118));
	Capture->SetRelativeRotation(FRotator(-6, 180, 0));
	Capture->FOVAngle = 37.f;
	Capture->bCaptureEveryFrame = true;
	Capture->bCaptureOnMovement = false;
	Capture->PrimitiveRenderMode = ESceneCapturePrimitiveRenderMode::PRM_UseShowOnlyList;
	Capture->CaptureSource = ESceneCaptureSource::SCS_FinalColorLDR;
	Capture->ShowFlags.SetAtmosphere(false);
	Capture->ShowFlags.SetFog(false);
	Capture->ShowFlags.SetVolumetricFog(false);
	Capture->ShowFlags.SetMotionBlur(false);
	Capture->PostProcessSettings.bOverride_AutoExposureMethod = true;
	Capture->PostProcessSettings.AutoExposureMethod = EAutoExposureMethod::AEM_Manual;
	Capture->PostProcessSettings.bOverride_AutoExposureApplyPhysicalCameraExposure = true;
	Capture->PostProcessSettings.AutoExposureApplyPhysicalCameraExposure = false;
	Capture->PostProcessSettings.bOverride_AutoExposureBias = true;
	Capture->PostProcessSettings.AutoExposureBias = 0.f;
}

void AHPCustomizationPreview::BeginPlay()
{
	Super::BeginPlay();
	SetActorLocation(SpawnLocation);

	const UHPCosmeticCatalog* Cat = UHPCosmeticCatalog::Get();
	Body->SetSkeletalMeshAsset(Cat->MannyMesh.LoadSynchronous());
	if (UAnimSequenceBase* Idle = LoadObject<UAnimSequenceBase>(nullptr, TEXT("/Game/Characters/Mannequins/Anims/Unarmed/MM_Idle.MM_Idle")))
	{
		Body->PlayAnimation(Idle, true);
	}

	auto Tint = [Cat](UStaticMeshComponent* C, const FLinearColor& Col)
	{
		if (UMaterialInterface* Base = Cat->PartTintMaterial.LoadSynchronous())
		{
			UMaterialInstanceDynamic* MID = UMaterialInstanceDynamic::Create(Base, C);
			MID->SetVectorParameterValue(Cat->PartTintParam, Col);
			C->SetMaterial(0, MID);
		}
	};
	Tint(Backdrop, FLinearColor(FColor(232, 196, 140)));
	Tint(Floor, FLinearColor(FColor(120, 85, 55)));

	RenderTarget = NewObject<UTextureRenderTarget2D>(this);
	RenderTarget->RenderTargetFormat = RTF_RGBA8_SRGB;
	RenderTarget->ClearColor = FLinearColor(0.9f, 0.77f, 0.55f);
	RenderTarget->InitAutoFormat(Resolution.X, Resolution.Y);
	RenderTarget->UpdateResourceImmediate(true);
	Capture->TextureTarget = RenderTarget;
	Capture->ShowOnlyActors.Add(this);
}

void AHPCustomizationPreview::SetLoadout(const FHPCosmeticLoadout& Loadout)
{
	FHPCosmeticApplier::ApplyToBody(Body, Loadout);
	// Idle anim may have been cleared by a mesh swap; keep it looping.
	if (Body->GetAnimationMode() != EAnimationMode::AnimationSingleNode || !Body->IsPlaying())
	{
		if (UAnimSequenceBase* Idle = LoadObject<UAnimSequenceBase>(nullptr, TEXT("/Game/Characters/Mannequins/Anims/Unarmed/MM_Idle.MM_Idle")))
		{
			Body->PlayAnimation(Idle, true);
		}
	}
}

void AHPCustomizationPreview::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	TargetYaw += IdleSpinDegPerSec * DeltaSeconds;
	Yaw = FMath::FInterpTo(Yaw, TargetYaw, DeltaSeconds, 8.f);
	Turntable->SetRelativeRotation(FRotator(0, Yaw, 0));
}
