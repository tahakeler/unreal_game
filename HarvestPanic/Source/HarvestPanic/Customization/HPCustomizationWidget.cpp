// Copyright HARVEST PANIC team. All Rights Reserved.

#include "HPCustomizationWidget.h"
#include "HPCosmeticCatalog.h"
#include "HPCosmeticsComponent.h"
#include "HPCustomizationPreview.h"
#include "HPLoadoutSaveGame.h"
#include "Blueprint/WidgetTree.h"
#include "Components/Border.h"
#include "Components/Button.h"
#include "Components/ButtonSlot.h"
#include "Components/HorizontalBox.h"
#include "Components/HorizontalBoxSlot.h"
#include "Components/Image.h"
#include "Components/Overlay.h"
#include "Components/OverlaySlot.h"
#include "Components/SizeBox.h"
#include "Components/Spacer.h"
#include "Components/TextBlock.h"
#include "Components/UniformGridPanel.h"
#include "Components/UniformGridSlot.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Components/WidgetSwitcher.h"
#include "Engine/TextureRenderTarget2D.h"
#include "Engine/World.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/PlayerState.h"
#include "InputCoreTypes.h"
#include "Blueprint/WidgetBlueprintLibrary.h"

#define LOCTEXT_NAMESPACE "HPWardrobe"

namespace HPWardrobeStyle
{
	static const FLinearColor Panel(0.09f, 0.055f, 0.035f, 0.96f);
	static const FLinearColor Wood(0.22f, 0.13f, 0.07f, 1.f);
	static const FLinearColor Selected(0.95f, 0.66f, 0.12f, 1.f);
	static const FLinearColor Cream(1.f, 0.94f, 0.80f, 1.f);
	static const FLinearColor Go(0.25f, 0.6f, 0.2f, 1.f);
	static const FLinearColor Fun(0.75f, 0.25f, 0.55f, 1.f);
	static const TCHAR* TabNames[] = { TEXT("Body"), TEXT("Colours"), TEXT("Hat"), TEXT("Face"), TEXT("Back"), TEXT("Name Tag") };
	static constexpr int32 NumTabs = 6;
}

void UHPWardrobeClickProxy::HandleClicked()
{
	if (UHPCustomizationWidget* W = Owner.Get())
	{
		W->HandleAction(Action, Arg, Arg2);
	}
}

// ------------------------------------------------------------------ lifecycle

void UHPCustomizationWidget::NativeOnInitialized()
{
	Super::NativeOnInitialized();
	// Tree must exist before the first TakeWidget(); NativeConstruct is too late for the root.
	BuildUI();
}

void UHPCustomizationWidget::NativeConstruct()
{
	Super::NativeConstruct();
	SetIsFocusable(true);

	// Start from the replicated loadout if we are in a match, else from the local save.
	const APlayerController* PC = GetOwningPlayer();
	if (const UHPCosmeticsComponent* Comp = PC ? UHPCosmeticsComponent::FindForPlayerState(PC->PlayerState) : nullptr)
	{
		Working = Comp->GetLoadout();
	}
	else
	{
		Working = UHPLoadoutSaveGame::LoadOrCreateLocal();
	}

	if (UWorld* World = GetWorld(); World && !Preview)
	{
		FActorSpawnParameters P;
		P.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
		P.ObjectFlags |= RF_Transient;
		Preview = World->SpawnActor<AHPCustomizationPreview>(AHPCustomizationPreview::StaticClass(), FTransform::Identity, P);
		if (Preview && PreviewImage)
		{
			PreviewImage->SetBrushResourceObject(Preview->GetRenderTarget());
			PreviewImage->SetDesiredSizeOverride(FVector2D(Preview->Resolution.X, Preview->Resolution.Y));
		}
	}
	Refresh();
}

void UHPCustomizationWidget::NativeDestruct()
{
	if (Preview)
	{
		Preview->Destroy();
		Preview = nullptr;
	}
	Super::NativeDestruct();
}

// ------------------------------------------------------------------ building

UTextBlock* UHPCustomizationWidget::MakeText(UPanelWidget* Parent, const FText& Text, float Size, FLinearColor Color)
{
	UTextBlock* T = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
	T->SetText(Text);
	T->SetFontSize(Size);
	T->SetColorAndOpacity(FSlateColor(Color));
	T->SetJustification(ETextJustify::Center);
	if (Parent) { Parent->AddChild(T); }
	return T;
}

UButton* UHPCustomizationWidget::MakeButton(UPanelWidget* Parent, const FText& Label, EHPWardrobeAction Action, int32 Arg, int32 Arg2, FLinearColor Color, float FontSize, UTextBlock** OutText)
{
	UButton* B = WidgetTree->ConstructWidget<UButton>(UButton::StaticClass());
	B->SetBackgroundColor(Color);
	UTextBlock* T = MakeText(nullptr, Label, FontSize, HPWardrobeStyle::Cream);
	B->AddChild(T);
	if (UButtonSlot* BS = Cast<UButtonSlot>(T->Slot)) { BS->SetPadding(FMargin(10, 6)); }
	if (OutText) { *OutText = T; }

	UHPWardrobeClickProxy* Proxy = NewObject<UHPWardrobeClickProxy>(this);
	Proxy->Owner = this; Proxy->Action = Action; Proxy->Arg = Arg; Proxy->Arg2 = Arg2;
	B->OnClicked.AddDynamic(Proxy, &UHPWardrobeClickProxy::HandleClicked);
	Proxies.Add(Proxy);
	if (Parent) { Parent->AddChild(B); }
	return B;
}

UUniformGridPanel* UHPCustomizationWidget::BuildSwatchGrid(UPanelWidget* Parent, const TArray<FHPNamedColor>& Colors, EHPWardrobeAction Action, int32 Columns, TArray<TObjectPtr<UButton>>& OutButtons)
{
	UUniformGridPanel* Grid = WidgetTree->ConstructWidget<UUniformGridPanel>(UUniformGridPanel::StaticClass());
	Grid->SetSlotPadding(FMargin(4));
	Grid->SetMinDesiredSlotWidth(52);
	Grid->SetMinDesiredSlotHeight(44);
	for (int32 i = 0; i < Colors.Num(); ++i)
	{
		UButton* B = MakeButton(nullptr, FText::GetEmpty(), Action, i, 0, Colors[i].Color, 20);
		B->SetToolTipText(Colors[i].DisplayName);
		Grid->AddChildToUniformGrid(B, i / Columns, i % Columns);
		OutButtons.Add(B);
	}
	if (Parent) { Parent->AddChild(Grid); }
	return Grid;
}

UWidget* UHPCustomizationWidget::BuildBodyPage()
{
	const UHPCosmeticCatalog* Cat = UHPCosmeticCatalog::Get();
	UVerticalBox* V = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	MakeText(V, LOCTEXT("BodyType", "Body type"), 20, HPWardrobeStyle::Selected);
	UHorizontalBox* Row = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	V->AddChild(Row);
	MakeButton(Row, FText::FromString(TEXT("<")), EHPWardrobeAction::BodyPrev, 0, 0, HPWardrobeStyle::Wood, 26);
	BodyText = MakeText(Row, FText::GetEmpty(), 26);
	if (UHorizontalBoxSlot* S = Cast<UHorizontalBoxSlot>(BodyText->Slot)) { S->SetSize(FSlateChildSize(ESlateSizeRule::Fill)); S->SetVerticalAlignment(VAlign_Center); }
	MakeButton(Row, FText::FromString(TEXT(">")), EHPWardrobeAction::BodyNext, 0, 0, HPWardrobeStyle::Wood, 26);

	UTextBlock* SkinLabel = MakeText(V, LOCTEXT("Skin", "Skin tone"), 20, HPWardrobeStyle::Selected);
	if (UVerticalBoxSlot* S = Cast<UVerticalBoxSlot>(SkinLabel->Slot)) { S->SetPadding(FMargin(0, 18, 0, 4)); }
	BuildSwatchGrid(V, Cat->SkinTones, EHPWardrobeAction::Skin, 7, SkinButtons);
	return V;
}

UWidget* UHPCustomizationWidget::BuildColorsPage()
{
	const UHPCosmeticCatalog* Cat = UHPCosmeticCatalog::Get();
	UVerticalBox* V = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	UHorizontalBox* Targets = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	V->AddChild(Targets);
	const FText Names[] = { LOCTEXT("Overalls", "Overalls"), LOCTEXT("Shirt", "Shirt"), LOCTEXT("Accent", "Boots & Gloves") };
	for (int32 i = 0; i < 3; ++i)
	{
		UButton* B = MakeButton(Targets, Names[i], EHPWardrobeAction::ColorTarget, i, 0, HPWardrobeStyle::Wood, 18);
		if (UHorizontalBoxSlot* S = Cast<UHorizontalBoxSlot>(B->Slot)) { S->SetPadding(FMargin(0, 0, 6, 10)); S->SetSize(FSlateChildSize(ESlateSizeRule::Fill)); }
		ColorTargetButtons.Add(B);
	}
	BuildSwatchGrid(V, Cat->Palette, EHPWardrobeAction::Swatch, 8, SwatchButtons);
	return V;
}

UWidget* UHPCustomizationWidget::BuildSlotPage(EHPCosmeticSlot Slot)
{
	const UHPCosmeticCatalog* Cat = UHPCosmeticCatalog::Get();
	UVerticalBox* V = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	UHorizontalBox* Row = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	V->AddChild(Row);
	MakeButton(Row, FText::FromString(TEXT("<")), EHPWardrobeAction::ItemPrev, (int32)Slot, 0, HPWardrobeStyle::Wood, 26);
	UTextBlock* Name = MakeText(Row, FText::GetEmpty(), 28);
	if (UHorizontalBoxSlot* S = Cast<UHorizontalBoxSlot>(Name->Slot)) { S->SetSize(FSlateChildSize(ESlateSizeRule::Fill)); S->SetVerticalAlignment(VAlign_Center); }
	MakeButton(Row, FText::FromString(TEXT(">")), EHPWardrobeAction::ItemNext, (int32)Slot, 0, HPWardrobeStyle::Wood, 26);
	SlotNameText[(int32)Slot] = Name;

	UUniformGridPanel* Grid = WidgetTree->ConstructWidget<UUniformGridPanel>(UUniformGridPanel::StaticClass());
	Grid->SetSlotPadding(FMargin(4));
	V->AddChild(Grid);
	if (UVerticalBoxSlot* S = Cast<UVerticalBoxSlot>(Grid->Slot)) { S->SetPadding(FMargin(0, 14, 0, 0)); }

	TArray<TPair<uint16, FText>> Entries;
	Entries.Emplace(0, LOCTEXT("None", "None"));
	for (const FHPCosmeticItem* I : Cat->GetItemsForSlot(Slot, true)) { Entries.Emplace(I->NetId, I->DisplayName); }
	for (int32 i = 0; i < Entries.Num(); ++i)
	{
		UButton* B = MakeButton(nullptr, Entries[i].Value, EHPWardrobeAction::Item, (int32)Slot, Entries[i].Key, HPWardrobeStyle::Wood, 17);
		Grid->AddChildToUniformGrid(B, i / 3, i % 3);
		ItemButtons.Add(B);
		ItemButtonIds.Emplace(Slot, Entries[i].Key);
	}
	return V;
}

UWidget* UHPCustomizationWidget::BuildTagPage()
{
	const UHPCosmeticCatalog* Cat = UHPCosmeticCatalog::Get();
	UVerticalBox* V = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	TagPreviewText = MakeText(V, FText::GetEmpty(), 34);
	if (UVerticalBoxSlot* S = Cast<UVerticalBoxSlot>(TagPreviewText->Slot)) { S->SetPadding(FMargin(0, 0, 0, 12)); }
	BuildSwatchGrid(V, Cat->Palette, EHPWardrobeAction::Swatch, 8, TagButtons);
	return V;
}

void UHPCustomizationWidget::BuildUI()
{
	if (bBuilt || !WidgetTree) { return; }
	bBuilt = true;
	SlotNameText.SetNum(3);

	UOverlay* Root = WidgetTree->ConstructWidget<UOverlay>(UOverlay::StaticClass(), TEXT("Root"));
	WidgetTree->RootWidget = Root;

	UBorder* Dim = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass());
	Dim->SetBrushColor(FLinearColor(0, 0, 0, 0.55f));
	Root->AddChild(Dim);
	if (UOverlaySlot* S = Cast<UOverlaySlot>(Dim->Slot)) { S->SetHorizontalAlignment(HAlign_Fill); S->SetVerticalAlignment(VAlign_Fill); }

	UBorder* Panel = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass());
	Panel->SetBrushColor(HPWardrobeStyle::Panel);
	Panel->SetPadding(FMargin(26));
	Root->AddChild(Panel);
	if (UOverlaySlot* S = Cast<UOverlaySlot>(Panel->Slot)) { S->SetHorizontalAlignment(HAlign_Center); S->SetVerticalAlignment(VAlign_Center); }

	UHorizontalBox* Columns = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	Panel->SetContent(Columns);

	// ---- left: live preview ----
	UVerticalBox* Left = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Columns->AddChild(Left);
	UBorder* Frame = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass());
	Frame->SetBrushColor(HPWardrobeStyle::Selected);
	Frame->SetPadding(FMargin(4));
	Left->AddChild(Frame);
	PreviewImage = WidgetTree->ConstructWidget<UImage>(UImage::StaticClass());
	PreviewImage->SetDesiredSizeOverride(FVector2D(560, 720));
	PreviewImage->SetColorAndOpacity(FLinearColor::White);
	Frame->SetContent(PreviewImage);
	UHorizontalBox* Spin = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	Left->AddChild(Spin);
	if (UVerticalBoxSlot* S = Cast<UVerticalBoxSlot>(Spin->Slot)) { S->SetPadding(FMargin(0, 8, 0, 0)); S->SetHorizontalAlignment(HAlign_Center); }
	MakeButton(Spin, LOCTEXT("SpinL", "<  Spin"), EHPWardrobeAction::SpinLeft, 0, 0, HPWardrobeStyle::Wood, 18);
	MakeButton(Spin, LOCTEXT("SpinR", "Spin  >"), EHPWardrobeAction::SpinRight, 0, 0, HPWardrobeStyle::Wood, 18);

	USpacer* Gap = WidgetTree->ConstructWidget<USpacer>(USpacer::StaticClass());
	Gap->SetSize(FVector2D(28, 1));
	Columns->AddChild(Gap);

	// ---- right: tabs + pages ----
	USizeBox* RightSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	RightSize->SetWidthOverride(640);
	Columns->AddChild(RightSize);
	UVerticalBox* Right = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	RightSize->SetContent(Right);

	MakeText(Right, LOCTEXT("Title", "GRANDPA'S WARDROBE"), 38, HPWardrobeStyle::Selected);
	SummaryText = MakeText(Right, FText::GetEmpty(), 15, FLinearColor(0.85f, 0.78f, 0.65f));
	if (UVerticalBoxSlot* S = Cast<UVerticalBoxSlot>(SummaryText->Slot)) { S->SetPadding(FMargin(0, 0, 0, 12)); }

	UHorizontalBox* Tabs = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	Right->AddChild(Tabs);
	for (int32 i = 0; i < HPWardrobeStyle::NumTabs; ++i)
	{
		UButton* B = MakeButton(Tabs, FText::FromString(HPWardrobeStyle::TabNames[i]), EHPWardrobeAction::Tab, i, 0, HPWardrobeStyle::Wood, 17);
		if (UHorizontalBoxSlot* S = Cast<UHorizontalBoxSlot>(B->Slot)) { S->SetPadding(FMargin(0, 0, 4, 0)); S->SetSize(FSlateChildSize(ESlateSizeRule::Fill)); }
		TabButtons.Add(B);
	}

	UBorder* PageFrame = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass());
	PageFrame->SetBrushColor(FLinearColor(0.16f, 0.1f, 0.06f, 1.f));
	PageFrame->SetPadding(FMargin(16));
	Right->AddChild(PageFrame);
	if (UVerticalBoxSlot* S = Cast<UVerticalBoxSlot>(PageFrame->Slot)) { S->SetSize(FSlateChildSize(ESlateSizeRule::Fill)); S->SetPadding(FMargin(0, 6, 0, 12)); }
	Pages = WidgetTree->ConstructWidget<UWidgetSwitcher>(UWidgetSwitcher::StaticClass());
	PageFrame->SetContent(Pages);
	Pages->AddChild(BuildBodyPage());
	Pages->AddChild(BuildColorsPage());
	Pages->AddChild(BuildSlotPage(EHPCosmeticSlot::Hat));
	Pages->AddChild(BuildSlotPage(EHPCosmeticSlot::Face));
	Pages->AddChild(BuildSlotPage(EHPCosmeticSlot::Back));
	Pages->AddChild(BuildTagPage());

	UHorizontalBox* Bottom = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	Right->AddChild(Bottom);
	MakeButton(Bottom, LOCTEXT("Surprise", "Surprise me!"), EHPWardrobeAction::Randomize, 0, 0, HPWardrobeStyle::Fun, 22);
	USpacer* Fill = WidgetTree->ConstructWidget<USpacer>(USpacer::StaticClass());
	Bottom->AddChild(Fill);
	if (UHorizontalBoxSlot* S = Cast<UHorizontalBoxSlot>(Fill->Slot)) { S->SetSize(FSlateChildSize(ESlateSizeRule::Fill)); }
	UButton* CancelB = MakeButton(Bottom, LOCTEXT("Cancel", "Cancel"), EHPWardrobeAction::Cancel, 0, 0, HPWardrobeStyle::Wood, 22);
	if (UHorizontalBoxSlot* S = Cast<UHorizontalBoxSlot>(CancelB->Slot)) { S->SetPadding(FMargin(0, 0, 8, 0)); }
	MakeButton(Bottom, LOCTEXT("Confirm", "Confirm"), EHPWardrobeAction::Confirm, 0, 0, HPWardrobeStyle::Go, 22);
}

// ------------------------------------------------------------------ state

uint8& UHPCustomizationWidget::ColorTargetRef()
{
	return ColorTarget == 0 ? Working.PrimaryColor : ColorTarget == 1 ? Working.SecondaryColor : Working.AccentColor;
}

void UHPCustomizationWidget::SetWorkingLoadout(const FHPCosmeticLoadout& InLoadout)
{
	Working = InLoadout;
	UHPCosmeticCatalog::Get()->SanitizeLoadout(Working);
	Refresh();
}

void UHPCustomizationWidget::SelectTab(int32 Index)
{
	ActiveTab = FMath::Clamp(Index, 0, HPWardrobeStyle::NumTabs - 1);
	Refresh();
}

void UHPCustomizationWidget::CycleItem(EHPCosmeticSlot Slot, int32 Dir)
{
	TArray<uint16> Ids = { 0 };
	for (const FHPCosmeticItem* I : UHPCosmeticCatalog::Get()->GetItemsForSlot(Slot, true)) { Ids.Add(I->NetId); }
	const int32 Cur = FMath::Max(0, Ids.IndexOfByKey(Working.GetSlotItem(Slot)));
	Working.SetSlotItem(Slot, Ids[(Cur + Dir + Ids.Num()) % Ids.Num()]);
}

void UHPCustomizationWidget::HandleAction(EHPWardrobeAction Action, int32 Arg, int32 Arg2)
{
	const UHPCosmeticCatalog* Cat = UHPCosmeticCatalog::Get();
	switch (Action)
	{
	case EHPWardrobeAction::Tab: ActiveTab = Arg; break;
	case EHPWardrobeAction::BodyPrev:
	case EHPWardrobeAction::BodyNext:
		Working.BodyPreset = Working.BodyPreset == EHPBodyPreset::Manny ? EHPBodyPreset::Quinn : EHPBodyPreset::Manny; break;
	case EHPWardrobeAction::Skin: Working.SkinTone = (uint8)Arg; break;
	case EHPWardrobeAction::ColorTarget: ColorTarget = Arg; break;
	case EHPWardrobeAction::Swatch:
		if (ActiveTab == 5) { Working.NameTagColor = (uint8)Arg; } else { ColorTargetRef() = (uint8)Arg; }
		break;
	case EHPWardrobeAction::Item: Working.SetSlotItem((EHPCosmeticSlot)Arg, (uint16)Arg2); break;
	case EHPWardrobeAction::ItemPrev: CycleItem((EHPCosmeticSlot)Arg, -1); break;
	case EHPWardrobeAction::ItemNext: CycleItem((EHPCosmeticSlot)Arg, +1); break;
	case EHPWardrobeAction::Randomize: Randomize(); return;
	case EHPWardrobeAction::Confirm: Confirm(); return;
	case EHPWardrobeAction::Cancel: Cancel(); return;
	case EHPWardrobeAction::SpinLeft: if (Preview) { Preview->AddYaw(-45.f); } return;
	case EHPWardrobeAction::SpinRight: if (Preview) { Preview->AddYaw(45.f); } return;
	}
	Cat->SanitizeLoadout(Working); // a locked skin/swatch click snaps back to a valid value
	Refresh();
}

void UHPCustomizationWidget::Randomize()
{
	FRandomStream Stream(FMath::Rand());
	Working = UHPCosmeticCatalog::Get()->MakeRandomLoadout(Stream);
	Refresh();
}

void UHPCustomizationWidget::Refresh()
{
	if (!bBuilt) { return; }
	using namespace HPWardrobeStyle;
	const UHPCosmeticCatalog* Cat = UHPCosmeticCatalog::Get();

	if (Pages) { Pages->SetActiveWidgetIndex(ActiveTab); }
	for (int32 i = 0; i < TabButtons.Num(); ++i) { TabButtons[i]->SetBackgroundColor(i == ActiveTab ? Selected : Wood); }
	for (int32 i = 0; i < ColorTargetButtons.Num(); ++i) { ColorTargetButtons[i]->SetBackgroundColor(i == ColorTarget ? Selected : Wood); }

	auto MarkSwatches = [Cat](TArray<TObjectPtr<UButton>>& Buttons, const TArray<FHPNamedColor>& Colors, int32 Sel)
	{
		for (int32 i = 0; i < Buttons.Num(); ++i)
		{
			const bool bLocked = !Cat->IsColorUnlocked(Colors[i]);
			Buttons[i]->SetRenderScale(i == Sel ? FVector2D(1.18f) : FVector2D(1.f));
			Buttons[i]->SetRenderOpacity(bLocked ? 0.3f : 1.f);
			if (UTextBlock* T = Cast<UTextBlock>(Buttons[i]->GetChildAt(0)))
			{
				T->SetText(FText::FromString(i == Sel ? TEXT("*") : bLocked ? TEXT("x") : TEXT("")));
			}
		}
	};
	MarkSwatches(SkinButtons, Cat->SkinTones, Working.SkinTone);
	MarkSwatches(SwatchButtons, Cat->Palette, ColorTargetRef());
	MarkSwatches(TagButtons, Cat->Palette, Working.NameTagColor);

	if (BodyText) { BodyText->SetText(Working.BodyPreset == EHPBodyPreset::Manny ? LOCTEXT("Manny", "Manny") : LOCTEXT("Quinn", "Quinn")); }
	for (int32 S = 0; S < 3; ++S)
	{
		const FHPCosmeticItem* I = Cat->FindItem(Working.GetSlotItem((EHPCosmeticSlot)S));
		if (SlotNameText[S]) { SlotNameText[S]->SetText(I ? I->DisplayName : LOCTEXT("None", "None")); }
	}
	for (int32 i = 0; i < ItemButtons.Num(); ++i)
	{
		const bool bSel = Working.GetSlotItem(ItemButtonIds[i].Key) == ItemButtonIds[i].Value;
		ItemButtons[i]->SetBackgroundColor(bSel ? Selected : Wood);
	}
	if (TagPreviewText)
	{
		const APlayerController* PC = GetOwningPlayer();
		const FString Name = PC && PC->PlayerState ? PC->PlayerState->GetPlayerName() : FString(TEXT("Farmer"));
		TagPreviewText->SetText(FText::FromString(Name));
		TagPreviewText->SetColorAndOpacity(FSlateColor(Cat->GetPaletteColor(Working.NameTagColor)));
	}
	if (SummaryText)
	{
		const FHPCosmeticItem* Hat = Cat->FindItem(Working.Hat);
		SummaryText->SetText(FText::Format(LOCTEXT("Summary", "{0} overalls, {1} shirt, {2}"),
			Cat->Palette.IsValidIndex(Working.PrimaryColor) ? Cat->Palette[Working.PrimaryColor].DisplayName : FText::GetEmpty(),
			Cat->Palette.IsValidIndex(Working.SecondaryColor) ? Cat->Palette[Working.SecondaryColor].DisplayName : FText::GetEmpty(),
			Hat ? Hat->DisplayName : LOCTEXT("NoHat", "no hat (brave)")));
	}
	if (Preview) { Preview->SetLoadout(Working); }
}

void UHPCustomizationWidget::Confirm()
{
	UHPCosmeticCatalog::Get()->SanitizeLoadout(Working);
	UHPLoadoutSaveGame::SaveLocal(Working);
	const APlayerController* PC = GetOwningPlayer();
	if (UHPCosmeticsComponent* Comp = PC ? UHPCosmeticsComponent::FindForPlayerState(PC->PlayerState) : nullptr)
	{
		Comp->RequestSetLoadout(Working);
	}
	OnConfirmed.Broadcast(Working);
	if (bCloseOnConfirm) { Close(); }
}

void UHPCustomizationWidget::Cancel()
{
	Close();
}

void UHPCustomizationWidget::Close()
{
	if (bRestoreGameInputOnClose)
	{
		if (APlayerController* PC = GetOwningPlayer())
		{
			PC->SetInputMode(FInputModeGameOnly());
			PC->SetShowMouseCursor(false);
		}
	}
	OnClosed.Broadcast();
	RemoveFromParent();
}

#undef LOCTEXT_NAMESPACE
