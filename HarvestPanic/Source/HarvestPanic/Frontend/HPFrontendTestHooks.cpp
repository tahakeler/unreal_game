#include "Frontend/HPFrontendTestHooks.h"
#include "GameFramework/PlayerController.h"
#include "Engine/World.h"
#include "TimerManager.h"
#include "UnrealClient.h"
#include "Misc/CommandLine.h"
#include "Misc/Parse.h"
#include "Kismet/KismetSystemLibrary.h"
#include "Containers/Ticker.h"

DEFINE_LOG_CATEGORY_STATIC(LogHPQA, Log, All);

void HPFrontendTestHooks::Arm(APlayerController* PC)
{
	if (!PC || !PC->IsLocalController()) return;
	const TCHAR* Cmd = FCommandLine::Get();
	FString Shot;
	float Delay = 4.f, QuitAfter = 0.f;
	FParse::Value(Cmd, TEXT("HPShotDelay="), Delay);
	TWeakObjectPtr<APlayerController> WeakPC = PC;
	if (FParse::Value(Cmd, TEXT("HPShot="), Shot) && !Shot.IsEmpty())
	{
		FTimerHandle H;
		PC->GetWorldTimerManager().SetTimer(H, FTimerDelegate::CreateWeakLambda(PC, [Shot, WeakPC]()
		{
			UE_LOG(LogHPQA, Log, TEXT("HPQA: screenshot -> %s"), *Shot);
			FScreenshotRequest::RequestScreenshot(Shot, true, false);
			FTimerHandle Q;
			if (WeakPC.IsValid())
			{
				WeakPC->GetWorldTimerManager().SetTimer(Q, FTimerDelegate::CreateWeakLambda(WeakPC.Get(), [WeakPC]()
				{
					UKismetSystemLibrary::QuitGame(WeakPC.Get(), WeakPC.Get(), EQuitPreference::Quit, false);
				}), 2.f, false);
			}
		}), Delay, false);
	}
	static bool bQuitArmed = false;
	if (!bQuitArmed && FParse::Value(Cmd, TEXT("HPQuitAfter="), QuitAfter) && QuitAfter > 0.f)
	{
		bQuitArmed = true;
		// Real time, survives map travel (the world timer would not).
		const double End = FPlatformTime::Seconds() + QuitAfter;
		FTSTicker::GetCoreTicker().AddTicker(FTickerDelegate::CreateLambda([End](float) -> bool
		{
			if (FPlatformTime::Seconds() >= End)
			{
				UE_LOG(LogHPQA, Log, TEXT("HPQA: quit-after reached"));
				FPlatformMisc::RequestExit(false, TEXT("HPQuitAfter"));
				return false;
			}
			return true;
		}), 0.5f);
	}
}
