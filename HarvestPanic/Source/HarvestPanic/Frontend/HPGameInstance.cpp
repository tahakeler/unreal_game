#include "Frontend/HPGameInstance.h"
#include "Frontend/HPGameUserSettings.h"
#include "OnlineSubsystem.h"
#include "OnlineSubsystemUtils.h"
#include "Online/OnlineSessionNames.h"
#include "Interfaces/OnlineExternalUIInterface.h"
#include "Engine/Engine.h"
#include "Engine/World.h"
#include "Kismet/GameplayStatics.h"
#include "GameFramework/PlayerController.h"
#include "Misc/CommandLine.h"
#include "Misc/Parse.h"
#include "Misc/PackageName.h"
#include "HAL/PlatformProcess.h"

DEFINE_LOG_CATEGORY_STATIC(LogHPSession, Log, All);

#define LOCTEXT_NAMESPACE "HPFrontend"

const FName UHPGameInstance::SessionName = NAME_GameSession;
const TCHAR* UHPGameInstance::MainMenuMap = TEXT("/Game/HarvestPanic/Frontend/Lvl_MainMenu");
const TCHAR* UHPGameInstance::LobbyMap = TEXT("/Game/HarvestPanic/Frontend/Lvl_Lobby");
const TCHAR* UHPGameInstance::DayMap = TEXT("/Game/HarvestPanic/Maps/Lvl_Muddlebury");
const TCHAR* UHPGameInstance::DayMapFallback = TEXT("/Game/ThirdPerson/Lvl_ThirdPerson");

void UHPGameInstance::Init()
{
	Super::Init();

	// Prefer the configured platform (Steam). If it failed to start (no Steam client, Mac dev),
	// fall back to the NULL subsystem, which supports LAN sessions.
	Subsystem = IOnlineSubsystem::Get();
	const bool bForceNull = FParse::Param(FCommandLine::Get(), TEXT("nosteam")) || FParse::Param(FCommandLine::Get(), TEXT("HPForceLAN"));
	if (!Subsystem || bForceNull || !Subsystem->GetSessionInterface().IsValid())
	{
		Subsystem = IOnlineSubsystem::Get(NULL_SUBSYSTEM);
	}
	bUsingNullFallback = Subsystem && Subsystem->GetSubsystemName() == NULL_SUBSYSTEM;
	UE_LOG(LogHPSession, Log, TEXT("HPSession: online subsystem = %s%s"),
		Subsystem ? *Subsystem->GetSubsystemName().ToString() : TEXT("<none>"),
		bUsingNullFallback ? TEXT(" (LAN fallback)") : TEXT(""));

	if (IOnlineSessionPtr Sessions = GetSessions())
	{
		CreateHandle = Sessions->AddOnCreateSessionCompleteDelegate_Handle(FOnCreateSessionCompleteDelegate::CreateUObject(this, &UHPGameInstance::HandleCreateComplete));
		FindHandle = Sessions->AddOnFindSessionsCompleteDelegate_Handle(FOnFindSessionsCompleteDelegate::CreateUObject(this, &UHPGameInstance::HandleFindComplete));
		JoinHandle = Sessions->AddOnJoinSessionCompleteDelegate_Handle(FOnJoinSessionCompleteDelegate::CreateUObject(this, &UHPGameInstance::HandleJoinComplete));
		DestroyHandle = Sessions->AddOnDestroySessionCompleteDelegate_Handle(FOnDestroySessionCompleteDelegate::CreateUObject(this, &UHPGameInstance::HandleDestroyComplete));
		InviteHandle = Sessions->AddOnSessionUserInviteAcceptedDelegate_Handle(FOnSessionUserInviteAcceptedDelegate::CreateUObject(this, &UHPGameInstance::HandleInviteAccepted));
	}

	if (GEngine)
	{
		NetFailHandle = GEngine->OnNetworkFailure().AddUObject(this, &UHPGameInstance::HandleNetworkFailure);
		TravelFailHandle = GEngine->OnTravelFailure().AddUObject(this, &UHPGameInstance::HandleTravelFailure);
	}

	UHPGameUserSettings::Get()->ApplyHPSettings(nullptr, false);
}

void UHPGameInstance::Shutdown()
{
	if (IOnlineSessionPtr Sessions = GetSessions())
	{
		Sessions->ClearOnCreateSessionCompleteDelegate_Handle(CreateHandle);
		Sessions->ClearOnFindSessionsCompleteDelegate_Handle(FindHandle);
		Sessions->ClearOnJoinSessionCompleteDelegate_Handle(JoinHandle);
		Sessions->ClearOnDestroySessionCompleteDelegate_Handle(DestroyHandle);
		Sessions->ClearOnSessionUserInviteAcceptedDelegate_Handle(InviteHandle);
	}
	if (GEngine)
	{
		GEngine->OnNetworkFailure().Remove(NetFailHandle);
		GEngine->OnTravelFailure().Remove(TravelFailHandle);
	}
	Super::Shutdown();
}

IOnlineSessionPtr UHPGameInstance::GetSessions() const
{
	return Subsystem ? Subsystem->GetSessionInterface() : nullptr;
}

FName UHPGameInstance::GetActiveSubsystemName() const
{
	return Subsystem ? Subsystem->GetSubsystemName() : NAME_None;
}

FString UHPGameInstance::GetLocalDisplayName() const
{
	FString Name;
	if (FParse::Value(FCommandLine::Get(), TEXT("HPName="), Name) && !Name.IsEmpty())
	{
		return Name.Left(20);
	}
	if (Subsystem && Subsystem->GetIdentityInterface().IsValid())
	{
		Name = Subsystem->GetIdentityInterface()->GetPlayerNickname(0);
	}
	return Name.IsEmpty() ? FString(FPlatformProcess::ComputerName()).Left(20) : Name.Left(20);
}

FText UHPGameInstance::ConsumePendingError()
{
	FText Out = PendingError;
	PendingError = FText::GetEmpty();
	return Out;
}

void UHPGameInstance::Fail(FHPOnSessionOpResult& Delegate, const FText& Error)
{
	bBusy = false;
	UE_LOG(LogHPSession, Warning, TEXT("HPSession: %s"), *Error.ToString());
	Delegate.Broadcast(false, Error);
}

void UHPGameInstance::HostSession(bool bLAN)
{
	IOnlineSessionPtr Sessions = GetSessions();
	if (!Sessions.IsValid())
	{
		Fail(OnHostResult, LOCTEXT("ErrNoOnline", "Online services are unavailable."));
		return;
	}
	if (bBusy)
	{
		return;
	}
	bBusy = true;
	bPendingHostLAN = bLAN || bUsingNullFallback;

	if (Sessions->GetNamedSession(SessionName))
	{
		// A stale session (e.g. after a crash-out) blocks CreateSession: destroy it first.
		bPendingHostAfterDestroy = true;
		Sessions->DestroySession(SessionName);
		return;
	}

	FOnlineSessionSettings Settings;
	Settings.bIsLANMatch = bPendingHostLAN;
	Settings.NumPublicConnections = MaxPlayers;
	Settings.bShouldAdvertise = true;
	Settings.bAllowJoinInProgress = true;
	Settings.bUsesPresence = !bPendingHostLAN;
	Settings.bAllowJoinViaPresence = !bPendingHostLAN;
	Settings.bUseLobbiesIfAvailable = !bPendingHostLAN;
	Settings.bAllowInvites = true;
	Settings.Set(SETTING_MAPNAME, FString(TEXT("Lvl_Lobby")), EOnlineDataAdvertisementType::ViaOnlineService);
	Settings.Set(FName(TEXT("HP_HOSTNAME")), GetLocalDisplayName(), EOnlineDataAdvertisementType::ViaOnlineServiceAndPing);

	UE_LOG(LogHPSession, Log, TEXT("HPSession: creating session (LAN=%d)"), bPendingHostLAN ? 1 : 0);
	if (!Sessions->CreateSession(0, SessionName, Settings))
	{
		Fail(OnHostResult, LOCTEXT("ErrCreate", "Couldn't open the farm gate (session create failed)."));
	}
}

void UHPGameInstance::HandleCreateComplete(FName InSessionName, bool bOk)
{
	if (!bOk)
	{
		Fail(OnHostResult, LOCTEXT("ErrCreate", "Couldn't open the farm gate (session create failed)."));
		return;
	}
	bBusy = false;
	UE_LOG(LogHPSession, Log, TEXT("HPSession: session created, opening lobby as listen server"));
	OnHostResult.Broadcast(true, FText::GetEmpty());
	UGameplayStatics::OpenLevel(this, FName(LobbyMap), true, TEXT("listen"));
}

void UHPGameInstance::HandleDestroyComplete(FName InSessionName, bool bOk)
{
	if (bPendingHostAfterDestroy)
	{
		bPendingHostAfterDestroy = false;
		bBusy = false;
		HostSession(bPendingHostLAN);
	}
}

void UHPGameInstance::FindSessions(bool bLAN)
{
	IOnlineSessionPtr Sessions = GetSessions();
	if (!Sessions.IsValid())
	{
		OnSessionsFound.Broadcast({});
		return;
	}
	if (Search.IsValid() && Search->SearchState == EOnlineAsyncTaskState::InProgress)
	{
		return;
	}
	Search = MakeShared<FOnlineSessionSearch>();
	Search->bIsLanQuery = bLAN || bUsingNullFallback;
	Search->MaxSearchResults = 50;
	Search->PingBucketSize = 50;
	if (!Search->bIsLanQuery)
	{
		Search->QuerySettings.Set(SEARCH_LOBBIES, true, EOnlineComparisonOp::Equals);
	}
	UE_LOG(LogHPSession, Log, TEXT("HPSession: searching (LAN=%d)"), Search->bIsLanQuery ? 1 : 0);
	if (!Sessions->FindSessions(0, Search.ToSharedRef()))
	{
		OnSessionsFound.Broadcast({});
	}
}

void UHPGameInstance::CancelFind()
{
	if (IOnlineSessionPtr Sessions = GetSessions())
	{
		if (Search.IsValid() && Search->SearchState == EOnlineAsyncTaskState::InProgress)
		{
			Sessions->CancelFindSessions();
		}
	}
}

void UHPGameInstance::HandleFindComplete(bool bOk)
{
	LastResults.Reset();
	if (Search.IsValid())
	{
		for (int32 i = 0; i < Search->SearchResults.Num(); ++i)
		{
			const FOnlineSessionSearchResult& R = Search->SearchResults[i];
			FHPServerEntry E;
			E.ResultIndex = i;
			if (!R.Session.SessionSettings.Get(FName(TEXT("HP_HOSTNAME")), E.HostName) || E.HostName.IsEmpty())
			{
				E.HostName = R.Session.OwningUserName;
			}
			E.MaxSlots = R.Session.SessionSettings.NumPublicConnections;
			E.OpenSlots = R.Session.NumOpenPublicConnections;
			E.PingMs = R.PingInMs;
			E.bLAN = R.Session.SessionSettings.bIsLANMatch;
			LastResults.Add(E);
		}
		LastResults.Sort([](const FHPServerEntry& A, const FHPServerEntry& B) { return A.PingMs < B.PingMs; });
	}
	UE_LOG(LogHPSession, Log, TEXT("HPSession: search finished ok=%d results=%d"), bOk ? 1 : 0, LastResults.Num());
	OnSessionsFound.Broadcast(LastResults);
}

void UHPGameInstance::JoinServerEntry(int32 ResultIndex)
{
	if (!Search.IsValid() || !Search->SearchResults.IsValidIndex(ResultIndex))
	{
		Fail(OnJoinResult, LOCTEXT("ErrGone", "That farm isn't there anymore. Refresh the list."));
		return;
	}
	JoinSearchResult(Search->SearchResults[ResultIndex]);
}

void UHPGameInstance::JoinSearchResult(const FOnlineSessionSearchResult& Result)
{
	IOnlineSessionPtr Sessions = GetSessions();
	if (!Sessions.IsValid() || bBusy)
	{
		return;
	}
	if (Result.Session.NumOpenPublicConnections <= 0)
	{
		Fail(OnJoinResult, LOCTEXT("ErrFull", "That farm is full (4/4)."));
		return;
	}
	bBusy = true;
	if (Sessions->GetNamedSession(SessionName))
	{
		Sessions->DestroySession(SessionName); // Leave our own stale session before joining.
	}
	if (!Sessions->JoinSession(0, SessionName, Result))
	{
		Fail(OnJoinResult, LOCTEXT("ErrJoin", "Couldn't join that farm."));
	}
}

void UHPGameInstance::HandleJoinComplete(FName InSessionName, EOnJoinSessionCompleteResult::Type Result)
{
	bBusy = false;
	if (Result != EOnJoinSessionCompleteResult::Success)
	{
		FText Err = LOCTEXT("ErrJoin", "Couldn't join that farm.");
		if (Result == EOnJoinSessionCompleteResult::SessionIsFull) { Err = LOCTEXT("ErrFull", "That farm is full (4/4)."); }
		else if (Result == EOnJoinSessionCompleteResult::SessionDoesNotExist) { Err = LOCTEXT("ErrGone", "That farm isn't there anymore. Refresh the list."); }
		Fail(OnJoinResult, Err);
		return;
	}
	FString Connect;
	IOnlineSessionPtr Sessions = GetSessions();
	if (!Sessions.IsValid() || !Sessions->GetResolvedConnectString(InSessionName, Connect))
	{
		Fail(OnJoinResult, LOCTEXT("ErrResolve", "Couldn't find the way to that farm (no address)."));
		return;
	}
	UE_LOG(LogHPSession, Log, TEXT("HPSession: joined session, travelling to %s"), *Connect);
	OnJoinResult.Broadcast(true, FText::GetEmpty());
	if (APlayerController* PC = GetFirstLocalPlayerController())
	{
		PC->ClientTravel(Connect, TRAVEL_Absolute);
	}
}

void UHPGameInstance::JoinByAddress(const FString& Address)
{
	FString Clean = Address.TrimStartAndEnd();
	if (Clean.IsEmpty())
	{
		Fail(OnJoinResult, LOCTEXT("ErrAddr", "Type an address first (like 192.168.1.20)."));
		return;
	}
	UE_LOG(LogHPSession, Log, TEXT("HPSession: direct connect to %s"), *Clean);
	if (APlayerController* PC = GetFirstLocalPlayerController())
	{
		OnJoinResult.Broadcast(true, FText::GetEmpty());
		PC->ClientTravel(Clean, TRAVEL_Absolute);
	}
}

bool UHPGameInstance::CanInvite() const
{
	return Subsystem && !bUsingNullFallback && Subsystem->GetExternalUIInterface().IsValid();
}

bool UHPGameInstance::ShowInviteOverlay()
{
	if (!CanInvite())
	{
		return false;
	}
	return Subsystem->GetExternalUIInterface()->ShowInviteUI(0, SessionName);
}

void UHPGameInstance::HandleInviteAccepted(const bool bOk, const int32 ControllerId, FUniqueNetIdPtr UserId, const FOnlineSessionSearchResult& Result)
{
	UE_LOG(LogHPSession, Log, TEXT("HPSession: invite accepted ok=%d"), bOk ? 1 : 0);
	if (bOk && Result.IsValid())
	{
		bBusy = false;
		JoinSearchResult(Result);
	}
}

FString UHPGameInstance::ResolveDayMapURL()
{
	const TCHAR* Map = FPackageName::DoesPackageExist(DayMap) ? DayMap : DayMapFallback;
	return FString::Printf(TEXT("%s?listen"), Map);
}

void UHPGameInstance::LeaveToMainMenu(const FText& Reason)
{
	if (!Reason.IsEmpty())
	{
		PendingError = Reason;
	}
	if (IOnlineSessionPtr Sessions = GetSessions())
	{
		if (Sessions->GetNamedSession(SessionName))
		{
			Sessions->DestroySession(SessionName);
		}
	}
	bBusy = false;
	UGameplayStatics::OpenLevel(this, FName(MainMenuMap), true);
}

void UHPGameInstance::HandleNetworkFailure(UWorld* World, UNetDriver* NetDriver, ENetworkFailure::Type FailureType, const FString& ErrorString)
{
	UE_LOG(LogHPSession, Warning, TEXT("HPSession: network failure %s: %s"), ENetworkFailure::ToString(FailureType), *ErrorString);
	FText Reason = LOCTEXT("ErrHostLeft", "The host went to bed. Back to the farmhouse.");
	if (FailureType == ENetworkFailure::PendingConnectionFailure)
	{
		Reason = LOCTEXT("ErrConnect", "Couldn't reach that farm. Check the address and try again.");
	}
	// The engine already returns us to the default map; make sure our session is cleaned up and the reason shown.
	LeaveToMainMenu(Reason);
}

void UHPGameInstance::HandleTravelFailure(UWorld* World, ETravelFailure::Type FailureType, const FString& ErrorString)
{
	UE_LOG(LogHPSession, Warning, TEXT("HPSession: travel failure %s: %s"), ETravelFailure::ToString(FailureType), *ErrorString);
	LeaveToMainMenu(LOCTEXT("ErrTravel", "The road to the farm washed out (travel failed)."));
}

#undef LOCTEXT_NAMESPACE
