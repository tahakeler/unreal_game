// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/session-networking.md (listen-server authority, max 4 players, join/drop),
//      day-loop.md (host-authoritative phase state machine), week-quota.md (Sunday check).
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "Economy/HPEconomyTypes.h"
#include "HPGameMode.generated.h"

class AHPGameState;

/**
 * Slice state machine: Morning(30s) → Workday(540s) → Dusk(180s, auto) → Payout (host: Next Day)
 * → ... after the 5th day → Sunday (Paid → next week / Evicted → restart run).
 * Phase durations are scaled by the dev cvar hp.DayLengthScale.
 */
UCLASS()
class HARVESTPANIC_API AHPGameMode : public AGameModeBase
{
	GENERATED_BODY()

public:
	AHPGameMode();

	/** session-networking Tuning Knob: MaxPlayers (2-4). */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Session", meta=(ClampMin=1, ClampMax=4))
	int32 MaxPlayers = 4;

	// ---- day-loop Tuning Knobs ----
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Day", meta=(ClampMin=15, ClampMax=60))
	float MorningSeconds = 30.f;
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Day", meta=(ClampMin=300, ClampMax=720))
	float WorkdaySeconds = 540.f;
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Day", meta=(ClampMin=90, ClampMax=300))
	float DuskSeconds = 180.f;
	/** Slice: no dusk vote yet — when true every day runs a Dusk phase. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Day")
	bool bAutoDusk = true;
	/** Start the first day automatically on BeginPlay. */
	UPROPERTY(EditDefaultsOnly, BlueprintReadOnly, Category="Day")
	bool bAutoStartRun = true;

	/** Host: end the current phase early (Morning skip / Workday → Dusk / Dusk → Payout). */
	UFUNCTION(BlueprintCallable, Category="Day") void AdvancePhase();
	/** Host: jump straight to the Payout screen for the current day. */
	UFUNCTION(BlueprintCallable, Category="Day") void EndDay();
	/** Host: from Payout → next day (or Sunday after day 5); from Sunday → next week or restart. */
	UFUNCTION(BlueprintCallable, Category="Day") void ContinueFromPayout();
	/** Host: wipe and restart the run (eviction or menu). */
	UFUNCTION(BlueprintCallable, Category="Day") void RestartRun();

	static float GetDayLengthScale();

protected:
	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;

private:
	AHPGameState* GetHPGameState() const;
	void StartDay();
	void EnterPhase(EHPDayPhase Phase);
	void RunSunday();
	int32 CurrentPlayerCount() const;

	int32 RunDayIndex = 0;
	int32 DayOfWeek = 0;
	int32 Week = 1;
};
