#include "Frontend/HPGameUserSettings.h"
#include "Engine/Engine.h"
#include "Engine/World.h"
#include "Engine/UserInterfaceSettings.h"
#include "AudioDevice.h"
#include "Misc/App.h"
#include "UObject/StrongObjectPtr.h"
#include "GameFramework/PlayerController.h"
#include "Camera/PlayerCameraManager.h"

FHPOnSettingsApplied UHPGameUserSettings::OnHPSettingsApplied;

UHPGameUserSettings::UHPGameUserSettings()
{
	SetHPDefaults();
}

void UHPGameUserSettings::SetHPDefaults()
{
	MouseSensitivity = 1.f;
	bInvertY = false;
	FieldOfView = 90.f;
	MasterVolume = 1.f;
	MusicVolume = 0.7f;
	SfxVolume = 1.f;
	VoiceVolume = 1.f;
	bPushToTalk = false;
	bHPFullscreen = true;
	ResolutionScalePercent = 100.f;
	UIScale = 1.f;
}

UHPGameUserSettings* UHPGameUserSettings::Get()
{
	if (GEngine)
	{
		if (UHPGameUserSettings* S = Cast<UHPGameUserSettings>(GEngine->GetGameUserSettings()))
		{
			return S;
		}
	}
	// Integration fallback until DefaultEngine.ini sets GameUserSettingsClassName.
	static TStrongObjectPtr<UHPGameUserSettings> Fallback;
	if (!Fallback.IsValid())
	{
		Fallback.Reset(NewObject<UHPGameUserSettings>(GetTransientPackage()));
		Fallback->LoadConfig();
		Fallback->SanitizeHP();
	}
	return Fallback.Get();
}

void UHPGameUserSettings::SanitizeHP()
{
	SetMouseSensitivity(MouseSensitivity);
	SetFieldOfView(FieldOfView);
	SetMasterVolume(MasterVolume);
	SetMusicVolume(MusicVolume);
	SetSfxVolume(SfxVolume);
	SetVoiceVolume(VoiceVolume);
	SetResolutionScalePercent(ResolutionScalePercent);
	SetUIScale(UIScale);
}

void UHPGameUserSettings::SetToDefaults()
{
	Super::SetToDefaults();
	SetHPDefaults();
}

void UHPGameUserSettings::LoadSettings(bool bForceReload)
{
	Super::LoadSettings(bForceReload);
	SanitizeHP();
}

void UHPGameUserSettings::ApplyHPSettings(UWorld* World, bool bSave)
{
	SanitizeHP();

	if (GetMutableDefault<UUserInterfaceSettings>())
	{
		GetMutableDefault<UUserInterfaceSettings>()->ApplicationScale = UIScale;
	}

	if (GEngine && !IsRunningDedicatedServer() && FApp::CanEverRender())
	{
		if (FAudioDeviceHandle Audio = GEngine->GetMainAudioDevice())
		{
			Audio->SetTransientPrimaryVolume(MasterVolume);
		}
		SetResolutionScaleValueEx(ResolutionScalePercent);
		SetFullscreenMode(bHPFullscreen ? EWindowMode::WindowedFullscreen : EWindowMode::Windowed);
		ApplySettings(false);
	}

	if (World)
	{
		for (FConstPlayerControllerIterator It = World->GetPlayerControllerIterator(); It; ++It)
		{
			APlayerController* PC = It->Get();
			if (PC && PC->IsLocalController() && PC->PlayerCameraManager)
			{
				PC->PlayerCameraManager->SetFOV(FieldOfView);
			}
		}
	}

	if (bSave)
	{
		SaveSettings();
	}
	OnHPSettingsApplied.Broadcast(*this);
}
