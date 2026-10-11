#include "Frontend/UI/HPFrontendStyle.h"
#include "Styling/SlateBrush.h"
#include "Styling/SlateTypes.h"
#include "Components/TextBlock.h"
#include "Components/Button.h"
#include "Components/Border.h"
#include "Components/EditableTextBox.h"
#include "Blueprint/WidgetTree.h"
#include "UObject/UnrealType.h"

void HPStyle::RoundBrush(FSlateBrush& Brush, const FLinearColor& Fill, float Radius, const FLinearColor& Outline, float OutlineWidth)
{
	Brush.DrawAs = ESlateBrushDrawType::RoundedBox;
	Brush.TintColor = FSlateColor(Fill);
	Brush.OutlineSettings = FSlateBrushOutlineSettings(Radius, FSlateColor(Outline), OutlineWidth);
	Brush.OutlineSettings.RoundingType = ESlateBrushRoundingType::FixedRadius;
}

void HPStyle::StyleButton(UButton* Button, bool bFocused)
{
	FButtonStyle& S = const_cast<FButtonStyle&>(Button->GetStyle());
	if (bFocused)
	{
		RoundBrush(S.Normal, Pumpkin, 14.f, Cream, 4.f);
	}
	else
	{
		RoundBrush(S.Normal, Barn, 14.f, SoilDark, 3.f);
	}
	RoundBrush(S.Hovered, Pumpkin, 14.f, Wheat, 4.f);
	RoundBrush(S.Pressed, Wheat, 14.f, Cream, 4.f);
	RoundBrush(S.Disabled, FLinearColor(0.15f, 0.12f, 0.1f, 0.8f), 14.f, FLinearColor::Transparent, 0.f);
	S.NormalPadding = FMargin(28.f, 12.f);
	S.PressedPadding = FMargin(28.f, 14.f, 28.f, 10.f);
	Button->SetStyle(S); // pushes to Slate if already built
}

void HPStyle::StyleBorder(UBorder* Border, const FLinearColor& Fill, float Radius, const FLinearColor& Outline, float OutlineWidth)
{
	if (FStructProperty* P = FindFProperty<FStructProperty>(UBorder::StaticClass(), TEXT("Background")))
	{
		RoundBrush(*P->ContainerPtrToValuePtr<FSlateBrush>(Border), FLinearColor::White, Radius, Outline, OutlineWidth);
	}
	Border->SetBrushColor(Fill);
}

void HPStyle::StyleTextBox(UEditableTextBox* Box, int32 Size)
{
	if (FStructProperty* P = FindFProperty<FStructProperty>(UEditableTextBox::StaticClass(), TEXT("WidgetStyle")))
	{
		FEditableTextBoxStyle* S = P->ContainerPtrToValuePtr<FEditableTextBoxStyle>(Box);
		S->TextStyle.Font.Size = Size;
	}
}

void HPStyle::SetFont(UTextBlock* Text, int32 Size, bool bBold)
{
	FSlateFontInfo& F = const_cast<FSlateFontInfo&>(Text->GetFont());
	F.Size = Size;
	F.TypefaceFontName = bBold ? FName(TEXT("Bold")) : FName(TEXT("Regular"));
	Text->SetFontSize(Size);
}

UTextBlock* HPStyle::MakeText(UWidgetTree* Tree, const FText& Text, int32 Size, const FLinearColor& Color, bool bBold)
{
	UTextBlock* T = Tree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
	T->SetText(Text);
	SetFont(T, Size, bBold);
	T->SetColorAndOpacity(FSlateColor(Color));
	T->SetShadowOffset(FVector2D(2.f, 2.f));
	T->SetShadowColorAndOpacity(FLinearColor(0, 0, 0, 0.55f));
	return T;
}
