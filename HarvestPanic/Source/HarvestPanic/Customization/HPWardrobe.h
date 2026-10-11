// Copyright HARVEST PANIC team. All Rights Reserved.
// In-world wardrobe (place at the barn). Walk into the trigger, press C to open
// UHPCustomizationWidget. The key is handled by UHPCustomizationSubsystem.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HPWardrobe.generated.h"

class USphereComponent;
class UStaticMeshComponent;
class UTextRenderComponent;

UCLASS()
class HARVESTPANIC_API AHPWardrobe : public AActor
{
	GENERATED_BODY()

public:
	AHPWardrobe();

	/** True if the given pawn is inside the interaction trigger. */
	bool IsPawnInRange(const APawn* Pawn) const;

	UPROPERTY(EditAnywhere, Category="Wardrobe") float InteractRadius = 220.f;
	UPROPERTY(EditAnywhere, Category="Wardrobe") FText PromptText = NSLOCTEXT("HPWardrobe", "Prompt", "[C] Change outfit");

	virtual void Tick(float DeltaSeconds) override;

protected:
	virtual void OnConstruction(const FTransform& Transform) override;
	virtual void BeginPlay() override;

	UPROPERTY(VisibleAnywhere) TObjectPtr<USceneComponent> Root;
	UPROPERTY(VisibleAnywhere) TObjectPtr<USphereComponent> Trigger;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UStaticMeshComponent> Cabinet;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UStaticMeshComponent> DoorL;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UStaticMeshComponent> DoorR;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UStaticMeshComponent> TopHat;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UTextRenderComponent> Sign;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UTextRenderComponent> Prompt;
};
