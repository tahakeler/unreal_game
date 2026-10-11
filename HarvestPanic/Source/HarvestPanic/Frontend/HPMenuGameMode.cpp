#include "Frontend/HPMenuGameMode.h"
#include "Frontend/HPGameInstance.h"
#include "Frontend/HPFrontendUISubsystem.h"
#include "Frontend/HPGameUserSettings.h"
#include "Camera/CameraComponent.h"
#include "Components/InputComponent.h"
#include "Engine/World.h"
#include "TimerManager.h"
#include "Misc/CommandLine.h"
#include "Misc/Parse.h"

DEFINE_LOG_CATEGORY_STATIC(LogHPMenu, Log, All);

AHPMenuCameraPawn::AHPMenuCameraPawn()
{
	PrimaryActorTick.bCanEverTick = true;
	Camera = CreateDefaultSubobject<UCameraComponent>(TEXT("Camera"));
	RootComponent = Camera;
	Camera->SetFieldOfView(70.f);
	bReplicates = false;
}

void AHPMenuCameraPawn::BeginPlay()
{
	Super::BeginPlay();
	StartLoc = GetActorLocation();
	StartRot = GetActorRotation();
}

void AHPMenuCameraPawn::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	Time += DeltaSeconds;
	const float Phase = 2.f * PI * Time / FMath::Max(1.f, SwayPeriod);
	FRotator R = StartRot;
	R.Yaw += SwayYaw * FMath::Sin(Phase);
	R.Pitch += 0.8f * FMath::Sin(Phase * 0.5f);
	SetActorLocationAndRotation(StartLoc + StartRot.RotateVector(FVector(0, DriftDistance * FMath::Sin(Phase), 0)), R);
}

AHPMenuGameMode::AHPMenuGameMode()
{
	DefaultPawnClass = AHPMenuCameraPawn::StaticClass();
	PlayerControllerClass = AHPMenuPlayerController::StaticClass();
}

void AHPMenuPlayerController::BeginPlay()
{
	Super::BeginPlay();
	if (!IsLocalController())
	{
		return;
	}
	UHPGameUserSettings::Get()->ApplyHPSettings(GetWorld(), false);
	if (UHPFrontendUISubsystem* UI = UHPFrontendUISubsystem::Get(this))
	{
		UI->ShowRoot(EHPMenuState::Title);
		if (UHPGameInstance* GI = GetGameInstance<UHPGameInstance>())
		{
			UI->ShowError(GI->ConsumePendingError());
		}
	}
	AutoTest();
}

void AHPMenuPlayerController::SetupInputComponent()
{
	Super::SetupInputComponent();
	InputComponent->BindKey(EKeys::Escape, IE_Pressed, this, &AHPMenuPlayerController::OnBack);
	InputComponent->BindKey(EKeys::Gamepad_FaceButton_Right, IE_Pressed, this, &AHPMenuPlayerController::OnBack);
	InputComponent->BindKey(EKeys::Gamepad_Special_Right, IE_Pressed, this, &AHPMenuPlayerController::OnBack);
}

void AHPMenuPlayerController::OnBack()
{
	if (UHPFrontendUISubsystem* UI = UHPFrontendUISubsystem::Get(this)) UI->HandleBackKey();
}

void AHPMenuPlayerController::AutoTest()
{
	// Headless hooks used by Scripts/Frontend/lan_test.sh:
	//   -HPAutoHost            host a LAN session (create session -> open lobby ?listen)
	//   -HPAutoJoin=127.0.0.1  direct connect
	//   -HPAutoFind            LAN search, join the first result
	//   -HPAutoScreen=Settings show a screen (screenshots)
	static bool bDone = false; // only once per process (we come back here on disconnect)
	if (bDone) return;
	UHPGameInstance* GI = GetGameInstance<UHPGameInstance>();
	UHPFrontendUISubsystem* UI = UHPFrontendUISubsystem::Get(this);
	if (!GI || !UI) return;
	const TCHAR* Cmd = FCommandLine::Get();
	FString Addr, Screen;
	if (FParse::Param(Cmd, TEXT("HPAutoHost")))
	{
		bDone = true;
		UE_LOG(LogHPMenu, Log, TEXT("HPMenu: auto-host"));
		UI->Go(EHPMenuState::PlayChoice);
		UI->Go(EHPMenuState::Connecting);
		GI->HostSession(true);
	}
	else if (FParse::Value(Cmd, TEXT("HPAutoJoin="), Addr))
	{
		bDone = true;
		FTimerHandle H;
		GetWorldTimerManager().SetTimer(H, FTimerDelegate::CreateWeakLambda(this, [GI, Addr]() { GI->JoinByAddress(Addr); }), 1.f, false);
	}
	else if (FParse::Param(Cmd, TEXT("HPAutoFind")))
	{
		bDone = true;
		UI->Go(EHPMenuState::PlayChoice);
		UI->Go(EHPMenuState::Browser); // browser starts a LAN search
		GI->OnSessionsFound.AddWeakLambda(this, [this, GI](const TArray<FHPServerEntry>& Entries)
		{
			if (Entries.Num() > 0)
			{
				UE_LOG(LogHPMenu, Log, TEXT("HPMenu: auto-find found %d, joining '%s'"), Entries.Num(), *Entries[0].HostName);
				if (UHPFrontendUISubsystem* UI2 = UHPFrontendUISubsystem::Get(this)) UI2->Go(EHPMenuState::Connecting);
				GI->JoinServerEntry(Entries[0].ResultIndex);
			}
			else
			{
				FTimerHandle H;
				GetWorldTimerManager().SetTimer(H, FTimerDelegate::CreateWeakLambda(this, [GI]() { GI->FindSessions(true); }), 2.f, false);
			}
		});
	}
	else if (FParse::Value(Cmd, TEXT("HPAutoScreen="), Screen))
	{
		bDone = true;
		if (Screen == TEXT("Play")) UI->Go(EHPMenuState::PlayChoice);
		else if (Screen == TEXT("Browser")) { UI->Go(EHPMenuState::PlayChoice); UI->Go(EHPMenuState::Browser); }
		else if (Screen == TEXT("Settings")) UI->Go(EHPMenuState::Settings);
		else if (Screen == TEXT("Customize")) UI->Go(EHPMenuState::Customize);
		else if (Screen == TEXT("Error")) UI->ShowError(NSLOCTEXT("HPFrontend", "ErrHostLeft", "The host went to bed. Back to the farmhouse."));
	}
}
