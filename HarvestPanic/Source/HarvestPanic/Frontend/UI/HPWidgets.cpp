#include "Frontend/UI/HPWidgets.h"
#include "Frontend/UI/HPFrontendStyle.h"
#include "Blueprint/WidgetTree.h"
#include "Components/TextBlock.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Components/HorizontalBox.h"
#include "Components/HorizontalBoxSlot.h"
#include "Components/SizeBox.h"
#include "Framework/Application/SlateApplication.h"
#include "InputCoreTypes.h"
#include "Components/Overlay.h"
#include "Components/OverlaySlot.h"
#include "Components/Border.h"
#include "Components/Image.h"
#include "Frontend/HPGameInstance.h"

// ---------------------------------------------------------------- UHPButton
UHPButton::UHPButton()
{
	HPStyle::StyleButton(this, false);
	OnClicked.AddDynamic(this, &UHPButton::HandleClicked);
	InitIsFocusable(true);
}

void UHPButton::HandleClicked()
{
	if (OnHPClick)
	{
		OnHPClick();
	}
}

void UHPButton::SetLabel(const FText& Text)
{
	if (Label)
	{
		Label->SetText(Text);
	}
}

void UHPButton::RefreshFocusVisual()
{
	const bool bFocused = HasKeyboardFocus() || HasUserFocus(nullptr) || IsHovered();
	const bool bWantFocusLook = HasKeyboardFocus();
	if (bWantFocusLook != bShowingFocus)
	{
		bShowingFocus = bWantFocusLook;
		HPStyle::StyleButton(this, bShowingFocus);
	}
	if (Label)
	{
		Label->SetColorAndOpacity(FSlateColor(bFocused ? HPStyle::SoilDark : HPStyle::Cream));
		Label->SetShadowColorAndOpacity(FLinearColor(0, 0, 0, bFocused ? 0.f : 0.55f));
	}
}

// ---------------------------------------------------------------- UHPSlider
UHPSlider::UHPSlider()
{
	OnValueChanged.AddDynamic(this, &UHPSlider::HandleChanged);
	SetSliderBarColor(HPStyle::Soil);
	SetSliderHandleColor(HPStyle::Wheat);
}

void UHPSlider::HandleChanged(float V)
{
	if (OnHPChanged)
	{
		OnHPChanged(V);
	}
}

// ---------------------------------------------------------------- UHPMenuPanel
TSharedRef<SWidget> UHPMenuPanel::RebuildWidget()
{
	if (!bBuilt && WidgetTree)
	{
		bBuilt = true;
		BuildTree();
	}
	return Super::RebuildWidget();
}

void UHPMenuPanel::NativeConstruct()
{
	Super::NativeConstruct();
	SetIsFocusable(true);
	FadeAlpha = 0.f;
	SetRenderOpacity(0.f);
}

void UHPMenuPanel::NativeTick(const FGeometry& MyGeometry, float InDeltaTime)
{
	Super::NativeTick(MyGeometry, InDeltaTime);
	if (FadeAlpha < 1.f)
	{
		FadeAlpha = FMath::Min(1.f, FadeAlpha + InDeltaTime / HPStyle::FadeTime);
		SetRenderOpacity(FadeAlpha);
	}
	for (UHPButton* B : Buttons)
	{
		if (B)
		{
			B->RefreshFocusVisual();
		}
	}
}

void UHPMenuPanel::FocusFirst()
{
	for (UHPButton* B : Buttons)
	{
		if (B && B->GetIsEnabled() && B->IsVisible())
		{
			B->SetKeyboardFocus();
			return;
		}
	}
	SetKeyboardFocus();
}

UHPButton* UHPMenuPanel::MakeButton(const FText& Text, TFunction<void()> OnClick, int32 FontSize)
{
	UHPButton* B = WidgetTree->ConstructWidget<UHPButton>(UHPButton::StaticClass());
	B->Label = HPStyle::MakeText(WidgetTree, Text, FontSize > 0 ? FontSize : HPStyle::ButtonSize);
	B->Label->SetJustification(ETextJustify::Center);
	B->AddChild(B->Label);
	B->OnHPClick = MoveTemp(OnClick);
	Buttons.Add(B);
	return B;
}

UHPSlider* UHPMenuPanel::MakeSliderRow(UVerticalBox* Parent, const FText& Label, float Min, float Max, float Step, float Value, TFunction<FText(float)> Format, TFunction<void(float)> OnChange)
{
	UHorizontalBox* Row = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	USizeBox* LabelBox = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	LabelBox->SetWidthOverride(360.f);
	LabelBox->AddChild(HPStyle::MakeText(WidgetTree, Label, HPStyle::BodySize));
	Row->AddChildToHorizontalBox(LabelBox)->SetVerticalAlignment(VAlign_Center);

	UHPSlider* S = WidgetTree->ConstructWidget<UHPSlider>(UHPSlider::StaticClass());
	S->SetMinValue(Min);
	S->SetMaxValue(Max);
	S->SetStepSize(Step);
	S->SetValue(Value);
	UHorizontalBoxSlot* SS = Row->AddChildToHorizontalBox(S);
	SS->SetSize(FSlateChildSize(ESlateSizeRule::Fill));
	SS->SetVerticalAlignment(VAlign_Center);
	SS->SetPadding(FMargin(8.f, 0.f));

	USizeBox* ValBox = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	ValBox->SetWidthOverride(110.f);
	UTextBlock* ValText = HPStyle::MakeText(WidgetTree, Format(Value), HPStyle::BodySize, HPStyle::Wheat);
	ValText->SetJustification(ETextJustify::Right);
	ValBox->AddChild(ValText);
	Row->AddChildToHorizontalBox(ValBox)->SetVerticalAlignment(VAlign_Center);

	S->OnHPChanged = [ValText, Format, OnChange](float V)
	{
		OnChange(V);
		ValText->SetText(Format(V));
	};
	Parent->AddChildToVerticalBox(Row)->SetPadding(FMargin(0.f, 6.f));
	return S;
}

UHPButton* UHPMenuPanel::MakeToggleRow(UVerticalBox* Parent, const FText& Label, TFunction<bool()> Get, TFunction<void(bool)> Set, const FText& OnText, const FText& OffText)
{
	UHorizontalBox* Row = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
	USizeBox* LabelBox = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	LabelBox->SetWidthOverride(360.f);
	LabelBox->AddChild(HPStyle::MakeText(WidgetTree, Label, HPStyle::BodySize));
	Row->AddChildToHorizontalBox(LabelBox)->SetVerticalAlignment(VAlign_Center);

	UHPButton* B = MakeButton(Get() ? OnText : OffText, nullptr, HPStyle::BodySize);
	TWeakObjectPtr<UHPButton> WeakB = B;
	B->OnHPClick = [WeakB, Get, Set, OnText, OffText]()
	{
		Set(!Get());
		if (WeakB.IsValid())
		{
			WeakB->SetLabel(Get() ? OnText : OffText);
		}
	};
	USizeBox* BtnBox = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	BtnBox->SetWidthOverride(260.f);
	BtnBox->AddChild(B);
	Row->AddChildToHorizontalBox(BtnBox)->SetVerticalAlignment(VAlign_Center);
	Parent->AddChildToVerticalBox(Row)->SetPadding(FMargin(0.f, 6.f));
	return B;
}

UHPGameInstance* UHPMenuPanel::GetHPGameInstance() const
{
	return Cast<UHPGameInstance>(GetGameInstance());
}

UVerticalBox* UHPMenuPanel::MakeCard(const FText& Title, float Width, bool bDimBackground, const FText& Subtitle)
{
	UOverlay* Root = WidgetTree->ConstructWidget<UOverlay>(UOverlay::StaticClass());
	WidgetTree->RootWidget = Root;
	if (bDimBackground)
	{
		UImage* Dim = WidgetTree->ConstructWidget<UImage>(UImage::StaticClass());
		Dim->SetColorAndOpacity(FLinearColor(0.02f, 0.01f, 0.f, 0.55f));
		UOverlaySlot* DS = Root->AddChildToOverlay(Dim);
		DS->SetHorizontalAlignment(HAlign_Fill);
		DS->SetVerticalAlignment(VAlign_Fill);
	}
	USizeBox* Size = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	Size->SetWidthOverride(Width);
	UBorder* Card = WidgetTree->ConstructWidget<UBorder>(UBorder::StaticClass());
	HPStyle::StyleBorder(Card, HPStyle::Soil.CopyWithNewOpacity(0.94f), 22.f, HPStyle::Wheat, 4.f);
	Card->SetPadding(FMargin(40.f, 30.f));
	Size->AddChild(Card);
	UOverlaySlot* CS = Root->AddChildToOverlay(Size);
	CS->SetHorizontalAlignment(HAlign_Center);
	CS->SetVerticalAlignment(VAlign_Center);

	UVerticalBox* Box = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Card->SetContent(Box);
	if (!Title.IsEmpty())
	{
		UTextBlock* T = HPStyle::MakeText(WidgetTree, Title, HPStyle::HeaderSize, HPStyle::Wheat);
		T->SetJustification(ETextJustify::Center);
		Box->AddChildToVerticalBox(T)->SetPadding(FMargin(0.f, 0.f, 0.f, Subtitle.IsEmpty() ? 18.f : 4.f));
	}
	if (!Subtitle.IsEmpty())
	{
		UTextBlock* T = HPStyle::MakeText(WidgetTree, Subtitle, HPStyle::BodySize, HPStyle::Muted, false);
		T->SetJustification(ETextJustify::Center);
		T->SetAutoWrapText(true);
		Box->AddChildToVerticalBox(T)->SetPadding(FMargin(0.f, 0.f, 0.f, 18.f));
	}
	return Box;
}

UHPButton* UHPMenuPanel::AddButton(UVerticalBox* Box, const FText& Text, TFunction<void()> OnClick)
{
	UHPButton* B = MakeButton(Text, MoveTemp(OnClick));
	UVerticalBoxSlot* S = Box->AddChildToVerticalBox(B);
	S->SetPadding(FMargin(0.f, 7.f));
	S->SetHorizontalAlignment(HAlign_Fill);
	return B;
}
