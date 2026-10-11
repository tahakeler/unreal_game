// HARVEST PANIC - persisted player settings (GameUserSettings.ini).
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameUserSettings.h"
#include "HPGameUserSettings.generated.h"

DECLARE_MULTICAST_DELEGATE_OneParam(FHPOnSettingsApplied, const class UHPGameUserSettings& /*Settings*/);

/**
 * Settings from onboarding-accessibility / hud-menus. Every setter clamps.
 * Systems that own the effect (camera, audio, voice) listen to OnHPSettingsApplied;
 * the frontend applies what it can directly (FOV, master volume, UI scale, window, resolution).
 */
UCLASS(config=GameUserSettings, configdonotcheckdefaults)
class HARVESTPANIC_API UHPGameUserSettings : public UGameUserSettings
{
	GENERATED_BODY()

public:
	UHPGameUserSettings();

	static constexpr float MinSensitivity = 0.1f, MaxSensitivity = 5.f;
	static constexpr float MinFOV = 80.f, MaxFOV = 110.f;
	static constexpr float MinResScale = 50.f, MaxResScale = 100.f;
	static constexpr float MinUIScale = 0.8f, MaxUIScale = 1.5f;

	/** Engine-configured instance (falls back to a transient object if the ini doesn't point here yet). */
	static UHPGameUserSettings* Get();

	void SetMouseSensitivity(float V) { MouseSensitivity = FMath::Clamp(V, MinSensitivity, MaxSensitivity); }
	void SetInvertY(bool b) { bInvertY = b; }
	void SetFieldOfView(float V) { FieldOfView = FMath::Clamp(V, MinFOV, MaxFOV); }
	void SetMasterVolume(float V) { MasterVolume = FMath::Clamp(V, 0.f, 1.f); }
	void SetMusicVolume(float V) { MusicVolume = FMath::Clamp(V, 0.f, 1.f); }
	void SetSfxVolume(float V) { SfxVolume = FMath::Clamp(V, 0.f, 1.f); }
	void SetVoiceVolume(float V) { VoiceVolume = FMath::Clamp(V, 0.f, 1.f); }
	void SetPushToTalk(bool b) { bPushToTalk = b; }
	void SetFullscreen(bool b) { bHPFullscreen = b; }
	void SetResolutionScalePercent(float V) { ResolutionScalePercent = FMath::Clamp(V, MinResScale, MaxResScale); }
	void SetUIScale(float V) { UIScale = FMath::Clamp(V, MinUIScale, MaxUIScale); }

	float GetMouseSensitivity() const { return MouseSensitivity; }
	bool GetInvertY() const { return bInvertY; }
	float GetFieldOfView() const { return FieldOfView; }
	float GetMasterVolume() const { return MasterVolume; }
	float GetMusicVolume() const { return MusicVolume; }
	float GetSfxVolume() const { return SfxVolume; }
	float GetVoiceVolume() const { return VoiceVolume; }
	bool GetPushToTalk() const { return bPushToTalk; }
	bool GetFullscreen() const { return bHPFullscreen; }
	float GetResolutionScalePercent() const { return ResolutionScalePercent; }
	float GetUIScale() const { return UIScale; }

	/** Re-clamps everything (used after loading a hand-edited ini). */
	void SanitizeHP();

	/** Applies engine-side effects, saves, and broadcasts OnHPSettingsApplied. */
	void ApplyHPSettings(UWorld* World, bool bSave = true);

	virtual void SetToDefaults() override;
	virtual void LoadSettings(bool bForceReload = false) override;

	static FHPOnSettingsApplied OnHPSettingsApplied;

protected:
	UPROPERTY(config) float MouseSensitivity;
	UPROPERTY(config) bool bInvertY;
	UPROPERTY(config) float FieldOfView;
	UPROPERTY(config) float MasterVolume;
	UPROPERTY(config) float MusicVolume;
	UPROPERTY(config) float SfxVolume;
	UPROPERTY(config) float VoiceVolume;
	UPROPERTY(config) bool bPushToTalk;
	UPROPERTY(config) bool bHPFullscreen;
	UPROPERTY(config) float ResolutionScalePercent;
	UPROPERTY(config) float UIScale;

	void SetHPDefaults();
};
