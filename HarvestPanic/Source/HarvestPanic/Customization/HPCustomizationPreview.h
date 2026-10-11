// Copyright HARVEST PANIC team. All Rights Reserved.
// Isolated preview mannequin + scene capture for the wardrobe widget. Spawned far
// above the level and captured with a show-only list, so it works in any map.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "HPCosmeticTypes.h"
#include "HPCustomizationPreview.generated.h"

class USkeletalMeshComponent;
class USceneCaptureComponent2D;
class UTextureRenderTarget2D;
class UPointLightComponent;
class UStaticMeshComponent;

UCLASS(NotPlaceable)
class HARVESTPANIC_API AHPCustomizationPreview : public AActor
{
	GENERATED_BODY()

public:
	AHPCustomizationPreview();

	void SetLoadout(const FHPCosmeticLoadout& Loadout);
	UTextureRenderTarget2D* GetRenderTarget() const { return RenderTarget; }
	void AddYaw(float Degrees) { TargetYaw += Degrees; }

	UPROPERTY(EditAnywhere, Category="Preview") FIntPoint Resolution = FIntPoint(560, 720);
	UPROPERTY(EditAnywhere, Category="Preview") float IdleSpinDegPerSec = 12.f;
	UPROPERTY(EditAnywhere, Category="Preview") FVector SpawnLocation = FVector(0, 0, 60000);

	virtual void Tick(float DeltaSeconds) override;

protected:
	virtual void BeginPlay() override;

	UPROPERTY(VisibleAnywhere) TObjectPtr<USceneComponent> Root;
	UPROPERTY(VisibleAnywhere) TObjectPtr<USceneComponent> Turntable;
	UPROPERTY(VisibleAnywhere) TObjectPtr<USkeletalMeshComponent> Body;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UStaticMeshComponent> Backdrop;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UStaticMeshComponent> Floor;
	UPROPERTY(VisibleAnywhere) TObjectPtr<USceneCaptureComponent2D> Capture;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UPointLightComponent> KeyLight;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UPointLightComponent> FillLight;
	UPROPERTY(Transient) TObjectPtr<UTextureRenderTarget2D> RenderTarget;

	float TargetYaw = 0.f;
	float Yaw = 0.f;
	bool bUserRotated = false;
};
