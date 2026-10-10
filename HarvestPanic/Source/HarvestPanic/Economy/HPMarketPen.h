// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/day-loop.md "Market zone" — an item is accepted after resting inside the pen
// for Market.AcceptSeconds (2.0 s) continuously while the market is open. Server-only selling.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HPMarketPen.generated.h"

class UBoxComponent;
class UTextRenderComponent;
class UHPGoodsComponent;

UCLASS()
class HARVESTPANIC_API AHPMarketPen : public AActor
{
	GENERATED_BODY()

public:
	AHPMarketPen();

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Market") TObjectPtr<UBoxComponent> SaleVolume;
	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Market") TObjectPtr<UTextRenderComponent> Sign;

	/** Market.AcceptSeconds (0.5–5). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Market", meta=(ClampMin=0.5, ClampMax=5.0))
	float AcceptSeconds = 2.0f;

	/** Destroy the sold actor (stand-in for the "SOLD crate" teleport). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Market")
	bool bDestroySoldActors = true;

protected:
	virtual void Tick(float DeltaSeconds) override;

private:
	/** Seconds each goods item has rested inside the pen. */
	TMap<TWeakObjectPtr<UHPGoodsComponent>, float> Dwell;
};
