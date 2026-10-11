// Copyright HARVEST PANIC team. All Rights Reserved.
// Data-driven cosmetic catalog. Defaults are authored in the C++ constructor so the
// game works with zero .uasset files; a designer can create a DA_HPCosmeticCatalog
// asset at /Game/HarvestPanic/Customization/ to override everything.
#pragma once

#include "CoreMinimal.h"
#include "Engine/DataAsset.h"
#include "HPCosmeticTypes.h"
#include "HPCosmeticCatalog.generated.h"

class USkeletalMesh;
class UMaterialInterface;

UCLASS(BlueprintType)
class HARVESTPANIC_API UHPCosmeticCatalog : public UPrimaryDataAsset
{
	GENERATED_BODY()

public:
	UHPCosmeticCatalog();

	/** Optional authored override asset path, else the class defaults are used. */
	static constexpr const TCHAR* OverrideAssetPath = TEXT("/Game/HarvestPanic/Customization/DA_HPCosmeticCatalog.DA_HPCosmeticCatalog");

	/** Active catalog (override asset if present, else CDO). Never null. */
	static const UHPCosmeticCatalog* Get();

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Bodies")
	TSoftObjectPtr<USkeletalMesh> MannyMesh;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Bodies")
	TSoftObjectPtr<USkeletalMesh> QuinnMesh;

	/** Material vector parameters written on body material slots. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Bodies")
	TArray<FName> BodyColorParams;

	/** Body material slot index that takes the primary (overalls) colour; others take secondary. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Bodies")
	int32 PrimaryMaterialSlot = 0;

	/** Mesh used to tint basic-shape parts (needs a "Color" vector param). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Parts")
	TSoftObjectPtr<UMaterialInterface> PartTintMaterial;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Parts")
	FName PartTintParam = TEXT("Color");

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Items")
	TArray<FHPCosmeticItem> Items;

	/** GDD gate: true for the vertical slice, false at ship (unlocks come from meta-progression). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Unlocks")
	bool bUnlockAllCosmetics = true;

	/** Fixed 24-swatch palette shared by primary/secondary/accent/name tag. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Colors")
	TArray<FHPNamedColor> Palette;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Colors")
	TArray<FHPNamedColor> SkinTones;


	/** Indexed by EHPEmote. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Emotes")
	TArray<FHPEmoteDef> Emotes;

	/** GDD: emote RPCs rate-limited to 2/s on the server regardless of per-emote cooldown. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Emotes", meta=(ClampMin="0"))
	float EmoteMinInterval = 0.5f;

	/** Minimum seconds between accepted Server_SetLoadout calls (anti-spam). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Network", meta=(ClampMin="0"))
	float LoadoutChangeMinInterval = 0.25f;

	/** Name tag height above the actor origin (cm) and world size. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="NameTag")
	float NameTagHeight = 118.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="NameTag")
	float NameTagWorldSize = 18.f;

	// ---- queries ----
	const FHPCosmeticItem* FindItem(uint16 NetId) const;
	const FHPCosmeticItem* FindItemByName(FName Id) const;
	bool IsItemUnlocked(const FHPCosmeticItem& Item) const { return bUnlockAllCosmetics || Item.bUnlocked; }
	bool IsColorUnlocked(const FHPNamedColor& C) const { return bUnlockAllCosmetics || C.bUnlocked; }
	/** Items for a slot, in catalog order. Includes the "None" entry. */
	TArray<const FHPCosmeticItem*> GetItemsForSlot(EHPCosmeticSlot Slot, bool bUnlockedOnly = true) const;
	FLinearColor GetPaletteColor(uint8 Index) const;
	FLinearColor GetSkinTone(uint8 Index) const;
	const FHPEmoteDef* GetEmote(EHPEmote Emote) const;
	TSoftObjectPtr<USkeletalMesh> GetBodyMesh(EHPBodyPreset Preset) const;

	/** True if every field references an existing, unlocked catalog entry in the right slot. */
	bool IsLoadoutValid(const FHPCosmeticLoadout& Loadout) const;

	/** Replace every invalid/locked field with a safe default. Returns true if anything changed. */
	bool SanitizeLoadout(FHPCosmeticLoadout& InOut) const;

	/** Weighted silly random loadout ("Surprise me" + first-time default). Always valid. */
	FHPCosmeticLoadout MakeRandomLoadout(FRandomStream& Stream) const;


private:
	void BuildDefaults();
	uint16 PickWeighted(EHPCosmeticSlot Slot, FRandomStream& Stream) const;
	uint8 PickUnlockedIndex(const TArray<FHPNamedColor>& InPalette, FRandomStream& Stream) const;
	bool IsPaletteIndexValid(const TArray<FHPNamedColor>& InPalette, uint8 Index) const;
};
