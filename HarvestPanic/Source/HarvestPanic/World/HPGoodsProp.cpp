// Copyright HARVEST PANIC team. All Rights Reserved.
#include "World/HPGoodsProp.h"
#include "Economy/HPGoodsComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/World.h"
#include "Materials/MaterialInstanceDynamic.h"

AHPGoodsProp::AHPGoodsProp()
{
	Goods = CreateDefaultSubobject<UHPGoodsComponent>(TEXT("Goods"));
}

void AHPGoodsProp::ConfigureForKind()
{
	if (Kind == EHPGoodsKind::PrizePumpkin)
	{
		Shape = EHPPropShape::Sphere;
		CarryHands = 4; // team lift (the preset's 8 is the full-cart GDD value; the slice uses 4)
		MassKg = 45.f;
		Goods->GoodsId = TEXT("PrizePumpkin");
		Goods->bUsePresetForGoodsId = true;
	}
	else
	{
		Shape = EHPPropShape::Cube;
		CarryHands = 2;
		MassKg = 20.f;
		Goods->GoodsId = TEXT("Crate");
		Goods->Category = EHPGoodsCategory::Crop;
		Goods->BaseValue = 40;
		Goods->Fragility = 1.0f;
		Goods->bUsePresetForGoodsId = false;
	}
	Goods->CarryHands = CarryHands;
	Goods->MassKg = MassKg;
}

void AHPGoodsProp::OnConstruction(const FTransform& Transform)
{
	ConfigureForKind();
	Super::OnConstruction(Transform);
	const bool bPumpkin = Kind == EHPGoodsKind::PrizePumpkin;
	Mesh->SetRelativeScale3D(bPumpkin ? FVector(0.9f, 0.9f, 0.8f) : FVector(0.6f));
	if (UMaterialInterface* Base = Mesh->GetMaterial(0))
	{
		UMaterialInstanceDynamic* MID = UMaterialInstanceDynamic::Create(Base, this);
		MID->SetVectorParameterValue(TEXT("Color"), bPumpkin ? FLinearColor(1.f, 0.4f, 0.02f) : FLinearColor(0.45f, 0.28f, 0.1f));
		Mesh->SetMaterial(0, MID);
	}
}

void AHPGoodsProp::BeginPlay()
{
	ConfigureForKind();
	Super::BeginPlay();
	if (HasAuthority())
	{
		// Goods BeginPlay applied the preset; keep the slice's hand count and mass.
		Goods->CarryHands = CarryHands;
		Goods->MassKg = MassKg;
		OnBruiseHit.AddDynamic(this, &AHPGoodsProp::HandleBruiseHit);
	}
}

void AHPGoodsProp::NotifyTouched(AActor* Toucher, bool bThrown)
{
	Super::NotifyTouched(Toucher, bThrown);
	if (HasAuthority())
	{
		Goods->RecordTouch(Toucher);
		if (bThrown)
		{
			LastThrowTime = GetWorld()->GetTimeSeconds();
		}
	}
}

void AHPGoodsProp::HandleBruiseHit(const FHPBruiseHit& Hit)
{
	const bool bThrown = GetWorld()->GetTimeSeconds() - LastThrowTime < 2.0;
	Goods->ApplyBruiseHit(Hit.ImpulseNs, nullptr, bThrown ? EHPBlameVerb::Threw : EHPBlameVerb::Dropped);
}
