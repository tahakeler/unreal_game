// Copyright HARVEST PANIC team. All Rights Reserved.
// Character customization: shared data types (loadout, catalog entries, emotes).
// See docs/customization.md for the full feature description.
#pragma once

#include "CoreMinimal.h"
#include "HPCosmeticTypes.generated.h"

class UStaticMesh;
class UAnimSequenceBase;

/** Which template mannequin body the farmer uses. */
UENUM(BlueprintType)
enum class EHPBodyPreset : uint8
{
	Manny,
	Quinn,
	MAX UMETA(Hidden)
};

/** Attachment slot a catalog item occupies. One item per slot. */
UENUM(BlueprintType)
enum class EHPCosmeticSlot : uint8
{
	Hat,
	Face,
	Back,
	MAX UMETA(Hidden)
};

/** Where a cosmetic part gets its tint from. */
UENUM(BlueprintType)
enum class EHPPartColorSource : uint8
{
	/** Keep the mesh's own material (used for Emberlight art meshes). */
	MeshMaterial,
	/** Fixed colour from FHPCosmeticPart::Tint. */
	Fixed,
	/** Loadout primary (overalls) colour. */
	Primary,
	/** Loadout secondary (shirt) colour. */
	Secondary,
	/** Loadout accent (boots/gloves) colour. */
	Accent,
	/** Loadout skin tone. */
	Skin
};

/** Emotes on keys 5..8. Index into UHPCosmeticCatalog::Emotes. */
UENUM(BlueprintType)
enum class EHPEmote : uint8
{
	Wave,
	Point,
	Dance,
	Faint,
	MAX UMETA(Hidden)
};

/**
 * The full replicated cosmetic choice of one player (GDD character-customization:
 * "FFarmerLoadout <= 32 bytes"). Colours are indices into the fixed 24-swatch
 * palette; items are stable uint16 catalog NetIds (0 = None). 12 bytes of payload.
 * Always run through UHPCosmeticCatalog::SanitizeLoadout before applying
 * anything received from the network or disk.
 */
USTRUCT(BlueprintType)
struct HARVESTPANIC_API FHPCosmeticLoadout
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, SaveGame, Category="Cosmetics")
	EHPBodyPreset BodyPreset = EHPBodyPreset::Manny;

	/** Palette index: overalls. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, SaveGame, Category="Cosmetics")
	uint8 PrimaryColor = 0;

	/** Palette index: shirt. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, SaveGame, Category="Cosmetics")
	uint8 SecondaryColor = 1;

	/** Palette index: boots/gloves accent (also tints some accessory parts). */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, SaveGame, Category="Cosmetics")
	uint8 AccentColor = 2;

	/** Index into catalog SkinTones. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, SaveGame, Category="Cosmetics")
	uint8 SkinTone = 0;

	/** Catalog item NetIds; 0 = None. */
	UPROPERTY(EditAnywhere, SaveGame, Category="Cosmetics")
	uint16 Hat = 0;

	UPROPERTY(EditAnywhere, SaveGame, Category="Cosmetics")
	uint16 Face = 0;

	UPROPERTY(EditAnywhere, SaveGame, Category="Cosmetics")
	uint16 Back = 0;

	/** Palette index for the overhead name tag. */
	UPROPERTY(EditAnywhere, BlueprintReadWrite, SaveGame, Category="Cosmetics")
	uint8 NameTagColor = 0;

	uint16 GetSlotItem(EHPCosmeticSlot Slot) const;
	void SetSlotItem(EHPCosmeticSlot Slot, uint16 NetId);

	bool operator==(const FHPCosmeticLoadout& O) const
	{
		return BodyPreset == O.BodyPreset && PrimaryColor == O.PrimaryColor && SecondaryColor == O.SecondaryColor
			&& AccentColor == O.AccentColor && SkinTone == O.SkinTone && Hat == O.Hat && Face == O.Face
			&& Back == O.Back && NameTagColor == O.NameTagColor;
	}
	bool operator!=(const FHPCosmeticLoadout& O) const { return !(*this == O); }

	/** Compact custom wire format (exactly 12 bytes). */
	bool NetSerialize(FArchive& Ar, class UPackageMap* Map, bool& bOutSuccess);

	FString ToDebugString() const;
};

template<>
struct TStructOpsTypeTraits<FHPCosmeticLoadout> : public TStructOpsTypeTraitsBase2<FHPCosmeticLoadout>
{
	enum { WithNetSerializer = true, WithIdenticalViaEquality = true };
};

/** One mesh piece of a cosmetic item. Items are built from 1..N parts. */
USTRUCT(BlueprintType)
struct HARVESTPANIC_API FHPCosmeticPart
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	TSoftObjectPtr<UStaticMesh> Mesh;

	/** Bone/socket on the template mannequin skeleton. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	FName Bone = TEXT("head");

	/**
	 * Offset relative to the bone origin, expressed in MESH COMPONENT axes
	 * (mannequin: +Z up, +Y forward/face, +X character's left). Converted to
	 * bone space with the reference pose so designers never deal with bone axes.
	 */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	FVector Offset = FVector::ZeroVector;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	FRotator Rotation = FRotator::ZeroRotator;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	FVector Scale = FVector::OneVector;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	EHPPartColorSource ColorSource = EHPPartColorSource::Fixed;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	FLinearColor Tint = FLinearColor::White;
};

/** A selectable item (hat, face accessory, back item). */
USTRUCT(BlueprintType)
struct HARVESTPANIC_API FHPCosmeticItem
{
	GENERATED_BODY()

	/** Stable network/save id. Never reuse or renumber; 0 is reserved for None. */
	UPROPERTY(EditAnywhere, Category="Cosmetics")
	uint16 NetId = 0;

	/** Readable id for logs/tests. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	FName Id = NAME_None;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	FText DisplayName;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	EHPCosmeticSlot Slot = EHPCosmeticSlot::Hat;

	/** Meta-progression hook: locked items are rejected by SanitizeLoadout. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	bool bUnlocked = true;

	/** Weight for "Surprise me" / first-time random default. 0 = never rolled. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	float RandomWeight = 1.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	TArray<FHPCosmeticPart> Parts;
};

USTRUCT(BlueprintType)
struct HARVESTPANIC_API FHPNamedColor
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	FText DisplayName;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	FLinearColor Color = FLinearColor::White;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Cosmetics")
	bool bUnlocked = true;
};

/** Emote tuning. Template anim if it loads, else purely procedural motion. */
USTRUCT(BlueprintType)
struct HARVESTPANIC_API FHPEmoteDef
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Emote")
	FText DisplayName;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Emote")
	TSoftObjectPtr<UAnimSequenceBase> Anim;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Emote")
	float PlayRate = 1.f;

	/** Seconds the emote lasts before the normal anim blueprint resumes. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Emote")
	float Duration = 2.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Emote")
	bool bLoopAnim = false;

	/** Faint: request a real ragdoll on the pawn via reflection (see docs/customization.md). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Emote")
	bool bRagdoll = false;

	/** Hold the last anim frame instead of looping (fall pose fallback). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Emote")
	bool bHoldLastFrame = false;

	/** Per-emote cooldown (s). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Emote")
	float Cooldown = 1.5f;

	/** Procedural layer: mesh yaw spin (deg/s) — the Dance. */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Emote")
	float SpinDegPerSec = 0.f;

	/** Procedural layer: vertical bob amplitude (cm) and frequency (Hz). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Emote")
	float BobAmplitude = 0.f;

	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Emote")
	float BobFrequency = 2.f;

	/** Procedural layer: side-to-side roll wobble (deg). */
	UPROPERTY(EditAnywhere, BlueprintReadOnly, Category="Emote")
	float WobbleDegrees = 0.f;
};
