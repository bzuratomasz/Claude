#pragma once

#include "CoreMinimal.h"
#include "Engine/DeveloperSettings.h"
#include "LTArtSettings.generated.h"

class UMaterialInterface;
class UStaticMesh;
class USkeletalMesh;
class UAnimInstance;
class UTexture2D;
class UFont;

/**
 * The photorealistic art the school is built from: Project Settings > Game > Lazy Turtle Art
 * (stored in Config/DefaultGame.ini). Download assets from Fab / Quixel Megascans into Content/LazyTurtle
 * and pick them here. Anything left empty falls back to engine basic shapes tinted with the floor theme
 * colours, so the game always runs.
 */
UCLASS(Config = Game, DefaultConfig, meta = (DisplayName = "Lazy Turtle Art"))
class LAZYTURTLE_API ULTArtSettings : public UDeveloperSettings
{
	GENERATED_BODY()

public:
	virtual FName GetCategoryName() const override { return TEXT("Game"); }

	// ---- surfaces (world-aligned materials work best: the builder does not unwrap UVs per cell)
	UPROPERTY(Config, EditAnywhere, Category = "Surfaces") TSoftObjectPtr<UMaterialInterface> WallMaterial;
	UPROPERTY(Config, EditAnywhere, Category = "Surfaces") TSoftObjectPtr<UMaterialInterface> RoyalWallMaterial;
	UPROPERTY(Config, EditAnywhere, Category = "Surfaces") TSoftObjectPtr<UMaterialInterface> CeilingMaterial;
	UPROPERTY(Config, EditAnywhere, Category = "Surfaces") TSoftObjectPtr<UMaterialInterface> MarbleFloor;
	UPROPERTY(Config, EditAnywhere, Category = "Surfaces") TSoftObjectPtr<UMaterialInterface> CheckerFloor;
	UPROPERTY(Config, EditAnywhere, Category = "Surfaces") TSoftObjectPtr<UMaterialInterface> ParquetFloor;
	UPROPERTY(Config, EditAnywhere, Category = "Surfaces") TSoftObjectPtr<UMaterialInterface> CarpetFloor;
	UPROPERTY(Config, EditAnywhere, Category = "Surfaces") TSoftObjectPtr<UMaterialInterface> RunnerFloor;
	UPROPERTY(Config, EditAnywhere, Category = "Surfaces") TSoftObjectPtr<UMaterialInterface> BlackboardMaterial;
	UPROPERTY(Config, EditAnywhere, Category = "Surfaces") TSoftObjectPtr<UMaterialInterface> GlassMaterial;
	/** Lit material for paintings and posters with a texture parameter named "Texture" (default: unlit engine widget material). */
	UPROPERTY(Config, EditAnywhere, Category = "Surfaces") TSoftObjectPtr<UMaterialInterface> PictureMaterial;

	// ---- props: key = model key from the floor layout ("tdesk", "desk0".."desk3", "plant", "bench", "gift",
	// "coin", "globe", "throne", "fountain", "clock", "m_abacus", "sc_telescope", "window", "door", "lockers",
	// "bookshelf", "display_case", "lamp", ...). Meshes face +X, pivot on the floor, real-world centimetres.
	UPROPERTY(Config, EditAnywhere, Category = "Props") TMap<FName, TSoftObjectPtr<UStaticMesh>> PropMeshes;

	// ---- pictures: key = caption id (0-4 paintings, 100+ Kórnik posters, 200+ subject posters)
	UPROPERTY(Config, EditAnywhere, Category = "Pictures") TMap<int32, TSoftObjectPtr<UTexture2D>> Pictures;

	// ---- characters (skeletal meshes face +X, ~real size; the anim class should use Speed (cm/s) and bIsInAir)
	UPROPERTY(Config, EditAnywhere, Category = "Characters") TSoftObjectPtr<USkeletalMesh> PlayerMesh;
	UPROPERTY(Config, EditAnywhere, Category = "Characters") TSoftClassPtr<UAnimInstance> PlayerAnim;
	UPROPERTY(Config, EditAnywhere, Category = "Characters") TSoftObjectPtr<USkeletalMesh> TeacherMesh;
	UPROPERTY(Config, EditAnywhere, Category = "Characters") TSoftClassPtr<UAnimInstance> TeacherAnim;
	UPROPERTY(Config, EditAnywhere, Category = "Characters") TSoftObjectPtr<USkeletalMesh> MasterMesh;
	UPROPERTY(Config, EditAnywhere, Category = "Characters") TSoftClassPtr<UAnimInstance> MasterAnim;
	UPROPERTY(Config, EditAnywhere, Category = "Characters") TSoftObjectPtr<USkeletalMesh> KidMesh;
	UPROPERTY(Config, EditAnywhere, Category = "Characters") TSoftClassPtr<UAnimInstance> KidAnim;

	// ---- 3D text (room signs, blackboards, banners, name labels): an *offline* (cached) font whose character set
	// includes Latin Extended-A (ą ć ę ł ń ś ź ż). Empty = engine default font, which lacks those letters, so they
	// are drawn without their diacritics.
	UPROPERTY(Config, EditAnywhere, Category = "Text") TSoftObjectPtr<UFont> TextFont;

	// ---- lighting
	UPROPERTY(Config, EditAnywhere, Category = "Lighting", meta = (ClampMin = "0")) float LampIntensity = 600.f;  // candela per ceiling lamp (a real school lamp is 400-800)
	UPROPERTY(Config, EditAnywhere, Category = "Lighting", meta = (ClampMin = "1000", ClampMax = "12000")) float LampTemperature = 4200.f;
	UPROPERTY(Config, EditAnywhere, Category = "Lighting", meta = (ClampMin = "0")) float SunIntensity = 8.f;      // lux * 1000 scale as DirectionalLight

	static const ULTArtSettings* Get() { return GetDefault<ULTArtSettings>(); }
};
