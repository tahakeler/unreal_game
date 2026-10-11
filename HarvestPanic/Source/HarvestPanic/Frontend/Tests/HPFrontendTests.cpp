// HARVEST PANIC - frontend automation tests (settings clamp/persist, menu state machine).
#include "Misc/AutomationTest.h"
#include "Frontend/HPGameUserSettings.h"
#include "Frontend/HPMenuStateMachine.h"
#include "Frontend/HPGameInstance.h"
#include "Misc/Paths.h"
#include "HAL/FileManager.h"
#include "Misc/ConfigCacheIni.h"

#if WITH_DEV_AUTOMATION_TESTS

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPSettingsClampTest, "HarvestPanic.Frontend.Settings.Clamp",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ClientContext | EAutomationTestFlags::EngineFilter)
bool FHPSettingsClampTest::RunTest(const FString& Parameters)
{
	UHPGameUserSettings* S = NewObject<UHPGameUserSettings>();
	S->SetFieldOfView(10.f);   TestEqual(TEXT("FOV low clamps to 80"), S->GetFieldOfView(), 80.f);
	S->SetFieldOfView(500.f);  TestEqual(TEXT("FOV high clamps to 110"), S->GetFieldOfView(), 110.f);
	S->SetFieldOfView(95.f);   TestEqual(TEXT("FOV in range kept"), S->GetFieldOfView(), 95.f);
	S->SetMasterVolume(-1.f);  TestEqual(TEXT("Volume low"), S->GetMasterVolume(), 0.f);
	S->SetMusicVolume(2.f);    TestEqual(TEXT("Volume high"), S->GetMusicVolume(), 1.f);
	S->SetSfxVolume(0.5f);     TestEqual(TEXT("Sfx kept"), S->GetSfxVolume(), 0.5f);
	S->SetVoiceVolume(9.f);    TestEqual(TEXT("Voice high"), S->GetVoiceVolume(), 1.f);
	S->SetMouseSensitivity(0.f); TestEqual(TEXT("Sens low"), S->GetMouseSensitivity(), UHPGameUserSettings::MinSensitivity);
	S->SetMouseSensitivity(99.f); TestEqual(TEXT("Sens high"), S->GetMouseSensitivity(), UHPGameUserSettings::MaxSensitivity);
	S->SetResolutionScalePercent(5.f); TestEqual(TEXT("Res low"), S->GetResolutionScalePercent(), 50.f);
	S->SetResolutionScalePercent(250.f); TestEqual(TEXT("Res high"), S->GetResolutionScalePercent(), 100.f);
	S->SetUIScale(0.1f); TestEqual(TEXT("UI low"), S->GetUIScale(), 0.8f);
	S->SetUIScale(3.f);  TestEqual(TEXT("UI high"), S->GetUIScale(), 1.5f);
	S->SetToDefaults();
	TestEqual(TEXT("Default FOV 90"), S->GetFieldOfView(), 90.f);
	TestFalse(TEXT("Default not inverted"), S->GetInvertY());
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPSettingsPersistTest, "HarvestPanic.Frontend.Settings.Persist",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ClientContext | EAutomationTestFlags::EngineFilter)
bool FHPSettingsPersistTest::RunTest(const FString& Parameters)
{
	const FString File = FConfigCacheIni::NormalizeConfigIniPath(FPaths::ProjectSavedDir() / TEXT("Automation/HPSettingsPersistTest.ini"));
	IFileManager::Get().Delete(*File);

	UHPGameUserSettings* A = NewObject<UHPGameUserSettings>();
	A->SetFieldOfView(104.f);
	A->SetInvertY(true);
	A->SetMusicVolume(0.25f);
	A->SetPushToTalk(true);
	A->SetUIScale(1.3f);
	A->SaveConfig(CPF_Config, *File);
	GConfig->Flush(false, File);

	UHPGameUserSettings* B = NewObject<UHPGameUserSettings>();
	B->LoadConfig(nullptr, *File);
	TestEqual(TEXT("FOV persisted"), B->GetFieldOfView(), 104.f);
	TestTrue(TEXT("InvertY persisted"), B->GetInvertY());
	TestEqual(TEXT("Music persisted"), B->GetMusicVolume(), 0.25f);
	TestTrue(TEXT("PTT persisted"), B->GetPushToTalk());
	TestEqual(TEXT("UI scale persisted"), B->GetUIScale(), 1.3f);

	// A hand-edited out-of-range ini is clamped on load.
	GConfig->SetFloat(*UHPGameUserSettings::StaticClass()->GetPathName(), TEXT("FieldOfView"), 300.f, File);
	UHPGameUserSettings* C = NewObject<UHPGameUserSettings>();
	C->LoadConfig(nullptr, *File);
	C->SanitizeHP();
	TestEqual(TEXT("Corrupt FOV clamped"), C->GetFieldOfView(), 110.f);

	GConfig->UnloadFile(File);
	IFileManager::Get().Delete(*File);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPMenuStateMachineTest, "HarvestPanic.Frontend.Menu.StateMachine",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ClientContext | EAutomationTestFlags::EngineFilter)
bool FHPMenuStateMachineTest::RunTest(const FString& Parameters)
{
	UHPMenuStateMachine* M = NewObject<UHPMenuStateMachine>();
	int32 Changes = 0;
	M->OnStateChanged.AddLambda([&Changes](EHPMenuState, EHPMenuState) { ++Changes; });

	M->Reset(EHPMenuState::Title);
	TestEqual(TEXT("Root is title"), M->Current(), EHPMenuState::Title);
	TestFalse(TEXT("Back at root fails"), M->Back());
	TestFalse(TEXT("Title -> Lobby illegal"), M->Push(EHPMenuState::Lobby));
	TestFalse(TEXT("Title -> Pause illegal"), M->Push(EHPMenuState::Pause));

	TestTrue(TEXT("Title -> Play"), M->Push(EHPMenuState::PlayChoice));
	TestTrue(TEXT("Play -> Browser"), M->Push(EHPMenuState::Browser));
	TestTrue(TEXT("Browser -> Connecting"), M->Push(EHPMenuState::Connecting));
	TestFalse(TEXT("Connecting -> Settings illegal"), M->Push(EHPMenuState::Settings));
	TestTrue(TEXT("Failed join backs out to Browser"), M->Back());
	TestEqual(TEXT("At browser"), M->Current(), EHPMenuState::Browser);
	TestTrue(TEXT("Back to Play"), M->Back());
	TestTrue(TEXT("Back to Title"), M->Back());
	TestEqual(TEXT("Depth 1"), M->Depth(), 1);

	TestTrue(TEXT("Title -> Settings"), M->Push(EHPMenuState::Settings));
	TestFalse(TEXT("Settings -> Settings illegal"), M->Push(EHPMenuState::Settings));
	TestTrue(TEXT("Settings back"), M->Back());
	TestTrue(TEXT("Title -> Customize"), M->Push(EHPMenuState::Customize));
	TestTrue(TEXT("Customize back"), M->Back());

	M->Reset(EHPMenuState::Lobby);
	TestTrue(TEXT("Lobby -> Pause"), M->Push(EHPMenuState::Pause));
	TestTrue(TEXT("Pause -> Settings"), M->Push(EHPMenuState::Settings));
	TestTrue(TEXT("Settings -> Pause"), M->Back());
	TestTrue(TEXT("Pause -> Lobby"), M->Back());
	TestTrue(TEXT("Lobby -> Customize"), M->Push(EHPMenuState::Customize));
	TestTrue(TEXT("Customize -> Lobby"), M->Back());

	M->Reset(EHPMenuState::InGame);
	TestFalse(TEXT("InGame -> Settings directly illegal"), M->Push(EHPMenuState::Settings));
	TestTrue(TEXT("InGame -> Pause"), M->Push(EHPMenuState::Pause));
	TestEqual(TEXT("Root InGame"), M->Root(), EHPMenuState::InGame);

	TestTrue(TEXT("Change events fired"), Changes > 10);
	return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FHPDayMapFallbackTest, "HarvestPanic.Frontend.Session.DayMapFallback",
	EAutomationTestFlags::EditorContext | EAutomationTestFlags::ClientContext | EAutomationTestFlags::EngineFilter)
bool FHPDayMapFallbackTest::RunTest(const FString& Parameters)
{
	const FString URL = UHPGameInstance::ResolveDayMapURL();
	TestTrue(TEXT("Listen option"), URL.EndsWith(TEXT("?listen")));
	TestTrue(TEXT("Muddlebury or ThirdPerson"), URL.Contains(TEXT("Lvl_Muddlebury")) || URL.Contains(TEXT("Lvl_ThirdPerson")));
	return true;
}

#endif
