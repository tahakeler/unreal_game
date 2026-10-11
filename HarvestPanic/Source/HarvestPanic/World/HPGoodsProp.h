// Copyright HARVEST PANIC team. All Rights Reserved.
// Sellable physical goods: Prize Pumpkin (4 hands) and crates. A carry prop wired to UHPGoodsComponent:
// grab/drop/throw record touches; physics bruise hits become Goods bruises (blame via touch history).
#pragma once

#include "CoreMinimal.h"
#include "Carry/HPCarryableProp.h"
#include "HPGoodsProp.generated.h"

class UHPGoodsComponent;

UENUM(BlueprintType)
enum class EHPGoodsKind : uint8 { PrizePumpkin, Crate };

UCLASS()
class HARVESTPANIC_API AHPGoodsProp : public AHPCarryableProp
{
	GENERATED_BODY()

public:
	AHPGoodsProp();

	/** What this prop is. Set before FinishSpawning (deferred spawn) or in the editor. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category="Goods") EHPGoodsKind Kind = EHPGoodsKind::Crate;

	UHPGoodsComponent* GetGoods() const { return Goods; }

	virtual void NotifyTouched(AActor* Toucher, bool bThrown) override;
	virtual void OnConstruction(const FTransform& Transform) override;

protected:
	virtual void BeginPlay() override;

	UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category="Goods") TObjectPtr<UHPGoodsComponent> Goods;

private:
	UFUNCTION() void HandleBruiseHit(const FHPBruiseHit& Hit);
	void ConfigureForKind();
	double LastThrowTime = -1000.0;
};
