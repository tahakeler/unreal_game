// Copyright HARVEST PANIC team. All Rights Reserved.
#include "World/HPGoodsSpawner.h"
#include "Engine/World.h"

AHPGoodsSpawner::AHPGoodsSpawner()
{
	RootComponent = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
}

void AHPGoodsSpawner::BeginPlay()
{
	Super::BeginPlay();
	if (bSpawnOnBeginPlay && HasAuthority())
	{
		SpawnGoods();
	}
}

TArray<AHPGoodsProp*> AHPGoodsSpawner::SpawnGoods()
{
	TArray<AHPGoodsProp*> Out;
	if (!HasAuthority())
	{
		return Out;
	}
	for (int32 i = 0; i < Count; ++i)
	{
		const float Angle = Count > 1 ? 2.f * PI * i / Count : 0.f;
		const FVector Offset = Count > 1 ? FVector(FMath::Cos(Angle), FMath::Sin(Angle), 0.f) * SpreadCm : FVector::ZeroVector;
		const FTransform T(GetActorRotation(), GetActorLocation() + Offset + FVector(0, 0, 50.f * i));
		FActorSpawnParameters P;
		P.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AdjustIfPossibleButAlwaysSpawn;
		AHPGoodsProp* Prop = GetWorld()->SpawnActorDeferred<AHPGoodsProp>(AHPGoodsProp::StaticClass(), T, nullptr, nullptr, P.SpawnCollisionHandlingOverride);
		if (Prop)
		{
			Prop->Kind = Kind;
			Prop->FinishSpawning(T);
			Out.Add(Prop);
		}
	}
	return Out;
}
