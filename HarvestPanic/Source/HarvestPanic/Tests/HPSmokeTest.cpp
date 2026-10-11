// Copyright HARVEST PANIC team. All Rights Reserved.
// Headless smoke test: `hp.RunSmoke` (use with -game -nullrhi -ExecCmds="hp.RunSmoke" on a map with tagged markers).
// Sequence: log spawned actors -> grab a chicken with player 0 -> throw -> carry a crate into the pen ->
// wait for the sale -> end the day -> log the wallet -> quit. Every step logs "[Smoke]".
#include "AI/HPCritter.h"
#include "Economy/HPGoodsComponent.h"
#include "Economy/HPMarketPen.h"
#include "Foundation/HPCarryComponent.h"
#include "Foundation/HPCharacter.h"
#include "Foundation/HPGameMode.h"
#include "Foundation/HPGameState.h"
#include "World/HPGoodsProp.h"
#include "Containers/Ticker.h"
#include "Engine/World.h"
#include "EngineUtils.h"
#include "GameFramework/PlayerController.h"
#include "HAL/IConsoleManager.h"
#include "Kismet/KismetSystemLibrary.h"

namespace
{
	struct FSmoke
	{
		TWeakObjectPtr<UWorld> World;
		double T = 0.0;
		double StepStart = 0.0;
		int32 Step = 0;
		int32 WalletStart = 0;
		TWeakObjectPtr<AHPCritter> Chicken;
		TWeakObjectPtr<AHPGoodsProp> Crate;
		TWeakObjectPtr<AHPMarketPen> Pen;
	};

	bool TickSmoke(float Dt, TSharedRef<FSmoke> S)
	{
		UWorld* World = S->World.Get();
		if (!World) { return false; }
		S->T = World->GetTimeSeconds() - S->StepStart; // world time: robust to ticker delta quirks
		AHPGameState* GS = World->GetGameState<AHPGameState>();
		APlayerController* PC = World->GetFirstPlayerController();
		AHPCharacter* Pawn = PC ? Cast<AHPCharacter>(PC->GetPawn()) : nullptr;
		if (!GS || !Pawn) { return true; }
		UHPCarryComponent* Carry = Pawn->GetCarryComponent();

		switch (S->Step)
		{
		case 0:
		{
			if (S->T < 3.0) { break; } // let the markers resolve and critters settle
			int32 Pigs = 0, Chickens = 0, Crates = 0, Pumpkins = 0;
			for (TActorIterator<AHPCritter> It(World); It; ++It) { (Cast<AHPCritter_Pig>(*It) ? Pigs : Chickens)++; if (!S->Chicken.IsValid() && Cast<AHPCritter_Chicken>(*It)) { S->Chicken = *It; } }
			for (TActorIterator<AHPGoodsProp> It(World); It; ++It) { (It->Kind == EHPGoodsKind::Crate ? Crates : Pumpkins)++; if (It->Kind == EHPGoodsKind::Crate) { S->Crate = *It; } }
			for (TActorIterator<AHPMarketPen> It(World); It; ++It) { S->Pen = *It; }
			S->WalletStart = GS->WalletBalance;
			UE_LOG(LogTemp, Display, TEXT("[Smoke] spawned: pigs=%d chickens=%d crates=%d pumpkins=%d pen=%d wallet=$%d phase=%d"), Pigs, Chickens, Crates, Pumpkins, S->Pen.IsValid(), GS->WalletBalance, (int32)GS->Phase);
			S->Step = 1;
			S->StepStart = World->GetTimeSeconds();
			break;
		}
		case 1:
			if (AHPCritter* C = S->Chicken.Get())
			{
				Pawn->TeleportTo(C->GetActorLocation() - FVector(100, 0, 0), FRotator::ZeroRotator);
				const bool bOk = Carry->ServerGrabActor(C);
				UE_LOG(LogTemp, Display, TEXT("[Smoke] grab chicken: %s state=%d hands=%d"), bOk ? TEXT("OK") : TEXT("FAIL"), (int32)C->GetCritterState(), C->GetHandsAttached());
			}
			S->Step = 2; S->StepStart = World->GetTimeSeconds();
			break;
		case 2:
			if (S->T > 1.0)
			{
				if (AHPCritter* C = S->Chicken.Get())
				{
					UE_LOG(LogTemp, Display, TEXT("[Smoke] holding=%d chickenState=%d (speedMult=%.2f)"), Carry->IsHolding(), (int32)C->GetCritterState(), Carry->GetSpeedMultiplier());
					Carry->ServerRelease(true);
					UE_LOG(LogTemp, Display, TEXT("[Smoke] released chicken; state=%d lastHandler=%d"), (int32)C->GetCritterState(), C->GetGoods()->LastHandlerId);
				}
				S->Step = 3; S->StepStart = World->GetTimeSeconds();
			}
			break;
		case 3:
			if (AHPGoodsProp* Crate = S->Crate.Get(); Crate && S->Pen.IsValid())
			{
				if (AHPGameMode* GM = World->GetAuthGameMode<AHPGameMode>(); GM && !GS->IsMarketOpen()) { GM->AdvancePhase(); } // Morning -> Workday
				Crate->SetActorLocation(S->Pen->GetActorLocation() + FVector(0, 0, 60), false, nullptr, ETeleportType::ResetPhysics);
				UE_LOG(LogTemp, Display, TEXT("[Smoke] crate moved into market pen"));
			}
			else { UE_LOG(LogTemp, Display, TEXT("[Smoke] no crate/pen present, skipping sale step")); }
			S->Step = 4; S->StepStart = World->GetTimeSeconds();
			break;
		case 4:
			if (S->T > 6.0)
			{
				UE_LOG(LogTemp, Display, TEXT("[Smoke] sale: wallet $%d -> $%d, items sold=%d, crateAlive=%d, marketOpen=%d"), S->WalletStart, GS->WalletBalance, GS->DayItemsSold, S->Crate.IsValid(), GS->IsMarketOpen());
				if (AHPGameMode* GM = World->GetAuthGameMode<AHPGameMode>()) { GM->EndDay(); }
				S->Step = 5; S->StepStart = World->GetTimeSeconds();
			}
			break;
		case 5:
			if (S->T > 1.0)
			{
				UE_LOG(LogTemp, Display, TEXT("[Smoke] end day: phase=%d dayIndex=%d summary.sales=%d"), (int32)GS->Phase, GS->DayIndex, GS->LastSummary.Sales);
				UE_LOG(LogTemp, Display, TEXT("[Smoke] DONE"));
				UKismetSystemLibrary::ExecuteConsoleCommand(World, TEXT("quit"));
				return false;
			}
			break;
		}
		return true;
	}
}

static FAutoConsoleCommandWithWorld GHPRunSmoke(TEXT("hp.RunSmoke"), TEXT("Headless slice smoke test (see Tests/HPSmokeTest.cpp)."),
	FConsoleCommandWithWorldDelegate::CreateLambda([](UWorld* World)
	{
		TSharedRef<FSmoke> S = MakeShared<FSmoke>();
		S->World = World;
		UE_LOG(LogTemp, Display, TEXT("[Smoke] START"));
		FTSTicker::GetCoreTicker().AddTicker(FTickerDelegate::CreateLambda([S](float Dt) { return TickSmoke(Dt, S); }), 0.25f);
	}));
