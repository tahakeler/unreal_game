// Copyright HARVEST PANIC team. All Rights Reserved.
// Tiny shared style helpers for code-built UMG (no .uasset authoring).
#pragma once

#include "CoreMinimal.h"
#include "Styling/CoreStyle.h"
#include "Components/TextBlock.h"
#include "Blueprint/WidgetTree.h"

namespace HPUI
{
	inline const FLinearColor Ink(0.10f, 0.08f, 0.06f, 1.f);
	inline const FLinearColor Paper(0.96f, 0.93f, 0.84f, 1.f);
	inline const FLinearColor Panel(0.04f, 0.05f, 0.06f, 0.78f);
	inline const FLinearColor White(0.97f, 0.97f, 0.95f, 1.f);
	inline const FLinearColor Muted(0.70f, 0.72f, 0.70f, 1.f);
	inline const FLinearColor Gold(1.00f, 0.80f, 0.25f, 1.f);
	inline const FLinearColor Good(0.30f, 0.85f, 0.40f, 1.f);
	inline const FLinearColor GoodInk(0.10f, 0.50f, 0.18f, 1.f);
	inline const FLinearColor Bad(0.95f, 0.35f, 0.30f, 1.f);
	inline const FLinearColor BadInk(0.70f, 0.12f, 0.10f, 1.f);

	inline UTextBlock* MakeText(UWidgetTree* Tree, const FString& Str, int32 Size, const FLinearColor& Color, bool bBold = false)
	{
		UTextBlock* T = Tree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
		T->SetText(FText::FromString(Str));
		T->SetFont(FCoreStyle::GetDefaultFontStyle(bBold ? "Bold" : "Regular", Size));
		T->SetColorAndOpacity(FSlateColor(Color));
		return T;
	}

	inline FString Money(int32 V)
	{
		const FString Num = FText::AsNumber(FMath::Abs(V)).ToString();
		return V < 0 ? FString::Printf(TEXT("-$%s"), *Num) : FString::Printf(TEXT("$%s"), *Num);
	}

	inline FString SignedMoney(int32 V)
	{
		return V > 0 ? TEXT("+") + Money(V) : Money(V);
	}

	inline const TCHAR* DayName(int32 DayOfWeek)
	{
		static const TCHAR* Names[] = { TEXT("MONDAY"), TEXT("TUESDAY"), TEXT("WEDNESDAY"), TEXT("THURSDAY"), TEXT("FRIDAY") };
		return Names[FMath::Clamp(DayOfWeek, 0, 4)];
	}
}
