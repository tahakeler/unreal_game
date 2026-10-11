// HARVEST PANIC - per-local-player owner of the frontend screen stack.
#pragma once

#include "CoreMinimal.h"
#include "Subsystems/LocalPlayerSubsystem.h"
#include "Frontend/HPMenuStateMachine.h"
#include "HPFrontendUISubsystem.generated.h"

class UHPMenuPanel;
class UUserWidget;
class APlayerController;

/**
 * Maps menu states to panels. Lives on the ULocalPlayer so it survives map travel; each map's
 * PlayerController calls ShowRoot() on BeginPlay. Gameplay code only needs:
 *   UHPFrontendUISubsystem::Get(PC)->TogglePause();
 */
UCLASS()
class HARVESTPANIC_API UHPFrontendUISubsystem : public ULocalPlayerSubsystem
{
	GENERATED_BODY()

public:
	static UHPFrontendUISubsystem* Get(const APlayerController* PC);

	virtual void Initialize(FSubsystemCollectionBase& Collection) override;

	/** Title (menu map), Lobby (lobby map) or InGame (day map). */
	void ShowRoot(EHPMenuState Root);
	bool Go(EHPMenuState Next);
	bool Back();
	/** Esc / gamepad B / Start from any frontend PlayerController. */
	bool HandleBackKey();
	/** True while a lobby text box has focus. */
	bool IsTyping() const;
	EHPMenuState GetState() const { return Machine ? Machine->Current() : EHPMenuState::Title; }
	UHPMenuStateMachine* GetMachine() const { return Machine; }

	/** Modal error popup over whatever is showing. */
	void ShowError(const FText& Message);

	/** Esc / Start in the lobby or a day map. Never pauses the world. */
	void TogglePause();
	/** Tab in the lobby: show/hide the crew list overlay (you can still walk while it's hidden). */
	void ToggleLobbyOverlay();
	bool IsLobbyOverlayVisible() const { return bLobbyOverlayVisible; }

	/** Called by the lobby PC when the server says "start anyway?" or when the countdown changes. */
	void NotifyLobbyChanged();
	void ShowStartAnywayPrompt(int32 Ready, int32 Total);

	UHPMenuPanel* GetActivePanel() const { return ActivePanel; }

	/** Path used to look up the customization widget from the character-customization branch. */
	static const TCHAR* CustomizationClassPath;

private:
	void HandleStateChanged(EHPMenuState Old, EHPMenuState New);
	void RebuildForState(EHPMenuState State);
	void ClearActive();
	void ApplyInputMode();
	APlayerController* GetPC() const;
	template<class T> T* CreatePanel();

	UPROPERTY() TObjectPtr<UHPMenuStateMachine> Machine;
	UPROPERTY() TObjectPtr<UHPMenuPanel> ActivePanel;
	UPROPERTY() TObjectPtr<UHPMenuPanel> ErrorPanel;
	bool bLobbyOverlayVisible = false;
};
