// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPCharacter.h"
#include "HPCarryComponent.h"
#include "HPNoiseSubsystem.h"
#include "HPGameState.h"
#include "Frontend/HPGameUserSettings.h"
#include "World/HPBarnRespawn.h"
#include "World/HPBridgePlank.h"
#include "EngineUtils.h"
#include "Carry/HPCarryable.h"
#include "Carry/HPCarryMath.h"
#include "Animation/AnimInstance.h"
#include "Camera/CameraComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/PoseableMeshComponent.h"
#include "Debug/DebugDrawService.h"
#include "Engine/Canvas.h"
#include "Engine/Engine.h"
#include "TimerManager.h"
#include "GameFramework/SpringArmComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Engine/LocalPlayer.h"
#include "Engine/OverlapResult.h"
#include "Engine/SkeletalMesh.h"
#include "Engine/World.h"
#include "EngineUtils.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/PlayerState.h"
#include "InputAction.h"
#include "InputMappingContext.h"
#include "InputModifiers.h"
#include "NativeGameplayTags.h"
#include "Net/UnrealNetwork.h"
#include "UObject/ConstructorHelpers.h"

UE_DEFINE_GAMEPLAY_TAG_STATIC(TAG_HP_Noise_Food, "Noise.Food");

namespace
{
	UInputAction* MakeAction(UObject* Outer, const TCHAR* Name, EInputActionValueType Type)
	{
		UInputAction* Action = NewObject<UInputAction>(Outer, Name, RF_Transient);
		Action->ValueType = Type;
		return Action;
	}

	void AddNegate(UInputMappingContext* IMC, FEnhancedActionKeyMapping& Mapping, bool bX, bool bY)
	{
		UInputModifierNegate* Negate = NewObject<UInputModifierNegate>(IMC);
		Negate->bX = bX;
		Negate->bY = bY;
		Negate->bZ = false;
		Mapping.Modifiers.Add(Negate);
	}

	void AddSwizzle(UInputMappingContext* IMC, FEnhancedActionKeyMapping& Mapping)
	{
		UInputModifierSwizzleAxis* Swizzle = NewObject<UInputModifierSwizzleAxis>(IMC);
		Swizzle->Order = EInputAxisSwizzle::YXZ;
		Mapping.Modifiers.Add(Swizzle);
	}
}

AHPCharacter::AHPCharacter()
{
	bReplicates = true;
	PrimaryActorTick.bCanEverTick = true;
	CarryComponent = CreateDefaultSubobject<UHPCarryComponent>(TEXT("CarryComponent"));

	// Template mannequin so the pawn is visible without a BP subclass.
	static ConstructorHelpers::FObjectFinder<USkeletalMesh> MeshAsset(TEXT("/Game/Characters/Mannequins/Meshes/SKM_Quinn_Simple.SKM_Quinn_Simple"));
	static ConstructorHelpers::FClassFinder<UAnimInstance> AnimClass(TEXT("/Game/Characters/Mannequins/Anims/Unarmed/ABP_Unarmed"));
	if (MeshAsset.Succeeded())
	{
		GetMesh()->SetSkeletalMeshAsset(MeshAsset.Object);
	}
	if (AnimClass.Succeeded())
	{
		GetMesh()->SetAnimInstanceClass(AnimClass.Class);
	}
	GetMesh()->SetRelativeLocationAndRotation(FVector(0.f, 0.f, -96.f), FRotator(0.f, -90.f, 0.f));
	// First person: others see the full body, the owner sees only the FP arms.
	GetMesh()->SetOwnerNoSee(true);

	// The template's third-person boom camera stays but is inactive.
	GetCameraBoom()->SetAutoActivate(false);
	GetFollowCamera()->SetAutoActivate(false);
	bUseControllerRotationYaw = true;
	GetCharacterMovement()->bOrientRotationToMovement = false;
	BaseEyeHeight = EyeHeightCm;

	FirstPersonCamera = CreateDefaultSubobject<UCameraComponent>(TEXT("FirstPersonCamera"));
	FirstPersonCamera->SetupAttachment(GetCapsuleComponent());
	FirstPersonCamera->SetRelativeLocation(FVector(0.f, 0.f, EyeHeightCm));
	FirstPersonCamera->bUsePawnControlRotation = true;
	FirstPersonCamera->SetFieldOfView(BaseFOV);

	FirstPersonArms = CreateDefaultSubobject<UPoseableMeshComponent>(TEXT("FirstPersonArms"));
	FirstPersonArms->SetupAttachment(FirstPersonCamera);
	FirstPersonArms->SetOnlyOwnerSee(true);
	FirstPersonArms->SetCastShadow(false);
	FirstPersonArms->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	FirstPersonArms->SetRelativeLocationAndRotation(ArmsOffset, FRotator(0.f, -90.f, 0.f));
	if (MeshAsset.Succeeded())
	{
		FirstPersonArms->SetSkinnedAssetAndUpdate(MeshAsset.Object);
	}

	UCharacterMovementComponent* MoveComp = GetCharacterMovement();
	MoveComp->MaxWalkSpeed = WalkSpeed;
	MoveComp->MaxWalkSpeedCrouched = CrouchSpeed;
	MoveComp->JumpZVelocity = JumpZVelocity;
	MoveComp->GetNavAgentPropertiesRef().bCanCrouch = true;
}

void AHPCharacter::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHPCharacter, BodyState);
	DOREPLIFETIME(AHPCharacter, Stamina);
	DOREPLIFETIME(AHPCharacter, bWantsSprint);
	DOREPLIFETIME(AHPCharacter, bSprintExhausted);
	DOREPLIFETIME_CONDITION(AHPCharacter, ActiveTool, COND_None);
	DOREPLIFETIME_CONDITION(AHPCharacter, FeedUsesLeft, COND_OwnerOnly);
}

void AHPCharacter::BeginPlay()
{
	Super::BeginPlay();
	MeshRelativeTransform = GetMesh()->GetRelativeTransform();
	FirstPersonArms->HideBoneByName(TEXT("neck_01"), EPhysBodyOp::PBO_None);
	FirstPersonArms->HideBoneByName(TEXT("thigh_l"), EPhysBodyOp::PBO_None);
	FirstPersonArms->HideBoneByName(TEXT("thigh_r"), EPhysBodyOp::PBO_None);
	if (!DebugDrawHandle.IsValid())
	{
		DebugDrawHandle = UDebugDrawService::Register(TEXT("Game"), FDebugDrawDelegate::CreateUObject(this, &AHPCharacter::DrawCrosshair));
	}
	GetCharacterMovement()->MaxWalkSpeedCrouched = CrouchSpeed;
	GetCharacterMovement()->JumpZVelocity = JumpZVelocity;
	if (HasAuthority())
	{
		Stamina = StaminaMax;
		FeedUsesLeft = FeedUsesPerBucket;
	}
	if (UHPGameUserSettings* Settings = UHPGameUserSettings::Get())
	{
		ApplyUserSettings(*Settings);
	}
	SettingsHandle = UHPGameUserSettings::OnHPSettingsApplied.AddWeakLambda(this, [this](const UHPGameUserSettings& Settings)
	{
		ApplyUserSettings(Settings);
	});
	bPawnReady = true;
	OnPawnReady.Broadcast(this);
}

void AHPCharacter::ApplyUserSettings(const UHPGameUserSettings& Settings)
{
	LookSensitivity = Settings.GetMouseSensitivity();
	bInvertLookY = Settings.GetInvertY();
	BaseFOV = Settings.GetFieldOfView();
}

void AHPCharacter::DoLook(float Yaw, float Pitch)
{
	Super::DoLook(Yaw * LookSensitivity, (bInvertLookY ? -Pitch : Pitch) * LookSensitivity);
}

void AHPCharacter::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	UHPGameUserSettings::OnHPSettingsApplied.Remove(SettingsHandle);
	if (DebugDrawHandle.IsValid())
	{
		UDebugDrawService::Unregister(DebugDrawHandle);
		DebugDrawHandle.Reset();
	}
	Super::EndPlay(EndPlayReason);
}

// ---------------------------------------------------------------- First person

void AHPCharacter::Client_ImpactShake_Implementation(float Trauma01)
{
	ShakeTrauma = FMath::Clamp(ShakeTrauma + Trauma01, 0.f, 1.f);
}

void AHPCharacter::PoseArm(FName UpperArm, FName LowerArm, const FVector& WorldTarget, float Side)
{
	// UE5 mannequin arm bones point along +X (left) / -X (right).
	const FVector Shoulder = FirstPersonArms->GetBoneLocationByName(UpperArm, EBoneSpaces::WorldSpace);
	FVector Dir = (WorldTarget - Shoulder).GetSafeNormal();
	if (Dir.IsNearlyZero())
	{
		return;
	}
	const FRotator Rot = FRotationMatrix::MakeFromXZ(Dir * Side, FirstPersonCamera->GetUpVector()).Rotator();
	FirstPersonArms->SetBoneRotationByName(UpperArm, Rot, EBoneSpaces::WorldSpace);
	FirstPersonArms->SetBoneRotationByName(LowerArm, Rot, EBoneSpaces::WorldSpace);
}

void AHPCharacter::TickFirstPerson(float DeltaSeconds)
{
	if (!IsLocallyControlled() || !FirstPersonCamera)
	{
		return;
	}
	const bool bRagdoll = BodyState != EHPBodyState::Normal;
	FirstPersonCamera->ClearAdditiveOffset();

	// Sprint FOV kick.
	const bool bSprintingNow = bWantsSprint && !bSprintExhausted && GetVelocity().SizeSquared2D() > FMath::Square(WalkSpeed * 1.05f);
	const float TargetFOV = BaseFOV + (bSprintingNow ? SprintFOVKick : 0.f);
	FirstPersonCamera->SetFieldOfView(FMath::FInterpTo(FirstPersonCamera->FieldOfView, TargetFOV, DeltaSeconds, FOVInterpSpeed));

	// Head bob scaled by ground speed.
	FVector BobOffset = FVector::ZeroVector;
	if (!bRagdoll && GetCharacterMovement()->IsMovingOnGround())
	{
		const float SpeedRatio = GetVelocity().Size2D() / FMath::Max(WalkSpeed, 1.f);
		BobPhase += DeltaSeconds * HeadBobFrequency * SpeedRatio * UE_TWO_PI;
		BobOffset.Z = FMath::Sin(BobPhase * 2.f) * HeadBobAmplitude * FMath::Min(SpeedRatio, 1.6f);
		BobOffset.Y = FMath::Cos(BobPhase) * HeadBobAmplitude * 0.5f * FMath::Min(SpeedRatio, 1.6f);
	}

	// Impact shake (trauma^2 random rotation).
	FRotator Shake = FRotator::ZeroRotator;
	if (ShakeTrauma > 0.f)
	{
		const float Amount = ShakeTrauma * ShakeTrauma * ShakeMaxDegrees;
		Shake = FRotator(FMath::FRandRange(-Amount, Amount), FMath::FRandRange(-Amount, Amount), FMath::FRandRange(-Amount, Amount));
		ShakeTrauma = FMath::Max(0.f, ShakeTrauma - ShakeDecayPerSec * DeltaSeconds);
	}
	FirstPersonCamera->AddAdditiveOffset(FTransform(Shake, BobOffset), 0.f);

	// Arms reach toward the held object (or rest low in front).
	FirstPersonArms->SetVisibility(!bRagdoll);
	if (bRagdoll)
	{
		return;
	}
	const FTransform CamXf = FirstPersonCamera->GetComponentTransform();
	FVector LTarget = CamXf.TransformPosition(FVector(IdleHandOffset.X, -IdleHandOffset.Y, IdleHandOffset.Z));
	FVector RTarget = CamXf.TransformPosition(IdleHandOffset);
	if (AActor* Held = CarryComponent->GetHeldActor())
	{
		const FVector Center = Held->GetActorLocation();
		const FVector Right = CamXf.GetUnitAxis(EAxis::Y);
		const float HalfWidth = Held->GetComponentsBoundingBox().GetExtent().Size2D() * 0.6f;
		LTarget = Center - Right * HalfWidth;
		RTarget = Center + Right * HalfWidth;
	}
	else if (CarryComponent->TraceAimTargetFromView())
	{
		LTarget += CamXf.GetUnitAxis(EAxis::X) * 15.f; // eager reach
		RTarget += CamXf.GetUnitAxis(EAxis::X) * 15.f;
	}
	if (LeftHandTarget.IsZero())
	{
		LeftHandTarget = LTarget;
		RightHandTarget = RTarget;
	}
	LeftHandTarget = FMath::VInterpTo(LeftHandTarget, LTarget, DeltaSeconds, ArmInterpSpeed);
	RightHandTarget = FMath::VInterpTo(RightHandTarget, RTarget, DeltaSeconds, ArmInterpSpeed);
	PoseArm(TEXT("upperarm_l"), TEXT("lowerarm_l"), LeftHandTarget, 1.f);
	PoseArm(TEXT("upperarm_r"), TEXT("lowerarm_r"), RightHandTarget, -1.f);
}

void AHPCharacter::DrawCrosshair(UCanvas* Canvas, APlayerController* PC)
{
	// Interim slice HUD (hud-menus owns the real one): centre dot + grab prompt.
	if (!Canvas || !IsLocallyControlled() || BodyState != EHPBodyState::Normal || GetController() != PC)
	{
		return;
	}
	const float CX = Canvas->ClipX * 0.5f;
	const float CY = Canvas->ClipY * 0.5f;
	Canvas->SetDrawColor(FColor::White);
	Canvas->K2_DrawBox(FVector2D(CX - 2.f, CY - 2.f), FVector2D(4.f, 4.f), 2.f, FLinearColor::White);

	FString Prompt;
	if (const AActor* Held = CarryComponent->GetHeldActor())
	{
		const IHPCarryable* C = Cast<IHPCarryable>(Held);
		Prompt = FString::Printf(TEXT("E: Drop   LMB: Throw   (%d/%d hands)"), C ? C->GetHandsAttached() : 0, C ? C->GetCarryHands() : 0);
	}
	else if (const IHPCarryable* C = Cast<IHPCarryable>(CarryComponent->TraceAimTargetFromView()))
	{
		Prompt = FString::Printf(TEXT("E: Grab (%d/%d hands)"), C->GetHandsAttached(), C->GetCarryHands());
	}
	if (!Prompt.IsEmpty() && GEngine)
	{
		float W, H;
		Canvas->StrLen(GEngine->GetMediumFont(), Prompt, W, H);
		Canvas->SetDrawColor(FColor::Black);
		Canvas->DrawText(GEngine->GetMediumFont(), Prompt, CX - W * 0.5f + 1.f, CY + 25.f);
		Canvas->SetDrawColor(FColor::White);
		Canvas->DrawText(GEngine->GetMediumFont(), Prompt, CX - W * 0.5f, CY + 24.f);
	}
}

// ---------------------------------------------------------------- Input

void AHPCharacter::BuildInput()
{
	if (RuntimeIMC)
	{
		return;
	}
	RuntimeIMC = NewObject<UInputMappingContext>(this, TEXT("IMC_HPRuntime"), RF_Transient);
	IA_Move = MakeAction(this, TEXT("IA_HPMove"), EInputActionValueType::Axis2D);
	IA_Look = MakeAction(this, TEXT("IA_HPLook"), EInputActionValueType::Axis2D);
	IA_Jump = MakeAction(this, TEXT("IA_HPJump"), EInputActionValueType::Boolean);
	IA_Sprint = MakeAction(this, TEXT("IA_HPSprint"), EInputActionValueType::Boolean);
	IA_Crouch = MakeAction(this, TEXT("IA_HPCrouch"), EInputActionValueType::Boolean);
	IA_Grab = MakeAction(this, TEXT("IA_HPGrab"), EInputActionValueType::Boolean);
	IA_Throw = MakeAction(this, TEXT("IA_HPThrow"), EInputActionValueType::Boolean);
	IA_Holler = MakeAction(this, TEXT("IA_HPHoller"), EInputActionValueType::Boolean);
	IA_Tool = MakeAction(this, TEXT("IA_HPTool"), EInputActionValueType::Boolean);
	IA_ToolNet = MakeAction(this, TEXT("IA_HPToolNet"), EInputActionValueType::Boolean);
	IA_ToolFeed = MakeAction(this, TEXT("IA_HPToolFeed"), EInputActionValueType::Boolean);

	UInputMappingContext* IMC = RuntimeIMC;
	// Move: X = right, Y = forward (matches AHarvestPanicCharacter::Move).
	{ FEnhancedActionKeyMapping& M = IMC->MapKey(IA_Move, EKeys::W); AddSwizzle(IMC, M); }
	{ FEnhancedActionKeyMapping& M = IMC->MapKey(IA_Move, EKeys::S); AddSwizzle(IMC, M); AddNegate(IMC, M, true, true); }
	{ FEnhancedActionKeyMapping& M = IMC->MapKey(IA_Move, EKeys::A); AddNegate(IMC, M, true, true); }
	IMC->MapKey(IA_Move, EKeys::D);
	IMC->MapKey(IA_Move, EKeys::Gamepad_Left2D);
	{ FEnhancedActionKeyMapping& M = IMC->MapKey(IA_Look, EKeys::Mouse2D); AddNegate(IMC, M, false, true); }
	IMC->MapKey(IA_Look, EKeys::Gamepad_Right2D);
	IMC->MapKey(IA_Jump, EKeys::SpaceBar);
	IMC->MapKey(IA_Jump, EKeys::Gamepad_FaceButton_Bottom);
	IMC->MapKey(IA_Sprint, EKeys::LeftShift);
	IMC->MapKey(IA_Crouch, EKeys::LeftControl);
	IMC->MapKey(IA_Crouch, EKeys::C);
	IMC->MapKey(IA_Grab, EKeys::E);
	IMC->MapKey(IA_Throw, EKeys::LeftMouseButton);
	IMC->MapKey(IA_Holler, EKeys::H);
	IMC->MapKey(IA_Tool, EKeys::Q);
	IMC->MapKey(IA_ToolNet, EKeys::One);
	IMC->MapKey(IA_ToolFeed, EKeys::Two);
}

void AHPCharacter::NotifyControllerChanged()
{
	Super::NotifyControllerChanged();
	if (const APlayerController* PC = Cast<APlayerController>(GetController()))
	{
		if (UEnhancedInputLocalPlayerSubsystem* Subsystem = ULocalPlayer::GetSubsystem<UEnhancedInputLocalPlayerSubsystem>(PC->GetLocalPlayer()))
		{
			BuildInput();
			Subsystem->AddMappingContext(RuntimeIMC, 1);
		}
	}
}

void AHPCharacter::SetupPlayerInputComponent(UInputComponent* PlayerInputComponent)
{
	BuildInput();
	UEnhancedInputComponent* EIC = Cast<UEnhancedInputComponent>(PlayerInputComponent);
	if (!EIC)
	{
		Super::SetupPlayerInputComponent(PlayerInputComponent);
		return;
	}
	EIC->BindAction(IA_Move, ETriggerEvent::Triggered, this, &AHPCharacter::Move);
	EIC->BindAction(IA_Look, ETriggerEvent::Triggered, this, &AHPCharacter::Look);
	EIC->BindAction(IA_Jump, ETriggerEvent::Started, this, &AHPCharacter::OnJumpStarted);
	EIC->BindAction(IA_Jump, ETriggerEvent::Completed, this, &ACharacter::StopJumping);
	EIC->BindAction(IA_Sprint, ETriggerEvent::Started, this, &AHPCharacter::OnSprintStarted);
	EIC->BindAction(IA_Sprint, ETriggerEvent::Completed, this, &AHPCharacter::OnSprintCompleted);
	EIC->BindAction(IA_Crouch, ETriggerEvent::Started, this, &AHPCharacter::OnCrouchToggle);
	EIC->BindAction(IA_Grab, ETriggerEvent::Started, this, &AHPCharacter::OnGrabStarted);
	EIC->BindAction(IA_Grab, ETriggerEvent::Completed, this, &AHPCharacter::OnGrabCompleted);
	EIC->BindAction(IA_Throw, ETriggerEvent::Started, this, &AHPCharacter::OnThrowStarted);
	EIC->BindAction(IA_Throw, ETriggerEvent::Completed, this, &AHPCharacter::OnThrowCompleted);
	EIC->BindAction(IA_Holler, ETriggerEvent::Started, this, &AHPCharacter::OnHoller);
	EIC->BindAction(IA_Tool, ETriggerEvent::Started, this, &AHPCharacter::OnUseTool);
	EIC->BindAction(IA_ToolNet, ETriggerEvent::Started, this, &AHPCharacter::OnSelectNet);
	EIC->BindAction(IA_ToolFeed, ETriggerEvent::Started, this, &AHPCharacter::OnSelectFeed);
}

void AHPCharacter::OnJumpStarted()
{
	if (BodyState == EHPBodyState::Ragdoll)
	{
		Server_GetUp();
		return;
	}
	if (BodyState == EHPBodyState::Normal)
	{
		Jump();
	}
}

void AHPCharacter::OnSprintStarted()  { bWantsSprint = true;  if (!HasAuthority()) { Server_SetSprint(true); } }
void AHPCharacter::OnSprintCompleted() { bWantsSprint = false; if (!HasAuthority()) { Server_SetSprint(false); } }
void AHPCharacter::Server_SetSprint_Implementation(bool bSprint) { bWantsSprint = bSprint; }

void AHPCharacter::OnCrouchToggle()
{
	if (BodyState != EHPBodyState::Normal) { return; }
	bIsCrouched ? UnCrouch() : Crouch();
}

void AHPCharacter::OnGrabStarted()
{
	if (BodyState != EHPBodyState::Normal) { return; }
	if (AHPCharacter* Target = FindReviveTarget())
	{
		Server_BeginRevive(Target);
		return;
	}
	CarryComponent->RequestGrabOrDrop();
}

void AHPCharacter::OnGrabCompleted() { Server_CancelRevive(); }
void AHPCharacter::OnThrowStarted()  { if (BodyState == EHPBodyState::Normal) { CarryComponent->BeginThrowCharge(); } }
void AHPCharacter::OnThrowCompleted() { CarryComponent->ReleaseThrow(); }
void AHPCharacter::OnHoller()        { Server_Holler(); }
void AHPCharacter::OnUseTool()       { if (BodyState == EHPBodyState::Normal) { Server_UseTool(); } }
void AHPCharacter::OnSelectNet()     { Server_SelectTool(EHPTool::Net); }
void AHPCharacter::OnSelectFeed()    { Server_SelectTool(EHPTool::FeedBucket); }

AHPCharacter* AHPCharacter::FindReviveTarget() const
{
	AHPCharacter* Best = nullptr;
	float BestDist = ReviveRangeCm;
	for (TActorIterator<AHPCharacter> It(GetWorld()); It; ++It)
	{
		AHPCharacter* Other = *It;
		if (Other == this || Other->BodyState != EHPBodyState::KnockedOut)
		{
			continue;
		}
		const FVector OtherLoc = Other->GetMesh() ? Other->GetMesh()->GetSocketLocation(Other->PelvisBone) : Other->GetActorLocation();
		const float Dist = FVector::Dist(OtherLoc, GetActorLocation());
		if (Dist < BestDist)
		{
			Best = Other;
			BestDist = Dist;
		}
	}
	return Best;
}

// ---------------------------------------------------------------- Server RPCs

void AHPCharacter::Server_Holler_Implementation()
{
	const double Now = GetWorld()->GetTimeSeconds();
	if (Now - LastHollerTime < HollerCooldown || BodyState == EHPBodyState::KnockedOut)
	{
		return;
	}
	LastHollerTime = Now;
	if (UHPNoiseSubsystem* Noise = GetWorld()->GetSubsystem<UHPNoiseSubsystem>())
	{
		FNoiseEvent Event;
		Event.Source = ENoiseSource::Shout;
		Event.Loudness = HollerLoudness;
		Event.Location = GetActorLocation();
		Event.Instigator = this;
		Event.BlamedPlayer = GetPlayerState();
		Noise->Emit(Event);
	}
}

void AHPCharacter::Server_SelectTool_Implementation(EHPTool Tool)
{
	ActiveTool = Tool;
}

void AHPCharacter::Server_UseTool_Implementation()
{
	UWorld* World = GetWorld();
	const double Now = World->GetTimeSeconds();
	if (BodyState != EHPBodyState::Normal)
	{
		return;
	}
	if (ActiveTool == EHPTool::Net)
	{
		if (Now - LastToolTime < NetCooldown || CarryComponent->IsHolding())
		{
			return;
		}
		LastToolTime = Now;
		// Simplification for the slice: the net scoops the nearest small carryable in the arc into
		// the player's hands (no bag body / critter escape yet -- critter-ai owns that).
		TArray<FOverlapResult> Overlaps;
		FCollisionQueryParams Params(SCENE_QUERY_STAT(HPNet), false, this);
		FCollisionObjectQueryParams NetObjects;
		NetObjects.AddObjectTypesToQuery(ECC_PhysicsBody);
		NetObjects.AddObjectTypesToQuery(ECC_Pawn); // critters
		World->OverlapMultiByObjectType(Overlaps, GetActorLocation(), FQuat::Identity, NetObjects,
			FCollisionShape::MakeSphere(NetReachCm), Params);
		const float CosHalfArc = FMath::Cos(FMath::DegreesToRadians(NetArcDegrees * 0.5f));
		AActor* Best = nullptr;
		float BestDist = TNumericLimits<float>::Max();
		for (const FOverlapResult& O : Overlaps)
		{
			AActor* A = O.GetActor();
			const IHPCarryable* C = Cast<IHPCarryable>(A);
			if (!C || C->GetCarryHands() > NetMaxCarryHands)
			{
				continue;
			}
			const FVector To = A->GetActorLocation() - GetActorLocation();
			if (FVector::DotProduct(To.GetSafeNormal2D(), GetActorForwardVector().GetSafeNormal2D()) < CosHalfArc)
			{
				continue;
			}
			if (To.Size() < BestDist)
			{
				Best = A;
				BestDist = To.Size();
			}
		}
		if (Best)
		{
			CarryComponent->ServerGrabActor(Best);
		}
	}
	else if (ActiveTool == EHPTool::FeedBucket)
	{
		if (Now - LastToolTime < FeedCooldown || FeedUsesLeft <= 0)
		{
			return;
		}
		LastToolTime = Now;
		--FeedUsesLeft;
		if (UHPNoiseSubsystem* Noise = World->GetSubsystem<UHPNoiseSubsystem>())
		{
			FNoiseEvent Event;
			Event.Source = ENoiseSource::Tool;
			Event.Loudness = FeedLoudness;
			Event.Location = GetActorLocation();
			Event.Instigator = this;
			Event.BlamedPlayer = GetPlayerState();
			Event.Tags.AddTag(TAG_HP_Noise_Food);
			Noise->Emit(Event);
		}
	}
}

void AHPCharacter::Server_GetUp_Implementation()
{
	if (BodyState == EHPBodyState::Ragdoll && SettledSeconds >= GetUpSettleSeconds)
	{
		SetBodyState(EHPBodyState::Normal);
	}
}

void AHPCharacter::Server_BeginRevive_Implementation(AHPCharacter* Target)
{
	if (IsValid(Target) && Target != this && Target->BodyState == EHPBodyState::KnockedOut
		&& FVector::Dist(Target->GetActorLocation(), GetActorLocation()) <= ReviveRangeCm * 1.5f)
	{
		ReviveTarget = Target;
		ReviveStartTime = GetWorld()->GetTimeSeconds();
	}
}

void AHPCharacter::Server_CancelRevive_Implementation()
{
	ReviveTarget.Reset();
}

void AHPCharacter::HPRunLater(float Seconds, const FString& Command)
{
	FTimerHandle Handle;
	TWeakObjectPtr<AHPCharacter> WeakThis(this);
	GetWorldTimerManager().SetTimer(Handle, FTimerDelegate::CreateLambda([WeakThis, Command]()
	{
		if (APlayerController* PC = WeakThis.IsValid() ? Cast<APlayerController>(WeakThis->GetController()) : nullptr)
		{
			PC->ConsoleCommand(Command);
		}
	}), FMath::Max(Seconds, 0.01f), false);
}

void AHPCharacter::HPSetView(float Pitch, float Yaw)
{
	if (AController* C = GetController())
	{
		C->SetControlRotation(FRotator(Pitch, Yaw, 0.f));
	}
}

void AHPCharacter::HPDumpArms()
{
	const FTransform Cam = FirstPersonCamera->GetComponentTransform();
	UE_LOG(LogTemp, Log, TEXT("[HPArms] asset=%s visible=%d bones=%d cam=%s"), *GetNameSafe(FirstPersonArms->GetSkinnedAsset()),
		FirstPersonArms->IsVisible(), FirstPersonArms->GetNumBones(), *Cam.GetLocation().ToString());
	for (const TCHAR* Bone : {TEXT("spine_05"), TEXT("upperarm_r"), TEXT("hand_r"), TEXT("hand_l")})
	{
		UE_LOG(LogTemp, Log, TEXT("[HPArms] %s local-to-cam=%s"), Bone,
			*Cam.InverseTransformPosition(FirstPersonArms->GetBoneLocationByName(Bone, EBoneSpaces::WorldSpace)).ToString());
	}
	UE_LOG(LogTemp, Log, TEXT("[HPArms] held=%s aim=%s"), *GetNameSafe(CarryComponent->GetHeldActor()), *GetNameSafe(CarryComponent->TraceAimTargetFromView()));
}

void AHPCharacter::HPGrab()
{
	OnGrabStarted();
}

void AHPCharacter::HPImpact(float ImpulseNs)
{
	Server_DebugImpact(ImpulseNs);
}

void AHPCharacter::Server_DebugImpact_Implementation(float ImpulseNs)
{
#if !UE_BUILD_SHIPPING
	ReceiveImpactImpulse(FMath::Clamp(ImpulseNs, 0.f, 10000.f), INDEX_NONE);
#endif
}

// ---------------------------------------------------------------- Ragdoll / KO

float AHPCharacter::GetCurrentRagdollThreshold() const
{
	return HPCarryMath::RagdollThreshold(RagdollImpulseNs, CarryComponent->GetLoadRatio(), RagdollLoadFactor,
		Stamina, TiredStamina, TiredThresholdMult, 1.f /* mud_mult: bridge-hazards not built yet */);
}

void AHPCharacter::ReceiveImpactImpulse(float ImpulseNs, int32 InstigatorPlayerId)
{
	if (!HasAuthority() || BodyState == EHPBodyState::KnockedOut)
	{
		return;
	}
	Client_ImpactShake(FMath::Clamp(ImpulseNs / FMath::Max(ShakeFullImpulseNs, 1.f), 0.f, 1.f));
	const float Threshold = GetCurrentRagdollThreshold();
	if (ImpulseNs < Threshold)
	{
		return;
	}
	const double Now = GetWorld()->GetTimeSeconds();
	RecentRagdollTimes.RemoveAll([&](double T) { return Now - T > RagdollKOWindow; });
	RecentRagdollTimes.Add(Now);
	const bool bKO = ImpulseNs >= KOImpulseMult * Threshold || RecentRagdollTimes.Num() >= RagdollsForKO;
	UE_LOG(LogTemp, Log, TEXT("[HPCharacter] %s impact %.0f N*s (threshold %.0f, instigator %d) -> %s"),
		*GetName(), ImpulseNs, Threshold, InstigatorPlayerId, bKO ? TEXT("KO") : TEXT("Ragdoll"));
	SetBodyState(bKO ? EHPBodyState::KnockedOut : EHPBodyState::Ragdoll);
}

USkeletalMeshComponent* AHPCharacter::GetBodyMesh3P() const
{
	return GetMesh();
}

UMeshComponent* AHPCharacter::GetArmsMesh1P() const
{
	return FirstPersonArms;
}

void AHPCharacter::ServerRespawnAtBarn(EHPBlameVerb Verb, bool bChargeFee)
{
	if (!HasAuthority())
	{
		return;
	}
	if (bChargeFee)
	{
		if (AHPGameState* GS = GetWorld()->GetGameState<AHPGameState>())
		{
			GS->ChargeRespawnFee(GetPlayerState(), Verb); // logs the Blame Event at the pre-teleport location
		}
	}
	if (CarryComponent->IsHolding())
	{
		CarryComponent->ServerRelease(false);
	}
	if (BodyState != EHPBodyState::Normal)
	{
		SetBodyState(EHPBodyState::Normal);
	}
	if (const AHPBarnRespawn* Barn = AHPBarnRespawn::Find(GetWorld()))
	{
		const APlayerState* PS = GetPlayerState();
		const FTransform Slot = Barn->GetRespawnTransform(PS ? PS->GetPlayerId() : 0);
		TeleportTo(Slot.GetLocation(), Slot.Rotator());
		if (AController* C = GetController())
		{
			C->SetControlRotation(Slot.Rotator());
		}
	}
	GetCharacterMovement()->StopMovementImmediately();
	GetCharacterMovement()->SetMovementMode(MOVE_Falling);
	Stamina = StaminaMax;
}

void AHPCharacter::HPEmoteFaint(float Seconds)
{
	if (!HasAuthority() || BodyState != EHPBodyState::Normal)
	{
		return;
	}
	SetBodyState(EHPBodyState::Ragdoll); // releases any held item with its velocity
	FTimerHandle Handle;
	GetWorldTimerManager().SetTimer(Handle, FTimerDelegate::CreateWeakLambda(this, [this]()
	{
		if (BodyState == EHPBodyState::Ragdoll)
		{
			SetBodyState(EHPBodyState::Normal);
		}
	}), FMath::Max(0.5f, Seconds), false);
}

void AHPCharacter::HPPatch()
{
	Server_PatchPlank();
}

void AHPCharacter::Server_PatchPlank_Implementation()
{
	AHPBridgePlank* Best = nullptr;
	float BestDist = 450.f;
	for (TActorIterator<AHPBridgePlank> It(GetWorld()); It; ++It)
	{
		const float D = FVector::Dist(It->GetActorLocation(), GetActorLocation());
		if (It->GetPlankState() == EHPPlankState::Broken && D < BestDist)
		{
			Best = *It;
			BestDist = D;
		}
	}
	if (Best)
	{
		Best->ServerPatch(this);
	}
}

void AHPCharacter::ServerRecover()
{
	if (HasAuthority() && BodyState != EHPBodyState::Normal)
	{
		SetBodyState(EHPBodyState::Normal);
	}
}

void AHPCharacter::SetBodyState(EHPBodyState NewState)
{
	check(HasAuthority());
	if (BodyState == NewState)
	{
		return;
	}
	if (NewState != EHPBodyState::Normal && CarryComponent->IsHolding())
	{
		CarryComponent->ServerRelease(false); // item flies with the hand's velocity
	}
	BodyState = NewState;
	BodyStateStartTime = GetWorld()->GetTimeSeconds();
	SettledSeconds = 0.f;
	ApplyBodyState();
}

void AHPCharacter::OnRep_BodyState()
{
	ApplyBodyState();
}

void AHPCharacter::ApplyBodyState()
{
	USkeletalMeshComponent* MeshComp = GetMesh();
	UCharacterMovementComponent* MoveComp = GetCharacterMovement();
	const bool bRagdoll = BodyState != EHPBodyState::Normal;
	const bool bAlreadyRagdoll = MeshComp->IsSimulatingPhysics();

	if (bRagdoll && !bAlreadyRagdoll)
	{
		MoveComp->StopMovementImmediately();
		MoveComp->DisableMovement();
		MoveComp->NetworkSmoothingMode = ENetworkSmoothingMode::Disabled;
		GetCapsuleComponent()->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		MeshComp->DetachFromComponent(FDetachmentTransformRules::KeepWorldTransform);
		MeshComp->SetCollisionProfileName(TEXT("Ragdoll"));
		MeshComp->SetAllBodiesSimulatePhysics(true);
		MeshComp->SetSimulatePhysics(true);
		MeshComp->WakeAllRigidBodies();
		if (IsLocallyControlled())
		{
			MeshComp->SetOwnerNoSee(false);
			FirstPersonCamera->bUsePawnControlRotation = false;
			FirstPersonCamera->AttachToComponent(MeshComp, FAttachmentTransformRules::SnapToTargetNotIncludingScale, HeadSocket);
			FirstPersonCamera->SetRelativeLocationAndRotation(FVector(0.f, 25.f, 0.f), FRotator(0.f, 90.f, -90.f));
		}
	}
	else if (!bRagdoll && bAlreadyRagdoll)
	{
		const FVector Pelvis = MeshComp->GetSocketLocation(PelvisBone);
		MeshComp->SetSimulatePhysics(false);
		MeshComp->SetAllBodiesSimulatePhysics(false);
		MeshComp->SetCollisionProfileName(TEXT("CharacterMesh"));
		MeshComp->AttachToComponent(GetCapsuleComponent(), FAttachmentTransformRules::SnapToTargetNotIncludingScale);
		MeshComp->SetRelativeTransform(MeshRelativeTransform);
		GetCapsuleComponent()->SetCollisionEnabled(ECollisionEnabled::QueryAndPhysics);
		MoveComp->NetworkSmoothingMode = ENetworkSmoothingMode::Exponential;
		MeshComp->SetOwnerNoSee(true);
		FirstPersonCamera->AttachToComponent(GetCapsuleComponent(), FAttachmentTransformRules::SnapToTargetNotIncludingScale);
		FirstPersonCamera->SetRelativeLocationAndRotation(FVector(0.f, 0.f, EyeHeightCm), FRotator::ZeroRotator);
		FirstPersonCamera->bUsePawnControlRotation = true;
		if (HasAuthority())
		{
			FHitResult Floor;
			FCollisionQueryParams Params(SCENE_QUERY_STAT(HPGetUp), false, this);
			const float HalfHeight = GetCapsuleComponent()->GetScaledCapsuleHalfHeight();
			FVector Target = Pelvis + FVector(0, 0, HalfHeight);
			if (GetWorld()->LineTraceSingleByChannel(Floor, Pelvis + FVector(0, 0, 50), Pelvis - FVector(0, 0, 300), ECC_Visibility, Params))
			{
				Target = Floor.Location + FVector(0, 0, HalfHeight + 2.f);
			}
			SetActorLocation(Target, false, nullptr, ETeleportType::TeleportPhysics);
			MoveComp->SetMovementMode(MOVE_Walking);
		}
	}
}

void AHPCharacter::TickBodyState(float DeltaSeconds)
{
	if (!HasAuthority())
	{
		return;
	}
	const double Now = GetWorld()->GetTimeSeconds();
	if (BodyState != EHPBodyState::Normal)
	{
		// Keep the (collision-less) capsule on the body for relevancy, revive range and get-up.
		const FVector Pelvis = GetMesh()->GetSocketLocation(PelvisBone);
		SetActorLocation(Pelvis, false, nullptr, ETeleportType::TeleportPhysics);
		const float Speed = GetMesh()->GetPhysicsLinearVelocity(PelvisBone).Size();
		SettledSeconds = Speed < GetUpMaxSpeed ? SettledSeconds + DeltaSeconds : 0.f;
		if (BodyState == EHPBodyState::KnockedOut && Now - BodyStateStartTime >= KODuration)
		{
			ServerRespawnAtBarn(EHPBlameVerb::KnockedOut, true); // unrevived: carted back to the barn for a fee
		}
	}

	if (AHPCharacter* Target = ReviveTarget.Get())
	{
		const bool bValid = BodyState == EHPBodyState::Normal && Target->BodyState == EHPBodyState::KnockedOut
			&& FVector::Dist(Target->GetActorLocation(), GetActorLocation()) <= ReviveRangeCm * 1.5f;
		if (!bValid)
		{
			ReviveTarget.Reset();
		}
		else if (Now - ReviveStartTime >= ReviveHoldSeconds)
		{
			Target->ServerRecover();
			ReviveTarget.Reset();
		}
	}
}

// ---------------------------------------------------------------- Stamina / speed

void AHPCharacter::TickStamina(float DeltaSeconds)
{
	if (!HasAuthority())
	{
		return;
	}
	const double Now = GetWorld()->GetTimeSeconds();
	const bool bMoving = GetVelocity().SizeSquared2D() > 25.f;
	const bool bSprinting = bWantsSprint && !bSprintExhausted && bMoving && !bIsCrouched && BodyState == EHPBodyState::Normal;
	const float CarryDrain = CarryDrainPerSec * CarryComponent->GetLoadRatio() * (CarryComponent->IsHolding() ? 1.f : 0.f);

	float Delta = -CarryDrain;
	if (bSprinting)
	{
		Delta -= SprintDrainPerSec;
		LastSprintTime = Now;
	}
	else if (Now - LastSprintTime >= StaminaRegenDelay)
	{
		Delta += StaminaRegenPerSec;
	}
	Stamina = FMath::Clamp(Stamina + Delta * DeltaSeconds, 0.f, StaminaMax);
	if (Stamina <= 0.f)
	{
		bSprintExhausted = true;
	}
	else if (bSprintExhausted && Stamina >= SprintResumeStamina)
	{
		bSprintExhausted = false;
	}
}

void AHPCharacter::UpdateMovementSpeed()
{
	UCharacterMovementComponent* MoveComp = GetCharacterMovement();
	const bool bSprint = bWantsSprint && !bSprintExhausted;
	const float Mult = CarryComponent->GetSpeedMultiplier();
	MoveComp->MaxWalkSpeed = (bSprint ? SprintSpeed : WalkSpeed) * Mult;
	MoveComp->MaxWalkSpeedCrouched = CrouchSpeed * Mult;
}

void AHPCharacter::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	TickStamina(DeltaSeconds);
	TickBodyState(DeltaSeconds);
	UpdateMovementSpeed();
	TickFirstPerson(DeltaSeconds);
}
