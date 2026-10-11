// Copyright HARVEST PANIC team. All Rights Reserved.
// Stateless helpers that turn an FHPCosmeticLoadout into components on a body mesh.
// Used by the replicated UHPCosmeticsComponent (real pawns) and by the preview actor.
#pragma once

#include "CoreMinimal.h"
#include "HPCosmeticTypes.h"

class USkeletalMeshComponent;
class UMeshComponent;
class UTextRenderComponent;
class APawn;
class AActor;

struct HARVESTPANIC_API FHPCosmeticApplier
{
	static const FName CosmeticTag;
	static const FName NameTagTag;

	/** True if the mesh uses the template mannequin skeleton (safe to swap Manny/Quinn + attach to its bones). */
	static bool IsMannequin(const USkeletalMeshComponent* Body);

	/**
	 * Swap body preset, tint body materials and rebuild all accessory parts.
	 * Idempotent: previous cosmetic parts on this body are destroyed first.
	 */
	static void ApplyToBody(USkeletalMeshComponent* Body, const FHPCosmeticLoadout& Loadout);

	/** Tint the first-person arms (if the pawn exposes UFUNCTION GetArmsMesh1P). */
	static void ApplyToArms(UMeshComponent* Arms, const FHPCosmeticLoadout& Loadout);

	/** 3P body = ACharacter::GetMesh(); 1P arms via reflection. Either may be null. */
	static USkeletalMeshComponent* FindBody(const APawn* Pawn);
	static UMeshComponent* FindArms1P(APawn* Pawn);

	/** Apply to every mesh of a pawn. */
	static void ApplyToPawn(APawn* Pawn, const FHPCosmeticLoadout& Loadout);

	/** Remove every cosmetic part attached under Body. */
	static void ClearParts(USkeletalMeshComponent* Body);

	/** Create/update the overhead name tag (hidden for the owner's own view). */
	static UTextRenderComponent* EnsureNameTag(AActor* Actor, const FString& Name, const FLinearColor& Color, float Height, float WorldSize);

	/** Turn a text render to face a world location (yaw only). */
	static void FaceNameTag(UTextRenderComponent* Tag, const FVector& ViewLocation);
};
