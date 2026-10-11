// HARVEST PANIC - Frontend menu state machine (UI-agnostic, unit tested).
#pragma once

#include "CoreMinimal.h"
#include "UObject/Object.h"
#include "HPMenuStateMachine.generated.h"

/** Every screen the frontend can show. Overlays (Settings, Customize) push onto a stack. */
UENUM(BlueprintType)
enum class EHPMenuState : uint8
{
	Title,
	PlayChoice,   // Host or Join
	Browser,      // Server browser / join by address
	Connecting,   // Waiting on create/find/join
	Settings,     // Overlay
	Customize,    // Overlay
	Lobby,        // In the lobby map
	InGame,       // Gameplay, no menu
	Pause         // In-game overlay (never pauses the world)
};

DECLARE_MULTICAST_DELEGATE_TwoParams(FHPOnMenuStateChanged, EHPMenuState /*Old*/, EHPMenuState /*New*/);

/**
 * Stack-based menu flow. Push goes forward, Back pops. Only legal transitions are accepted
 * so a stray input (e.g. Back on the Title screen) can never leave the UI in a broken state.
 */
UCLASS()
class HARVESTPANIC_API UHPMenuStateMachine : public UObject
{
	GENERATED_BODY()

public:
	/** Resets the stack to a single root state (Title in the menu map, Lobby in the lobby, InGame in play). */
	void Reset(EHPMenuState Root);

	/** Returns true if the transition was legal and applied. */
	bool Push(EHPMenuState Next);

	/** Pops one level. Returns false at the root. */
	bool Back();

	/** Replaces the top (e.g. Connecting -> Browser after a failed join). */
	bool Replace(EHPMenuState Next);

	EHPMenuState Current() const { return Stack.Num() ? Stack.Last() : EHPMenuState::Title; }
	EHPMenuState Root() const { return Stack.Num() ? Stack[0] : EHPMenuState::Title; }
	int32 Depth() const { return Stack.Num(); }
	static bool IsLegal(EHPMenuState From, EHPMenuState To);

	FHPOnMenuStateChanged OnStateChanged;

private:
	TArray<EHPMenuState> Stack;
};
