// Copyright HARVEST PANIC team. All Rights Reserved.
#include "UI/HPPayoutWidget.h"
#include "UI/HPUIStyle.h"
#include "HPGameState.h"
#include "HPGameMode.h"
#include "Economy/HPEconomyMath.h"
#include "Blueprint/WidgetTree.h"
#include "Components/Border.h"
#include "Components/BorderSlot.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Components/HorizontalBox.h"
#include "Components/HorizontalBoxSlot.h"
#include "Components/ScrollBox.h"
#include "Components/SizeBox.h"
#include "Components/Button.h"
#include "Components/TextBlock.h"
#include "GameFramework/PlayerController.h"

TSharedRef<SWidget> UHPPayoutWidget::RebuildWidget()
{
	if (WidgetTree && !WidgetTree->RootWidget)
	{
		BuildTree();
	}
	return Super::RebuildWidget();
}

static UButton* MakeButton(UWidgetTree* Tree, const FString& Label, const FLinearColor& Color, UTextBlock*& OutLabel)
{
	UButton* B = Tree->ConstructWidget<UButton>(UButton::StaticClass());
	B->SetBackgroundColor(Color);
	OutLabel = HPUI::MakeText(Tree, Label, 16, HPUI::Ink, true);
	B->SetContent(OutLabel);
	return B;
}

void UHPPayoutWidget::BuildTree()
{
	// Full-screen dim → centered paper receipt (fixed width) → scrollable rows + button bar.
	UBorder* Dim = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass(), TEXT("Root"));
	Dim->SetBrushColor(FLinearColor(0.f, 0.f, 0.f, 0.65f));
	Dim->SetHorizontalAlignment(HAlign_Center);
	Dim->SetVerticalAlignment(VAlign_Center);
	WidgetTree->RootWidget = Dim;

	USizeBox* Size = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	Size->SetWidthOverride(560.f);
	Size->SetMaxDesiredHeight(660.f);
	Dim->SetContent(Size);

	UBorder* Paper = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass());
	Paper->SetBrushColor(HPUI::Paper);
	Paper->SetPadding(FMargin(28.f, 22.f));
	Size->SetContent(Paper);

	UVerticalBox* Outer = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Paper->SetContent(Outer);

	UScrollBox* Scroll = WidgetTree->ConstructWidget<UScrollBox>(UScrollBox::StaticClass());
	ReceiptBox = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Scroll->AddChild(ReceiptBox);
	UVerticalBoxSlot* ScrollSlot = Outer->AddChildToVerticalBox(Scroll);
	ScrollSlot->SetSize(FSlateChildSize(ESlateSizeRule::Fill));

	UHorizontalBox* Buttons = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	UTextBlock* RestartLabel = nullptr;
	RestartButton = MakeButton(WidgetTree, TEXT("Restart Run"), FLinearColor(0.85f, 0.55f, 0.5f), RestartLabel);
	UTextBlock* NextLabelRaw = nullptr;
	NextButton = MakeButton(WidgetTree, TEXT("Next Day"), FLinearColor(0.55f, 0.85f, 0.55f), NextLabelRaw);
	NextLabel = NextLabelRaw;
	RestartButton->OnClicked.AddDynamic(this, &UHPPayoutWidget::OnRestartClicked);
	NextButton->OnClicked.AddDynamic(this, &UHPPayoutWidget::OnNextClicked);
	Buttons->AddChildToHorizontalBox(RestartButton)->SetPadding(FMargin(0.f, 0.f, 12.f, 0.f));
	UHorizontalBoxSlot* Spacer = Buttons->AddChildToHorizontalBox(HPUI::MakeText(WidgetTree, TEXT(""), 12, HPUI::Ink));
	Spacer->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
	Buttons->AddChildToHorizontalBox(NextButton);
	Outer->AddChildToVerticalBox(Buttons)->SetPadding(FMargin(0.f, 16.f, 0.f, 0.f));

	HostNote = HPUI::MakeText(WidgetTree, TEXT(""), 12, FLinearColor(0.4f, 0.35f, 0.3f));
	Outer->AddChildToVerticalBox(HostNote)->SetHorizontalAlignment(HAlign_Right);
}

void UHPPayoutWidget::AddRow(const FString& Left, const FString& Right, const FLinearColor& Color, int32 Size, bool bBold)
{
	UHorizontalBox* Row = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	UTextBlock* L = HPUI::MakeText(WidgetTree, Left, Size, Color, bBold);
	L->SetAutoWrapText(true);
	Row->AddChildToHorizontalBox(L)->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
	if (!Right.IsEmpty())
	{
		UHorizontalBoxSlot* RS = Row->AddChildToHorizontalBox(HPUI::MakeText(WidgetTree, Right, Size, Color, bBold));
		RS->SetPadding(FMargin(12.f, 0.f, 0.f, 0.f));
	}
	ReceiptBox->AddChildToVerticalBox(Row)->SetPadding(FMargin(0.f, 2.f));
}

void UHPPayoutWidget::RebuildReceipt(const AHPGameState* GS)
{
	ReceiptBox->ClearChildren();
	const FHPDaySummary& S = GS->LastSummary;
	const FString Rule = TEXT("- - - - - - - - - - - - - - - - - - - - - - - - - - - - - -");

	if (GS->Phase == EHPDayPhase::Sunday)
	{
		const bool bPaid = S.SundayResult == EHPSundayResult::Paid;
		AddRow(FString::Printf(TEXT("SUNDAY  ·  WEEK %d"), S.WeekIndex), TEXT(""), HPUI::Ink, 24, true);
		AddRow(TEXT("The Landlord has arrived. He brought a clipboard."), TEXT(""), HPUI::Ink, 14);
		AddRow(Rule, TEXT(""), HPUI::Ink, 12);
		AddRow(TEXT("Rent due"), HPUI::Money(S.QuotaDue), HPUI::Ink, 18);
		AddRow(TEXT("Wallet after"), HPUI::Money(S.WalletAfter), HPUI::Ink, 18, true);
		AddRow(Rule, TEXT(""), HPUI::Ink, 12);
		if (bPaid)
		{
			AddRow(S.WalletAfter == 0 ? TEXT("PAID. Exactly. The Landlord seems disappointed.") : TEXT("PAID. The Landlord grunts and leaves."),
				TEXT(""), HPUI::GoodInk, 20, true);
		}
		else
		{
			AddRow(TEXT("EVICTED."), TEXT(""), HPUI::BadInk, 28, true);
			AddRow(TEXT("Grandpa's farm now belongs to a man named Gary. Gary has never touched a pig."), TEXT(""), HPUI::BadInk, 14);
		}
		NextLabel->SetText(FText::FromString(bPaid ? TEXT("Next Week") : TEXT("Try Again")));
		return;
	}

	AddRow(FString::Printf(TEXT("PAYOUT  ·  %s, WEEK %d"), HPUI::DayName(S.DayOfWeek), S.WeekIndex), TEXT(""), HPUI::Ink, 22, true);

	// Headline = highest-Severity event of the day.
	const FBlameEvent* Headline = nullptr;
	for (const FBlameEvent& E : GS->DayBlameEvents)
	{
		if (!E.bSecret && (!Headline || E.Severity > Headline->Severity)) Headline = &E;
	}
	if (Headline)
	{
		FHPReceiptLine HL;
		HL.PlayerId = Headline->PrimaryInstigator;
		HL.Verb = Headline->Verb;
		HL.ItemId = Headline->ItemId;
		HL.Count = 1;
		AddRow(FString::Printf(TEXT("HEADLINE: %s (%s)"),
			*UHPEconomyMath::FormatReceiptLine(HL, GS->GetPlayerNameById(HL.PlayerId)), *HPUI::Money(Headline->DollarDelta)),
			TEXT(""), HPUI::BadInk, 14, true);
	}
	AddRow(Rule, TEXT(""), HPUI::Ink, 12);
	AddRow(FString::Printf(TEXT("Sales (%d items)"), S.ItemsSold), HPUI::Money(S.Sales), HPUI::Ink);
	AddRow(S.bOrderComplete ? TEXT("Order complete bonus") : TEXT("Order bonus (order incomplete)"),
		HPUI::Money(S.OrderBonus), S.bOrderComplete ? HPUI::GoodInk : HPUI::Ink);
	AddRow(TEXT("Fees & fines"), HPUI::Money(-S.Costs), S.Costs > 0 ? HPUI::BadInk : HPUI::Ink);
	AddRow(TEXT("Day net"), HPUI::SignedMoney(S.Net), S.Net >= 0 ? HPUI::GoodInk : HPUI::BadInk, 18, true);
	AddRow(TEXT("Wallet"), HPUI::Money(S.WalletAfter), HPUI::Ink, 18, true);
	AddRow(FString::Printf(TEXT("Sunday rent (week %d)"), S.WeekIndex), HPUI::Money(S.QuotaDue), HPUI::Ink, 14);
	AddRow(Rule, TEXT(""), HPUI::Ink, 12);

	AddRow(TEXT("BLAME LEDGER"), TEXT(""), HPUI::Ink, 18, true);
	const TArray<FHPReceiptLine> Lines = UHPEconomyMath::BuildReceipt(GS->DayBlameEvents);
	if (Lines.Num() == 0)
	{
		AddRow(TEXT("Spotless. Suspicious."), TEXT(""), HPUI::Ink, 16);
	}
	int32 CurrentPlayer = MIN_int32;
	int32 ShownForPlayer = 0;
	int32 HiddenForPlayer = 0;
	auto FlushHidden = [&]()
	{
		if (HiddenForPlayer > 0)
		{
			AddRow(FString::Printf(TEXT("    +%d more"), HiddenForPlayer), TEXT(""), FLinearColor(0.4f, 0.35f, 0.3f), 13);
		}
	};
	for (const FHPReceiptLine& L : Lines)
	{
		if (L.PlayerId != CurrentPlayer)
		{
			FlushHidden();
			CurrentPlayer = L.PlayerId;
			ShownForPlayer = 0;
			HiddenForPlayer = 0;
			AddRow(GS->GetPlayerNameById(L.PlayerId).ToUpper(), TEXT(""), HPUI::Ink, 14, true);
		}
		if (ShownForPlayer >= 5) // top 5 lines per player
		{
			++HiddenForPlayer;
			continue;
		}
		++ShownForPlayer;
		AddRow(TEXT("  ") + UHPEconomyMath::FormatReceiptLine(L, GS->GetPlayerNameById(L.PlayerId)),
			HPUI::SignedMoney(L.Dollars), L.bPositive ? HPUI::GoodInk : HPUI::BadInk, 15);
	}
	FlushHidden();
	NextLabel->SetText(FText::FromString(S.DayOfWeek >= UHPEconomyMath::DaysPerWeek - 1 ? TEXT("Face the Landlord") : TEXT("Next Day")));
}

void UHPPayoutWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	const AHPGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHPGameState>() : nullptr;
	if (!GS || !ReceiptBox)
	{
		return;
	}
	const bool bVisible = GS->Phase == EHPDayPhase::Payout || GS->Phase == EHPDayPhase::Sunday;
	SetVisibility(bVisible ? ESlateVisibility::Visible : ESlateVisibility::Collapsed);
	SetMenuInput(bVisible);
	if (!bVisible)
	{
		BuiltKey = -1;
		return;
	}
	const int64 Key = (static_cast<int64>(GS->Phase) << 48) ^ (static_cast<int64>(GS->DayIndex) << 24)
		^ (static_cast<int64>(GS->DayBlameEvents.Num()) << 8) ^ static_cast<int64>(GS->LastSummary.WalletAfter & 0xFF);
	if (Key != BuiltKey)
	{
		BuiltKey = Key;
		RebuildReceipt(GS);
	}
	const bool bHost = GS->HasAuthority();
	NextButton->SetIsEnabled(bHost);
	RestartButton->SetIsEnabled(bHost);
	HostNote->SetText(FText::FromString(bHost ? TEXT("") : TEXT("Waiting for the host...")));
}

void UHPPayoutWidget::SetMenuInput(bool bMenu)
{
	if (bMenu == bMenuInput)
	{
		return;
	}
	bMenuInput = bMenu;
	if (APlayerController* PC = GetOwningPlayer())
	{
		PC->SetShowMouseCursor(bMenu);
		if (bMenu)
		{
			FInputModeGameAndUI Mode;
			Mode.SetHideCursorDuringCapture(false);
			PC->SetInputMode(Mode);
		}
		else
		{
			PC->SetInputMode(FInputModeGameOnly());
		}
	}
}

void UHPPayoutWidget::OnNextClicked()
{
	if (AHPGameMode* GM = GetWorld() ? GetWorld()->GetAuthGameMode<AHPGameMode>() : nullptr)
	{
		GM->ContinueFromPayout();
	}
}

void UHPPayoutWidget::OnRestartClicked()
{
	if (AHPGameMode* GM = GetWorld() ? GetWorld()->GetAuthGameMode<AHPGameMode>() : nullptr)
	{
		GM->RestartRun();
	}
}
