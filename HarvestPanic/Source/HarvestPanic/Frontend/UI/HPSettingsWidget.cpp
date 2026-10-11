#include "Frontend/UI/HPSettingsWidget.h"
#include "Frontend/UI/HPFrontendStyle.h"
#include "Frontend/HPFrontendUISubsystem.h"
#include "Frontend/HPGameUserSettings.h"
#include "Blueprint/WidgetTree.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "Components/ScrollBox.h"
#include "Components/SizeBox.h"
#include "Components/TextBlock.h"

#define LOCTEXT_NAMESPACE "HPFrontend"

static FText Pct(float V) { return FText::AsPercent(V); }

void UHPSettingsWidget::BuildTree()
{
	UVerticalBox* Card = MakeCard(LOCTEXT("SettingsTitle", "Settings"), 1000.f);
	USizeBox* ScrollSize = WidgetTree->ConstructWidget<USizeBox>(USizeBox::StaticClass());
	ScrollSize->SetMaxDesiredHeight(640.f);
	UScrollBox* Scroll = WidgetTree->ConstructWidget<UScrollBox>(UScrollBox::StaticClass());
	ScrollSize->AddChild(Scroll);
	UVerticalBox* Box = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
	Scroll->AddChild(Box);
	Card->AddChildToVerticalBox(ScrollSize);

	UHPGameUserSettings* S = UHPGameUserSettings::Get();
	TWeakObjectPtr<UHPSettingsWidget> Self = this;
	auto Dirty = [Self]() { if (Self.IsValid()) Self->bDirty = true; };
	auto Header = [this, Box](const FText& T)
	{
		Box->AddChildToVerticalBox(HPStyle::MakeText(WidgetTree, T, 30, HPStyle::Pumpkin))->SetPadding(FMargin(0, 14, 0, 2));
	};

	Header(LOCTEXT("HdrControls", "Controls"));
	MakeSliderRow(Box, LOCTEXT("Sens", "Mouse sensitivity"), UHPGameUserSettings::MinSensitivity, UHPGameUserSettings::MaxSensitivity, 0.1f, S->GetMouseSensitivity(),
		[](float V) { return FText::AsNumber(FMath::RoundToFloat(V * 10.f) / 10.f); }, [S, Dirty](float V) { S->SetMouseSensitivity(V); Dirty(); });
	MakeToggleRow(Box, LOCTEXT("InvertY", "Invert look Y"), [S]() { return S->GetInvertY(); }, [S, Dirty](bool b) { S->SetInvertY(b); Dirty(); },
		LOCTEXT("On", "On"), LOCTEXT("Off", "Off"));
	MakeSliderRow(Box, LOCTEXT("FOV", "Field of view"), UHPGameUserSettings::MinFOV, UHPGameUserSettings::MaxFOV, 1.f, S->GetFieldOfView(),
		[](float V) { return FText::AsNumber(FMath::RoundToInt(V)); }, [S, Dirty](float V) { S->SetFieldOfView(V); Dirty(); });

	Header(LOCTEXT("HdrAudio", "Audio"));
	MakeSliderRow(Box, LOCTEXT("Master", "Master volume"), 0.f, 1.f, 0.05f, S->GetMasterVolume(), Pct, [S, Dirty](float V) { S->SetMasterVolume(V); Dirty(); });
	MakeSliderRow(Box, LOCTEXT("Music", "Music volume"), 0.f, 1.f, 0.05f, S->GetMusicVolume(), Pct, [S, Dirty](float V) { S->SetMusicVolume(V); Dirty(); });
	MakeSliderRow(Box, LOCTEXT("Sfx", "Effects volume"), 0.f, 1.f, 0.05f, S->GetSfxVolume(), Pct, [S, Dirty](float V) { S->SetSfxVolume(V); Dirty(); });
	MakeSliderRow(Box, LOCTEXT("Voice", "Voice volume"), 0.f, 1.f, 0.05f, S->GetVoiceVolume(), Pct, [S, Dirty](float V) { S->SetVoiceVolume(V); Dirty(); });
	MakeToggleRow(Box, LOCTEXT("MicMode", "Microphone"), [S]() { return S->GetPushToTalk(); }, [S, Dirty](bool b) { S->SetPushToTalk(b); Dirty(); },
		LOCTEXT("PTT", "Push to talk"), LOCTEXT("Open", "Open mic"));

	Header(LOCTEXT("HdrVideo", "Video"));
	MakeToggleRow(Box, LOCTEXT("Window", "Display"), [S]() { return S->GetFullscreen(); }, [S, Dirty](bool b) { S->SetFullscreen(b); Dirty(); },
		LOCTEXT("Fullscreen", "Fullscreen"), LOCTEXT("Windowed", "Windowed"));
	MakeSliderRow(Box, LOCTEXT("ResScale", "Resolution scale"), UHPGameUserSettings::MinResScale, UHPGameUserSettings::MaxResScale, 5.f, S->GetResolutionScalePercent(),
		[](float V) { return FText::AsPercent(V / 100.f); }, [S, Dirty](float V) { S->SetResolutionScalePercent(V); Dirty(); });
	MakeSliderRow(Box, LOCTEXT("UIScale", "Interface size"), UHPGameUserSettings::MinUIScale, UHPGameUserSettings::MaxUIScale, 0.05f, S->GetUIScale(), Pct,
		[S, Dirty](float V) { S->SetUIScale(V); Dirty(); });

	AddButton(Card, LOCTEXT("Defaults", "Reset to defaults"), [Self, S]()
	{
		S->SetToDefaults();
		S->ApplyHPSettings(Self.IsValid() ? Self->GetWorld() : nullptr);
		if (Self.IsValid() && Self->Owner.IsValid())
		{
			// Rebuild so the controls show the defaults.
			TWeakObjectPtr<UHPFrontendUISubsystem> O = Self->Owner;
			O->Back();
			O->Go(EHPMenuState::Settings);
		}
	});
	AddButton(Card, LOCTEXT("SaveBack", "Save and go back"), [Self]() { if (Self.IsValid() && Self->Owner.IsValid()) Self->Owner->Back(); });
}

void UHPSettingsWidget::NativeDestruct()
{
	// Leaving the screen any way (Back button, Esc, B) applies + saves.
	if (bDirty)
	{
		UHPGameUserSettings::Get()->ApplyHPSettings(GetWorld());
		bDirty = false;
	}
	Super::NativeDestruct();
}

#undef LOCTEXT_NAMESPACE
