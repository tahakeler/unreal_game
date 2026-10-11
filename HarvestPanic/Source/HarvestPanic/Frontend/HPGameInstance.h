// HARVEST PANIC - session lifecycle (host / find / join / invite / disconnect).
#pragma once

#include "CoreMinimal.h"
#include "Engine/GameInstance.h"
#include "Interfaces/OnlineSessionInterface.h"
#include "OnlineSessionSettings.h"
#include "Engine/EngineBaseTypes.h"
#include "HPGameInstance.generated.h"

class IOnlineSubsystem;

/** One row of the server browser (UI-facing copy of a search result). */
USTRUCT()
struct FHPServerEntry
{
	GENERATED_BODY()
	int32 ResultIndex = INDEX_NONE;
	FString HostName;
	int32 OpenSlots = 0;
	int32 MaxSlots = 4;
	int32 PingMs = 0;
	bool bLAN = false;
};

DECLARE_MULTICAST_DELEGATE_OneParam(FHPOnSessionsFound, const TArray<FHPServerEntry>& /*Entries*/);
DECLARE_MULTICAST_DELEGATE_TwoParams(FHPOnSessionOpResult, bool /*bSuccess*/, const FText& /*Error*/);

/**
 * Owns all IOnlineSession usage. Widgets only call Host/Find/Join and listen to delegates.
 * Steam when available; otherwise the NULL subsystem with LAN forced on (Mac dev, no Steam client).
 */
UCLASS()
class HARVESTPANIC_API UHPGameInstance : public UGameInstance
{
	GENERATED_BODY()

public:
	static const FName SessionName;
	static const TCHAR* MainMenuMap;
	static const TCHAR* LobbyMap;
	static const TCHAR* DayMap;
	static const TCHAR* DayMapFallback;
	static constexpr int32 MaxPlayers = 4;

	virtual void Init() override;
	virtual void Shutdown() override;

	/** Creates a session then opens the lobby map as a listen server. */
	void HostSession(bool bLAN);
	/** Searches for sessions; results arrive via OnSessionsFound. */
	void FindSessions(bool bLAN);
	void CancelFind();
	/** Joins a browser row. */
	void JoinServerEntry(int32 ResultIndex);
	/** Direct connect (dev / LAN): "127.0.0.1" or "192.168.1.5:7777". */
	void JoinByAddress(const FString& Address);
	/** Opens the platform invite overlay. Returns false when not supported (NULL subsystem). */
	bool ShowInviteOverlay();
	bool CanInvite() const;

	/** Leaves the session and returns to the main menu, optionally showing an error popup there. */
	void LeaveToMainMenu(const FText& Reason = FText::GetEmpty());

	/** Host-side: resolve the day map (Muddlebury if it exists, otherwise the ThirdPerson fallback). */
	static FString ResolveDayMapURL();

	/** Menu reads and clears this on load (disconnect reasons, failed joins). */
	FText ConsumePendingError();
	void SetPendingError(const FText& Error) { PendingError = Error; }

	FName GetActiveSubsystemName() const;
	bool IsUsingFallbackSubsystem() const { return bUsingNullFallback; }
	bool IsBusy() const { return bBusy; }
	const TArray<FHPServerEntry>& GetLastResults() const { return LastResults; }

	FHPOnSessionsFound OnSessionsFound;
	FHPOnSessionOpResult OnHostResult;
	FHPOnSessionOpResult OnJoinResult;

	/** Display name for lobby rows. -HPName=Foo overrides (useful for LAN tests). */
	FString GetLocalDisplayName() const;

protected:
	IOnlineSessionPtr GetSessions() const;

	void HandleCreateComplete(FName InSessionName, bool bOk);
	void HandleFindComplete(bool bOk);
	void HandleJoinComplete(FName InSessionName, EOnJoinSessionCompleteResult::Type Result);
	void HandleDestroyComplete(FName InSessionName, bool bOk);
	void HandleInviteAccepted(const bool bOk, const int32 ControllerId, FUniqueNetIdPtr UserId, const FOnlineSessionSearchResult& Result);
	void HandleNetworkFailure(UWorld* World, UNetDriver* NetDriver, ENetworkFailure::Type FailureType, const FString& ErrorString);
	void HandleTravelFailure(UWorld* World, ETravelFailure::Type FailureType, const FString& ErrorString);

	void Fail(FHPOnSessionOpResult& Delegate, const FText& Error);
	void JoinSearchResult(const FOnlineSessionSearchResult& Result);

	IOnlineSubsystem* Subsystem = nullptr;
	bool bUsingNullFallback = false;
	bool bBusy = false;
	bool bPendingHostLAN = false;
	bool bPendingHostAfterDestroy = false;
	FText PendingError;

	TSharedPtr<FOnlineSessionSearch> Search;
	TArray<FHPServerEntry> LastResults;

	FDelegateHandle CreateHandle, FindHandle, JoinHandle, DestroyHandle, InviteHandle;
	FDelegateHandle NetFailHandle, TravelFailHandle;
};
