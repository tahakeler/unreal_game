// Copyright HARVEST PANIC team. All Rights Reserved.
// GDD: design/gdd/character-customization.md (VS catalogue, 24-swatch palette, unlock gate).

#include "HPCosmeticCatalog.h"
#include "Engine/StaticMesh.h"
#include "Engine/SkeletalMesh.h"
#include "Animation/AnimSequenceBase.h"
#include "Materials/MaterialInterface.h"
#include "Serialization/Archive.h"

#define LOCTEXT_NAMESPACE "HPCosmetics"

// ---------------------------------------------------------------- loadout helpers

uint16 FHPCosmeticLoadout::GetSlotItem(EHPCosmeticSlot Slot) const
{
	switch (Slot)
	{
	case EHPCosmeticSlot::Hat:  return Hat;
	case EHPCosmeticSlot::Face: return Face;
	case EHPCosmeticSlot::Back: return Back;
	default: return 0;
	}
}

void FHPCosmeticLoadout::SetSlotItem(EHPCosmeticSlot Slot, uint16 NetId)
{
	switch (Slot)
	{
	case EHPCosmeticSlot::Hat:  Hat = NetId; break;
	case EHPCosmeticSlot::Face: Face = NetId; break;
	case EHPCosmeticSlot::Back: Back = NetId; break;
	default: break;
	}
}

bool FHPCosmeticLoadout::NetSerialize(FArchive& Ar, UPackageMap* Map, bool& bOutSuccess)
{
	uint8 Body = static_cast<uint8>(BodyPreset);
	Ar << Body << PrimaryColor << SecondaryColor << AccentColor << SkinTone << Hat << Face << Back << NameTagColor;
	if (Ar.IsLoading())
	{
		// Out-of-range enum is clamped here; everything else is sanitized by the catalog.
		BodyPreset = Body < static_cast<uint8>(EHPBodyPreset::MAX) ? static_cast<EHPBodyPreset>(Body) : EHPBodyPreset::Manny;
	}
	bOutSuccess = !Ar.IsError();
	return true;
}

FString FHPCosmeticLoadout::ToDebugString() const
{
	return FString::Printf(TEXT("Body=%d Col=%d/%d/%d Skin=%d Hat=%d Face=%d Back=%d Tag=%d"),
		(int32)BodyPreset, PrimaryColor, SecondaryColor, AccentColor, SkinTone, Hat, Face, Back, NameTagColor);
}

// ---------------------------------------------------------------- catalog

namespace HPCatalogDefaults
{
	static const TCHAR* Sphere   = TEXT("/Engine/BasicShapes/Sphere.Sphere");
	static const TCHAR* Cube     = TEXT("/Engine/BasicShapes/Cube.Cube");
	static const TCHAR* Cylinder = TEXT("/Engine/BasicShapes/Cylinder.Cylinder");
	static const TCHAR* Cone     = TEXT("/Engine/BasicShapes/Cone.Cone");

	static FHPCosmeticPart Part(const TCHAR* MeshPath, FName Bone, FVector Offset, FVector Scale,
		EHPPartColorSource Src, FLinearColor Tint = FLinearColor::White, FRotator Rot = FRotator::ZeroRotator)
	{
		FHPCosmeticPart P;
		P.Mesh = TSoftObjectPtr<UStaticMesh>(FSoftObjectPath(MeshPath));
		P.Bone = Bone;
		P.Offset = Offset;
		P.Scale = Scale;
		P.Rotation = Rot;
		P.ColorSource = Src;
		P.Tint = Tint;
		return P;
	}

	static FHPNamedColor Col(const TCHAR* Name, uint8 R, uint8 G, uint8 B, bool bUnlocked = true)
	{
		FHPNamedColor C;
		C.DisplayName = FText::FromString(Name);
		C.Color = FLinearColor(FColor(R, G, B));
		C.bUnlocked = bUnlocked;
		return C;
	}
}

UHPCosmeticCatalog::UHPCosmeticCatalog()
{
	BuildDefaults();
}

void UHPCosmeticCatalog::BuildDefaults()
{
	using namespace HPCatalogDefaults;
	const FName Head(TEXT("head"));
	const FName Chest(TEXT("spine_05"));
	const EHPPartColorSource Fixed = EHPPartColorSource::Fixed;
	const EHPPartColorSource Own = EHPPartColorSource::MeshMaterial;

	MannyMesh = TSoftObjectPtr<USkeletalMesh>(FSoftObjectPath(TEXT("/Game/Characters/Mannequins/Meshes/SKM_Manny_Simple.SKM_Manny_Simple")));
	QuinnMesh = TSoftObjectPtr<USkeletalMesh>(FSoftObjectPath(TEXT("/Game/Characters/Mannequins/Meshes/SKM_Quinn_Simple.SKM_Quinn_Simple")));
	BodyColorParams = { TEXT("Paint Tint") };
	PartTintMaterial = TSoftObjectPtr<UMaterialInterface>(FSoftObjectPath(TEXT("/Engine/BasicShapes/BasicShapeMaterial.BasicShapeMaterial")));

	// ---- 24-swatch palette (GDD: no free RGB, readable on the farm) ----
	Palette = {
		Col(TEXT("Barn Red"), 168, 32, 28),     Col(TEXT("Tomato"), 235, 72, 45),       Col(TEXT("Pumpkin"), 245, 130, 20),
		Col(TEXT("Mustard"), 230, 180, 30),     Col(TEXT("Butter"), 250, 228, 120),     Col(TEXT("Lime"), 150, 210, 50),
		Col(TEXT("Clover"), 40, 150, 60),       Col(TEXT("Moss"), 85, 105, 45),         Col(TEXT("Teal"), 20, 140, 135),
		Col(TEXT("Sky"), 110, 185, 240),        Col(TEXT("Denim"), 45, 85, 160),        Col(TEXT("Navy"), 25, 35, 85),
		Col(TEXT("Lavender"), 170, 140, 220),   Col(TEXT("Plum"), 110, 40, 120),        Col(TEXT("Bubblegum"), 245, 120, 185),
		Col(TEXT("Rose"), 200, 70, 100),        Col(TEXT("Chocolate"), 90, 55, 35),     Col(TEXT("Saddle"), 150, 95, 50),
		Col(TEXT("Wheat"), 220, 195, 140),      Col(TEXT("Cream"), 245, 240, 225),      Col(TEXT("Stone"), 150, 150, 145),
		Col(TEXT("Charcoal"), 55, 55, 60),      Col(TEXT("Soot"), 20, 20, 22),          Col(TEXT("Hi-Vis"), 210, 255, 30)
	};

	// ---- skin: 12 base (always free) + 2 fantasy (Grandpa's Letters) ----
	SkinTones = {
		Col(TEXT("Porcelain"), 255, 224, 200), Col(TEXT("Ivory"), 241, 204, 170),  Col(TEXT("Peach"), 232, 185, 150),
		Col(TEXT("Sand"), 222, 170, 125),      Col(TEXT("Honey"), 205, 150, 100),  Col(TEXT("Olive"), 190, 145, 95),
		Col(TEXT("Amber"), 175, 120, 75),      Col(TEXT("Caramel"), 155, 100, 60), Col(TEXT("Bronze"), 130, 85, 50),
		Col(TEXT("Mocha"), 105, 70, 45),       Col(TEXT("Cocoa"), 80, 52, 35),     Col(TEXT("Ebony"), 58, 38, 28),
		Col(TEXT("Swamp Green"), 110, 190, 90, false), Col(TEXT("Frostbite Blue"), 110, 160, 230, false)
	};

	const FLinearColor Orange(FColor(245, 110, 15));
	const FLinearColor White(FColor(245, 245, 240));
	const FLinearColor Black(FColor(15, 15, 18));
	const FLinearColor Metal(FColor(140, 145, 150));
	const FLinearColor Brown(FColor(105, 65, 35));
	const FLinearColor DarkBrown(FColor(55, 35, 25));
	const FLinearColor Ivory(FColor(240, 230, 200));
	const FLinearColor Green(FColor(40, 120, 35));

	auto Item = [this](uint16 NetId, const TCHAR* Id, FText Name, EHPCosmeticSlot Slot, bool bUnlocked, TArray<FHPCosmeticPart> Parts, float Weight = 1.f)
	{
		FHPCosmeticItem I;
		I.NetId = NetId; I.Id = Id; I.DisplayName = Name; I.Slot = Slot; I.bUnlocked = bUnlocked;
		I.RandomWeight = Weight; I.Parts = MoveTemp(Parts);
		Items.Add(MoveTemp(I));
	};
	const EHPCosmeticSlot H = EHPCosmeticSlot::Hat, F = EHPCosmeticSlot::Face, B = EHPCosmeticSlot::Back;

	// NetIds are STABLE (save files + network). 1xx hats, 2xx face, 3xx back. Never renumber.
	// Offsets are in mesh-component axes relative to the bone: +Z up, +Y face-forward.
	// ---- Hats ----
	Item(101, TEXT("StrawHat"), LOCTEXT("StrawHat", "Straw Hat"), H, true, {
		Part(TEXT("/Game/Emberlight/Meshes/Props/SM_Strawhat.SM_Strawhat"), Head, FVector(0, 1, 19), FVector(1.0f), Own) }, 1.5f);
	Item(102, TEXT("TruckerCap"), LOCTEXT("TruckerCap", "Trucker Cap"), H, true, {
		Part(Sphere, Head, FVector(0, 1, 19), FVector(0.25f, 0.25f, 0.17f), EHPPartColorSource::Secondary),
		Part(Cube, Head, FVector(0, 13, 18), FVector(0.2f, 0.14f, 0.015f), EHPPartColorSource::Accent) });
	Item(103, TEXT("BucketHelmet"), LOCTEXT("BucketHelmet", "Bucket Helmet"), H, false, {
		Part(Cylinder, Head, FVector(0, 1, 17), FVector(0.27f, 0.27f, 0.26f), Fixed, Metal),
		Part(Cylinder, Head, FVector(0, 1, 5), FVector(0.29f, 0.29f, 0.02f), Fixed, Metal * 0.7f),
		Part(Cube, Head, FVector(0, 1, 31), FVector(0.3f, 0.015f, 0.015f), Fixed, DarkBrown) });
	Item(104, TEXT("PumpkinHead"), LOCTEXT("PumpkinHead", "Pumpkin Head"), H, false, {
		Part(Sphere, Head, FVector(0, 2, 11), FVector(0.38f, 0.36f, 0.32f), Fixed, Orange),
		Part(Cylinder, Head, FVector(0, 2, 29), FVector(0.05f, 0.05f, 0.08f), Fixed, Green),
		Part(Cone, Head, FVector(-5.5f, 19, 15), FVector(0.05f, 0.03f, 0.05f), Fixed, Black, FRotator(0, 0, -90)),
		Part(Cone, Head, FVector(5.5f, 19, 15), FVector(0.05f, 0.03f, 0.05f), Fixed, Black, FRotator(0, 0, -90)),
		Part(Cube, Head, FVector(0, 19, 6), FVector(0.12f, 0.03f, 0.025f), Fixed, Black) }, 1.5f);
	Item(105, TEXT("CowboyHat"), LOCTEXT("CowboyHat", "Cowboy Hat"), H, true, {
		Part(Cylinder, Head, FVector(0, 1, 19), FVector(0.52f, 0.5f, 0.02f), Fixed, Brown),
		Part(Cylinder, Head, FVector(0, 1, 26), FVector(0.24f, 0.26f, 0.14f), Fixed, Brown),
		Part(Cylinder, Head, FVector(0, 1, 21.5f), FVector(0.245f, 0.265f, 0.03f), EHPPartColorSource::Accent) });
	Item(106, TEXT("ChefToque"), LOCTEXT("ChefToque", "Chef Toque"), H, true, {
		Part(Cylinder, Head, FVector(0, 0, 28), FVector(0.24f, 0.24f, 0.24f), Fixed, White),
		Part(Sphere, Head, FVector(0, 0, 41), FVector(0.33f, 0.33f, 0.2f), Fixed, White) });
	{
		TArray<FHPCosmeticPart> Flowers;
		const FLinearColor Petals[] = { FLinearColor(FColor(255, 120, 180)), FLinearColor(FColor(255, 220, 60)), White,
			FLinearColor(FColor(170, 120, 255)), FLinearColor(FColor(255, 90, 70)) };
		for (int32 i = 0; i < 10; ++i)
		{
			const float A = i * 2.f * PI / 10.f;
			Flowers.Add(Part(Sphere, Head, FVector(FMath::Cos(A) * 11.f, 1.f + FMath::Sin(A) * 11.f, 17.f), FVector(0.065f), Fixed, Petals[i % 5]));
		}
		Flowers.Add(Part(Cylinder, Head, FVector(0, 1, 16.5f), FVector(0.23f, 0.23f, 0.015f), Fixed, Green));
		Item(107, TEXT("FlowerCrown"), LOCTEXT("FlowerCrown", "Flower Crown"), H, true, MoveTemp(Flowers));
	}
	Item(108, TEXT("TrafficCone"), LOCTEXT("TrafficCone", "Traffic Cone"), H, false, {
		Part(Cone, Head, FVector(0, 0, 40), FVector(0.3f, 0.3f, 0.55f), Fixed, Orange),
		Part(Cylinder, Head, FVector(0, 0, 42), FVector(0.2f, 0.2f, 0.07f), Fixed, White),
		Part(Cube, Head, FVector(0, 0, 13.5f), FVector(0.36f, 0.36f, 0.03f), Fixed, Orange) }, 1.5f);
	Item(109, TEXT("VikingHorns"), LOCTEXT("VikingHorns", "Viking Horns"), H, true, {
		Part(Sphere, Head, FVector(0, 1, 16), FVector(0.26f, 0.26f, 0.22f), Fixed, Metal),
		Part(Cone, Head, FVector(15, 1, 25), FVector(0.07f, 0.07f, 0.22f), Fixed, Ivory, FRotator(-40, 0, 0)),
		Part(Cone, Head, FVector(-15, 1, 25), FVector(0.07f, 0.07f, 0.22f), Fixed, Ivory, FRotator(40, 0, 0)) });
	Item(110, TEXT("GrandpasFedora"), LOCTEXT("GrandpasFedora", "Grandpa's Fedora"), H, false, {
		Part(Cylinder, Head, FVector(0, 1, 20), FVector(0.38f, 0.38f, 0.015f), Fixed, FLinearColor(FColor(70, 60, 50))),
		Part(Cylinder, Head, FVector(0, 1, 26), FVector(0.23f, 0.25f, 0.13f), Fixed, FLinearColor(FColor(70, 60, 50))),
		Part(Cylinder, Head, FVector(0, 1, 22), FVector(0.235f, 0.255f, 0.03f), Fixed, Black) });
	Item(111, TEXT("CarrotAntenna"), LOCTEXT("CarrotAntenna", "Carrot Antenna"), H, true, {
		Part(TEXT("/Game/Emberlight/Meshes/Fruits/SM_Carrot.SM_Carrot"), Head, FVector(0, 0, 18), FVector(1.0f), Own) }, 0.7f);

	// ---- Face ----
	Item(201, TEXT("Moustache"), LOCTEXT("Moustache", "Moustache"), F, true, {
		Part(Cube, Head, FVector(0, 12.5f, 3.5f), FVector(0.11f, 0.03f, 0.03f), Fixed, DarkBrown),
		Part(Sphere, Head, FVector(6, 12, 2.5f), FVector(0.045f), Fixed, DarkBrown),
		Part(Sphere, Head, FVector(-6, 12, 2.5f), FVector(0.045f), Fixed, DarkBrown) });
	Item(202, TEXT("Goggles"), LOCTEXT("Goggles", "Goggles"), F, true, {
		Part(Cylinder, Head, FVector(4, 12, 9), FVector(0.075f, 0.075f, 0.03f), EHPPartColorSource::Accent, FLinearColor::White, FRotator(0, 0, 90)),
		Part(Cylinder, Head, FVector(-4, 12, 9), FVector(0.075f, 0.075f, 0.03f), EHPPartColorSource::Accent, FLinearColor::White, FRotator(0, 0, 90)),
		Part(Cylinder, Head, FVector(4, 13.2f, 9), FVector(0.055f, 0.055f, 0.01f), Fixed, FLinearColor(0.4f, 0.8f, 1.f), FRotator(0, 0, 90)),
		Part(Cylinder, Head, FVector(-4, 13.2f, 9), FVector(0.055f, 0.055f, 0.01f), Fixed, FLinearColor(0.4f, 0.8f, 1.f), FRotator(0, 0, 90)),
		Part(Cylinder, Head, FVector(0, 1, 9), FVector(0.235f, 0.235f, 0.025f), Fixed, Black) });
	Item(203, TEXT("Sunglasses"), LOCTEXT("Sunglasses", "Sunglasses"), F, true, {
		Part(Cube, Head, FVector(3.8f, 12.5f, 9), FVector(0.06f, 0.01f, 0.04f), Fixed, Black),
		Part(Cube, Head, FVector(-3.8f, 12.5f, 9), FVector(0.06f, 0.01f, 0.04f), Fixed, Black),
		Part(Cube, Head, FVector(0, 12.6f, 10), FVector(0.03f, 0.01f, 0.008f), Fixed, Black) });
	Item(204, TEXT("ClownNose"), LOCTEXT("ClownNose", "Clown Nose"), F, true, {
		Part(Sphere, Head, FVector(0, 13, 6), FVector(0.055f), Fixed, FLinearColor(1.f, 0.02f, 0.02f)) }, 0.7f);

	// ---- Back ----
	Item(301, TEXT("BackpackBasket"), LOCTEXT("BackpackBasket", "Backpack Basket"), B, true, {
		Part(TEXT("/Game/Emberlight/Meshes/Props/SM_Harvest_Basket.SM_Harvest_Basket"), Chest, FVector(0, -24, -6), FVector(0.6f), Own),
		Part(Cube, Chest, FVector(8, -8, 0), FVector(0.03f, 0.03f, 0.3f), Fixed, DarkBrown),
		Part(Cube, Chest, FVector(-8, -8, 0), FVector(0.03f, 0.03f, 0.3f), Fixed, DarkBrown) });
	Item(302, TEXT("Cape"), LOCTEXT("Cape", "Cape"), B, true, {
		Part(Cube, Chest, FVector(0, -16, -32), FVector(0.42f, 0.015f, 0.75f), EHPPartColorSource::Primary, FLinearColor::White, FRotator(0, 0, 8)),
		Part(Cylinder, Chest, FVector(0, -2, 10), FVector(0.3f, 0.28f, 0.03f), EHPPartColorSource::Accent) });
	Item(303, TEXT("PitchforkSheath"), LOCTEXT("PitchforkSheath", "Pitchfork Holster"), B, true, {
		Part(TEXT("/Game/Emberlight/Meshes/Props/SM_Pitchfork.SM_Pitchfork"), Chest, FVector(0, -16, 0), FVector(0.8f), Own, FLinearColor::White, FRotator(0, 0, 20)) }, 0.7f);

	// ---- Emotes (indexed by EHPEmote: 5 Wave / 6 Point / 7 Dance / 8 Faint) ----
	auto Emote = [this](const TCHAR* Name, const TCHAR* Anim, float Rate, float Duration, float Cooldown)
	{
		FHPEmoteDef E;
		E.DisplayName = FText::FromString(Name);
		E.Anim = TSoftObjectPtr<UAnimSequenceBase>(FSoftObjectPath(Anim));
		E.PlayRate = Rate; E.Duration = Duration; E.Cooldown = Cooldown;
		return E;
	};
	FHPEmoteDef Wave = Emote(TEXT("Wave"), TEXT("/Game/Characters/Mannequins/Anims/Unarmed/Attack/MM_Attack_02.MM_Attack_02"), 0.55f, 1.8f, 1.5f);
	Wave.bLoopAnim = true; Wave.WobbleDegrees = 4.f; Wave.BobFrequency = 1.5f;
	FHPEmoteDef Point = Emote(TEXT("Point"), TEXT("/Game/Characters/Mannequins/Anims/Unarmed/Attack/MM_Attack_01.MM_Attack_01"), 0.8f, 1.6f, 1.5f);
	Point.bHoldLastFrame = true;
	FHPEmoteDef Dance = Emote(TEXT("Dance"), TEXT("/Game/Characters/Mannequins/Anims/Unarmed/Jump/MM_Land.MM_Land"), 1.6f, 3.0f, 3.0f);
	Dance.bLoopAnim = true; Dance.SpinDegPerSec = 360.f; Dance.BobAmplitude = 7.f; Dance.BobFrequency = 2.5f; Dance.WobbleDegrees = 10.f;
	FHPEmoteDef Faint = Emote(TEXT("Faint"), TEXT("/Game/Characters/Mannequins/Anims/Death/MM_Death_Back_01.MM_Death_Back_01"), 1.3f, 2.0f, 5.0f);
	Faint.bHoldLastFrame = true; Faint.bRagdoll = true;
	Emotes = { Wave, Point, Dance, Faint };
}

const UHPCosmeticCatalog* UHPCosmeticCatalog::Get()
{
	static TWeakObjectPtr<const UHPCosmeticCatalog> Cached;
	static bool bTriedOverride = false;
	if (Cached.IsValid())
	{
		return Cached.Get();
	}
	if (!bTriedOverride)
	{
		bTriedOverride = true;
		if (FPackageName::DoesPackageExist(TEXT("/Game/HarvestPanic/Customization/DA_HPCosmeticCatalog")))
		{
			if (const UHPCosmeticCatalog* Asset = LoadObject<UHPCosmeticCatalog>(nullptr, OverrideAssetPath))
			{
				const_cast<UHPCosmeticCatalog*>(Asset)->AddToRoot();
				Cached = Asset;
				return Asset;
			}
		}
	}
	Cached = GetDefault<UHPCosmeticCatalog>();
	return Cached.Get();
}

const FHPCosmeticItem* UHPCosmeticCatalog::FindItem(uint16 NetId) const
{
	return NetId == 0 ? nullptr : Items.FindByPredicate([NetId](const FHPCosmeticItem& I) { return I.NetId == NetId; });
}

const FHPCosmeticItem* UHPCosmeticCatalog::FindItemByName(FName Id) const
{
	return Items.FindByPredicate([Id](const FHPCosmeticItem& I) { return I.Id == Id; });
}

TArray<const FHPCosmeticItem*> UHPCosmeticCatalog::GetItemsForSlot(EHPCosmeticSlot Slot, bool bUnlockedOnly) const
{
	TArray<const FHPCosmeticItem*> Out;
	for (const FHPCosmeticItem& I : Items)
	{
		if (I.Slot == Slot && (!bUnlockedOnly || IsItemUnlocked(I)))
		{
			Out.Add(&I);
		}
	}
	return Out;
}

FLinearColor UHPCosmeticCatalog::GetPaletteColor(uint8 Index) const
{
	return Palette.IsValidIndex(Index) ? Palette[Index].Color : FLinearColor::White;
}

FLinearColor UHPCosmeticCatalog::GetSkinTone(uint8 Index) const
{
	return SkinTones.IsValidIndex(Index) ? SkinTones[Index].Color : FLinearColor(0.8f, 0.6f, 0.45f);
}

const FHPEmoteDef* UHPCosmeticCatalog::GetEmote(EHPEmote Emote) const
{
	const int32 Idx = static_cast<int32>(Emote);
	return Emotes.IsValidIndex(Idx) ? &Emotes[Idx] : nullptr;
}

TSoftObjectPtr<USkeletalMesh> UHPCosmeticCatalog::GetBodyMesh(EHPBodyPreset Preset) const
{
	return Preset == EHPBodyPreset::Quinn ? QuinnMesh : MannyMesh;
}

bool UHPCosmeticCatalog::IsPaletteIndexValid(const TArray<FHPNamedColor>& InPalette, uint8 Index) const
{
	return InPalette.IsValidIndex(Index) && IsColorUnlocked(InPalette[Index]);
}

bool UHPCosmeticCatalog::IsLoadoutValid(const FHPCosmeticLoadout& L) const
{
	FHPCosmeticLoadout Copy = L;
	return !SanitizeLoadout(Copy);
}

bool UHPCosmeticCatalog::SanitizeLoadout(FHPCosmeticLoadout& L) const
{
	bool bChanged = false;
	if (static_cast<uint8>(L.BodyPreset) >= static_cast<uint8>(EHPBodyPreset::MAX))
	{
		L.BodyPreset = EHPBodyPreset::Manny; bChanged = true;
	}
	auto FixColor = [&](uint8& Idx, const TArray<FHPNamedColor>& Pal, uint8 Fallback)
	{
		if (!IsPaletteIndexValid(Pal, Idx)) { Idx = Fallback; bChanged = true; }
	};
	FixColor(L.PrimaryColor, Palette, 10);
	FixColor(L.SecondaryColor, Palette, 0);
	FixColor(L.AccentColor, Palette, 17);
	FixColor(L.SkinTone, SkinTones, 3);
	FixColor(L.NameTagColor, Palette, 4);

	for (uint8 S = 0; S < static_cast<uint8>(EHPCosmeticSlot::MAX); ++S)
	{
		const EHPCosmeticSlot Slot = static_cast<EHPCosmeticSlot>(S);
		const uint16 Id = L.GetSlotItem(Slot);
		if (Id == 0)
		{
			continue;
		}
		const FHPCosmeticItem* Item = FindItem(Id);
		if (!Item || Item->Slot != Slot || !IsItemUnlocked(*Item))
		{
			L.SetSlotItem(Slot, 0);
			bChanged = true;
		}
	}
	return bChanged;
}

uint16 UHPCosmeticCatalog::PickWeighted(EHPCosmeticSlot Slot, FRandomStream& Stream) const
{
	// "None" participates with a small weight so random farmers are usually dressed up.
	constexpr float NoneWeight = 0.35f;
	const TArray<const FHPCosmeticItem*> Options = GetItemsForSlot(Slot, true);
	float Total = NoneWeight;
	for (const FHPCosmeticItem* I : Options) { Total += FMath::Max(0.f, I->RandomWeight); }
	float Roll = Stream.FRandRange(0.f, Total);
	for (const FHPCosmeticItem* I : Options)
	{
		Roll -= FMath::Max(0.f, I->RandomWeight);
		if (Roll <= 0.f && I->RandomWeight > 0.f)
		{
			return I->NetId;
		}
	}
	return 0;
}

uint8 UHPCosmeticCatalog::PickUnlockedIndex(const TArray<FHPNamedColor>& InPalette, FRandomStream& Stream) const
{
	TArray<uint8> Valid;
	for (int32 i = 0; i < InPalette.Num() && i < 256; ++i)
	{
		if (IsColorUnlocked(InPalette[i])) { Valid.Add(static_cast<uint8>(i)); }
	}
	return Valid.Num() ? Valid[Stream.RandHelper(Valid.Num())] : 0;
}

FHPCosmeticLoadout UHPCosmeticCatalog::MakeRandomLoadout(FRandomStream& Stream) const
{
	FHPCosmeticLoadout L;
	L.BodyPreset = Stream.FRand() < 0.5f ? EHPBodyPreset::Manny : EHPBodyPreset::Quinn;
	L.PrimaryColor = PickUnlockedIndex(Palette, Stream);
	// Clashing is funnier than matching, but never pick the same swatch twice in a row.
	do { L.SecondaryColor = PickUnlockedIndex(Palette, Stream); } while (Palette.Num() > 1 && L.SecondaryColor == L.PrimaryColor);
	L.AccentColor = PickUnlockedIndex(Palette, Stream);
	// Fantasy skins stay rare even when unlocked: random only rolls the 12 base tones.
	TArray<FHPNamedColor> BaseSkins = SkinTones.FilterByPredicate([](const FHPNamedColor& C) { return C.bUnlocked; });
	L.SkinTone = BaseSkins.Num() ? static_cast<uint8>(Stream.RandHelper(BaseSkins.Num())) : 0;
	L.NameTagColor = PickUnlockedIndex(Palette, Stream);
	L.Hat = PickWeighted(EHPCosmeticSlot::Hat, Stream);
	L.Face = PickWeighted(EHPCosmeticSlot::Face, Stream);
	L.Back = PickWeighted(EHPCosmeticSlot::Back, Stream);
	SanitizeLoadout(L);
	return L;
}

#undef LOCTEXT_NAMESPACE
