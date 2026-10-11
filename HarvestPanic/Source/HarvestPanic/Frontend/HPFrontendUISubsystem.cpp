#include "Frontend/HPFrontendUISubsystem.h"
#include "Frontend/UI/HPWidgets.h"
#include "Frontend/UI/HPMenuScreens.h"
#include "Frontend/UI/HPSettingsWidget.h"
#include "Frontend/UI/HPLobbyWidget.h"
#include "Engine/LocalPlayer.h"
#include "GameFramework/PlayerController.h"
#include "Blueprint/UserWidget.h"

const TCHAR* UHPFrontendUISubsystem::CustomizationClassPath = TEXT("/Script/HarvestPanic.HPCustomizationWidget");

UHPFrontendUISubsystem* UHPFrontendUISubsystem::Get(const APlayerController* PC)
{
	const ULocalPlayer* LP = PC ? PC->GetLocalPlayer() : nullptr;
	return LP ? LP->GetSubsystem<UHPFrontendUISubsystem>() : nullptr;
}

void UHPFrontendUISubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);
	Machine = NewObject<UHPMenuStateMachine>(this);
	Machine->OnStateChanged.AddUObject(this, &UHPFrontendUISubsystem::HandleStateChanged);
}

APlayerController* UHPFrontendUISubsystem::GetPC() const
{
	const ULocalPlayer* LP = GetLocalPlayer();
	return LP ? LP->GetPlayerController(LP->GetWorld()) : nullptr;
}

template<class T> T* UHPFrontendUISubsystem::CreatePanel()
{
	APlayerController* PC = GetPC();
	if (!PC)
	{
		return nullptr;
	}
	T* W = CreateWidget<T>(PC, T::StaticClass());
	W->Owner = this;
	return W;
}

void UHPFrontendUISubsystem::ShowRoot(EHPMenuState Root)
{
	// Widgets from the previous world are already gone after travel; drop our references.
	ActivePanel = nullptr;
	ErrorPanel = nullptr;
	bLobbyOverlayVisible = false;
	Machine->Reset(Root);
}

bool UHPFrontendUISubsystem::Go(EHPMenuState Next)
{
	return Machine->Push(Next);
}

bool UHPFrontendUISubsystem::Back()
{
	if (ErrorPanel)
	{
		ErrorPanel->RemoveFromParent();
		ErrorPanel = nullptr;
		if (ActivePanel) { ActivePanel->FocusFirst(); }
		ApplyInputMode();
		return true;
	}
	if (Machine->Current() == EHPMenuState::Lobby && bLobbyOverlayVisible)
	{
		ToggleLobbyOverlay();
		return true;
	}
	return Machine->Back();
}

void UHPFrontendUISubsystem::HandleStateChanged(EHPMenuState Old, EHPMenuState New)
{
	RebuildForState(New);
}

void UHPFrontendUISubsystem::ClearActive()
{
	if (ActivePanel)
	{
		ActivePanel->RemoveFromParent();
		ActivePanel = nullptr;
	}
}

void UHPFrontendUISubsystem::RebuildForState(EHPMenuState State)
{
	ClearActive();
	UHPMenuPanel* P = nullptr;
	switch (State)
	{
	case EHPMenuState::Title:      P = CreatePanel<UHPTitleWidget>(); break;
	case EHPMenuState::PlayChoice: P = CreatePanel<UHPPlayChoiceWidget>(); break;
	case EHPMenuState::Browser:    P = CreatePanel<UHPServerBrowserWidget>(); break;
	case EHPMenuState::Connecting: P = CreatePanel<UHPConnectingWidget>(); break;
	case EHPMenuState::Settings:   P = CreatePanel<UHPSettingsWidget>(); break;
	case EHPMenuState::Customize:  P = CreatePanel<UHPCustomizeHostWidget>(); break;
	case EHPMenuState::Lobby:      P = CreatePanel<UHPLobbyWidget>(); break;
	case EHPMenuState::Pause:      P = CreatePanel<UHPPauseWidget>(); break;
	case EHPMenuState::InGame:     default: break;
	}
	if (P)
	{
		TWeakObjectPtr<UHPFrontendUISubsystem> WeakThis = this;
		P->OnBackRequested = [WeakThis]() { return WeakThis.IsValid() && WeakThis->Back(); };
		P->AddToViewport(10);
		ActivePanel = P;
		if (State == EHPMenuState::Lobby && !bLobbyOverlayVisible)
		{
			P->SetVisibility(ESlateVisibility::Collapsed);
		}
		else
		{
			P->FocusFirst();
		}
	}
	ApplyInputMode();
}

void UHPFrontendUISubsystem::ApplyInputMode()
{
	APlayerController* PC = GetPC();
	if (!PC)
	{
		return;
	}
	const EHPMenuState S = Machine->Current();
	const bool bWorldControls = (S == EHPMenuState::InGame) || (S == EHPMenuState::Lobby && !bLobbyOverlayVisible);
	if (bWorldControls && !ErrorPanel)
	{
		PC->SetInputMode(FInputModeGameOnly());
		PC->SetShowMouseCursor(false);
	}
	else
	{
		UUserWidget* FocusW = ErrorPanel ? ErrorPanel.Get() : ActivePanel.Get();
		// GameAndUI everywhere: Esc / gamepad B bubble to the PlayerController, which routes Back.
		// Lobby / pause overlays: UI gets input but the world keeps simulating (the farm does not pause).
		FInputModeGameAndUI Mode;
		if (FocusW) { Mode.SetWidgetToFocus(FocusW->TakeWidget()); }
		Mode.SetHideCursorDuringCapture(false);
		PC->SetInputMode(Mode);
		PC->SetShowMouseCursor(true);
		if (UHPMenuPanel* FP = ErrorPanel ? ErrorPanel.Get() : ActivePanel.Get())
		{
			FP->FocusFirst();
		}
	}
}

void UHPFrontendUISubsystem::ShowError(const FText& Message)
{
	if (Message.IsEmpty())
	{
		return;
	}
	if (ErrorPanel)
	{
		ErrorPanel->RemoveFromParent();
	}
	UHPErrorPopupWidget* E = CreatePanel<UHPErrorPopupWidget>();
	if (!E)
	{
		return;
	}
	E->Message = Message;
	TWeakObjectPtr<UHPFrontendUISubsystem> WeakThis = this;
	E->OnBackRequested = [WeakThis]() { return WeakThis.IsValid() && WeakThis->Back(); };
	E->AddToViewport(50);
	ErrorPanel = E;
	ApplyInputMode();
}

void UHPFrontendUISubsystem::TogglePause()
{
	const EHPMenuState S = Machine->Current();
	if (S == EHPMenuState::Pause)
	{
		Machine->Back();
	}
	else if (S == EHPMenuState::Settings || S == EHPMenuState::Customize)
	{
		Machine->Back();
	}
	else if (S == EHPMenuState::Lobby || S == EHPMenuState::InGame)
	{
		if (S == EHPMenuState::Lobby && bLobbyOverlayVisible)
		{
			ToggleLobbyOverlay();
			return;
		}
		Machine->Push(EHPMenuState::Pause);
	}
}

void UHPFrontendUISubsystem::ToggleLobbyOverlay()
{
	if (Machine->Current() != EHPMenuState::Lobby || !ActivePanel)
	{
		return;
	}
	bLobbyOverlayVisible = !bLobbyOverlayVisible;
	ActivePanel->SetVisibility(bLobbyOverlayVisible ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);
	ApplyInputMode();
}

void UHPFrontendUISubsystem::NotifyLobbyChanged()
{
	if (UHPLobbyWidget* L = Cast<UHPLobbyWidget>(ActivePanel))
	{
		L->RefreshFromState();
	}
}

void UHPFrontendUISubsystem::ShowStartAnywayPrompt(int32 Ready, int32 Total)
{
	if (UHPLobbyWidget* L = Cast<UHPLobbyWidget>(ActivePanel))
	{
		if (!bLobbyOverlayVisible)
		{
			ToggleLobbyOverlay();
		}
		L->ShowStartAnyway(Ready, Total);
	}
}

bool UHPFrontendUISubsystem::HandleBackKey()
{
	if (ErrorPanel)
	{
		return Back();
	}
	if (ActivePanel && ActivePanel->HandleBackKey())
	{
		return true;
	}
	const EHPMenuState S = Machine->Current();
	if (S == EHPMenuState::Lobby || S == EHPMenuState::InGame)
	{
		TogglePause();
		return true;
	}
	return false;
}

bool UHPFrontendUISubsystem::IsTyping() const
{
	const UHPLobbyWidget* L = Cast<UHPLobbyWidget>(ActivePanel);
	return L && IsLobbyOverlayVisible() && L->IsTyping();
}
