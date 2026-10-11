#include "Frontend/HPLobbyGame.h"
#include "Frontend/HPGameInstance.h"
#include "Frontend/HPFrontendUISubsystem.h"
#include "Frontend/UI/HPLobbyWidget.h"
#include "Net/UnrealNetwork.h"
#include "Engine/World.h"
#include "TimerManager.h"
#include "GameFramework/GameSession.h"
#include "GameFramework/Pawn.h"
#include "GameFramework/Character.h"
#include "Components/BoxComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Components/TextRenderComponent.h"
#include "Components/PointLightComponent.h"
#include "Components/InputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "InputMappingContext.h"
#include "Engine/LocalPlayer.h"
#include "Engine/StaticMesh.h"
#include "UObject/ConstructorHelpers.h"
#include "Misc/CommandLine.h"
#include "Misc/Parse.h"
#include "Misc/PackageName.h"
#include "Interfaces/OnlineSessionInterface.h"
#include "OnlineSubsystemUtils.h"

DEFINE_LOG_CATEGORY_STATIC(LogHPLobby, Log, All);
#define LOCTEXT_NAMESPACE "HPFrontend"

static const TCHAR* HPNetTag(const UWorld* W)
{
	return (W && W->GetNetMode() == NM_Client) ? TEXT("client") : TEXT("server");
}

// =============================================================== PlayerState
void AHPLobbyPlayerState::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHPLobbyPlayerState, bReady);
	DOREPLIFETIME(AHPLobbyPlayerState, bOnPorch);
	DOREPLIFETIME(AHPLobbyPlayerState, bIsHost);
}

void AHPLobbyPlayerState::BeginPlay()
{
	Super::BeginPlay();
	UE_LOG(LogHPLobby, Log, TEXT("HPLobby[%s]: player state '%s' present (ready=%d host=%d)"), HPNetTag(GetWorld()), *GetPlayerName(), bReady ? 1 : 0, bIsHost ? 1 : 0);
}

void AHPLobbyPlayerState::OnRep_PlayerName()
{
	Super::OnRep_PlayerName();
	UE_LOG(LogHPLobby, Log, TEXT("HPLobby[%s]: name replicated -> '%s'"), HPNetTag(GetWorld()), *GetPlayerName());
	if (const AHPLobbyGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHPLobbyGameState>() : nullptr) GS->NotifyLocalUI();
}

void AHPLobbyPlayerState::OnRep_Ready()
{
	UE_LOG(LogHPLobby, Log, TEXT("HPLobby[%s]: '%s' ready=%d"), HPNetTag(GetWorld()), *GetPlayerName(), bReady ? 1 : 0);
	if (const AHPLobbyGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHPLobbyGameState>() : nullptr) GS->NotifyLocalUI();
}

bool AHPLobbyPlayerState::IsHost() const { return bIsHost; }

// =============================================================== GameState
void AHPLobbyGameState::GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& OutLifetimeProps) const
{
	Super::GetLifetimeReplicatedProps(OutLifetimeProps);
	DOREPLIFETIME(AHPLobbyGameState, ChatLines);
	DOREPLIFETIME(AHPLobbyGameState, CountdownSeconds);
}

void AHPLobbyGameState::AddPlayerState(APlayerState* PS)
{
	Super::AddPlayerState(PS);
	UE_LOG(LogHPLobby, Log, TEXT("HPLobby[%s]: roster now %d"), HPNetTag(GetWorld()), PlayerArray.Num());
	NotifyLocalUI();
}

void AHPLobbyGameState::RemovePlayerState(APlayerState* PS)
{
	Super::RemovePlayerState(PS);
	NotifyLocalUI();
}

void AHPLobbyGameState::OnRep_Lobby()
{
	NotifyLocalUI();
}

void AHPLobbyGameState::AddChat(const FString& Line)
{
	ChatLines.Add(Line);
	while (ChatLines.Num() > MaxChatLines) ChatLines.RemoveAt(0);
	UE_LOG(LogHPLobby, Log, TEXT("HPLobby[%s]: chat '%s'"), HPNetTag(GetWorld()), *Line);
	NotifyLocalUI();
}

void AHPLobbyGameState::GetReadyCounts(int32& OutReady, int32& OutTotal) const
{
	OutReady = 0;
	OutTotal = 0;
	for (APlayerState* PS : PlayerArray)
	{
		if (const AHPLobbyPlayerState* L = Cast<AHPLobbyPlayerState>(PS))
		{
			++OutTotal;
			OutReady += L->bReady ? 1 : 0;
		}
	}
}

void AHPLobbyGameState::NotifyLocalUI() const
{
	if (UWorld* W = GetWorld())
	{
		if (APlayerController* PC = W->GetFirstPlayerController())
		{
			if (PC->IsLocalController())
			{
				if (UHPFrontendUISubsystem* UI = UHPFrontendUISubsystem::Get(PC)) UI->NotifyLobbyChanged();
			}
		}
	}
}

// =============================================================== GameMode
AHPLobbyGameMode::AHPLobbyGameMode()
{
	GameStateClass = AHPLobbyGameState::StaticClass();
	PlayerStateClass = AHPLobbyPlayerState::StaticClass();
	PlayerControllerClass = AHPLobbyPlayerController::StaticClass();
	bUseSeamlessTravel = false;
	// Walk around as the template farmer until the HP character branch provides its own BP.
	static ConstructorHelpers::FClassFinder<APawn> PawnBP(TEXT("/Game/ThirdPerson/Blueprints/BP_ThirdPersonCharacter"));
	if (PawnBP.Succeeded())
	{
		DefaultPawnClass = PawnBP.Class;
	}
}

AHPLobbyGameState* AHPLobbyGameMode::LobbyGS() const { return GetGameState<AHPLobbyGameState>(); }

void AHPLobbyGameMode::PreLogin(const FString& Options, const FString& Address, const FUniqueNetIdRepl& UniqueId, FString& ErrorMessage)
{
	Super::PreLogin(Options, Address, UniqueId, ErrorMessage);
	if (ErrorMessage.IsEmpty() && GetNumPlayers() >= UHPGameInstance::MaxPlayers)
	{
		ErrorMessage = TEXT("That farm is full (4/4).");
	}
}

void AHPLobbyGameMode::PostLogin(APlayerController* NewPlayer)
{
	Super::PostLogin(NewPlayer);
	if (AHPLobbyPlayerState* PS = NewPlayer ? NewPlayer->GetPlayerState<AHPLobbyPlayerState>() : nullptr)
	{
		PS->bIsHost = NewPlayer->IsLocalController();
		UE_LOG(LogHPLobby, Log, TEXT("HPLobby[server]: PostLogin '%s' host=%d players=%d"), *PS->GetPlayerName(), PS->bIsHost ? 1 : 0, GetNumPlayers());
	}
	if (AHPLobbyGameState* GS = LobbyGS())
	{
		GS->NotifyLocalUI();
	}
}

void AHPLobbyGameMode::Logout(AController* Exiting)
{
	if (APlayerState* PS = Exiting ? Exiting->PlayerState.Get() : nullptr)
	{
		if (AHPLobbyGameState* GS = LobbyGS())
		{
			GS->AddChat(FString::Printf(TEXT("%s left the yard."), *PS->GetPlayerName()));
		}
	}
	Super::Logout(Exiting);
}

void AHPLobbyGameMode::SetReady(APlayerController* PC, bool bReady)
{
	AHPLobbyPlayerState* PS = PC ? PC->GetPlayerState<AHPLobbyPlayerState>() : nullptr;
	if (!PS || PS->bReady == bReady)
	{
		return;
	}
	PS->bReady = bReady;
	PS->OnRep_Ready(); // server-side log/UI
	if (!bReady && LobbyGS() && LobbyGS()->CountdownSeconds > 0)
	{
		CancelCountdown(true);
	}
	if (AHPLobbyGameState* GS = LobbyGS()) GS->NotifyLocalUI();
}

void AHPLobbyGameMode::RingBell(APlayerController* Ringer)
{
	AHPLobbyGameState* GS = LobbyGS();
	AHPLobbyPlayerState* PS = Ringer ? Ringer->GetPlayerState<AHPLobbyPlayerState>() : nullptr;
	if (!GS || !PS)
	{
		return;
	}
	if (!PS->bIsHost)
	{
		GS->AddChat(FString::Printf(TEXT("*clonk* %s wants to start!"), *PS->GetPlayerName()));
		return;
	}
	if (GS->CountdownSeconds > 0)
	{
		return;
	}
	int32 Ready = 0, Total = 0;
	GS->GetReadyCounts(Ready, Total);
	const double Now = GetWorld()->GetTimeSeconds();
	const bool bArmed = (Now - StartAnywayArmedAt) <= StartAnywayWindow;
	if (Ready < Total && !bArmed)
	{
		StartAnywayArmedAt = Now;
		if (AHPLobbyPlayerController* LPC = Cast<AHPLobbyPlayerController>(Ringer)) LPC->ClientStartAnywayPrompt(Ready, Total);
		return;
	}
	StartAnywayArmedAt = -100.0;
	GS->AddChat(TEXT("DING DING DING! Dinner's in 3..."));
	GS->CountdownSeconds = StartCountdown;
	GS->NotifyLocalUI();
	if (StartCountdown <= 0)
	{
		StartDay();
		return;
	}
	GetWorldTimerManager().SetTimer(CountdownTimer, this, &AHPLobbyGameMode::TickCountdown, 1.f, true);
}

void AHPLobbyGameMode::TickCountdown()
{
	AHPLobbyGameState* GS = LobbyGS();
	if (!GS) return;
	GS->CountdownSeconds = FMath::Max(0, GS->CountdownSeconds - 1);
	GS->NotifyLocalUI();
	if (GS->CountdownSeconds == 0)
	{
		GetWorldTimerManager().ClearTimer(CountdownTimer);
		StartDay();
	}
}

void AHPLobbyGameMode::CancelCountdown(bool bSad)
{
	GetWorldTimerManager().ClearTimer(CountdownTimer);
	if (AHPLobbyGameState* GS = LobbyGS())
	{
		GS->CountdownSeconds = 0;
		if (bSad) GS->AddChat(TEXT("*sad bell* Someone wandered off the porch."));
	}
}

void AHPLobbyGameMode::StartDay()
{
	const FString URL = UHPGameInstance::ResolveDayMapURL();
	UE_LOG(LogHPLobby, Log, TEXT("HPLobby[server]: starting the day -> ServerTravel %s"), *URL);
	if (IOnlineSessionPtr Sessions = Online::GetSessionInterface(GetWorld()))
	{
		Sessions->StartSession(UHPGameInstance::SessionName);
	}
	GetWorld()->ServerTravel(URL, true);
}

void AHPLobbyGameMode::Chat(APlayerController* From, const FString& Text)
{
	const FString Clean = Text.TrimStartAndEnd().Left(120);
	APlayerState* PS = From ? From->PlayerState.Get() : nullptr;
	if (PS && !Clean.IsEmpty() && LobbyGS())
	{
		LobbyGS()->AddChat(FString::Printf(TEXT("%s: %s"), *PS->GetPlayerName(), *Clean));
	}
}

void AHPLobbyGameMode::Kick(APlayerController* Requester, APlayerState* Target)
{
	AHPLobbyPlayerState* ReqPS = Requester ? Requester->GetPlayerState<AHPLobbyPlayerState>() : nullptr;
	if (!ReqPS || !ReqPS->bIsHost || !Target || Target == ReqPS)
	{
		return;
	}
	APlayerController* TargetPC = Cast<APlayerController>(Target->GetOwner());
	if (TargetPC && GameSession)
	{
		GameSession->KickPlayer(TargetPC, LOCTEXT("Kicked", "The host sent you home from the farm."));
	}
}

// =============================================================== PlayerController
void AHPLobbyPlayerController::BeginPlay()
{
	Super::BeginPlay();
	if (!IsLocalController())
	{
		return;
	}
	// Template input contexts if present (the template's /Game/Input may not be in every checkout).
	if (UEnhancedInputLocalPlayerSubsystem* EIS = ULocalPlayer::GetSubsystem<UEnhancedInputLocalPlayerSubsystem>(GetLocalPlayer()))
	{
		for (const TCHAR* Path : { TEXT("/Game/Input/IMC_Default.IMC_Default"), TEXT("/Game/Input/IMC_MouseLook.IMC_MouseLook") })
		{
			if (FPackageName::DoesPackageExist(FPackageName::ObjectPathToPackageName(FString(Path))))
			{
				if (UInputMappingContext* IMC = LoadObject<UInputMappingContext>(nullptr, Path)) EIS->AddMappingContext(IMC, 0);
			}
		}
	}
	if (UHPGameInstance* GI = GetGameInstance<UHPGameInstance>())
	{
		ServerSetDisplayName(GI->GetLocalDisplayName());
	}
	if (UHPFrontendUISubsystem* UI = UHPFrontendUISubsystem::Get(this))
	{
		UI->ShowRoot(EHPMenuState::Lobby);
	}
	if (UHPLobbyHintWidget* Hint = CreateWidget<UHPLobbyHintWidget>(this, UHPLobbyHintWidget::StaticClass()))
	{
		Hint->AddToViewport(5);
	}
	AutoTest();
}

void AHPLobbyPlayerController::AutoTest()
{
	// Headless test hooks: -HPAutoReady (ready after 2 s) and -HPAutoBell (host rings once everyone is ready).
	if (FParse::Param(FCommandLine::Get(), TEXT("HPAutoReady")))
	{
		FTimerHandle H;
		GetWorldTimerManager().SetTimer(H, FTimerDelegate::CreateWeakLambda(this, [this]() { ServerSetReady(true); }), 2.f, false);
	}
	if (FParse::Param(FCommandLine::Get(), TEXT("HPAutoChat")))
	{
		FTimerHandle H;
		GetWorldTimerManager().SetTimer(H, FTimerDelegate::CreateWeakLambda(this, [this]() { ServerChat(TEXT("howdy from the test farm")); }), 3.f, false);
	}
	if (FParse::Param(FCommandLine::Get(), TEXT("HPAutoBell")) && HasAuthority())
	{
		FTimerHandle H;
		GetWorldTimerManager().SetTimer(H, FTimerDelegate::CreateWeakLambda(this, [this]()
		{
			const AHPLobbyGameState* GS = GetWorld()->GetGameState<AHPLobbyGameState>();
			int32 R = 0, T = 0;
			if (GS) GS->GetReadyCounts(R, T);
			if (GS && T >= 2 && R == T && GS->CountdownSeconds == 0) ServerRingBell();
		}), 1.f, true);
	}
}

void AHPLobbyPlayerController::SetupInputComponent()
{
	Super::SetupInputComponent();
	if (!InputComponent) return;
	InputComponent->BindKey(EKeys::Escape, IE_Pressed, this, &AHPLobbyPlayerController::OnPausePressed).bExecuteWhenPaused = true;
	InputComponent->BindKey(EKeys::Gamepad_Special_Right, IE_Pressed, this, &AHPLobbyPlayerController::OnPausePressed);
	InputComponent->BindKey(EKeys::Gamepad_FaceButton_Right, IE_Pressed, this, &AHPLobbyPlayerController::OnGamepadBack);
	InputComponent->BindKey(EKeys::Tab, IE_Pressed, this, &AHPLobbyPlayerController::OnTabPressed);
	InputComponent->BindKey(EKeys::Gamepad_Special_Left, IE_Pressed, this, &AHPLobbyPlayerController::OnTabPressed);
	InputComponent->BindKey(EKeys::R, IE_Pressed, this, &AHPLobbyPlayerController::ToggleReady);
	InputComponent->BindKey(EKeys::Gamepad_FaceButton_Top, IE_Pressed, this, &AHPLobbyPlayerController::ToggleReady);
	InputComponent->BindKey(EKeys::B, IE_Pressed, this, &AHPLobbyPlayerController::OnBellPressed);
}

void AHPLobbyPlayerController::OnPausePressed() { if (UHPFrontendUISubsystem* UI = UHPFrontendUISubsystem::Get(this)) UI->HandleBackKey(); }
void AHPLobbyPlayerController::OnGamepadBack()
{
	// Gamepad B is only "back" while a menu is up; otherwise it belongs to gameplay.
	UHPFrontendUISubsystem* UI = UHPFrontendUISubsystem::Get(this);
	if (UI && (UI->GetState() != EHPMenuState::Lobby || UI->IsLobbyOverlayVisible())) UI->HandleBackKey();
}
void AHPLobbyPlayerController::OnTabPressed() { if (UHPFrontendUISubsystem* UI = UHPFrontendUISubsystem::Get(this)) UI->ToggleLobbyOverlay(); }
void AHPLobbyPlayerController::OnBellPressed()
{
	const UHPFrontendUISubsystem* UI = UHPFrontendUISubsystem::Get(this);
	if (!UI || !UI->IsTyping()) ServerRingBell();
}

void AHPLobbyPlayerController::ToggleReady()
{
	if (const UHPFrontendUISubsystem* UI = UHPFrontendUISubsystem::Get(this)) { if (UI->IsTyping()) return; }
	const AHPLobbyPlayerState* PS = GetPlayerState<AHPLobbyPlayerState>();
	ServerSetReady(!(PS && PS->bReady));
}

void AHPLobbyPlayerController::ServerSetReady_Implementation(bool bNewReady)
{
	if (AHPLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHPLobbyGameMode>()) GM->SetReady(this, bNewReady);
}
void AHPLobbyPlayerController::ServerRingBell_Implementation()
{
	if (AHPLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHPLobbyGameMode>()) GM->RingBell(this);
}
void AHPLobbyPlayerController::ServerChat_Implementation(const FString& Text)
{
	if (AHPLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHPLobbyGameMode>()) GM->Chat(this, Text);
}
void AHPLobbyPlayerController::ServerKick_Implementation(APlayerState* Target)
{
	if (AHPLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHPLobbyGameMode>()) GM->Kick(this, Target);
}
void AHPLobbyPlayerController::ServerSetDisplayName_Implementation(const FString& Name)
{
	const FString Clean = Name.TrimStartAndEnd().Left(20);
	if (!Clean.IsEmpty() && PlayerState)
	{
		if (AGameModeBase* GM = GetWorld()->GetAuthGameMode()) GM->ChangeName(this, Clean, false);
		UE_LOG(LogHPLobby, Log, TEXT("HPLobby[server]: display name set '%s'"), *Clean);
	}
}
void AHPLobbyPlayerController::ClientStartAnywayPrompt_Implementation(int32 Ready, int32 Total)
{
	if (UHPFrontendUISubsystem* UI = UHPFrontendUISubsystem::Get(this)) UI->ShowStartAnywayPrompt(Ready, Total);
}

void AHPLobbyPlayerController::ClientWasKicked_Implementation(const FText& KickReason)
{
	if (UHPGameInstance* GI = GetGameInstance<UHPGameInstance>())
	{
		GI->SetPendingError(KickReason);
	}
	Super::ClientWasKicked_Implementation(KickReason);
}

// =============================================================== Porch zone
AHPPorchReadyZone::AHPPorchReadyZone()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.TickInterval = 0.1f;
	Zone = CreateDefaultSubobject<UBoxComponent>(TEXT("Zone"));
	Zone->SetBoxExtent(FVector(250.f, 400.f, 150.f));
	Zone->SetCollisionProfileName(TEXT("Trigger"));
	RootComponent = Zone;
	Sign = CreateDefaultSubobject<UTextRenderComponent>(TEXT("Sign"));
	Sign->SetupAttachment(Zone);
	Sign->SetRelativeLocation(FVector(0, 0, 220.f));
	Sign->SetHorizontalAlignment(EHTA_Center);
	Sign->SetWorldSize(42.f);
	Sign->SetTextRenderColor(FColor(255, 214, 120));
	Sign->SetText(LOCTEXT("PorchSign", "PORCH - stand here to get ready"));
	PorchLight = CreateDefaultSubobject<UPointLightComponent>(TEXT("PorchLight"));
	PorchLight->SetupAttachment(Zone);
	PorchLight->SetRelativeLocation(FVector(0, 0, 260.f));
	PorchLight->SetIntensity(3000.f);
	PorchLight->SetLightColor(FLinearColor(1.f, 0.6f, 0.25f));
	PorchLight->SetAttenuationRadius(900.f);
}

void AHPPorchReadyZone::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (!HasAuthority()) return;
	AHPLobbyGameMode* GM = GetWorld()->GetAuthGameMode<AHPLobbyGameMode>();
	if (!GM) return;

	TSet<APlayerState*> Inside;
	TArray<AActor*> Overlaps;
	Zone->GetOverlappingActors(Overlaps, APawn::StaticClass());
	for (AActor* A : Overlaps)
	{
		const APawn* P = Cast<APawn>(A);
		if (P && P->GetPlayerState()) Inside.Add(P->GetPlayerState());
	}
	// Step off -> un-ready (only if they were readied by the porch, i.e. tracked here).
	for (auto It = TimeOnPorch.CreateIterator(); It; ++It)
	{
		APlayerState* PS = It.Key().Get();
		if (!PS || !Inside.Contains(PS))
		{
			if (AHPLobbyPlayerState* L = Cast<AHPLobbyPlayerState>(PS))
			{
				L->bOnPorch = false;
				if (APlayerController* PC = Cast<APlayerController>(L->GetOwner())) GM->SetReady(PC, false);
			}
			It.RemoveCurrent();
		}
	}
	for (APlayerState* PS : Inside)
	{
		float& T = TimeOnPorch.FindOrAdd(PS);
		const bool bWas = T >= GM->ReadyHoldTime;
		T += DeltaSeconds;
		if (AHPLobbyPlayerState* L = Cast<AHPLobbyPlayerState>(PS))
		{
			L->bOnPorch = true;
			if (!bWas && T >= GM->ReadyHoldTime)
			{
				if (APlayerController* PC = Cast<APlayerController>(L->GetOwner())) GM->SetReady(PC, true);
			}
		}
	}
	PorchLight->SetLightColor(Inside.Num() > 0 ? FLinearColor(0.5f, 1.f, 0.4f) : FLinearColor(1.f, 0.6f, 0.25f));
}

// =============================================================== Dinner bell
AHPDinnerBell::AHPDinnerBell()
{
	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	RootComponent = Mesh;
	static ConstructorHelpers::FObjectFinder<UStaticMesh> BellMesh(TEXT("/Game/Emberlight/Meshes/Props/SM_BellPost.SM_BellPost"));
	if (BellMesh.Succeeded()) Mesh->SetStaticMesh(BellMesh.Object);
	Sign = CreateDefaultSubobject<UTextRenderComponent>(TEXT("Sign"));
	Sign->SetupAttachment(Mesh);
	Sign->SetRelativeLocation(FVector(0, 0, 330.f));
	Sign->SetHorizontalAlignment(EHTA_Center);
	Sign->SetWorldSize(38.f);
	Sign->SetTextRenderColor(FColor(255, 243, 214));
	Sign->SetText(LOCTEXT("BellSign", "DINNER BELL - host presses B to start the day"));
}

#undef LOCTEXT_NAMESPACE
