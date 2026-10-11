#include "Frontend/UI/HPLobbyWidget.h"
#include "Frontend/UI/HPFrontendStyle.h"
#include "Frontend/HPFrontendUISubsystem.h"
#include "Frontend/HPGameInstance.h"
#include "Frontend/HPLobbyGame.h"
#include "Blueprint/WidgetTree.h"
#include "Components/TextBlock.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Components/HorizontalBox.h"
#include "Components/HorizontalBoxSlot.h"
#include "Components/Overlay.h"
#include "Components/OverlaySlot.h"
#include "Components/SizeBox.h"
#include "Components/Border.h"
#include "Components/EditableTextBox.h"
#include "Engine/World.h"

#define LOCTEXT_NAMESPACE "HPFrontend"

AHPLobbyPlayerController* UHPLobbyWidget::GetLobbyPC() const
{
	return Cast<AHPLobbyPlayerController>(GetOwningPlayer());
}

void UHPLobbyWidget::BuildTree()
{
	UOverlay* Root = WidgetTree->ConstructWidget<UOverlay>(UOverlay::StaticClass());
	WidgetTree->RootWidget = Root;

	USizeBox* Size = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	Size->SetWidthOverride(760.f);
	UBorder* Card = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass());
	HPStyle::StyleBorder(Card, HPStyle::Soil.CopyWithNewOpacity(0.93f), 22.f, HPStyle::Wheat, 4.f);
	Card->SetPadding(FMargin(34.f, 26.f));
	Size->AddChild(Card);
	UOverlaySlot* CS = Root->AddChildToOverlay(Size);
	CS->SetHorizontalAlignment(HAlign_Right);
	CS->SetVerticalAlignment(VAlign_Center);
	CS->SetPadding(FMargin(0, 0, 70.f, 0));

	UVerticalBox* Box = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Card->SetContent(Box);
	Box->AddChildToVerticalBox(HPStyle::MakeText(WidgetTree, LOCTEXT("LobbyTitle", "The Crew"), HPStyle::HeaderSize, HPStyle::Wheat));
	StatusText = HPStyle::MakeText(WidgetTree, FText::GetEmpty(), HPStyle::BodySize, HPStyle::Muted, false);
	Box->AddChildToVerticalBox(StatusText)->SetPadding(FMargin(0, 0, 0, 10));

	RosterBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Box->AddChildToVerticalBox(RosterBox)->SetPadding(FMargin(0, 0, 0, 10));

	PromptText = HPStyle::MakeText(WidgetTree, FText::GetEmpty(), HPStyle::BodySize, HPStyle::Pumpkin);
	PromptText->SetAutoWrapText(true);
	PromptText->SetVisibility(ESlateVisibility::Collapsed);
	Box->AddChildToVerticalBox(PromptText)->SetPadding(FMargin(0, 0, 0, 8));

	TWeakObjectPtr<UHPLobbyWidget> Self = this;
	UHorizontalBox* Row1 = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	ReadyButton = MakeButton(LOCTEXT("ReadyUp", "Ready up (R)"), [Self]() { if (Self.IsValid() && Self->GetLobbyPC()) Self->GetLobbyPC()->ToggleReady(); }, HPStyle::BodySize);
	Row1->AddChildToHorizontalBox(ReadyButton)->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
	BellButton = MakeButton(LOCTEXT("Bell", "Ring the dinner bell (B)"), [Self]() { if (Self.IsValid() && Self->GetLobbyPC()) Self->GetLobbyPC()->ServerRingBell(); }, HPStyle::BodySize);
	UHorizontalBoxSlot* BS = Row1->AddChildToHorizontalBox(BellButton);
	BS->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
	BS->SetPadding(FMargin(10, 0, 0, 0));
	Box->AddChildToVerticalBox(Row1)->SetPadding(FMargin(0, 4));

	UHorizontalBox* Row2 = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	auto AddSmall = [this, Row2](const FText& T, TFunction<void()> F, bool bFirst)
	{
		UHPButton* B = MakeButton(T, MoveTemp(F), 24);
		UHorizontalBoxSlot* S = Row2->AddChildToHorizontalBox(B);
		S->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
		S->SetPadding(FMargin(bFirst ? 0.f : 8.f, 0, 0, 0));
		return B;
	};
	const bool bCanInvite = GetHPGameInstance() && GetHPGameInstance()->CanInvite();
	UHPButton* InviteB = AddSmall(LOCTEXT("Invite", "Invite"), [Self]() { if (Self.IsValid() && Self->GetHPGameInstance()) Self->GetHPGameInstance()->ShowInviteOverlay(); }, true);
	InviteB->SetIsEnabled(bCanInvite);
	if (!bCanInvite) InviteB->SetToolTipText(LOCTEXT("InviteOff", "Invites need Steam. On LAN, friends use Join > Address."));
	AddSmall(LOCTEXT("Customize", "Customize"), [Self]() { if (Self.IsValid() && Self->Owner.IsValid()) Self->Owner->Go(EHPMenuState::Customize); }, false);
	AddSmall(LOCTEXT("Settings", "Settings"), [Self]() { if (Self.IsValid() && Self->Owner.IsValid()) Self->Owner->Go(EHPMenuState::Settings); }, false);
	AddSmall(LOCTEXT("LeaveShort", "Leave"), [Self]() { if (Self.IsValid() && Self->GetHPGameInstance()) Self->GetHPGameInstance()->LeaveToMainMenu(); }, false);
	Box->AddChildToVerticalBox(Row2)->SetPadding(FMargin(0, 4, 0, 12));

	// Crew chat line.
	ChatBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Box->AddChildToVerticalBox(ChatBox);
	ChatEntry = WidgetTree->ConstructWidget<UEditableTextBox>(UEditableTextBox::StaticClass());
	ChatEntry->SetHintText(LOCTEXT("ChatHint", "Say something to the crew... (Enter)"));
	HPStyle::StyleTextBox(ChatEntry, 24);
	UHorizontalBox* ChatRow = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	UHorizontalBoxSlot* ES = ChatRow->AddChildToHorizontalBox(ChatEntry);
	ES->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
	ES->SetVerticalAlignment(VAlign_Center);
	UHorizontalBoxSlot* SayS = ChatRow->AddChildToHorizontalBox(MakeButton(LOCTEXT("Say", "Say"), [Self]() { if (Self.IsValid()) Self->SendChat(); }, 24));
	SayS->SetPadding(FMargin(8, 0, 0, 0));
	Box->AddChildToVerticalBox(ChatRow)->SetPadding(FMargin(0, 6, 0, 0));

	Box->AddChildToVerticalBox(HPStyle::MakeText(WidgetTree, LOCTEXT("LobbyKeys", "Esc: hide   R: ready   B: bell   Enter/Say: chat"), 22, HPStyle::Muted, false))
		->SetPadding(FMargin(0, 10, 0, 0));

	// Small always-visible hint at the bottom when the panel is collapsed lives in UHPLobbyHintWidget.
}

void UHPLobbyWidget::NativeConstruct()
{
	Super::NativeConstruct();
	RefreshFromState();
}

void UHPLobbyWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	if (PromptTimeLeft > 0.f)
	{
		PromptTimeLeft -= InDeltaTime;
		if (PromptTimeLeft <= 0.f && PromptText) PromptText->SetVisibility(ESlateVisibility::Collapsed);
	}
	// Cheap safety poll: replication callbacks cover most changes, this covers late PlayerState arrivals.
	PollTimer += InDeltaTime;
	if (PollTimer > 1.f)
	{
		PollTimer = 0.f;
		RefreshFromState();
	}
}

void UHPLobbyWidget::SendChat()
{
	const FText Text = ChatEntry ? ChatEntry->GetText() : FText::GetEmpty();
	if (!Text.IsEmpty())
	{
		if (AHPLobbyPlayerController* PC = GetLobbyPC()) PC->ServerChat(Text.ToString());
		ChatEntry->SetText(FText::GetEmpty());
	}
}

bool UHPLobbyWidget::IsTyping() const
{
	return ChatEntry && ChatEntry->HasKeyboardFocus();
}

bool UHPLobbyWidget::HandleBackKey()
{
	if (Owner.IsValid())
	{
		if (Owner->IsLobbyOverlayVisible()) Owner->ToggleLobbyOverlay();
		else Owner->TogglePause();
	}
	return true;
}

void UHPLobbyWidget::ShowStartAnyway(int32 Ready, int32 Total)
{
	if (!PromptText) return;
	PromptText->SetText(FText::Format(LOCTEXT("StartAnyway", "Start anyway? ({0}/{1} ready) Ring the bell again within 3 s."), Ready, Total));
	PromptText->SetVisibility(ESlateVisibility::HitTestInvisible);
	PromptTimeLeft = 3.f;
}

void UHPLobbyWidget::RefreshFromState()
{
	if (!RosterBox) return;
	UWorld* World = GetWorld();
	const AHPLobbyGameState* GS = World ? World->GetGameState<AHPLobbyGameState>() : nullptr;
	const AHPLobbyPlayerState* Me = GetOwningPlayer() ? GetOwningPlayer()->GetPlayerState<AHPLobbyPlayerState>() : nullptr;
	const bool bIAmHost = Me && Me->bIsHost;

	for (UWidget* Child : RosterBox->GetAllChildren())
	{
		if (UHorizontalBox* HB = Cast<UHorizontalBox>(Child))
		{
			for (UWidget* C : HB->GetAllChildren()) Buttons.Remove(Cast<UHPButton>(C));
		}
	}
	RosterBox->ClearChildren();

	int32 Ready = 0, Total = 0;
	if (GS)
	{
		GS->GetReadyCounts(Ready, Total);
		for (APlayerState* PSBase : GS->PlayerArray)
		{
			const AHPLobbyPlayerState* PS = Cast<AHPLobbyPlayerState>(PSBase);
			if (!PS) continue;
			UHorizontalBox* Row = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
			UTextBlock* Check = HPStyle::MakeText(WidgetTree, PS->bReady ? LOCTEXT("ReadyMark", "READY") : LOCTEXT("NotReadyMark", "..."), HPStyle::BodySize, PS->bReady ? HPStyle::Leaf : HPStyle::Muted);
			USizeBox* CheckBox = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
			CheckBox->SetWidthOverride(110.f);
			CheckBox->AddChild(Check);
			Row->AddChildToHorizontalBox(CheckBox)->SetVerticalAlignment(VAlign_Center);
			FText Name = FText::FromString(PS->GetPlayerName());
			if (PS->bIsHost) Name = FText::Format(LOCTEXT("HostTag", "{0}  (host)"), Name);
			if (PS == Me) Name = FText::Format(LOCTEXT("YouTag", "{0}  - you"), Name);
			UHorizontalBoxSlot* NS = Row->AddChildToHorizontalBox(HPStyle::MakeText(WidgetTree, Name, HPStyle::BodySize, HPStyle::Cream));
			NS->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
			NS->SetVerticalAlignment(VAlign_Center);
			if (bIAmHost && PS != Me)
			{
				TWeakObjectPtr<APlayerState> Target = const_cast<AHPLobbyPlayerState*>(PS);
				TWeakObjectPtr<UHPLobbyWidget> Self = this;
				Row->AddChildToHorizontalBox(MakeButton(LOCTEXT("Kick", "Send home"), [Self, Target]()
				{
					if (Self.IsValid() && Self->GetLobbyPC() && Target.IsValid()) Self->GetLobbyPC()->ServerKick(Target.Get());
				}, 22));
			}
			RosterBox->AddChildToVerticalBox(Row)->SetPadding(FMargin(0, 4));
		}
		// Empty slots so the 4-player shape is obvious.
		for (int32 i = Total; i < UHPGameInstance::MaxPlayers; ++i)
		{
			RosterBox->AddChildToVerticalBox(HPStyle::MakeText(WidgetTree, LOCTEXT("EmptySlot", "        (empty chair)"), HPStyle::BodySize, HPStyle::Muted.CopyWithNewOpacity(0.6f), false))
				->SetPadding(FMargin(0, 4));
		}

		ChatBox->ClearChildren();
		for (const FString& Line : GS->ChatLines)
		{
			UTextBlock* T = HPStyle::MakeText(WidgetTree, FText::FromString(Line), 22, HPStyle::Cream, false);
			T->SetAutoWrapText(true);
			ChatBox->AddChildToVerticalBox(T);
		}
	}

	if (StatusText)
	{
		if (GS && GS->CountdownSeconds > 0)
		{
			StatusText->SetText(FText::Format(LOCTEXT("Countdown", "Dinner bell! Heading out in {0}..."), GS->CountdownSeconds));
			StatusText->SetColorAndOpacity(FSlateColor(HPStyle::Pumpkin));
		}
		else
		{
			StatusText->SetText(Total <= 1
				? LOCTEXT("Solo", "Just you so far. It's tuned for 2-4 farmers, but solo works.")
				: FText::Format(LOCTEXT("ReadyCount", "{0}/{1} ready. Stand on the porch or press R."), Ready, Total));
			StatusText->SetColorAndOpacity(FSlateColor(HPStyle::Muted));
		}
	}
	if (ReadyButton)
	{
		ReadyButton->SetLabel(Me && Me->bReady ? LOCTEXT("Unready", "Not ready yet (R)") : LOCTEXT("ReadyUp", "Ready up (R)"));
	}
	if (BellButton)
	{
		BellButton->SetLabel(bIAmHost ? LOCTEXT("Bell", "Ring the dinner bell (B)") : LOCTEXT("BellGuest", "Nudge the host (B)"));
	}
}

// ================================================================ Hint strip
TSharedRef<SWidget> UHPLobbyHintWidget::RebuildWidget()
{
	if (WidgetTree && !WidgetTree->RootWidget)
	{
		UOverlay* Root = WidgetTree->ConstructWidget<UOverlay>(UOverlay::StaticClass());
		WidgetTree->RootWidget = Root;
		UBorder* B = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass());
		HPStyle::StyleBorder(B, HPStyle::SoilDark.CopyWithNewOpacity(0.75f), 14.f);
		B->SetPadding(FMargin(22, 10));
		Hint = HPStyle::MakeText(WidgetTree, FText::GetEmpty(), 24, HPStyle::Cream, false);
		B->SetContent(Hint);
		UOverlaySlot* S = Root->AddChildToOverlay(B);
		S->SetHorizontalAlignment(HAlign_Center);
		S->SetVerticalAlignment(VAlign_Bottom);
		S->SetPadding(FMargin(0, 0, 0, 36));
		SetVisibility(ESlateVisibility::HitTestInvisible);
	}
	return Super::RebuildWidget();
}

void UHPLobbyHintWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	const UWorld* World = GetWorld();
	const AHPLobbyGameState* GS = World ? World->GetGameState<AHPLobbyGameState>() : nullptr;
	const UHPFrontendUISubsystem* UI = UHPFrontendUISubsystem::Get(GetOwningPlayer());
	const bool bShow = UI && UI->GetState() == EHPMenuState::Lobby && !UI->IsLobbyOverlayVisible();
	if (Hint)
	{
		Hint->SetVisibility(bShow ? ESlateVisibility::HitTestInvisible : ESlateVisibility::Collapsed);
		int32 R = 0, T = 0;
		if (GS) GS->GetReadyCounts(R, T);
		if (GS && GS->CountdownSeconds > 0)
		{
			Hint->SetText(FText::Format(LOCTEXT("HintCountdown", "Heading out in {0}... (step off the porch to cancel)"), GS->CountdownSeconds));
		}
		else
		{
			Hint->SetText(FText::Format(LOCTEXT("Hint", "{0}/{1} ready   |   Porch or R: ready   B: dinner bell   Tab: crew   Esc: menu"), R, T));
		}
	}
	if (UWidget* Root = WidgetTree ? WidgetTree->RootWidget.Get() : nullptr)
	{
		Root->SetVisibility(bShow ? ESlateVisibility::HitTestInvisible : ESlateVisibility::Collapsed);
	}
}

#undef LOCTEXT_NAMESPACE
