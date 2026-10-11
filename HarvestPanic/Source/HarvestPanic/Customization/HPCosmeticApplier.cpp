// Copyright HARVEST PANIC team. All Rights Reserved.

#include "HPCosmeticApplier.h"
#include "HPCosmeticCatalog.h"
#include "Components/SkeletalMeshComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Components/TextRenderComponent.h"
#include "Engine/SkeletalMesh.h"
#include "Engine/StaticMesh.h"
#include "Animation/Skeleton.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "GameFramework/Character.h"
#include "UObject/UnrealType.h"

const FName FHPCosmeticApplier::CosmeticTag(TEXT("HPCosmetic"));
const FName FHPCosmeticApplier::NameTagTag(TEXT("HPNameTag"));

namespace
{
	/** Reference-pose component-space transform of a bone (independent of the current animation). */
	bool GetRefPoseBoneCS(const USkeletalMesh* Mesh, FName Bone, FTransform& Out)
	{
		if (!Mesh) { return false; }
		const FReferenceSkeleton& Ref = Mesh->GetRefSkeleton();
		int32 Idx = Ref.FindBoneIndex(Bone);
		if (Idx == INDEX_NONE) { return false; }
		const TArray<FTransform>& Pose = Ref.GetRefBonePose();
		Out = Pose[Idx];
		Idx = Ref.GetParentIndex(Idx);
		while (Idx != INDEX_NONE)
		{
			Out = Out * Pose[Idx];
			Idx = Ref.GetParentIndex(Idx);
		}
		return true;
	}

	FLinearColor ResolvePartColor(const FHPCosmeticPart& Part, const FHPCosmeticLoadout& L, const UHPCosmeticCatalog& Cat)
	{
		switch (Part.ColorSource)
		{
		case EHPPartColorSource::Primary:   return Cat.GetPaletteColor(L.PrimaryColor);
		case EHPPartColorSource::Secondary: return Cat.GetPaletteColor(L.SecondaryColor);
		case EHPPartColorSource::Accent:    return Cat.GetPaletteColor(L.AccentColor);
		case EHPPartColorSource::Skin:      return Cat.GetSkinTone(L.SkinTone);
		default:                            return Part.Tint;
		}
	}

	void TintMeshSlots(UMeshComponent* Mesh, const FHPCosmeticLoadout& L, const UHPCosmeticCatalog& Cat, bool bArms)
	{
		const int32 NumSlots = Mesh->GetNumMaterials();
		for (int32 i = 0; i < NumSlots; ++i)
		{
			UMaterialInstanceDynamic* MID = Mesh->CreateAndSetMaterialInstanceDynamic(i);
			if (!MID) { continue; }
			// Body: primary slot = overalls, the rest = shirt. Arms (GDD): accent gloves.
			const FLinearColor C = bArms ? Cat.GetPaletteColor(L.AccentColor)
				: (i == Cat.PrimaryMaterialSlot ? Cat.GetPaletteColor(L.PrimaryColor) : Cat.GetPaletteColor(L.SecondaryColor));
			for (const FName& Param : Cat.BodyColorParams)
			{
				MID->SetVectorParameterValue(Param, C);
			}
		}
	}
}

bool FHPCosmeticApplier::IsMannequin(const USkeletalMeshComponent* Body)
{
	const USkeletalMesh* Mesh = Body ? Body->GetSkeletalMeshAsset() : nullptr;
	const USkeleton* Skel = Mesh ? Mesh->GetSkeleton() : nullptr;
	return Skel && Skel->GetFName() == FName(TEXT("SK_Mannequin"));
}

void FHPCosmeticApplier::ClearParts(USkeletalMeshComponent* Body)
{
	if (!Body) { return; }
	TArray<USceneComponent*> Children = Body->GetAttachChildren();
	for (USceneComponent* Child : Children)
	{
		if (Child && Child->ComponentHasTag(CosmeticTag))
		{
			Child->DestroyComponent();
		}
	}
}

void FHPCosmeticApplier::ApplyToBody(USkeletalMeshComponent* Body, const FHPCosmeticLoadout& InLoadout)
{
	if (!Body || !Body->GetOwner()) { return; }
	const UHPCosmeticCatalog& Cat = *UHPCosmeticCatalog::Get();
	FHPCosmeticLoadout L = InLoadout;
	Cat.SanitizeLoadout(L);

	ClearParts(Body);
	if (!IsMannequin(Body))
	{
		// Unknown rig (e.g. a future custom farmer). Bones may not match: skip rather than float hats in space.
		return;
	}

	// Body preset. Sync load: assets are tiny and the GDD allows the lobby to preload.
	if (USkeletalMesh* Wanted = Cat.GetBodyMesh(L.BodyPreset).LoadSynchronous())
	{
		if (Body->GetSkeletalMeshAsset() != Wanted)
		{
			Body->EmptyOverrideMaterials();
			Body->SetSkeletalMeshAsset(Wanted);
		}
	}
	TintMeshSlots(Body, L, Cat, false);

	UMaterialInterface* TintBase = Cat.PartTintMaterial.LoadSynchronous();
	const USkeletalMesh* MeshAsset = Body->GetSkeletalMeshAsset();
	AActor* Owner = Body->GetOwner();

	for (uint8 S = 0; S < static_cast<uint8>(EHPCosmeticSlot::MAX); ++S)
	{
		const FHPCosmeticItem* Item = Cat.FindItem(L.GetSlotItem(static_cast<EHPCosmeticSlot>(S)));
		if (!Item) { continue; }
		for (const FHPCosmeticPart& Part : Item->Parts)
		{
			UStaticMesh* SM = Part.Mesh.LoadSynchronous();
			FTransform BoneCS;
			if (!SM || !GetRefPoseBoneCS(MeshAsset, Part.Bone, BoneCS)) { continue; }

			UStaticMeshComponent* Comp = NewObject<UStaticMeshComponent>(Owner, NAME_None, RF_Transient);
			Comp->ComponentTags.Add(CosmeticTag);
			Comp->SetStaticMesh(SM);
			Comp->SetCollisionEnabled(ECollisionEnabled::NoCollision);
			Comp->SetGenerateOverlapEvents(false);
			Comp->SetCanEverAffectNavigation(false);
			Comp->bReceivesDecals = false;
			// Mirror the body's visibility rules: if the 3P body is hidden in 1P, so is the hat.
			Comp->SetOwnerNoSee(Body->bOwnerNoSee);
			Comp->SetOnlyOwnerSee(Body->bOnlyOwnerSee);
			Comp->SetupAttachment(Body, Part.Bone);

			const FTransform Desired(Part.Rotation.Quaternion(), BoneCS.GetLocation() + Part.Offset, Part.Scale);
			const FTransform Rel = Desired.GetRelativeTransform(FTransform(BoneCS.GetRotation(), BoneCS.GetLocation()));
			Comp->SetRelativeTransform(Rel);
			Comp->RegisterComponent();

			if (Part.ColorSource != EHPPartColorSource::MeshMaterial && TintBase)
			{
				UMaterialInstanceDynamic* MID = UMaterialInstanceDynamic::Create(TintBase, Comp);
				MID->SetVectorParameterValue(Cat.PartTintParam, ResolvePartColor(Part, L, Cat));
				for (int32 i = 0; i < Comp->GetNumMaterials(); ++i) { Comp->SetMaterial(i, MID); }
			}
		}
	}
}

void FHPCosmeticApplier::ApplyToArms(UMeshComponent* Arms, const FHPCosmeticLoadout& L)
{
	if (Arms)
	{
		TintMeshSlots(Arms, L, *UHPCosmeticCatalog::Get(), true);
	}
}

USkeletalMeshComponent* FHPCosmeticApplier::FindBody(const APawn* Pawn)
{
	const ACharacter* Char = Cast<ACharacter>(Pawn);
	return Char ? Char->GetMesh() : nullptr;
}

UMeshComponent* FHPCosmeticApplier::FindArms1P(APawn* Pawn)
{
	if (!Pawn) { return nullptr; }
	UFunction* Fn = Pawn->FindFunction(TEXT("GetArmsMesh1P"));
	if (!Fn || Fn->NumParms != 1) { return nullptr; }
	FObjectProperty* Ret = CastField<FObjectProperty>(Fn->GetReturnProperty());
	if (!Ret || !Ret->PropertyClass->IsChildOf(UMeshComponent::StaticClass())) { return nullptr; }

	TArray<uint8, TInlineAllocator<64>> Params;
	Params.SetNumZeroed(FMath::Max<int32>(Fn->ParmsSize, 8));
	Pawn->ProcessEvent(Fn, Params.GetData());
	return Cast<UMeshComponent>(Ret->GetObjectPropertyValue_InContainer(Params.GetData()));
}

void FHPCosmeticApplier::ApplyToPawn(APawn* Pawn, const FHPCosmeticLoadout& L)
{
	ApplyToBody(FindBody(Pawn), L);
	ApplyToArms(FindArms1P(Pawn), L);
}

UTextRenderComponent* FHPCosmeticApplier::EnsureNameTag(AActor* Actor, const FString& Name, const FLinearColor& Color, float Height, float WorldSize)
{
	if (!Actor || !Actor->GetRootComponent()) { return nullptr; }
	UTextRenderComponent* Tag = nullptr;
	TArray<UTextRenderComponent*> Existing;
	Actor->GetComponents(Existing);
	for (UTextRenderComponent* T : Existing)
	{
		if (T->ComponentHasTag(NameTagTag)) { Tag = T; break; }
	}
	if (!Tag)
	{
		Tag = NewObject<UTextRenderComponent>(Actor, NAME_None, RF_Transient);
		Tag->ComponentTags.Add(NameTagTag);
		Tag->SetupAttachment(Actor->GetRootComponent());
		Tag->SetHorizontalAlignment(EHTA_Center);
		Tag->SetVerticalAlignment(EVRTA_TextCenter);
		Tag->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		Tag->SetCastShadow(false);
		// Visible to everyone else, hidden for the owner in first person.
		Tag->SetOwnerNoSee(true);
		Tag->RegisterComponent();
	}
	Tag->SetRelativeLocation(FVector(0, 0, Height));
	Tag->SetWorldSize(WorldSize);
	Tag->SetText(FText::FromString(Name));
	Tag->SetTextRenderColor(Color.ToFColor(true));
	return Tag;
}

void FHPCosmeticApplier::FaceNameTag(UTextRenderComponent* Tag, const FVector& ViewLocation)
{
	if (!Tag) { return; }
	const FVector To = ViewLocation - Tag->GetComponentLocation();
	// Text renders face +X; yaw only so it never tilts.
	Tag->SetWorldRotation(FRotator(0.f, To.Rotation().Yaw, 0.f));
}
