#include "Frontend/UI/HPMenuScreens.h"
#include "Frontend/UI/HPFrontendStyle.h"
#include "Frontend/HPFrontendUISubsystem.h"
#include "Frontend/HPGameInstance.h"
#include "Blueprint/WidgetTree.h"
#include "Components/TextBlock.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Components/HorizontalBox.h"
#include "Components/HorizontalBoxSlot.h"
#include "Components/Overlay.h"
#include "Components/OverlaySlot.h"
#include "Components/SizeBox.h"
#include "Components/ScrollBox.h"
#include "Components/Image.h"
#include "Components/EditableTextBox.h"
#include "Kismet/KismetSystemLibrary.h"
#include "GameFramework/PlayerController.h"
#include "Engine/World.h"
#include "UObject/UObjectGlobals.h"

#define LOCTEXT_NAMESPACE "HPFrontend"

// ================================================================ Title
void UHPTitleWidget::BuildTree()
{
	UOverlay* Root = WidgetTree->ConstructWidget<UOverlay>(UOverlay::StaticClass());
	WidgetTree->RootWidget = Root;

	// Soft left-side vignette so text reads over the farm diorama.
	UImage* Shade = WidgetTree->ConstructWidget<UImage>(UImage::StaticClass());
	Shade->SetColorAndOpacity(FLinearColor(0.03f, 0.015f, 0.f, 0.45f));
	USizeBox* ShadeBox = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	ShadeBox->SetWidthOverride(760.f);
	ShadeBox->AddChild(Shade);
	UOverlaySlot* SS = Root->AddChildToOverlay(ShadeBox);
	SS->SetHorizontalAlignment(HAlign_Left);
	SS->SetVerticalAlignment(VAlign_Fill);

	UVerticalBox* Col = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	UOverlaySlot* CS = Root->AddChildToOverlay(Col);
	CS->SetHorizontalAlignment(HAlign_Left);
	CS->SetVerticalAlignment(VAlign_Center);
	CS->SetPadding(FMargin(110.f, 0.f, 0.f, 0.f));

	UTextBlock* L1 = HPStyle::MakeText(WidgetTree, LOCTEXT("Logo1", "HARVEST"), HPStyle::LogoSize, HPStyle::Wheat);
	L1->SetShadowOffset(FVector2D(5.f, 6.f));
	L1->SetShadowColorAndOpacity(HPStyle::Barn);
	Col->AddChildToVerticalBox(L1);
	UTextBlock* L2 = HPStyle::MakeText(WidgetTree, LOCTEXT("Logo2", "PANIC!"), HPStyle::LogoSize + 20, HPStyle::Pumpkin);
	L2->SetShadowOffset(FVector2D(5.f, 6.f));
	L2->SetShadowColorAndOpacity(HPStyle::SoilDark);
	L2->SetRenderTransformAngle(-4.f);
	Col->AddChildToVerticalBox(L2)->SetPadding(FMargin(40.f, -30.f, 0.f, 0.f));
	Col->AddChildToVerticalBox(HPStyle::MakeText(WidgetTree, LOCTEXT("Tagline", "A co-op farm. Everything has legs."), HPStyle::BodySize, HPStyle::Cream, false))
		->SetPadding(FMargin(6.f, 4.f, 0.f, 34.f));

	USizeBox* BtnSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	BtnSize->SetWidthOverride(420.f);
	UVerticalBox* Btns = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	BtnSize->AddChild(Btns);
	Col->AddChildToVerticalBox(BtnSize);

	TWeakObjectPtr<UHPTitleWidget> Self = this;
	AddButton(Btns, LOCTEXT("Play", "Play"), [Self]() { if (Self.IsValid() && Self->Owner.IsValid()) Self->Owner->Go(EHPMenuState::PlayChoice); });
	AddButton(Btns, LOCTEXT("Customize", "Customize"), [Self]() { if (Self.IsValid() && Self->Owner.IsValid()) Self->Owner->Go(EHPMenuState::Customize); });
	AddButton(Btns, LOCTEXT("Settings", "Settings"), [Self]() { if (Self.IsValid() && Self->Owner.IsValid()) Self->Owner->Go(EHPMenuState::Settings); });
	AddButton(Btns, LOCTEXT("Quit", "Quit"), [Self]()
	{
		if (Self.IsValid())
		{
			UKismetSystemLibrary::QuitGame(Self.Get(), Self->GetOwningPlayer(), EQuitPreference::Quit, false);
		}
	});

	// Footer: which online backend we're on (so a Mac dev knows invites are off).
	FText Status = LOCTEXT("OnlineUnknown", "Offline");
	if (UHPGameInstance* GI = GetHPGameInstance())
	{
		Status = GI->IsUsingFallbackSubsystem()
			? LOCTEXT("OnlineLAN", "LAN mode - Steam isn't running, so invites are off")
			: FText::Format(LOCTEXT("OnlineSteam", "Online: {0}"), FText::FromName(GI->GetActiveSubsystemName()));
	}
	UTextBlock* Foot = HPStyle::MakeText(WidgetTree, Status, 22, HPStyle::Muted, false);
	UOverlaySlot* FS = Root->AddChildToOverlay(Foot);
	FS->SetHorizontalAlignment(HAlign_Right);
	FS->SetVerticalAlignment(VAlign_Bottom);
	FS->SetPadding(FMargin(0.f, 0.f, 40.f, 30.f));
}

// ================================================================ Play choice
void UHPPlayChoiceWidget::StartHost(bool bLAN)
{
	if (Owner.IsValid() && Owner->Go(EHPMenuState::Connecting))
	{
		if (UHPGameInstance* GI = GetHPGameInstance())
		{
			GI->HostSession(bLAN);
		}
	}
}

void UHPPlayChoiceWidget::BuildTree()
{
	UVerticalBox* Box = MakeCard(LOCTEXT("PlayTitle", "Who's farming?"), 620.f, true,
		LOCTEXT("PlaySub", "Host a farm for up to 4 farmers, or join a friend's."));
	TWeakObjectPtr<UHPPlayChoiceWidget> Self = this;
	const bool bFallback = GetHPGameInstance() && GetHPGameInstance()->IsUsingFallbackSubsystem();
	if (!bFallback)
	{
		AddButton(Box, LOCTEXT("HostOnline", "Host a farm (friends)"), [Self]() { if (Self.IsValid()) Self->StartHost(false); });
	}
	AddButton(Box, LOCTEXT("HostLAN", "Host a LAN farm"), [Self]() { if (Self.IsValid()) Self->StartHost(true); });
	AddButton(Box, LOCTEXT("Join", "Join a farm"), [Self]() { if (Self.IsValid() && Self->Owner.IsValid()) Self->Owner->Go(EHPMenuState::Browser); });
	AddButton(Box, LOCTEXT("Back", "Back"), [Self]() { if (Self.IsValid() && Self->Owner.IsValid()) Self->Owner->Back(); });
}

// ================================================================ Server browser
void UHPServerBrowserWidget::BuildTree()
{
	bLAN = !GetHPGameInstance() || GetHPGameInstance()->IsUsingFallbackSubsystem();
	UVerticalBox* Box = MakeCard(LOCTEXT("BrowserTitle", "Find a farm"), 980.f);
	TWeakObjectPtr<UHPServerBrowserWidget> Self = this;

	UHorizontalBox* Top = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	UHPButton* LanBtn = MakeButton(bLAN ? LOCTEXT("ModeLAN", "Showing: LAN") : LOCTEXT("ModeOnline", "Showing: Online"), nullptr, HPStyle::BodySize);
	TWeakObjectPtr<UHPButton> WeakLan = LanBtn;
	LanBtn->OnHPClick = [Self, WeakLan]()
	{
		if (!Self.IsValid()) return;
		const bool bFallback = Self->GetHPGameInstance() && Self->GetHPGameInstance()->IsUsingFallbackSubsystem();
		Self->bLAN = bFallback ? true : !Self->bLAN;
		if (WeakLan.IsValid()) WeakLan->SetLabel(Self->bLAN ? LOCTEXT("ModeLAN", "Showing: LAN") : LOCTEXT("ModeOnline", "Showing: Online"));
		Self->Refresh();
	};
	Top->AddChildToHorizontalBox(LanBtn)->SetPadding(FMargin(0, 0, 12, 0));
	UHPButton* RefreshBtn = MakeButton(LOCTEXT("Refresh", "Refresh"), [Self]() { if (Self.IsValid()) Self->Refresh(); }, HPStyle::BodySize);
	Top->AddChildToHorizontalBox(RefreshBtn);
	StatusText = HPStyle::MakeText(WidgetTree, FText::GetEmpty(), HPStyle::BodySize, HPStyle::Muted, false);
	UHorizontalBoxSlot* StS = Top->AddChildToHorizontalBox(StatusText);
	StS->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
	StS->SetHorizontalAlignment(HAlign_Right);
	StS->SetVerticalAlignment(VAlign_Center);
	Box->AddChildToVerticalBox(Top)->SetPadding(FMargin(0, 0, 0, 12));

	USizeBox* ListSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	ListSize->SetHeightOverride(320.f);
	UScrollBox* Scroll = WidgetTree->ConstructWidget<UScrollBox>(UScrollBox::StaticClass());
	ListSize->AddChild(Scroll);
	ListBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Scroll->AddChild(ListBox);
	Box->AddChildToVerticalBox(ListSize);

	UHorizontalBox* AddrRow = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	AddrRow->AddChildToHorizontalBox(HPStyle::MakeText(WidgetTree, LOCTEXT("Address", "Address:"), HPStyle::BodySize))->SetVerticalAlignment(VAlign_Center);
	AddressBox = WidgetTree->ConstructWidget<UEditableTextBox>(UEditableTextBox::StaticClass());
	AddressBox->SetHintText(LOCTEXT("AddressHint", "127.0.0.1 or 192.168.1.20"));
	HPStyle::StyleTextBox(AddressBox, HPStyle::BodySize);
	UHorizontalBoxSlot* AS = AddrRow->AddChildToHorizontalBox(AddressBox);
	AS->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
	AS->SetPadding(FMargin(12, 0));
	AS->SetVerticalAlignment(VAlign_Center);
	AddrRow->AddChildToHorizontalBox(MakeButton(LOCTEXT("JoinAddr", "Join"), [Self]()
	{
		if (!Self.IsValid() || !Self->Owner.IsValid()) return;
		const FString Addr = Self->AddressBox ? Self->AddressBox->GetText().ToString() : FString();
		if (Self->Owner->Go(EHPMenuState::Connecting))
		{
			if (UHPGameInstance* GI = Self->GetHPGameInstance()) GI->JoinByAddress(Addr);
		}
	}, HPStyle::BodySize));
	Box->AddChildToVerticalBox(AddrRow)->SetPadding(FMargin(0, 14, 0, 6));

	AddButton(Box, LOCTEXT("Back", "Back"), [Self]() { if (Self.IsValid() && Self->Owner.IsValid()) Self->Owner->Back(); });
}

void UHPServerBrowserWidget::NativeConstruct()
{
	Super::NativeConstruct();
	if (UHPGameInstance* GI = GetHPGameInstance())
	{
		ResultsHandle = GI->OnSessionsFound.AddUObject(this, &UHPServerBrowserWidget::HandleResults);
	}
	Refresh();
}

void UHPServerBrowserWidget::NativeDestruct()
{
	if (UHPGameInstance* GI = GetHPGameInstance())
	{
		GI->OnSessionsFound.Remove(ResultsHandle);
		GI->CancelFind();
	}
	Super::NativeDestruct();
}

void UHPServerBrowserWidget::Refresh()
{
	if (StatusText) StatusText->SetText(LOCTEXT("Searching", "Looking over the fences..."));
	if (UHPGameInstance* GI = GetHPGameInstance())
	{
		GI->FindSessions(bLAN);
	}
}

void UHPServerBrowserWidget::HandleResults(const TArray<FHPServerEntry>& Entries)
{
	if (!ListBox) return;
	// Remove old row buttons from the focus list.
	for (UWidget* Child : ListBox->GetAllChildren())
	{
		Buttons.Remove(Cast<UHPButton>(Child));
	}
	ListBox->ClearChildren();
	if (StatusText)
	{
		StatusText->SetText(Entries.Num() == 0 ? LOCTEXT("NoFarms", "No farms found. Try Refresh, or type an address.")
			: FText::Format(LOCTEXT("FoundFarms", "{0} farm(s) found"), Entries.Num()));
	}
	TWeakObjectPtr<UHPServerBrowserWidget> Self = this;
	for (const FHPServerEntry& E : Entries)
	{
		const int32 Players = E.MaxSlots - E.OpenSlots;
		const FText Row = FText::Format(LOCTEXT("Row", "{0}'s farm    {1}/{2}    {3} ms{4}"),
			FText::FromString(E.HostName), Players, E.MaxSlots, E.PingMs, E.bLAN ? LOCTEXT("LanTag", "    LAN") : FText::GetEmpty());
		const int32 Index = E.ResultIndex;
		UHPButton* B = MakeButton(Row, [Self, Index]()
		{
			if (Self.IsValid() && Self->Owner.IsValid() && Self->Owner->Go(EHPMenuState::Connecting))
			{
				if (UHPGameInstance* GI = Self->GetHPGameInstance()) GI->JoinServerEntry(Index);
			}
		}, HPStyle::BodySize);
		B->SetIsEnabled(E.OpenSlots > 0); // full rows greyed out
		ListBox->AddChildToVerticalBox(B)->SetPadding(FMargin(0, 4));
	}
}

// ================================================================ Connecting
void UHPConnectingWidget::BuildTree()
{
	UVerticalBox* Box = MakeCard(LOCTEXT("Connecting", "Hitching the wagon..."), 620.f, true,
		LOCTEXT("ConnectingSub", "Talking to the other farm. This usually takes a few seconds."));
	TWeakObjectPtr<UHPConnectingWidget> Self = this;
	AddButton(Box, LOCTEXT("Cancel", "Cancel"), [Self]()
	{
		if (!Self.IsValid()) return;
		if (UHPGameInstance* GI = Self->GetHPGameInstance()) GI->CancelFind();
		if (Self->Owner.IsValid()) Self->Owner->Back();
	});
}

void UHPConnectingWidget::NativeConstruct()
{
	Super::NativeConstruct();
	if (UHPGameInstance* GI = GetHPGameInstance())
	{
		HostHandle = GI->OnHostResult.AddUObject(this, &UHPConnectingWidget::HandleResult);
		JoinHandle = GI->OnJoinResult.AddUObject(this, &UHPConnectingWidget::HandleResult);
	}
}

void UHPConnectingWidget::NativeDestruct()
{
	if (UHPGameInstance* GI = GetHPGameInstance())
	{
		GI->OnHostResult.Remove(HostHandle);
		GI->OnJoinResult.Remove(JoinHandle);
	}
	Super::NativeDestruct();
}

void UHPConnectingWidget::HandleResult(bool bOk, const FText& Error)
{
	if (bOk || !Owner.IsValid())
	{
		return; // success -> travel replaces the menu
	}
	TWeakObjectPtr<UHPFrontendUISubsystem> O = Owner;
	O->Back();
	O->ShowError(Error);
}

// ================================================================ Error popup
void UHPErrorPopupWidget::BuildTree()
{
	UVerticalBox* Box = MakeCard(LOCTEXT("ErrTitle", "Well, that's a problem."), 680.f, true, Message);
	TWeakObjectPtr<UHPErrorPopupWidget> Self = this;
	AddButton(Box, LOCTEXT("OK", "OK"), [Self]() { if (Self.IsValid() && Self->OnBackRequested) Self->OnBackRequested(); });
}

// ================================================================ Customize host
void UHPCustomizeHostWidget::BuildTree()
{
	UVerticalBox* Box = MakeCard(LOCTEXT("CustomizeTitle", "Wardrobe"), 900.f);
	// Loaded by name so this compiles without the character-customization branch.
	UClass* CustomClass = StaticLoadClass(UUserWidget::StaticClass(), nullptr, UHPFrontendUISubsystem::CustomizationClassPath, nullptr, LOAD_Quiet | LOAD_NoWarn);
	UUserWidget* Custom = CustomClass ? CreateWidget<UUserWidget>(GetOwningPlayer(), CustomClass) : nullptr;
	if (Custom)
	{
		Box->AddChildToVerticalBox(Custom)->SetPadding(FMargin(0, 0, 0, 12));
	}
	else
	{
		UTextBlock* T = HPStyle::MakeText(WidgetTree, LOCTEXT("ComingSoon", "Coming soon! Grandpa is still ironing the overalls."), HPStyle::BodySize, HPStyle::Cream, false);
		T->SetJustification(ETextJustify::Center);
		T->SetAutoWrapText(true);
		Box->AddChildToVerticalBox(T)->SetPadding(FMargin(0, 20, 0, 30));
	}
	TWeakObjectPtr<UHPCustomizeHostWidget> Self = this;
	AddButton(Box, LOCTEXT("Back", "Back"), [Self]() { if (Self.IsValid() && Self->Owner.IsValid()) Self->Owner->Back(); });
}

// ================================================================ Pause
void UHPPauseWidget::BuildTree()
{
	UVerticalBox* Box = MakeCard(LOCTEXT("PauseTitle", "Paused (sort of)"), 600.f, true,
		LOCTEXT("PauseSub", "The farm does not pause."));
	TWeakObjectPtr<UHPPauseWidget> Self = this;
	AddButton(Box, LOCTEXT("Resume", "Resume"), [Self]() { if (Self.IsValid() && Self->Owner.IsValid()) Self->Owner->TogglePause(); });
	AddButton(Box, LOCTEXT("Settings", "Settings"), [Self]() { if (Self.IsValid() && Self->Owner.IsValid()) Self->Owner->Go(EHPMenuState::Settings); });
	if (UHPGameInstance* GI = GetHPGameInstance())
	{
		if (GI->CanInvite())
		{
			AddButton(Box, LOCTEXT("Invite", "Invite friends"), [Self]() { if (Self.IsValid() && Self->GetHPGameInstance()) Self->GetHPGameInstance()->ShowInviteOverlay(); });
		}
	}
	UWorld* World = GetWorld();
	const bool bHost = World && World->GetNetMode() != NM_Client;
	const bool bInDay = Owner.IsValid() && Owner->GetMachine()->Root() == EHPMenuState::InGame;
	if (bHost && bInDay)
	{
		AddButton(Box, LOCTEXT("ReturnLobby", "Return everyone to the lobby"), [Self]()
		{
			if (Self.IsValid() && Self->GetWorld())
			{
				Self->GetWorld()->ServerTravel(FString(UHPGameInstance::LobbyMap) + TEXT("?listen"));
			}
		});
	}
	AddButton(Box, LOCTEXT("Leave", "Leave to title"), [Self]()
	{
		if (Self.IsValid() && Self->GetHPGameInstance()) Self->GetHPGameInstance()->LeaveToMainMenu();
	});
}

#undef LOCTEXT_NAMESPACE
