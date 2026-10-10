// Copyright HARVEST PANIC team. All Rights Reserved.
#include "Economy/HPMarketPen.h"
#include "Economy/HPGoodsComponent.h"
#include "HPGameState.h"
#include "Components/BoxComponent.h"
#include "Components/TextRenderComponent.h"
#include "Engine/World.h"

AHPMarketPen::AHPMarketPen()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.TickInterval = 0.1f;
	bReplicates = true;

	SaleVolume = CreateDefaultSubobject<UBoxComponent>(TEXT("SaleVolume"));
	SaleVolume->SetBoxExtent(FVector(300.f, 300.f, 150.f));
	SaleVolume->SetCollisionProfileName(TEXT("Trigger"));
	SaleVolume->SetGenerateOverlapEvents(true);
	SaleVolume->SetHiddenInGame(false);
	SaleVolume->ShapeColor = FColor(80, 200, 120);
	RootComponent = SaleVolume;

	Sign = CreateDefaultSubobject<UTextRenderComponent>(TEXT("Sign"));
	Sign->SetupAttachment(SaleVolume);
	Sign->SetRelativeLocation(FVector(0.f, 0.f, 200.f));
	Sign->SetHorizontalAlignment(EHTA_Center);
	Sign->SetWorldSize(60.f);
	Sign->SetText(FText::FromString(TEXT("MARKET")));
}

void AHPMarketPen::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (!HasAuthority())
	{
		return;
	}
	AHPGameState* GS = GetWorld()->GetGameState<AHPGameState>();
	const bool bOpen = GS && GS->IsMarketOpen();

	TArray<AActor*> Inside;
	SaleVolume->GetOverlappingActors(Inside);

	TMap<TWeakObjectPtr<UHPGoodsComponent>, float> NewDwell;
	for (AActor* A : Inside)
	{
		UHPGoodsComponent* Goods = A ? A->FindComponentByClass<UHPGoodsComponent>() : nullptr;
		if (!Goods || Goods->IsSold() || Goods->Tags.HasTagExact(FGameplayTag::RequestGameplayTag(TEXT("Goods.Pocketed"), false)))
		{
			continue;
		}
		// Continuous dwell only: anything that left the pen restarts from zero (living goods can escape).
		const float T = Dwell.FindRef(Goods) + DeltaSeconds;
		if (bOpen && T >= AcceptSeconds)
		{
			const int32 Value = GS->SellGoods(Goods);
			if (Value >= 0)
			{
				Sign->SetText(FText::FromString(FString::Printf(TEXT("SOLD +$%d"), Value)));
				if (bDestroySoldActors)
				{
					A->Destroy();
				}
				continue;
			}
		}
		NewDwell.Add(Goods, T);
	}
	Dwell = MoveTemp(NewDwell);
}
