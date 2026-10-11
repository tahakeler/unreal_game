// HARVEST PANIC - warm farm palette + in-place styling helpers for the C++-built frontend.
// Note: the module links UMG/Slate but not SlateCore, so styles are edited in place on the
// widgets' own style structs instead of constructing new FSlateBrush/FButtonStyle values.
#pragma once

#include "CoreMinimal.h"

class UTextBlock;
class UWidgetTree;
class UButton;
class UBorder;
class UEditableTextBox;
struct FSlateBrush;

namespace HPStyle
{
	// Palette (placeholder until art-director signs off; kept in one place on purpose).
	inline const FLinearColor Cream      = FLinearColor::FromSRGBColor(FColor(255, 243, 214));
	inline const FLinearColor Wheat      = FLinearColor::FromSRGBColor(FColor(244, 196, 98));
	inline const FLinearColor Pumpkin    = FLinearColor::FromSRGBColor(FColor(222, 112, 38));
	inline const FLinearColor Barn       = FLinearColor::FromSRGBColor(FColor(150, 48, 36));
	inline const FLinearColor Soil       = FLinearColor::FromSRGBColor(FColor(58, 38, 26));
	inline const FLinearColor SoilDark   = FLinearColor::FromSRGBColor(FColor(34, 22, 15));
	inline const FLinearColor Leaf       = FLinearColor::FromSRGBColor(FColor(110, 160, 64));
	inline const FLinearColor Muted      = FLinearColor::FromSRGBColor(FColor(190, 170, 140));

	/** Min 24 px @1080p body text (hud-menus rule 3). */
	constexpr int32 BodySize = 26;
	constexpr int32 ButtonSize = 30;
	constexpr int32 HeaderSize = 44;
	constexpr int32 LogoSize = 110;
	constexpr float FadeTime = 0.15f;

	void RoundBrush(FSlateBrush& Brush, const FLinearColor& Fill, float Radius, const FLinearColor& Outline, float OutlineWidth);
	void StyleButton(UButton* Button, bool bFocused);
	void StyleBorder(UBorder* Border, const FLinearColor& Fill, float Radius = 18.f, const FLinearColor& Outline = FLinearColor::Transparent, float OutlineWidth = 0.f);
	void StyleTextBox(UEditableTextBox* Box, int32 Size);
	void SetFont(UTextBlock* Text, int32 Size, bool bBold);
	UTextBlock* MakeText(UWidgetTree* Tree, const FText& Text, int32 Size, const FLinearColor& Color = Cream, bool bBold = true);
}
