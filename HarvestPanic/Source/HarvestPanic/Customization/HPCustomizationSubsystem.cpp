// Copyright HARVEST PANIC team. All Rights Reserved.

#include "HPCustomizationSubsystem.h"
#include "HPCosmeticsComponent.h"
#include "HPCosmeticApplier.h"
#include "HPCosmeticCatalog.h"
#include "HPCustomizationWidget.h"
#include "HPWardrobe.h"
#include "HarvestPanic.h"
#include "EngineUtils.h"
#include "Engine/World.h"
#include "Engine/GameInstance.h"
#include "Engine/LocalPlayer.h"
#include "Engine/Engine.h"
#include "Engine/SkeletalMesh.h"
#include "GameFramework/GameModeBase.h"
#include "GameFramework/GameStateBase.h"
#include "GameFramework/PlayerState.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/Character.h"
#include "Camera/CameraActor.h"
#include "Camera/CameraComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/TextRenderComponent.h"
#include "Animation/AnimInstance.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "InputAction.h"
#include "InputMappingContext.h"
#include "UnrealClient.h"
#include "TimerManager.h"
#include "Misc/Paths.h"
#include "HAL/IConsoleManager.h"

// ------------------------------------------------------------------ lifecycle

void UHPCustomizationSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);
	PostLoginHandle = FGameModeEvents::GameModePostLoginEvent.AddUObject(this, &UHPCustomizationSubsystem::HandlePostLogin);
	TickHandle = FTSTicker::GetCoreTicker().AddTicker(FTickerDelegate::CreateUObject(this, &UHPCustomizationSubsystem::HandleTick));
}

void UHPCustomizationSubsystem::Deinitialize()
{
	FGameModeEvents::GameModePostLoginEvent.Remove(PostLoginHandle);
	FTSTicker::GetCoreTicker().RemoveTicker(TickHandle);
	Super::Deinitialize();
}

void UHPCustomizationSubsystem::HandlePostLogin(AGameModeBase* GameMode, APlayerController* NewPlayer)
{
	// Static event fires for every game instance (PIE): only handle our own world.
	if (NewPlayer && NewPlayer->GetGameInstance() == GetGameInstance())
	{
		UHPCosmeticsComponent::EnsureOnPlayerState(NewPlayer->PlayerState);
	}
}

bool UHPCustomizationSubsystem::HandleTick(float DeltaTime)
{
	UWorld* World = GetGameInstance() ? GetGameInstance()->GetWorld() : nullptr;
	if (!World) { return true; }

	for (int32 i = ShowcaseEmotes.Num() - 1; i >= 0; --i)
	{
		if (!ShowcaseEmotes[i].Tick(DeltaTime)) { ShowcaseEmotes.RemoveAt(i); }
	}

	EnsureTimer -= DeltaTime;
	if (EnsureTimer > 0.f) { return true; }
	EnsureTimer = 0.25f;
	EnsureServerComponents(World);   // covers seamless travel, bots, anything PostLogin missed
	EnsureLocalInput(World);
	return true;
}

void UHPCustomizationSubsystem::EnsureServerComponents(UWorld* World)
{
	if (World->GetNetMode() == NM_Client) { return; }
	if (const AGameStateBase* GS = World->GetGameState())
	{
		for (APlayerState* PS : GS->PlayerArray)
		{
			if (PS && !PS->IsABot()) { UHPCosmeticsComponent::EnsureOnPlayerState(PS); }
		}
	}
}

// ------------------------------------------------------------------ input

void UHPCustomizationSubsystem::BuildInputAssets()
{
	if (MappingContext) { return; }
	MappingContext = NewObject<UInputMappingContext>(this, TEXT("IMC_HPCustomization"));
	const FKey EmoteKeys[] = { EKeys::Five, EKeys::Six, EKeys::Seven, EKeys::Eight };
	const TCHAR* Names[] = { TEXT("IA_HPEmote_Wave"), TEXT("IA_HPEmote_Point"), TEXT("IA_HPEmote_Dance"), TEXT("IA_HPEmote_Faint") };
	for (int32 i = 0; i < 4; ++i)
	{
		UInputAction* A = NewObject<UInputAction>(this, Names[i]);
		A->ValueType = EInputActionValueType::Boolean;
		MappingContext->MapKey(A, EmoteKeys[i]);
		EmoteActions.Add(A);
	}
	WardrobeAction = NewObject<UInputAction>(this, TEXT("IA_HPWardrobe"));
	WardrobeAction->ValueType = EInputActionValueType::Boolean;
	MappingContext->MapKey(WardrobeAction, EKeys::C);
}

void UHPCustomizationSubsystem::EnsureLocalInput(UWorld* World)
{
	APlayerController* PC = GetGameInstance()->GetFirstLocalPlayerController(World);
	if (!PC || !PC->GetLocalPlayer()) { return; }
	BuildInputAssets();

	if (UEnhancedInputLocalPlayerSubsystem* EIS = PC->GetLocalPlayer()->GetSubsystem<UEnhancedInputLocalPlayerSubsystem>())
	{
		// Re-added if other code clears all mappings (e.g. on possess).
		if (!EIS->HasMappingContext(MappingContext))
		{
			EIS->AddMappingContext(MappingContext, InputPriority);
		}
	}

	if (InputPC.Get() != PC || !InputComp || !InputComp->IsRegistered())
	{
		InputComp = NewObject<UEnhancedInputComponent>(PC, TEXT("HPCustomizationInput"));
		InputComp->RegisterComponent();
		InputComp->BindAction(EmoteActions[0], ETriggerEvent::Started, this, &UHPCustomizationSubsystem::OnEmote1);
		InputComp->BindAction(EmoteActions[1], ETriggerEvent::Started, this, &UHPCustomizationSubsystem::OnEmote2);
		InputComp->BindAction(EmoteActions[2], ETriggerEvent::Started, this, &UHPCustomizationSubsystem::OnEmote3);
		InputComp->BindAction(EmoteActions[3], ETriggerEvent::Started, this, &UHPCustomizationSubsystem::OnEmote4);
		InputComp->BindAction(WardrobeAction, ETriggerEvent::Started, this, &UHPCustomizationSubsystem::OnWardrobeKey);
		PC->PushInputComponent(InputComp);
		InputPC = PC;
	}
}

void UHPCustomizationSubsystem::TriggerEmote(EHPEmote Emote)
{
	APlayerController* PC = InputPC.Get();
	if (UHPCosmeticsComponent* Comp = PC ? UHPCosmeticsComponent::FindForPlayerState(PC->PlayerState) : nullptr)
	{
		Comp->RequestEmote(Emote);
	}
}

void UHPCustomizationSubsystem::OnWardrobeKey()
{
	APlayerController* PC = InputPC.Get();
	if (!PC || OpenWidget.IsValid()) { return; }
	for (TActorIterator<AHPWardrobe> It(PC->GetWorld()); It; ++It)
	{
		if (It->IsPawnInRange(PC->GetPawn()))
		{
			OpenWardrobe(PC);
			return;
		}
	}
}

UHPCustomizationWidget* UHPCustomizationSubsystem::OpenWardrobe(APlayerController* PC)
{
	if (!PC) { return nullptr; }
	if (OpenWidget.IsValid()) { return OpenWidget.Get(); }
	UHPCustomizationWidget* W = CreateWidget<UHPCustomizationWidget>(PC, UHPCustomizationWidget::StaticClass());
	if (!W) { return nullptr; }
	W->bRestoreGameInputOnClose = true;
	W->AddToViewport(50);
	FInputModeUIOnly Mode;
	Mode.SetWidgetToFocus(W->TakeWidget());
	PC->SetInputMode(Mode);
	PC->SetShowMouseCursor(true);
	OpenWidget = W;
	return W;
}

// ------------------------------------------------------------------ QA showcase

namespace
{
	FHPCosmeticLoadout MakeShowcaseLoadout(int32 Lineup, int32 Index)
	{
		FHPCosmeticLoadout L;
		struct FRow { EHPBodyPreset Body; uint8 P, S, A, Skin; uint16 Hat, Face, Back; uint8 Tag; };
		static const FRow Rows[2][4] = {
			{ { EHPBodyPreset::Manny, 10, 0, 16, 3, 101, 201, 301, 3 },
			  { EHPBodyPreset::Quinn, 14, 19, 21, 9, 108, 203, 302, 14 },
			  { EHPBodyPreset::Manny, 6, 3, 17, 6, 109, 202, 303, 5 },
			  { EHPBodyPreset::Quinn, 11, 2, 22, 1, 104, 0, 302, 2 } },
			{ { EHPBodyPreset::Quinn, 0, 18, 16, 4, 105, 201, 0, 0 },
			  { EHPBodyPreset::Manny, 19, 9, 21, 10, 106, 204, 302, 9 },
			  { EHPBodyPreset::Quinn, 12, 5, 13, 2, 107, 203, 301, 12 },
			  { EHPBodyPreset::Manny, 21, 23, 22, 7, 110, 202, 303, 23 } } };
		const FRow& R = Rows[FMath::Clamp(Lineup, 0, 1)][Index % 4];
		L.BodyPreset = R.Body; L.PrimaryColor = R.P; L.SecondaryColor = R.S; L.AccentColor = R.A; L.SkinTone = R.Skin;
		L.Hat = R.Hat; L.Face = R.Face; L.Back = R.Back; L.NameTagColor = R.Tag;
		return L;
	}
}

void UHPCustomizationSubsystem::RunShowcase(UWorld* World, const FString& OutDirIn, bool bQuitAfter)
{
	APlayerController* PC = World ? GetGameInstance()->GetFirstLocalPlayerController(World) : nullptr;
	if (!PC) { UE_LOG(LogHarvestPanic, Warning, TEXT("[Customization] showcase: no local PC")); return; }
	const FString OutDir = OutDirIn.IsEmpty() ? FPaths::ProjectSavedDir() / TEXT("Customization") : OutDirIn;

	FVector Base = FVector::ZeroVector;
	FRotator Facing = FRotator::ZeroRotator;
	if (APawn* P = PC->GetPawn())
	{
		Base = P->GetActorLocation();
		Facing = FRotator(0, P->GetActorRotation().Yaw, 0);
		P->SetActorHiddenInGame(true);
	}
	const FVector Fwd = Facing.Vector();
	const FVector Right = FRotationMatrix(Facing).GetUnitAxis(EAxis::Y);
	const FVector LineCenter = Base + Fwd * 500.f;

	const UHPCosmeticCatalog* Cat = UHPCosmeticCatalog::Get();
	UClass* AnimClass = LoadClass<UAnimInstance>(nullptr, TEXT("/Game/Characters/Mannequins/Anims/Unarmed/ABP_Unarmed.ABP_Unarmed_C"));
	const TCHAR* Names[2][4] = { { TEXT("Big Earl"), TEXT("ConeQueen"), TEXT("Ragnar_Mudd"), TEXT("Pumpkin Pete") },
		{ TEXT("Dusty"), TEXT("Chef Boy"), TEXT("Daisy"), TEXT("Grandpa Jr.") } };

	TArray<TWeakObjectPtr<ACharacter>> Farmers;
	auto SpawnLineup = [=, this, &Farmers](int32 Lineup)
	{
		for (TWeakObjectPtr<ACharacter>& F : Farmers) { if (F.IsValid()) { F->Destroy(); } }
		Farmers.Reset();
		for (int32 i = 0; i < 4; ++i)
		{
			const FVector Loc = LineCenter + Right * ((i - 1.5f) * 120.f) + FVector(0, 0, 20);
			FActorSpawnParameters SP;
			SP.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AdjustIfPossibleButAlwaysSpawn;
			ACharacter* C = World->SpawnActor<ACharacter>(ACharacter::StaticClass(), Loc, Facing + FRotator(0, 180, 0), SP);
			if (!C) { continue; }
			USkeletalMeshComponent* M = C->GetMesh();
			M->SetSkeletalMeshAsset(Cat->MannyMesh.LoadSynchronous());
			M->SetRelativeLocationAndRotation(FVector(0, 0, -90), FRotator(0, -90, 0));
			if (AnimClass) { M->SetAnimInstanceClass(AnimClass); }
			const FHPCosmeticLoadout L = MakeShowcaseLoadout(Lineup, i);
			FHPCosmeticApplier::ApplyToBody(M, L);
			FHPCosmeticApplier::EnsureNameTag(C, Names[Lineup][i], Cat->GetPaletteColor(L.NameTagColor), Cat->NameTagHeight, Cat->NameTagWorldSize);
			Farmers.Add(C);
		}
	};
	SpawnLineup(0);

	ACameraActor* Cam = World->SpawnActor<ACameraActor>(LineCenter - Fwd * 390.f + FVector(0, 0, 55), Facing + FRotator(-4, 0, 0));
	Cam->GetCameraComponent()->SetFieldOfView(62.f);
	Cam->GetCameraComponent()->bConstrainAspectRatio = false;
	PC->SetViewTarget(Cam);

	// Name tags face the showcase camera.
	auto FaceTags = [Cam, &Farmers]()
	{
		for (const TWeakObjectPtr<ACharacter>& F : Farmers)
		{
			if (!F.IsValid()) { continue; }
			TArray<UTextRenderComponent*> Tags; F->GetComponents(Tags);
			for (UTextRenderComponent* T : Tags) { FHPCosmeticApplier::FaceNameTag(T, Cam->GetActorLocation()); }
		}
	};

	// Timed script (game time). Lambdas capture by value; Farmers lives in a shared array.
	TSharedRef<TArray<TWeakObjectPtr<ACharacter>>> Shared = MakeShared<TArray<TWeakObjectPtr<ACharacter>>>(Farmers);
	FTimerManager& TM = World->GetTimerManager();
	auto At = [&TM](float T, TFunction<void()> Fn)
	{
		FTimerHandle H;
		TM.SetTimer(H, FTimerDelegate::CreateLambda(MoveTemp(Fn)), T, false);
	};
	auto Shot = [OutDir](const TCHAR* Name, bool bUI)
	{
		const FString Path = OutDir / Name;
		FScreenshotRequest::RequestScreenshot(Path, bUI, false);
		UE_LOG(LogHarvestPanic, Display, TEXT("[Customization] screenshot -> %s"), *Path);
	};
	FaceTags();

	At(1.5f, [this, Shared]()
	{
		// Pumpkin Pete dances in the first lineup shot: emotes should read in a still too.
		if (Shared->IsValidIndex(3) && (*Shared)[3].IsValid())
		{
			FHPEmotePlayback& E = ShowcaseEmotes.AddDefaulted_GetRef();
			E.Start((*Shared)[3]->GetMesh(), EHPEmote::Wave, false);
		}
	});
	At(3.0f, [Shot]() { Shot(TEXT("customization-lineup-a.png"), false); });
	At(3.5f, [=, this]()
	{
		for (TWeakObjectPtr<ACharacter>& F : *Shared) { if (F.IsValid()) { F->Destroy(); } }
		Shared->Reset();
		for (int32 i = 0; i < 4; ++i)
		{
			const FVector Loc = LineCenter + Right * ((i - 1.5f) * 120.f) + FVector(0, 0, 20);
			FActorSpawnParameters SP;
			SP.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AdjustIfPossibleButAlwaysSpawn;
			ACharacter* C = World->SpawnActor<ACharacter>(ACharacter::StaticClass(), Loc, Facing + FRotator(0, 180, 0), SP);
			if (!C) { continue; }
			USkeletalMeshComponent* M = C->GetMesh();
			M->SetSkeletalMeshAsset(Cat->MannyMesh.LoadSynchronous());
			M->SetRelativeLocationAndRotation(FVector(0, 0, -90), FRotator(0, -90, 0));
			if (AnimClass) { M->SetAnimInstanceClass(AnimClass); }
			const FHPCosmeticLoadout L = MakeShowcaseLoadout(1, i);
			FHPCosmeticApplier::ApplyToBody(M, L);
			UTextRenderComponent* Tag = FHPCosmeticApplier::EnsureNameTag(C, Names[1][i], Cat->GetPaletteColor(L.NameTagColor), Cat->NameTagHeight, Cat->NameTagWorldSize);
			FHPCosmeticApplier::FaceNameTag(Tag, Cam->GetActorLocation());
			Shared->Add(C);
		}
		if ((*Shared).IsValidIndex(1))
		{
			FHPEmotePlayback& E = ShowcaseEmotes.AddDefaulted_GetRef();
			E.Start((*Shared)[1]->GetMesh(), EHPEmote::Faint, false);
		}
	});
	At(5.5f, [Shot]() { Shot(TEXT("customization-lineup-b.png"), false); });
	At(6.0f, [this, PC]() { if (UHPCustomizationWidget* W = OpenWardrobe(PC)) { W->SelectTab(2); } });
	At(8.5f, [Shot]() { Shot(TEXT("customization-widget-hat.png"), true); });
	At(9.0f, [this]() { if (OpenWidget.IsValid()) { OpenWidget->SelectTab(1); } });
	At(10.5f, [Shot]() { Shot(TEXT("customization-widget-colours.png"), true); });
	if (bQuitAfter)
	{
		At(12.0f, [PC]() { PC->ConsoleCommand(TEXT("quit")); });
	}
}

// ------------------------------------------------------------------ console commands

static FAutoConsoleCommandWithWorldAndArgs GHPCustomizationShowcaseCmd(
	TEXT("HP.Customization.Showcase"),
	TEXT("QA: lineup of customized farmers + wardrobe widget screenshots. Args: [OutDir] [quit]"),
	FConsoleCommandWithWorldAndArgsDelegate::CreateLambda([](const TArray<FString>& Args, UWorld* World)
	{
		if (!World || !World->GetGameInstance()) { return; }
		if (UHPCustomizationSubsystem* S = World->GetGameInstance()->GetSubsystem<UHPCustomizationSubsystem>())
		{
			const bool bQuit = Args.Contains(TEXT("quit"));
			S->RunShowcase(World, Args.Num() > 0 && Args[0] != TEXT("quit") ? Args[0] : FString(), bQuit);
		}
	}));

static FAutoConsoleCommandWithWorldAndArgs GHPCustomizationOpenCmd(
	TEXT("HP.Customization.Open"),
	TEXT("Open the wardrobe widget for the first local player."),
	FConsoleCommandWithWorldAndArgsDelegate::CreateLambda([](const TArray<FString>& Args, UWorld* World)
	{
		if (!World || !World->GetGameInstance()) { return; }
		if (UHPCustomizationSubsystem* S = World->GetGameInstance()->GetSubsystem<UHPCustomizationSubsystem>())
		{
			S->OpenWardrobe(World->GetGameInstance()->GetFirstLocalPlayerController(World));
		}
	}));

static FAutoConsoleCommandWithWorldAndArgs GHPEmoteCmd(
	TEXT("HP.Emote"),
	TEXT("Play emote 0..3 (Wave, Point, Dance, Faint) for the first local player."),
	FConsoleCommandWithWorldAndArgsDelegate::CreateLambda([](const TArray<FString>& Args, UWorld* World)
	{
		APlayerController* PC = World && World->GetGameInstance() ? World->GetGameInstance()->GetFirstLocalPlayerController(World) : nullptr;
		if (UHPCosmeticsComponent* C = PC ? UHPCosmeticsComponent::FindForPlayerState(PC->PlayerState) : nullptr)
		{
			const int32 Idx = Args.Num() ? FCString::Atoi(*Args[0]) : 0;
			C->RequestEmote(static_cast<EHPEmote>(FMath::Clamp(Idx, 0, 3)));
		}
	}));
