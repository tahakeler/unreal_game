// Copyright HARVEST PANIC team. All Rights Reserved.
#include "UI/HPDayHUDWidget.h"
#include "UI/HPUIStyle.h"
#include "HPGameState.h"
#include "Economy/HPEconomyMath.h"
#include "Blueprint/WidgetTree.h"
#include "Components/CanvasPanel.h"
#include "Components/CanvasPanelSlot.h"
#include "Components/Border.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Components/ProgressBar.h"
#include "Components/TextBlock.h"
#include "Components/SizeBox.h"

TSharedRef<SWidget> UHPDayHUDWidget::RebuildWidget()
{
	if (WidgetTree && !WidgetTree->RootWidget)
	{
		BuildTree();
	}
	return Super::RebuildWidget();
}

static UBorder* MakePanel(UWidgetTree* Tree, UWidget* Content)
{
	UBorder* B = Tree->ConstructWidget<UBorder>(UBorder::StaticClass());
	B->SetBrushColor(HPUI::Panel);
	B->SetPadding(FMargin(18.f, 12.f));
	B->SetContent(Content);
	return B;
}

static UCanvasPanelSlot* Place(UCanvasPanel* Canvas, UWidget* W, FVector2D Anchor, FVector2D Offset)
{
	UCanvasPanelSlot* S = Canvas->AddChildToCanvas(W);
	S->SetAnchors(FAnchors(Anchor.X, Anchor.Y));
	S->SetAlignment(Anchor);
	S->SetPosition(Offset);
	S->SetAutoSize(true);
	return S;
}

void UHPDayHUDWidget::BuildTree()
{
	UCanvasPanel* Canvas = WidgetTree->ConstructWidget<UCanvasPanel>(UCanvasPanel::StaticClass(), TEXT("Root"));
	WidgetTree->RootWidget = Canvas;

	// ---- Top-center: wallet + quota ----
	{
		UVerticalBox* Box = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
		WalletText = HPUI::MakeText(WidgetTree, TEXT("$0"), 34, HPUI::Gold, true);
		Box->AddChildToVerticalBox(WalletText)->SetHorizontalAlignment(HAlign_Center);
		QuotaText = HPUI::MakeText(WidgetTree, TEXT(""), 14, HPUI::White);
		Box->AddChildToVerticalBox(QuotaText)->SetHorizontalAlignment(HAlign_Center);

		USizeBox* BarSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
		BarSize->SetWidthOverride(320.f);
		BarSize->SetHeightOverride(10.f);
		QuotaBar = WidgetTree->ConstructWidget<UProgressBar>(UProgressBar::StaticClass());
		QuotaBar->SetFillColorAndOpacity(HPUI::Good);
		BarSize->SetContent(QuotaBar);
		UVerticalBoxSlot* BarSlot = Box->AddChildToVerticalBox(BarSize);
		BarSlot->SetHorizontalAlignment(HAlign_Center);
		BarSlot->SetPadding(FMargin(0.f, 6.f, 0.f, 0.f));
		Place(Canvas, MakePanel(WidgetTree, Box), FVector2D(0.5f, 0.f), FVector2D(0.f, 16.f));
	}

	// ---- Top-right: day / phase / clock ----
	{
		UVerticalBox* Box = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
		DayText = HPUI::MakeText(WidgetTree, TEXT(""), 14, HPUI::Muted, true);
		Box->AddChildToVerticalBox(DayText)->SetHorizontalAlignment(HAlign_Right);
		ClockText = HPUI::MakeText(WidgetTree, TEXT("0:00"), 32, HPUI::White, true);
		Box->AddChildToVerticalBox(ClockText)->SetHorizontalAlignment(HAlign_Right);
		PhaseText = HPUI::MakeText(WidgetTree, TEXT(""), 14, HPUI::Gold, true);
		Box->AddChildToVerticalBox(PhaseText)->SetHorizontalAlignment(HAlign_Right);
		Place(Canvas, MakePanel(WidgetTree, Box), FVector2D(1.f, 0.f), FVector2D(-16.f, 16.f));
	}

	// ---- Left: Morning Order card ----
	{
		UVerticalBox* Box = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
		Box->AddChildToVerticalBox(HPUI::MakeText(WidgetTree, TEXT("MORNING ORDER"), 14, HPUI::Gold, true))
			->SetPadding(FMargin(0.f, 0.f, 0.f, 6.f));
		for (int32 i = 0; i < 4; ++i)
		{
			UTextBlock* T = HPUI::MakeText(WidgetTree, TEXT(""), 16, HPUI::White);
			OrderLineTexts.Add(T);
			Box->AddChildToVerticalBox(T)->SetPadding(FMargin(0.f, 2.f));
		}
		OrderBonusText = HPUI::MakeText(WidgetTree, TEXT(""), 12, HPUI::Muted);
		Box->AddChildToVerticalBox(OrderBonusText)->SetPadding(FMargin(0.f, 6.f, 0.f, 0.f));
		Place(Canvas, MakePanel(WidgetTree, Box), FVector2D(0.f, 0.f), FVector2D(16.f, 16.f));
	}
}

void UHPDayHUDWidget::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	const AHPGameState* GS = GetWorld() ? GetWorld()->GetGameState<AHPGameState>() : nullptr;
	if (!GS || !WalletText)
	{
		return;
	}
	const bool bVisible = GS->Phase == EHPDayPhase::Morning || GS->Phase == EHPDayPhase::Workday || GS->Phase == EHPDayPhase::Dusk;
	SetVisibility(bVisible ? ESlateVisibility::HitTestInvisible : ESlateVisibility::Collapsed);
	if (!bVisible)
	{
		return;
	}

	// Wallet + quota
	const int32 Due = GS->GetQuotaDue();
	WalletText->SetText(FText::FromString(HPUI::Money(GS->WalletBalance)));
	WalletText->SetColorAndOpacity(FSlateColor(GS->WalletBalance < 0 ? HPUI::Bad : HPUI::Gold));
	const int32 Share = FMath::RoundToInt(GS->WeeklyQuota / static_cast<float>(UHPEconomyMath::DaysPerWeek));
	FString Quota = FString::Printf(TEXT("Sunday rent %s  ·  today's share %s"), *HPUI::Money(Due), *HPUI::Money(Share));
	if (GS->WeekIOU > 0)
	{
		Quota += FString::Printf(TEXT("  (IOU %s)"), *HPUI::Money(GS->WeekIOU));
	}
	QuotaText->SetText(FText::FromString(Quota));
	QuotaBar->SetPercent(Due > 0 ? FMath::Clamp(GS->WalletBalance / static_cast<float>(Due), 0.f, 1.f) : 1.f);

	// Clock
	const int32 Secs = FMath::CeilToInt(GS->GetPhaseSecondsRemaining());
	DayText->SetText(FText::FromString(FString::Printf(TEXT("WEEK %d · %s"), GS->WeekIndex, HPUI::DayName(GS->DayOfWeek))));
	ClockText->SetText(FText::FromString(FString::Printf(TEXT("%d:%02d"), Secs / 60, Secs % 60)));
	const bool bSundown = GS->Phase == EHPDayPhase::Workday && Secs <= 30;
	ClockText->SetColorAndOpacity(FSlateColor(bSundown ? HPUI::Bad : HPUI::White));
	const TCHAR* PhaseLabel = TEXT("");
	switch (GS->Phase)
	{
	case EHPDayPhase::Morning: PhaseLabel = TEXT("MORNING · read the order"); break;
	case EHPDayPhase::Workday: PhaseLabel = bSundown ? TEXT("SUNDOWN · market closing!") : TEXT("WORKDAY · market open"); break;
	case EHPDayPhase::Dusk:    PhaseLabel = TEXT("DUSK · night market x1.25"); break;
	default: break;
	}
	PhaseText->SetText(FText::FromString(PhaseLabel));

	// Order card
	bool bAllDone = GS->OrderLines.Num() > 0;
	for (int32 i = 0; i < OrderLineTexts.Num(); ++i)
	{
		UTextBlock* T = OrderLineTexts[i];
		if (!GS->OrderLines.IsValidIndex(i))
		{
			T->SetVisibility(ESlateVisibility::Collapsed);
			continue;
		}
		const FHPOrderLine& L = GS->OrderLines[i];
		bAllDone &= L.IsComplete();
		T->SetVisibility(ESlateVisibility::HitTestInvisible);
		T->SetText(FText::FromString(FString::Printf(TEXT("%s  %s   %d / %d"),
			L.IsComplete() ? TEXT("[x]") : TEXT("[ ]"),
			*UHPEconomyMath::GoodsDisplayName(L.GoodsId), FMath::Min(L.Filled, L.Quantity), L.Quantity)));
		T->SetColorAndOpacity(FSlateColor(L.IsComplete() ? HPUI::Good : HPUI::White));
	}
	OrderBonusText->SetText(FText::FromString(FString::Printf(TEXT("Order items sell x1.5  ·  all 3 lines: +%s"),
		*HPUI::Money(UHPEconomyMath::OrderCompleteBonus(GS->WeekIndex)))));
	OrderBonusText->SetColorAndOpacity(FSlateColor(bAllDone ? HPUI::Good : HPUI::Muted));
}
