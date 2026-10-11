// Copyright HARVEST PANIC team. All Rights Reserved.
#include "World/HPMarkerResolver.h"
#include "World/HPBarnRespawn.h"
#include "World/HPBridgePlank.h"
#include "World/HPGoodsSpawner.h"
#include "World/HPRiverVolume.h"
#include "AI/HPCritter.h"
#include "AI/HPCritterSpawner.h"
#include "Economy/HPMarketPen.h"
#include "Components/BoxComponent.h"
#include "Components/BillboardComponent.h"
#include "Engine/World.h"
#include "EngineUtils.h"
#include "GameFramework/PlayerStart.h"

DEFINE_LOG_CATEGORY_STATIC(LogHPMarkers, Log, All);

bool UHPMarkerResolver::ShouldCreateSubsystem(UObject* Outer) const
{
	const UWorld* World = Cast<UWorld>(Outer);
	return World && (World->IsGameWorld()) && Super::ShouldCreateSubsystem(Outer);
}

void UHPMarkerResolver::OnWorldBeginPlay(UWorld& InWorld)
{
	Super::OnWorldBeginPlay(InWorld);
	if (InWorld.GetNetMode() != NM_Client)
	{
		const int32 Num = ResolveMarkers();
		UE_LOG(LogHPMarkers, Log, TEXT("Resolved markers: %d gameplay actors spawned"), Num);
	}
}

EHPMarkerKind UHPMarkerResolver::ParseTag(FName Tag, int32& OutCount, int32 DefaultCount)
{
	FString Name = Tag.ToString();
	OutCount = DefaultCount;
	if (!Name.StartsWith(TEXT("HP_")))
	{
		return EHPMarkerKind::Unknown;
	}
	FString Left, Right;
	if (Name.Split(TEXT(":"), &Left, &Right))
	{
		Name = Left;
		if (Right.IsNumeric())
		{
			OutCount = FMath::Max(0, FCString::Atoi(*Right));
		}
	}
	static const TMap<FString, EHPMarkerKind> Table = {
		{ TEXT("HP_PlayerStart"), EHPMarkerKind::PlayerStart },
		{ TEXT("HP_MarketPen"), EHPMarkerKind::MarketPen },
		{ TEXT("HP_CritterSpawner_Pig"), EHPMarkerKind::CritterSpawnerPig },
		{ TEXT("HP_CritterSpawner_Chicken"), EHPMarkerKind::CritterSpawnerChicken },
		{ TEXT("HP_Goods_PrizePumpkin"), EHPMarkerKind::GoodsPrizePumpkin },
		{ TEXT("HP_Goods_Crate"), EHPMarkerKind::GoodsCrate },
		{ TEXT("HP_BridgePlank"), EHPMarkerKind::BridgePlank },
		{ TEXT("HP_River"), EHPMarkerKind::River },
		{ TEXT("HP_RiverPile"), EHPMarkerKind::RiverPile },
		{ TEXT("HP_BarnRespawn"), EHPMarkerKind::BarnRespawn },
	};
	const EHPMarkerKind* Found = Table.Find(Name);
	return Found ? *Found : EHPMarkerKind::Unknown;
}

FVector UHPMarkerResolver::MarkerHalfExtent(const AActor* Marker)
{
	if (const UBoxComponent* Box = Marker->FindComponentByClass<UBoxComponent>())
	{
		return Box->GetScaledBoxExtent();
	}
	return Marker->GetActorScale3D().GetAbs() * 50.f;
}

void UHPMarkerResolver::CollectPlayerStartMarkers(const UWorld* World, TArray<AActor*>& OutMarkers)
{
	for (TActorIterator<AActor> It(const_cast<UWorld*>(World)); It; ++It)
	{
		int32 Count = 0;
		for (const FName& Tag : It->Tags)
		{
			if (ParseTag(Tag, Count) == EHPMarkerKind::PlayerStart)
			{
				OutMarkers.Add(*It);
				break;
			}
		}
	}
}

int32 UHPMarkerResolver::ResolveMarkers()
{
	UWorld* World = GetWorld();
	if (!World || World->GetNetMode() == NM_Client)
	{
		return 0;
	}
	const int32 Before = Spawned.Num();
	struct FFound { AActor* Actor; EHPMarkerKind Kind; int32 Count; };
	TArray<FFound> Found;
	for (TActorIterator<AActor> It(World); It; ++It)
	{
		AActor* A = *It;
		if (Processed.Contains(A))
		{
			continue;
		}
		for (const FName& Tag : A->Tags)
		{
			int32 Count = 1;
			const EHPMarkerKind Kind = ParseTag(Tag, Count);
			if (Kind != EHPMarkerKind::Unknown)
			{
				Found.Add({ A, Kind, Count });
				Processed.Add(A);
			}
		}
	}
	// Piles and rivers first so each river can be handed its pile.
	Found.StableSort([](const FFound& L, const FFound& R)
	{
		auto Rank = [](EHPMarkerKind K) { return K == EHPMarkerKind::RiverPile ? 0 : (K == EHPMarkerKind::River ? 1 : 2); };
		return Rank(L.Kind) < Rank(R.Kind);
	});
	for (const FFound& Entry : Found)
	{
		SpawnForMarker(Entry.Actor, Entry.Kind, Entry.Count);
	}
	// Give every river its nearest pile.
	for (const TWeakObjectPtr<AHPRiverVolume>& Weak : Rivers)
	{
		AHPRiverVolume* River = Weak.Get();
		if (!River)
		{
			continue;
		}
		float Best = TNumericLimits<float>::Max();
		for (const FVector& Pile : PileLocations)
		{
			const float D = FVector::DistSquared(Pile, River->GetActorLocation());
			if (D < Best)
			{
				Best = D;
				River->PileLocation = Pile;
				River->bHasPile = true;
			}
		}
	}
	return Spawned.Num() - Before;
}

void UHPMarkerResolver::SpawnCritterSpawner(AActor* Marker, UClass* CritterClass, int32 Count)
{
	UWorld* World = GetWorld();
	const FTransform T = Marker->GetActorTransform();
	AHPCritterSpawner* Spawner = World->SpawnActorDeferred<AHPCritterSpawner>(AHPCritterSpawner::StaticClass(), FTransform(T.GetRotation(), T.GetLocation()));
	if (!Spawner)
	{
		return;
	}
	Spawner->CritterClass = CritterClass;
	Spawner->Count = Count;
	const FVector Scale = Marker->GetActorScale3D().GetAbs();
	Spawner->Radius = FMath::Max(300.f, FMath::Max(Scale.X, Scale.Y) * 50.f);
	Spawner->FinishSpawning(FTransform(T.GetRotation(), T.GetLocation()));
	Spawned.Add(Spawner);
}

void UHPMarkerResolver::SpawnForMarker(AActor* Marker, EHPMarkerKind Kind, int32 Count)
{
	UWorld* World = GetWorld();
	const FTransform T(Marker->GetActorRotation(), Marker->GetActorLocation());
	UE_LOG(LogHPMarkers, Log, TEXT("Marker %s kind=%d count=%d at %s"), *Marker->GetName(), static_cast<int32>(Kind), Count, *T.GetLocation().ToCompactString());
	switch (Kind)
	{
	case EHPMarkerKind::PlayerStart:
		break; // consumed by AHPGameMode::ChoosePlayerStart (nothing to spawn)
	case EHPMarkerKind::MarketPen:
		if (AHPMarketPen* Pen = World->SpawnActor<AHPMarketPen>(AHPMarketPen::StaticClass(), T))
		{
			Pen->SaleVolume->SetBoxExtent(MarkerHalfExtent(Marker));
			Spawned.Add(Pen);
		}
		break;
	case EHPMarkerKind::CritterSpawnerPig:
		SpawnCritterSpawner(Marker, AHPCritter_Pig::StaticClass(), Count);
		break;
	case EHPMarkerKind::CritterSpawnerChicken:
		SpawnCritterSpawner(Marker, AHPCritter_Chicken::StaticClass(), Count);
		break;
	case EHPMarkerKind::GoodsPrizePumpkin:
	case EHPMarkerKind::GoodsCrate:
		if (AHPGoodsSpawner* Goods = World->SpawnActorDeferred<AHPGoodsSpawner>(AHPGoodsSpawner::StaticClass(), T))
		{
			Goods->Kind = Kind == EHPMarkerKind::GoodsPrizePumpkin ? EHPGoodsKind::PrizePumpkin : EHPGoodsKind::Crate;
			Goods->Count = 1;
			Goods->FinishSpawning(T);
			Spawned.Add(Goods);
		}
		break;
	case EHPMarkerKind::BridgePlank:
		if (AHPBridgePlank* Plank = World->SpawnActor<AHPBridgePlank>(AHPBridgePlank::StaticClass(), T))
		{
			Plank->BindToMeshActor(Marker);
			Spawned.Add(Plank);
		}
		break;
	case EHPMarkerKind::River:
		if (AHPRiverVolume* River = World->SpawnActor<AHPRiverVolume>(AHPRiverVolume::StaticClass(), T))
		{
			River->Volume->SetBoxExtent(MarkerHalfExtent(Marker));
			Rivers.Add(River);
			Spawned.Add(River);
		}
		break;
	case EHPMarkerKind::RiverPile:
		PileLocations.Add(Marker->GetActorLocation());
		break;
	case EHPMarkerKind::BarnRespawn:
		if (AHPBarnRespawn* Barn = World->SpawnActor<AHPBarnRespawn>(AHPBarnRespawn::StaticClass(), T))
		{
			Spawned.Add(Barn);
		}
		break;
	default:
		break;
	}
}
