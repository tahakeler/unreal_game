#include "Frontend/HPMenuStateMachine.h"

bool UHPMenuStateMachine::IsLegal(EHPMenuState From, EHPMenuState To)
{
	if (From == To)
	{
		return false;
	}
	// Overlays are reachable from any non-overlay, non-connecting screen.
	if (To == EHPMenuState::Settings)
	{
		return From != EHPMenuState::Connecting && From != EHPMenuState::Settings && From != EHPMenuState::InGame;
	}
	if (To == EHPMenuState::Customize)
	{
		return From == EHPMenuState::Title || From == EHPMenuState::Lobby;
	}
	switch (From)
	{
	case EHPMenuState::Title:      return To == EHPMenuState::PlayChoice;
	case EHPMenuState::PlayChoice: return To == EHPMenuState::Browser || To == EHPMenuState::Connecting;
	case EHPMenuState::Browser:    return To == EHPMenuState::Connecting;
	case EHPMenuState::Connecting: return To == EHPMenuState::Browser || To == EHPMenuState::Lobby;
	case EHPMenuState::Lobby:      return To == EHPMenuState::InGame || To == EHPMenuState::Pause;
	case EHPMenuState::InGame:     return To == EHPMenuState::Pause;
	case EHPMenuState::Pause:      return false; // Settings handled above
	default:                       return false;
	}
}

void UHPMenuStateMachine::Reset(EHPMenuState Root)
{
	const EHPMenuState Old = Current();
	Stack.Reset();
	Stack.Add(Root);
	OnStateChanged.Broadcast(Old, Root);
}

bool UHPMenuStateMachine::Push(EHPMenuState Next)
{
	if (Stack.Num() == 0)
	{
		Reset(Next);
		return true;
	}
	const EHPMenuState Old = Current();
	if (!IsLegal(Old, Next))
	{
		return false;
	}
	Stack.Add(Next);
	OnStateChanged.Broadcast(Old, Next);
	return true;
}

bool UHPMenuStateMachine::Back()
{
	if (Stack.Num() <= 1)
	{
		return false;
	}
	const EHPMenuState Old = Stack.Pop();
	OnStateChanged.Broadcast(Old, Current());
	return true;
}

bool UHPMenuStateMachine::Replace(EHPMenuState Next)
{
	if (Stack.Num() <= 1)
	{
		return false;
	}
	const EHPMenuState Old = Current();
	const EHPMenuState Under = Stack[Stack.Num() - 2];
	if (!IsLegal(Under, Next) && !IsLegal(Old, Next))
	{
		return false;
	}
	Stack.Last() = Next;
	// Collapse duplicates (e.g. Browser, Connecting->Browser) so Back still works naturally.
	if (Stack.Num() >= 2 && Stack[Stack.Num() - 2] == Next)
	{
		Stack.Pop();
	}
	OnStateChanged.Broadcast(Old, Next);
	return true;
}
