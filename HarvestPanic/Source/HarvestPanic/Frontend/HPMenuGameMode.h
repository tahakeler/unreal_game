// HARVEST PANIC - main menu map: diorama camera + title UI.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "GameFramework/Pawn.h"
#include "GameFramework/PlayerController.h"
#include "HPMenuGameMode.generated.h"

class UCameraComponent;

/** Slow drifting camera over the farm diorama. */
UCLASS()
class HARVESTPANIC_API AHPMenuCameraPawn : public APawn
{
	GENERATED_BODY()
public:
	AHPMenuCameraPawn();
	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;

	UPROPERTY(VisibleAnywhere) TObjectPtr<UCameraComponent> Camera;
	/** Yaw sway amplitude (deg) and period (s); kept gentle for motion comfort. */
	UPROPERTY(EditAnywhere, Category="Menu") float SwayYaw = 6.f;
	UPROPERTY(EditAnywhere, Category="Menu") float SwayPeriod = 40.f;
	UPROPERTY(EditAnywhere, Category="Menu") float DriftDistance = 120.f;
private:
	FVector StartLoc;
	FRotator StartRot;
	float Time = 0.f;
};

UCLASS()
class HARVESTPANIC_API AHPMenuPlayerController : public APlayerController
{
	GENERATED_BODY()
protected:
	virtual void BeginPlay() override;
	virtual void SetupInputComponent() override;
	void OnBack();
	void AutoTest();
};

UCLASS()
class HARVESTPANIC_API AHPMenuGameMode : public AGameModeBase
{
	GENERATED_BODY()
public:
	AHPMenuGameMode();
};
