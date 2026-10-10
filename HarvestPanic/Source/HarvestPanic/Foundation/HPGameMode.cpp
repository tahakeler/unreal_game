// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPGameMode.h"
#include "HPGameState.h"
#include "HPPlayerState.h"
#include "Economy/HPEconomyMath.h"
#include "Economy/HPGoodsComponent.h"
#include "UI/HPHUD.h"
#include "HAL/IConsoleManager.h"
#include "Engine/World.h"
#include "Engine/Engine.h"
#include "TimerManager.h"
#include "UnrealClient.h"
#include "GameFramework/PlayerController.h"
#include "GameFramework/PlayerState.h"
#include "Kismet/GameplayStatics.h"

static TAutoConsoleVariable<float> CVarHPDayLengthScale(
	TEXT("hp.DayLengthScale"), 1.0f,
	TEXT("Dev: multiplies Morning/Workday/Dusk durations (e.g. 0.05 for fast testing)."),
	ECVF_Cheat);

AHPGameMode::AHPGameMode()
{
	GameStateClass = AHPGameState::StaticClass();
	PlayerStateClass = AHPPlayerState::StaticClass();
	HUDClass = AHPHUD::StaticClass();
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.TickInterval = 0.1f;
}

float AHPGameMode::GetDayLengthScale()
{
	return FMath::Max(0.01f, CVarHPDayLengthScale.GetValueOnGameThread());
}

AHPGameState* AHPGameMode::GetHPGameState() const
{
	return GetGameState<AHPGameState>();
}

int32 AHPGameMode::CurrentPlayerCount() const
{
	return FMath::Clamp(GameState ? GameState->PlayerArray.Num() : 1, 1, MaxPlayers);
}

void AHPGameMode::BeginPlay()
{
	Super::BeginPlay();
	if (bAutoStartRun)
	{
		// Defer one tick so the listen-server host's PlayerState exists for the Monday player-count sample.
		GetWorldTimerManager().SetTimerForNextTick(this, &AHPGameMode::RestartRun);
	}
}

void AHPGameMode::RestartRun()
{
	AHPGameState* GS = GetHPGameState();
	if (!GS)
	{
		return;
	}
	RunDayIndex = 0;
	DayOfWeek = 0;
	Week = 1;
	GS->ServerResetRun();
	GS->ServerSetWeek(Week, CurrentPlayerCount());
	StartDay();
}

void AHPGameMode::StartDay()
{
	AHPGameState* GS = GetHPGameState();
	if (DayOfWeek == 0)
	{
		GS->ServerSetWeek(Week, CurrentPlayerCount()); // player count sampled Monday morning
	}
	GS->ServerBeginDay(RunDayIndex, DayOfWeek, Week, /*Seed*/ 7919 * (RunDayIndex + 1) + Week);
	EnterPhase(EHPDayPhase::Morning);
}

void AHPGameMode::EnterPhase(EHPDayPhase NewPhase)
{
	AHPGameState* GS = GetHPGameState();
	const float Scale = GetDayLengthScale();
	float Duration = 0.f;
	switch (NewPhase)
	{
	case EHPDayPhase::Morning: Duration = MorningSeconds * Scale; break;
	case EHPDayPhase::Workday: Duration = WorkdaySeconds * Scale; break;
	case EHPDayPhase::Dusk:    Duration = DuskSeconds * Scale; break;
	default: break; // Payout / Sunday wait for the host
	}
	GS->ServerSetPhase(NewPhase, Duration);
	UE_LOG(LogTemp, Log, TEXT("[HP] Phase -> %s (%.0fs) day %d week %d"),
		*UEnum::GetValueAsString(NewPhase), Duration, RunDayIndex, Week);
}

void AHPGameMode::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	AHPGameState* GS = GetHPGameState();
	if (!GS)
	{
		return;
	}
	const bool bTimed = GS->Phase == EHPDayPhase::Morning || GS->Phase == EHPDayPhase::Workday || GS->Phase == EHPDayPhase::Dusk;
	if (bTimed && GS->GetPhaseSecondsRemaining() <= 0.f)
	{
		AdvancePhase();
	}
}

void AHPGameMode::AdvancePhase()
{
	AHPGameState* GS = GetHPGameState();
	if (!GS) return;
	switch (GS->Phase)
	{
	case EHPDayPhase::Morning: EnterPhase(EHPDayPhase::Workday); break;
	case EHPDayPhase::Workday:
		if (bAutoDusk) { EnterPhase(EHPDayPhase::Dusk); } else { EndDay(); }
		break;
	case EHPDayPhase::Dusk: EndDay(); break;
	case EHPDayPhase::Payout:
	case EHPDayPhase::Sunday: ContinueFromPayout(); break;
	default: break;
	}
}

void AHPGameMode::EndDay()
{
	AHPGameState* GS = GetHPGameState();
	if (!GS || GS->Phase == EHPDayPhase::Payout || GS->Phase == EHPDayPhase::Sunday || GS->Phase == EHPDayPhase::PreRun)
	{
		return;
	}
	const int32 WalletBefore = GS->WalletBalance - GS->DaySales + GS->DayCosts;
	FHPDaySummary S;
	S.DayIndex = RunDayIndex;
	S.WeekIndex = Week;
	S.DayOfWeek = DayOfWeek;
	S.bOrderComplete = GS->OrderLines.Num() > 0 && GS->OrderLines.FindByPredicate(
		[](const FHPOrderLine& L) { return !L.IsComplete(); }) == nullptr;
	if (S.bOrderComplete)
	{
		S.OrderBonus = UHPEconomyMath::OrderCompleteBonus(Week);
		GS->AddWalletTxn(EHPWalletTxnType::OrderBonus, S.OrderBonus);
	}
	S.Sales = GS->DaySales;
	S.ItemsSold = GS->DayItemsSold;
	S.Costs = GS->DayCosts;
	S.WalletAfter = GS->WalletBalance;
	S.Net = S.WalletAfter - WalletBefore;
	S.QuotaDue = GS->GetQuotaDue();
	GS->ServerSetSummary(S);
	UE_LOG(LogTemp, Log, TEXT("[HP] Payout: sales=%d items=%d orderBonus=%d costs=%d net=%d wallet=%d due=%d"),
		S.Sales, S.ItemsSold, S.OrderBonus, S.Costs, S.Net, S.WalletAfter, S.QuotaDue);
	for (const FHPReceiptLine& L : UHPEconomyMath::BuildReceipt(GS->DayBlameEvents))
	{
		UE_LOG(LogTemp, Log, TEXT("[HP] Receipt: %s  %d"),
			*UHPEconomyMath::FormatReceiptLine(L, GS->GetPlayerNameById(L.PlayerId)), L.Dollars);
	}
	EnterPhase(EHPDayPhase::Payout);
}

void AHPGameMode::ContinueFromPayout()
{
	AHPGameState* GS = GetHPGameState();
	if (!GS) return;

	if (GS->Phase == EHPDayPhase::Payout)
	{
		++RunDayIndex;
		++DayOfWeek;
		if (DayOfWeek >= UHPEconomyMath::DaysPerWeek)
		{
			RunSunday();
		}
		else
		{
			StartDay();
		}
	}
	else if (GS->Phase == EHPDayPhase::Sunday)
	{
		if (GS->LastSummary.SundayResult == EHPSundayResult::Evicted)
		{
			RestartRun();
		}
		else
		{
			DayOfWeek = 0;
			++Week;
			StartDay();
		}
	}
}

void AHPGameMode::RunSunday()
{
	// week-quota Sunday sequence (slice: no awards/collateral). Due = Q(w) + IOU.
	AHPGameState* GS = GetHPGameState();
	FHPDaySummary S = GS->LastSummary;
	S.QuotaDue = GS->GetQuotaDue();
	S.WeekIndex = Week;
	if (GS->WalletBalance >= S.QuotaDue)
	{
		GS->AddWalletTxn(EHPWalletTxnType::Quota, -S.QuotaDue);
		S.SundayResult = EHPSundayResult::Paid;
	}
	else
	{
		S.SundayResult = EHPSundayResult::Evicted;
	}
	S.WalletAfter = GS->WalletBalance;
	GS->ServerSetSummary(S);
	EnterPhase(EHPDayPhase::Sunday);
}

// ======================= Dev console commands =======================
namespace HPDev
{
	static AHPGameMode* GetGM(UWorld* World)
	{
		return World ? World->GetAuthGameMode<AHPGameMode>() : nullptr;
	}

	static int32 LocalPlayerId(UWorld* World)
	{
		APlayerController* PC = World ? World->GetFirstPlayerController() : nullptr;
		return PC && PC->PlayerState ? PC->PlayerState->GetPlayerId() : HP_NO_PLAYER;
	}

	/** Sells N transient goods of a type through the real market path. */
	static void DebugSell(UWorld* World, FName GoodsId, int32 Count, float BruiseImpulse)
	{
		AHPGameState* GS = World ? World->GetGameState<AHPGameState>() : nullptr;
		if (!GS || !GS->HasAuthority()) return;
		for (int32 i = 0; i < Count; ++i)
		{
			AActor* Dummy = World->SpawnActor<AActor>();
			if (!Dummy) continue;
			UHPGoodsComponent* Goods = NewObject<UHPGoodsComponent>(Dummy);
			Goods->GoodsId = GoodsId;
			Goods->RegisterComponent(); // BeginPlay applies preset
			Goods->ApplyPreset(GoodsId);
			if (APlayerController* PC = World->GetFirstPlayerController())
			{
				Goods->RecordTouch(PC->PlayerState);
			}
			if (BruiseImpulse > 0.f)
			{
				Goods->ApplyBruiseHit(BruiseImpulse, nullptr, EHPBlameVerb::Dropped);
			}
			GS->SellGoods(Goods);
			Dummy->Destroy();
		}
	}

	static FAutoConsoleCommandWithWorldAndArgs EndDayCmd(TEXT("HP.EndDay"), TEXT("Host: end the day and show the Payout screen."),
		FConsoleCommandWithWorldAndArgsDelegate::CreateLambda([](const TArray<FString>&, UWorld* W)
		{
			if (AHPGameMode* GM = GetGM(W)) GM->EndDay();
		}));

	static FAutoConsoleCommandWithWorldAndArgs NextCmd(TEXT("HP.NextDay"), TEXT("Host: continue from Payout/Sunday."),
		FConsoleCommandWithWorldAndArgsDelegate::CreateLambda([](const TArray<FString>&, UWorld* W)
		{
			if (AHPGameMode* GM = GetGM(W)) GM->ContinueFromPayout();
		}));

	static FAutoConsoleCommandWithWorldAndArgs AdvanceCmd(TEXT("HP.Advance"), TEXT("Host: skip to the next phase."),
		FConsoleCommandWithWorldAndArgsDelegate::CreateLambda([](const TArray<FString>&, UWorld* W)
		{
			if (AHPGameMode* GM = GetGM(W)) GM->AdvancePhase();
		}));

	static FAutoConsoleCommandWithWorldAndArgs SellCmd(TEXT("HP.DebugSell"),
		TEXT("HP.DebugSell <GoodsId> [Count] [BruiseImpulse] — sell transient goods through the market path (market must be open)."),
		FConsoleCommandWithWorldAndArgsDelegate::CreateLambda([](const TArray<FString>& Args, UWorld* W)
		{
			const FName Id = Args.Num() > 0 ? FName(*Args[0]) : FName(TEXT("Pig"));
			const int32 N = Args.Num() > 1 ? FCString::Atoi(*Args[1]) : 1;
			const float I = Args.Num() > 2 ? FCString::Atof(*Args[2]) : 0.f;
			DebugSell(W, Id, FMath::Max(1, N), I);
		}));

	static FAutoConsoleCommandWithWorldAndArgs FeeCmd(TEXT("HP.DebugRespawnFee"), TEXT("Charge the local player a respawn fee."),
		FConsoleCommandWithWorldAndArgsDelegate::CreateLambda([](const TArray<FString>&, UWorld* W)
		{
			AHPGameState* GS = W ? W->GetGameState<AHPGameState>() : nullptr;
			APlayerController* PC = W ? W->GetFirstPlayerController() : nullptr;
			if (GS && PC) GS->ChargeRespawnFee(PC->PlayerState, EHPBlameVerb::Drowned);
		}));

	/** HP.QAShots <AbsDir> — scripted evidence capture: HUD shot, HP.EndDay, payout shot, quit. */
	static FAutoConsoleCommandWithWorldAndArgs QACmd(TEXT("HP.QAShots"), TEXT("HP.QAShots <AbsDir> [bQuit=1]"),
		FConsoleCommandWithWorldAndArgsDelegate::CreateLambda([](const TArray<FString>& Args, UWorld* W)
		{
			AHPGameMode* GM = GetGM(W);
			AHPGameState* GS = W ? W->GetGameState<AHPGameState>() : nullptr;
			if (!GM || !GS) { UE_LOG(LogTemp, Error, TEXT("[HP] QAShots needs AHPGameMode")); return; }
			const FString Dir = Args.Num() > 0 ? Args[0] : FPaths::ProjectSavedDir() / TEXT("Screenshots");
			const bool bQuit = Args.Num() < 2 || Args[1] != TEXT("0");

			if (GS->Phase == EHPDayPhase::PreRun) GM->RestartRun();
			if (GS->Phase == EHPDayPhase::Morning) GM->AdvancePhase();
			DebugSell(W, TEXT("Pig"), 2, 0.f);
			DebugSell(W, TEXT("Egg"), 4, 0.f);
			DebugSell(W, TEXT("PrizePumpkin"), 1, 2400.f); // dropped: bruised before sale
			DebugSell(W, TEXT("Chicken"), 1, 1500.f);
			GS->EmitBlame(EHPBlameVerb::DroppedInRiver, TEXT("Pig"), -60, {}, FVector::ZeroVector); // Nature
			GS->ChargeRespawnFee(W->GetFirstPlayerController() ? W->GetFirstPlayerController()->PlayerState : nullptr, EHPBlameVerb::Drowned);

			FTimerManager& TM = W->GetTimerManager();
			FTimerHandle H1, H2, H3, H4;
			TM.SetTimer(H1, FTimerDelegate::CreateLambda([Dir]()
			{
				FScreenshotRequest::RequestScreenshot(Dir / TEXT("day_hud.png"), true, false);
			}), 2.5f, false);
			TWeakObjectPtr<AHPGameMode> WeakGM(GM);
			TM.SetTimer(H2, FTimerDelegate::CreateLambda([WeakGM]() { if (WeakGM.IsValid()) WeakGM->EndDay(); }), 3.5f, false);
			TM.SetTimer(H3, FTimerDelegate::CreateLambda([Dir]()
			{
				FScreenshotRequest::RequestScreenshot(Dir / TEXT("payout_receipt.png"), true, false);
			}), 5.5f, false);
			if (bQuit)
			{
				TM.SetTimer(H4, FTimerDelegate::CreateLambda([]() { FPlatformMisc::RequestExit(false, TEXT("HP.QAShots")); }), 8.0f, false);
			}
		}));
}
