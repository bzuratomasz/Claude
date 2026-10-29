#include "LTWorld.h"

#include "LazyTurtle.h"
#include "LTArtSettings.h"
#include "LTSchool.h"

#include "Animation/AnimInstance.h"
#include "Camera/PlayerCameraManager.h"
#include "Components/BoxComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/DirectionalLightComponent.h"
#include "Components/ExponentialHeightFogComponent.h"
#include "Components/InstancedStaticMeshComponent.h"
#include "Components/PointLightComponent.h"
#include "Components/PostProcessComponent.h"
#include "Components/RectLightComponent.h"
#include "Components/SceneComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Components/SkyAtmosphereComponent.h"
#include "Components/SkyLightComponent.h"
#include "Components/SphereComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Components/TextRenderComponent.h"
#include "Engine/CollisionProfile.h"
#include "Engine/Font.h"
#include "Engine/SkeletalMesh.h"
#include "Engine/StaticMesh.h"
#include "Engine/Texture2D.h"
#include "Engine/World.h"
#include "GameFramework/Pawn.h"
#include "HAL/IConsoleManager.h"
#include "Kismet/GameplayStatics.h"
#include "Materials/MaterialInstanceDynamic.h"
#include "Materials/MaterialInterface.h"
#include "Math/RotationMatrix.h"
#include "UObject/UnrealType.h"

// ============================================================================================ shared helpers

namespace LTW
{
	constexpr double Cs = LT::CellSize;
	constexpr double Wh = LT::WallHeight;

	/** sRGB 0-255 to linear. */
	FLinearColor Lin(uint8 R, uint8 G, uint8 B) { return FLinearColor(FColor(R, G, B)); }

	FLinearColor Scaled(const FLinearColor& C, float K) { return FLinearColor(C.R * K, C.G * K, C.B * K, 1.f); }

	UStaticMesh* Shape(const TCHAR* Name)
	{
		return LoadObject<UStaticMesh>(nullptr, *FString::Printf(TEXT("/Engine/BasicShapes/%s.%s"), Name, Name));
	}
	UStaticMesh* Cube() { return Shape(TEXT("Cube")); }
	UStaticMesh* Sphere() { return Shape(TEXT("Sphere")); }
	UStaticMesh* Cylinder() { return Shape(TEXT("Cylinder")); }
	UStaticMesh* Cone() { return Shape(TEXT("Cone")); }
	UStaticMesh* Plane() { return Shape(TEXT("Plane")); }

	UMaterialInterface* BasicMaterial()
	{
		return LoadObject<UMaterialInterface>(nullptr, TEXT("/Engine/BasicShapes/BasicShapeMaterial.BasicShapeMaterial"));
	}

	/**
	 * A glowing base material for lamp diffusers, window panes and sparkles. The engine's emissive mesh material is
	 * tried once; without it the callers fall back to the lit basic shape material.
	 */
	UMaterialInterface* EmissiveBase()
	{
		static bool bTried = false;
		static TWeakObjectPtr<UMaterialInterface> Cached;
		if (!bTried)
		{
			bTried = true;
			Cached = LoadObject<UMaterialInterface>(nullptr, TEXT("/Engine/EngineMaterials/EmissiveMeshMaterial.EmissiveMeshMaterial"));
		}
		return Cached.Get();
	}

	/**
	 * Material for the art-setting pictures: the art settings' PictureMaterial (texture parameter "Texture") or the
	 * unlit textured material used by widget components (texture parameter "SlateUI").
	 */
	UMaterialInterface* PictureBase()
	{
		if (const ULTArtSettings* S = ULTArtSettings::Get())
			if (UMaterialInterface* M = S->PictureMaterial.LoadSynchronous()) return M;
		return LoadObject<UMaterialInterface>(nullptr, TEXT("/Engine/EngineMaterials/Widget3DPassThrough_Opaque.Widget3DPassThrough_Opaque"));
	}

	/** A prop mesh from the art settings: exact key first, then the key without its trailing digits ("desk2" -> "desk"). */
	UStaticMesh* ArtMesh(FName Key)
	{
		const ULTArtSettings* S = ULTArtSettings::Get();
		if (!S || Key.IsNone()) return nullptr;
		if (const TSoftObjectPtr<UStaticMesh>* P = S->PropMeshes.Find(Key))
			if (UStaticMesh* M = P->LoadSynchronous()) return M;
		FString Base = Key.ToString();
		while (Base.Len() > 1 && FChar::IsDigit(Base[Base.Len() - 1])) Base.LeftChopInline(1);
		if (Base != Key.ToString())
			if (const TSoftObjectPtr<UStaticMesh>* P = S->PropMeshes.Find(FName(*Base)))
				return P->LoadSynchronous();
		return nullptr;
	}

	/** Registers a component created at runtime and keeps it referenced by its actor. */
	void RegisterPart(AActor* Owner, UActorComponent* C)
	{
		Owner->AddInstanceComponent(C);
		if (Owner->GetWorld()) C->RegisterComponent();
	}

	/** A basic shape (100 cm) of SizeCm under Parent; no collision. */
	UStaticMeshComponent* Part(AActor* Owner, USceneComponent* Parent, UStaticMesh* Mesh, const FVector& Loc, const FRotator& Rot,
		const FVector& SizeCm, UMaterialInterface* Mat)
	{
		UStaticMeshComponent* C = NewObject<UStaticMeshComponent>(Owner);
		C->SetMobility(EComponentMobility::Movable);
		C->SetStaticMesh(Mesh);
		C->SetupAttachment(Parent);
		C->SetRelativeLocationAndRotation(Loc, Rot);
		C->SetRelativeScale3D(SizeCm / 100.0);
		if (Mat) C->SetMaterial(0, Mat);
		C->SetCollisionEnabled(ECollisionEnabled::NoCollision);
		C->SetGenerateOverlapEvents(false);
		RegisterPart(Owner, C);
		return C;
	}

	/** Blocks the pawn but not the camera (furniture, people). Round = capsule, otherwise a box. */
	UPrimitiveComponent* Blocker(AActor* Owner, USceneComponent* Parent, const FVector& Center, const FVector& Half, bool bRound)
	{
		UShapeComponent* C;
		if (bRound)
		{
			UCapsuleComponent* Cap = NewObject<UCapsuleComponent>(Owner);
			Cap->SetCapsuleSize(Half.X, FMath::Max(Half.Z, Half.X));
			C = Cap;
		}
		else
		{
			UBoxComponent* Box = NewObject<UBoxComponent>(Owner);
			Box->SetBoxExtent(Half);
			C = Box;
		}
		C->SetMobility(EComponentMobility::Movable);
		C->SetupAttachment(Parent);
		C->SetRelativeLocation(Center);
		C->SetCollisionProfileName(UCollisionProfile::BlockAllDynamic_ProfileName);
		C->SetCollisionResponseToChannel(ECC_Camera, ECR_Ignore);
		C->SetGenerateOverlapEvents(false);
		C->SetHiddenInGame(true);
		RegisterPart(Owner, C);
		return C;
	}

	FVector FaceNormal(ELTFace F)
	{
		switch (F)
		{
		case ELTFace::N: return FVector(0, -1, 0);
		case ELTFace::S: return FVector(0, 1, 0);
		case ELTFace::W: return FVector(-1, 0, 0);
		default: return FVector(1, 0, 0);
		}
	}

	FIntPoint FaceNeighbour(int32 X, int32 Y, ELTFace F)
	{
		const FVector N = FaceNormal(F);
		return FIntPoint(X + (int32)N.X, Y + (int32)N.Y);
	}

	FRotator FaceRot(ELTFace F) { return FaceNormal(F).Rotation(); }

	/**
	 * A point in front of a wall face: Out = distance from the wall surface, Lateral along the wall (the face's local +Y,
	 * i.e. to the viewer's left), Z above the floor.
	 */
	FVector FacePoint(int32 X, int32 Y, ELTFace F, double Out, double Lateral, double Z)
	{
		const FVector N = FaceNormal(F);
		const FVector Side(-N.Y, N.X, 0);
		return FVector((X + 0.5) * Cs, (Y + 0.5) * Cs, Z) + N * (Cs * 0.5 + Out) + Side * Lateral;
	}

	/** Basic Plane (+Z normal) turned to face +X with its UV upright when seen from the front. */
	FRotator PictureLocalRot() { return FRotationMatrix::MakeFromXY(FVector(0, -1, 0), FVector(0, 0, -1)).Rotator(); }

	FRotator Compose(const FRotator& Outer, const FRotator& Inner) { return (FQuat(Outer) * FQuat(Inner)).Rotator(); }

	const FLinearColor Gold = Lin(214, 168, 60);
	const FLinearColor Daylight = Lin(215, 232, 255);
	const FLinearColor Metal = Lin(120, 124, 130);

	FLinearColor Palette(int32 I)
	{
		static const FLinearColor P[] = { Lin(196, 72, 60), Lin(60, 120, 190), Lin(235, 180, 50), Lin(80, 160, 90),
			Lin(150, 90, 170), Lin(240, 130, 60), Lin(70, 170, 170), Lin(200, 90, 130) };
		return P[((I % 8) + 8) % 8];
	}

	FLinearColor SubjectColor(int32 Subject)
	{
		const TArray<FLTSubject>& S = FLTSchool::Subjects();
		return S.IsValidIndex(Subject) ? S[Subject].Color : Palette(Subject);
	}

	const FLTFloorTheme& Theme(int32 Floor)
	{
		static const FLTFloorTheme Fallback{ TEXT(""), TEXT(""), Lin(245, 236, 210), Lin(90, 150, 200), Lin(160, 110, 70),
			Lin(235, 230, 220), Lin(150, 160, 175), Lin(60, 60, 70) };
		const TArray<FLTFloorTheme>& T = FLTSchool::Themes();
		return T.IsValidIndex(Floor) ? T[Floor] : Fallback;
	}

	/** Small per-actor cache so an actor creates one material instance per colour. */
	struct FTints
	{
		UObject* Outer;
		TMap<uint32, UMaterialInstanceDynamic*> Map;
		explicit FTints(UObject* InOuter) : Outer(InOuter) {}
		UMaterialInterface* operator()(const FLinearColor& C)
		{
			const uint32 Key = C.ToFColor(false).ToPackedARGB();
			if (UMaterialInstanceDynamic** Found = Map.Find(Key)) return *Found;
			UMaterialInstanceDynamic* M = FLTTurtleVisual::Tinted(Outer, C);
			Map.Add(Key, M);
			return M;
		}
	};

	/** The plain Latin letter of a Polish one (ą -> a, Ł -> L, ...), or 0. */
	TCHAR PolishBase(TCHAR Ch)
	{
		static const TCHAR* From = TEXT("ąćęłńóśźżĄĆĘŁŃÓŚŹŻ");
		static const TCHAR* To = TEXT("acelnoszzACELNOSZZ");
		for (int32 I = 0; From[I]; I++)
			if (From[I] == Ch) return To[I];
		return 0;
	}

	/**
	 * Sets the text of a 3D label. UTextRenderComponent only draws the glyphs baked into its *offline* font, and the
	 * engine default (RobotoDistanceField) has no Latin Extended-A, so ą ć ę ł ń ś ź ż would show as empty boxes.
	 * The art settings' TextFont (an offline font with Polish letters) is used when set; otherwise letters the font
	 * cannot draw fall back to their plain form so names, signs and blackboards stay readable.
	 */
	void SetLabelText(UTextRenderComponent* T, const FString& S)
	{
		if (!T) return;
		if (const ULTArtSettings* Art = ULTArtSettings::Get())
			if (!Art->TextFont.IsNull())
				if (UFont* Font = Art->TextFont.LoadSynchronous()) T->SetFont(Font);
		FString Out = S;
		const UFont* Font = T->Font;
		if (Font && Font->FontCacheType == EFontCacheType::Offline)
			for (int32 I = 0; I < Out.Len(); I++)
			{
				const TCHAR Ch = Out[I];
				// RemapChar returns 127 (the font's "null character") for glyphs the font does not have
				if (Ch > 127 && Font->RemapChar(Ch) == 127)
					if (const TCHAR Base = PolishBase(Ch)) Out[I] = Base;
			}
		T->SetText(FText::FromString(Out));
	}
}

using namespace LTW;

// ============================================================================================ FLTTurtleVisual

UMaterialInstanceDynamic* FLTTurtleVisual::Tinted(UObject* Outer, const FLinearColor& Color)
{
	UMaterialInterface* Base = BasicMaterial();
	if (!Base) return nullptr;
	UMaterialInstanceDynamic* M = UMaterialInstanceDynamic::Create(Base, Outer);
	M->SetVectorParameterValue(TEXT("Color"), Color);
	return M;
}

TArray<UStaticMeshComponent*> FLTTurtleVisual::Build(AActor* Owner, USceneComponent* Parent, float Height,
	const FLinearColor& ShellColor, const FLinearColor& SkinColor, bool bBackpack, const FLinearColor& AccessoryColor)
{
	TArray<UStaticMeshComponent*> Out;
	if (!Owner || !Parent) return Out;

	FTints T(Owner);
	UMaterialInterface* MShell = T(ShellColor);
	UMaterialInterface* MScute = T(Scaled(ShellColor, 0.62f));
	UMaterialInterface* MRim = T(FMath::Lerp(Scaled(ShellColor, 0.5f), Lin(200, 170, 90), 0.35f));
	UMaterialInterface* MSkin = T(SkinColor);
	UMaterialInterface* MBelly = T(FMath::Lerp(SkinColor, Lin(235, 220, 150), 0.55f));
	UMaterialInterface* MWhite = T(Lin(245, 245, 240));
	UMaterialInterface* MBlack = T(Lin(20, 18, 16));
	UMaterialInterface* MAcc = T(AccessoryColor);
	UMaterialInterface* MPack = T(Lin(250, 200, 30));
	UMaterialInterface* MPackDark = T(Lin(215, 150, 20));
	UStaticMesh* Sph = Sphere();
	UStaticMesh* Cyl = Cylinder();
	UStaticMesh* Box = Cube();

	auto P = [&](UStaticMesh* M, const FVector& L, const FVector& S, UMaterialInterface* Mat, const FRotator& R = FRotator::ZeroRotator)
	{
		Out.Add(Part(Owner, Parent, M, L, R, S, Mat));
	};

	if (Height < 120.f)
	{
		// ---- on all fours (the player, classmates): low dome, head on a short neck, cap
		const double U = Height / 80.0;
		P(Sph, FVector(-4, 0, 44) * U, FVector(88, 72, 64) * U, MShell);
		P(Cyl, FVector(-4, 0, 30) * U, FVector(94, 78, 5) * U, MRim);
		P(Sph, FVector(0, 0, 74) * U, FVector(30, 26, 6) * U, MScute);
		P(Sph, FVector(20, 0, 68) * U, FVector(24, 24, 6) * U, MScute, FRotator(-25, 0, 0));
		P(Sph, FVector(-26, 0, 66) * U, FVector(24, 24, 6) * U, MScute, FRotator(25, 0, 0));
		P(Sph, FVector(-4, 26, 60) * U, FVector(24, 22, 6) * U, MScute, FRotator(0, 0, -32));
		P(Sph, FVector(-4, -26, 60) * U, FVector(24, 22, 6) * U, MScute, FRotator(0, 0, 32));
		P(Sph, FVector(0, 0, 24) * U, FVector(80, 62, 26) * U, MBelly);
		for (int32 SX = -1; SX <= 1; SX += 2)
			for (int32 SY = -1; SY <= 1; SY += 2)
			{
				P(Cyl, FVector(SX * 26, SY * 27, 10) * U, FVector(17, 17, 20) * U, MSkin);
				P(Sph, FVector(SX * 26 + 3, SY * 27, 3) * U, FVector(22, 20, 8) * U, MSkin);
			}
		P(Sph, FVector(38, 0, 44) * U, FVector(30, 20, 22) * U, MSkin, FRotator(35, 0, 0));
		P(Sph, FVector(54, 0, 56) * U, FVector(32, 28, 28) * U, MSkin);
		for (int32 S = -1; S <= 1; S += 2)
		{
			P(Sph, FVector(66, S * 8, 61) * U, FVector(9, 9, 9) * U, MWhite);
			P(Sph, FVector(69.5, S * 8, 61.5) * U, FVector(5, 5, 5) * U, MBlack);
		}
		P(Box, FVector(69.3, 0, 52) * U, FVector(1.2, 8, 1.2) * U, MBlack);
		P(Shape(TEXT("Cone")), FVector(-48, 0, 22) * U, FVector(8, 8, 14) * U, MSkin, FRotator(100, 0, 0));
		// cap in the accessory colour
		P(Cyl, FVector(52, 0, 69) * U, FVector(28, 26, 6) * U, MAcc);
		P(Box, FVector(67, 0, 67.5) * U, FVector(14, 20, 1.5) * U, MAcc);
		if (bBackpack)
		{
			P(Box, FVector(-6, 0, 82) * U, FVector(32, 40, 16) * U, MPack);
			P(Box, FVector(-23, 0, 80) * U, FVector(3, 26, 10) * U, MPackDark);
			for (int32 S = -1; S <= 1; S += 2)
				P(Box, FVector(10, S * 14, 76) * U, FVector(26, 3, 2) * U, MPackDark, FRotator(-20, 0, 0));
		}
	}
	else
	{
		// ---- upright (teachers, the Master Turtle): shell on the back, plastron in front, arms and legs
		const double U = Height / 100.0;
		for (int32 S = -1; S <= 1; S += 2)
		{
			P(Sph, FVector(6, S * 11, 3) * U, FVector(22, 13, 7) * U, MSkin);
			P(Cyl, FVector(0, S * 11, 14) * U, FVector(13, 13, 24) * U, MSkin);
			P(Cyl, FVector(4, S * 23, 55) * U, FVector(10, 10, 28) * U, MSkin, FRotator(0, 0, S * 12));
			P(Sph, FVector(5, S * 26, 40) * U, FVector(11, 11, 11) * U, MSkin);
		}
		P(Sph, FVector(3, 0, 50) * U, FVector(32, 38, 56) * U, MBelly);
		P(Sph, FVector(-11, 0, 52) * U, FVector(30, 46, 64) * U, MShell);
		P(Sph, FVector(-25, 0, 58) * U, FVector(5, 22, 26) * U, MScute);
		P(Sph, FVector(-23, 0, 34) * U, FVector(5, 18, 14) * U, MScute);
		P(Sph, FVector(-22, 0, 78) * U, FVector(5, 18, 12) * U, MScute);
		P(Cyl, FVector(2, 0, 80) * U, FVector(14, 14, 8) * U, MSkin);
		P(Sph, FVector(4, 0, 88) * U, FVector(28, 26, 25) * U, MSkin);
		for (int32 S = -1; S <= 1; S += 2)
		{
			P(Sph, FVector(16, S * 6, 91) * U, FVector(8, 8, 8) * U, MWhite);
			P(Sph, FVector(19, S * 6, 91.5) * U, FVector(4.5, 4.5, 4.5) * U, MBlack);
		}
		P(Box, FVector(16.8, 0, 83) * U, FVector(1.5, 7, 1) * U, MBlack);
		P(Shape(TEXT("Cone")), FVector(-24, 0, 26) * U, FVector(7, 7, 12) * U, MSkin, FRotator(100, 0, 0));
		// scarf in the accessory colour (subject cue for teachers)
		P(Cyl, FVector(2, 0, 76) * U, FVector(24, 24, 5) * U, MAcc);
		P(Box, FVector(12, 6, 68) * U, FVector(2, 6, 14) * U, MAcc, FRotator(0, 0, 8));
		if (Height >= 180.f)
		{
			// the Master Turtle: mortarboard with a golden tassel and a white beard
			UMaterialInterface* MGold = T(Gold);
			P(Cyl, FVector(4, 0, 100) * U, FVector(24, 24, 5) * U, MBlack);
			P(Box, FVector(4, 0, 103.5) * U, FVector(32, 32, 2) * U, MBlack, FRotator(0, 45, 0));
			P(Sph, FVector(4, 0, 105) * U, FVector(3, 3, 3) * U, MGold);
			P(Sph, FVector(14, 12, 100) * U, FVector(3, 3, 6) * U, MGold);
			P(Shape(TEXT("Cone")), FVector(14, 0, 72) * U, FVector(12, 14, 16) * U, MWhite, FRotator(180, 0, 0));
		}
		if (bBackpack)
		{
			P(Box, FVector(-31, 0, 56) * U, FVector(12, 30, 32) * U, MPack);
			P(Box, FVector(-38, 0, 50) * U, FVector(3, 20, 12) * U, MPackDark);
		}
	}
	for (UStaticMeshComponent* C : Out) C->SetCastShadow(true);
	return Out;
}

// ============================================================================================ ALTProp

ALTProp::ALTProp()
{
	PrimaryActorTick.bCanEverTick = false;
	Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	Root->SetMobility(EComponentMobility::Movable);
	RootComponent = Root;
	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(Root);
	Mesh->SetMobility(EComponentMobility::Movable);
	Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Mesh->SetGenerateOverlapEvents(false);
}

void ALTProp::Init(const FLTPropSpawn& S, const FLinearColor& Tint)
{
	SetActorLocationAndRotation(LT::MapToWorld(S.X, S.Y, S.Z), FRotator(0, LT::MapYawToDegrees(S.Yaw), 0));
	const double Wc = S.W * Cs, Hc = FMath::Max(S.H * Cs, 10.0);

	// collision from the layout (local frame); desks and benches use their real footprint below
	const bool bSolid = S.Radius > 0 || S.HalfX > 0;
	bool bRound = S.Radius > 0;
	FVector BlockCenter(0, 0, Hc * 0.5);
	FVector BlockHalf = bRound ? FVector(S.Radius * Cs, S.Radius * Cs, Hc * 0.5)
		: FVector(S.HalfX * Cs, (S.HalfY > 0 ? S.HalfY : 0.3) * Cs, Hc * 0.5);

	FTints T(this);
	UStaticMesh* Box = Cube();
	UStaticMesh* Cyl = Cylinder();
	UStaticMesh* Sph = Sphere();
	auto P = [&](UStaticMesh* M, const FVector& L, const FVector& Size, const FLinearColor& C, const FRotator& R = FRotator::ZeroRotator)
	{
		return Part(this, Root, M, L, R, Size, T(C));
	};
	auto Main = [&](UStaticMesh* M, const FVector& L, const FVector& Size, const FLinearColor& C, const FRotator& R = FRotator::ZeroRotator)
	{
		Mesh->SetStaticMesh(M);
		Mesh->SetRelativeLocationAndRotation(L, R);
		Mesh->SetRelativeScale3D(Size / 100.0);
		Mesh->SetMaterial(0, T(C));
	};

	const FLinearColor Wood = Tint;
	const FLinearColor DarkWood = Scaled(Tint, 0.7f);

	if (UStaticMesh* Art = ArtMesh(S.Key))
	{
		Mesh->SetStaticMesh(Art);
		Mesh->SetRelativeScale3D(FVector(S.Scale));
		if (S.Kind == ELTProp::TeacherDesk) { BlockCenter = FVector(0, 0, 38); BlockHalf = FVector(38, 80, 38); }
	}
	else switch (S.Kind)
	{
	case ELTProp::TeacherDesk:
		// front (+X) faces the class; the teacher stands behind (-X)
		Main(Box, FVector(0, 0, 74), FVector(75, 160, 4), Wood);
		P(Box, FVector(0, 55, 36), FVector(65, 42, 72), DarkWood);
		P(Box, FVector(35, 0, 45), FVector(2, 150, 52), DarkWood);
		for (int32 SY = -1; SY <= 1; SY += 2) P(Box, FVector(-33, SY * 75, 36), FVector(4, 4, 72), DarkWood);
		for (int32 I = 0; I < 3; I++) P(Box, FVector(-33, 55, 16 + I * 22), FVector(1, 12, 1.5), Metal);
		BlockCenter = FVector(0, 0, 38);
		BlockHalf = FVector(38, 80, 38);
		bRound = false;
		break;

	case ELTProp::StudentDesk:
		// a double school desk facing +X with two chairs behind it
		Main(Box, FVector(0, 0, 70), FVector(55, 120, 3), Wood);
		P(Box, FVector(-3, 0, 56), FVector(45, 114, 2), DarkWood);
		for (int32 SY = -1; SY <= 1; SY += 2)
		{
			P(Box, FVector(0, SY * 57, 35), FVector(50, 3, 70), Metal);
			const double CY = SY * 30;
			P(Box, FVector(-48, CY, 44), FVector(40, 40, 3), Wood);
			P(Box, FVector(-68, CY, 64), FVector(3, 38, 28), Wood);
			P(Box, FVector(-48, CY + 19, 22), FVector(36, 2, 44), Metal);
			P(Box, FVector(-48, CY - 19, 22), FVector(36, 2, 44), Metal);
		}
		BlockCenter = FVector(-18, 0, 38);
		BlockHalf = FVector(48, 62, 38);
		bRound = false;
		break;

	case ELTProp::Bench:
	{
		const double L = FMath::Max(Wc, 60.0);
		Main(Box, FVector(0, 0, 44), FVector(40, L, 5), Wood);
		for (int32 SY = -1; SY <= 1; SY += 2) P(Box, FVector(0, SY * (L * 0.5 - 12), 22), FVector(36, 4, 44), Metal);
		P(Box, FVector(-19, 0, 68), FVector(4, L, 30), Wood);
		BlockCenter = FVector(0, 0, 35);
		BlockHalf = FVector(22, L * 0.5, 35);
		bRound = false;
		break;
	}

	case ELTProp::Plant:
		Main(Cyl, FVector(0, 0, 20), FVector(38, 38, 40), Lin(170, 92, 60));
		P(Cyl, FVector(0, 0, 39.5), FVector(34, 34, 2), Lin(70, 50, 35));
		P(Cyl, FVector(0, 0, Hc * 0.4), FVector(4, 4, Hc * 0.4), Lin(90, 70, 40));
		P(Sph, FVector(0, 0, Hc * 0.58), FVector(Hc * 0.48, Hc * 0.48, Hc * 0.42), Tint);
		P(Sph, FVector(9, -7, Hc * 0.75), FVector(Hc * 0.36, Hc * 0.36, Hc * 0.34), Scaled(Tint, 0.8f));
		P(Sph, FVector(-7, 8, Hc * 0.86), FVector(Hc * 0.28, Hc * 0.28, Hc * 0.26), Scaled(Tint, 1.15f));
		break;

	case ELTProp::Globe:
		Main(Sph, FVector(0, 0, Hc - 26), FVector(46, 46, 46), Lin(60, 115, 195));
		P(Cyl, FVector(0, 0, 2), FVector(36, 36, 4), Lin(120, 80, 50));
		P(Cyl, FVector(0, 0, (Hc - 50) * 0.5), FVector(4, 4, Hc - 50), Lin(120, 80, 50));
		P(Sph, FVector(14, 6, Hc - 18), FVector(22, 18, 20), Lin(90, 160, 80));
		P(Sph, FVector(-10, -12, Hc - 34), FVector(18, 18, 16), Lin(90, 160, 80));
		P(Cyl, FVector(0, 0, Hc - 26), FVector(52, 52, 1.5), Gold, FRotator(0, 0, 90));
		break;

	case ELTProp::Clock:
		Main(Box, FVector(0, 0, Hc * 0.5), FVector(40, 50, Hc), Wood);
		P(Cyl, FVector(21, 0, Hc - 32), FVector(36, 36, 2), Lin(245, 240, 225), FRotator(90, 0, 0));
		P(Box, FVector(22.5, 0, Hc - 27), FVector(1, 2, 12), Lin(20, 20, 20));
		P(Box, FVector(22.5, -4, Hc - 32), FVector(1, 9, 1.5), Lin(20, 20, 20));
		P(Box, FVector(20.5, 0, Hc * 0.4), FVector(1, 22, 55), Lin(40, 35, 30));
		P(Cyl, FVector(21.3, 0, Hc * 0.3), FVector(14, 14, 1), Gold, FRotator(90, 0, 0));
		break;

	case ELTProp::Fountain:
		Main(Cyl, FVector(0, 0, 40), FVector(34, 34, 80), Lin(215, 215, 210));
		P(Box, FVector(4, 0, 87), FVector(45, 52, 14), Lin(185, 190, 195));
		P(Cyl, FVector(10, 0, 98), FVector(3, 3, 10), Metal);
		break;

	case ELTProp::Throne:
	{
		const FLinearColor Velvet = Lin(150, 25, 40);
		Main(Box, FVector(0, 0, 22), FVector(70, 90, 45), Gold);
		P(Box, FVector(2, 0, 50), FVector(62, 78, 10), Velvet);
		P(Box, FVector(-30, 0, 105), FVector(14, 96, 150), Gold);
		P(Box, FVector(-22, 0, 105), FVector(3, 72, 100), Velvet);
		for (int32 SY = -1; SY <= 1; SY += 2)
		{
			P(Box, FVector(0, SY * 45, 62), FVector(64, 12, 26), Gold);
			P(Sph, FVector(-30, SY * 45, 186), FVector(14, 14, 14), Gold);
		}
		break;
	}

	case ELTProp::Artifact:
	{
		const FString Key = S.Key.ToString();
		if (Key.Contains(TEXT("planet")))
		{
			// a solar system mobile hanging from the ceiling
			const double Z = Wh - 80 - S.Z * Cs;
			Main(Sph, FVector(0, 0, Z), FVector(30, 30, 30), Lin(255, 190, 40));
			P(Cyl, FVector(0, 0, (Z + Wh - S.Z * Cs) * 0.5), FVector(1, 1, Wh - S.Z * Cs - Z), Lin(40, 40, 40));
			const FLinearColor PlanetCol[] = { Lin(160, 150, 140), Lin(220, 180, 110), Lin(70, 120, 200), Lin(190, 80, 50), Lin(210, 170, 120), Lin(200, 190, 140) };
			for (int32 I = 0; I < 6; I++)
			{
				const double D = 30 + I * 18, A = I * 1.1, R = 6 + (I == 4 ? 8 : I % 3 * 2);
				P(Sph, FVector(FMath::Cos(A) * D, FMath::Sin(A) * D, Z + (I % 2 ? -10 : 8)), FVector(R * 2), PlanetCol[I]);
			}
		}
		else if (S.Radius <= 0 && S.W <= 0.15 && S.H <= 0.15)
		{
			// rugs and mats lie flat on the floor
			Main(Box, FVector(0, 0, 0.8), FVector(110, 170, 1.5), Tint);
			P(Box, FVector(0, 0, 1.7), FVector(90, 150, 0.5), Scaled(Tint, 0.7f));
		}
		else if (Hc > Wc * 1.4)
		{
			Main(Cyl, FVector(0, 0, Hc * 0.5 + 3), FVector(Wc * 0.55, Wc * 0.55, Hc - 6), Tint);
			P(Box, FVector(0, 0, 3), FVector(Wc * 0.7, Wc * 0.7, 6), Lin(70, 60, 55));
		}
		else
		{
			Main(Box, FVector(0, 0, Hc * 0.43), FVector(Wc * 0.65, Wc, Hc * 0.86), Tint);
			P(Box, FVector(0, 0, Hc * 0.93), FVector(Wc * 0.7, Wc * 1.04, Hc * 0.14), Scaled(Tint, 0.65f));
		}
		break;
	}

	default:
		Main(Box, FVector(0, 0, Hc * 0.5), FVector(Wc, Wc, Hc), Tint);
		break;
	}

	TArray<USceneComponent*> PartComps;     // not "Children": that would hide AActor::Children (C4458)
	Root->GetChildrenComponents(true, PartComps);
	for (USceneComponent* C : PartComps)
		if (UPrimitiveComponent* Prim = Cast<UPrimitiveComponent>(C)) Prim->SetCastShadow(true);

	if (bSolid) Blocker(this, Root, BlockCenter, BlockHalf, bRound);
}

// ============================================================================================ ALTTurtleNPC

ALTTurtleNPC::ALTTurtleNPC()
{
	PrimaryActorTick.bCanEverTick = true;
	Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	Root->SetMobility(EComponentMobility::Movable);
	RootComponent = Root;

	Visual = CreateDefaultSubobject<USceneComponent>(TEXT("Visual"));
	Visual->SetupAttachment(Root);

	Body = CreateDefaultSubobject<USkeletalMeshComponent>(TEXT("Body"));
	Body->SetupAttachment(Root);
	Body->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Body->SetGenerateOverlapEvents(false);
	Body->SetVisibility(false);

	NameLabel = CreateDefaultSubobject<UTextRenderComponent>(TEXT("NameLabel"));
	NameLabel->SetupAttachment(Root);
	NameLabel->SetHorizontalAlignment(EHTA_Center);
	NameLabel->SetVerticalAlignment(EVRTA_TextCenter);
	NameLabel->SetTextRenderColor(FColor(255, 250, 235));
	NameLabel->SetWorldSize(14.f);
	NameLabel->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	NameLabel->SetCastShadow(false);
}

void ALTTurtleNPC::Init(const FLTPropSpawn& S, const FLinearColor& Accent)
{
	const double YawDeg = LT::MapYawToDegrees(S.Yaw);
	SetActorLocationAndRotation(LT::MapToWorld(S.X, S.Y, S.Z), FRotator(0, YawDeg, 0));
	WantYaw = YawDeg;
	bTurning = false;
	DisplayName = S.Name;

	const ULTArtSettings* Art = ULTArtSettings::Get();
	USkeletalMesh* SkMesh = nullptr;
	UClass* AnimClass = nullptr;
	FLinearColor Shell, Skin;
	bool bBackpack = false;
	switch (S.Kind)
	{
	case ELTProp::Master:
		Height = 190.f;
		Shell = Lin(150, 132, 92);
		Skin = Lin(165, 185, 125);
		if (Art) { SkMesh = Art->MasterMesh.LoadSynchronous(); AnimClass = Art->MasterAnim.LoadSynchronous(); }
		break;
	case ELTProp::Kid:
	{
		Height = 90.f;
		static const FLinearColor KidShells[] = { Lin(70, 140, 70), Lin(120, 95, 55), Lin(55, 130, 125) };
		Shell = KidShells[((S.Variant % 3) + 3) % 3];
		Skin = Lin(165, 208, 110);
		bBackpack = true;
		if (Art) { SkMesh = Art->KidMesh.LoadSynchronous(); AnimClass = Art->KidAnim.LoadSynchronous(); }
		break;
	}
	default:
	{
		Height = 155.f;
		static const FLinearColor TeacherShells[] = { Lin(95, 120, 60), Lin(120, 100, 65), Lin(80, 110, 85), Lin(130, 115, 70), Lin(90, 95, 70) };
		Shell = TeacherShells[((S.Variant % 5) + 5) % 5];
		Skin = Lin(150, 190, 105);
		if (Art) { SkMesh = Art->TeacherMesh.LoadSynchronous(); AnimClass = Art->TeacherAnim.LoadSynchronous(); }
		break;
	}
	}

	if (SkMesh)
	{
		Body->SetSkeletalMeshAsset(SkMesh);
		if (AnimClass) Body->SetAnimInstanceClass(AnimClass);
		const double MeshH = SkMesh->GetBounds().BoxExtent.Z * 2.0;
		Body->SetRelativeScale3D(FVector(MeshH > 1.0 ? Height / MeshH : 1.0));
		Body->SetVisibility(true);
		Body->SetCastShadow(true);
	}
	else
	{
		FLTTurtleVisual::Build(this, Visual, Height, Shell, Skin, bBackpack, Accent);
	}

	SetLabelText(NameLabel, DisplayName);
	NameLabel->SetRelativeLocation(FVector(0, 0, Height + (S.Kind == ELTProp::Kid ? 18.f : 26.f)));
	NameLabel->SetWorldSize(S.Kind == ELTProp::Kid ? 11.f : 14.f);
	NameLabel->SetVisibility(!DisplayName.IsEmpty());

	// teachers and the Master Turtle block the way (classmates walk through the corridor and never trap the player)
	if (S.Kind != ELTProp::Kid && (S.Radius > 0 || S.HalfX > 0))
	{
		const double R = (S.Radius > 0 ? S.Radius : FMath::Max(S.HalfX, S.HalfY)) * Cs;
		Blocker(this, Root, FVector(0, 0, Height * 0.5), FVector(R, R, Height * 0.5), true);
	}
}

void ALTTurtleNPC::FaceTowards(const FVector& Target)
{
	const FVector D = Target - GetActorLocation();
	if (D.SizeSquared2D() < 1.0) return;
	WantYaw = D.Rotation().Yaw;
	bTurning = true;
}

void ALTTurtleNPC::SetAnimSpeed(float Speed)
{
	UAnimInstance* Anim = Body ? Body->GetAnimInstance() : nullptr;
	if (!Anim) return;
	if (FNumericProperty* Prop = FindFProperty<FNumericProperty>(Anim->GetClass(), TEXT("Speed")))
		if (Prop->IsFloatingPoint())
			Prop->SetFloatingPointPropertyValue(Prop->ContainerPtrToValuePtr<void>(Anim), (double)Speed);
}

void ALTTurtleNPC::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	if (bTurning)
	{
		const float Cur = (float)GetActorRotation().Yaw;
		const float New = FMath::FixedTurn(Cur, (float)WantYaw, 300.f * DeltaSeconds);
		SetActorRotation(FRotator(0, New, 0));
		if (FMath::Abs(FRotator::NormalizeAxis(New - (float)WantYaw)) < 0.5f) bTurning = false;
	}

	// waddle while walking, breathe while standing
	const bool bWalking = CurrentSpeed > 1.f;
	WalkPhase += DeltaSeconds * (bWalking ? 9.f : 1.8f);
	if (Visual)
	{
		const float S = FMath::Sin(WalkPhase);
		Visual->SetRelativeLocationAndRotation(
			FVector(0, 0, bWalking ? FMath::Abs(S) * Height * 0.03f : S * Height * 0.004f),
			FRotator(0, 0, bWalking ? S * 5.f : 0.f));
	}
	SetAnimSpeed(CurrentSpeed);

	// the name label turns to the camera
	if (NameLabel && NameLabel->IsVisible())
		if (APlayerCameraManager* Cam = UGameplayStatics::GetPlayerCameraManager(this, 0))
		{
			FVector To = Cam->GetCameraLocation() - NameLabel->GetComponentLocation();
			To.Z = 0;
			if (!To.IsNearlyZero()) NameLabel->SetWorldRotation(FRotator(0, To.Rotation().Yaw, 0));
		}
}

// ============================================================================================ ALTKid

void ALTKid::Init(const FLTPropSpawn& S, const FLinearColor& Accent)
{
	Super::Init(S, Accent);
	Dir = S.Walk < 0 ? -1 : 1;
	if (S.Walk == 0) Speed = 0.f;
}

void ALTKid::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	float Moving = 0.f;
	if (PauseTime > 0)
	{
		PauseTime -= DeltaSeconds;
	}
	else if (Speed > 0)
	{
		// Game.Ambient: walk along x, turn at the ends, stop now and then for 1-3 s
		FVector L = GetActorLocation();
		double X = L.X / Cs + Dir * Speed * DeltaSeconds;
		if (X < MinX) { X = MinX; Dir = 1; }
		else if (X > MaxX) { X = MaxX; Dir = -1; }
		L.X = X * Cs;
		SetActorLocation(L);
		WantYaw = Dir > 0 ? 0.0 : 180.0;
		bTurning = true;
		Moving = (float)(Speed * Cs);
		if (FMath::FRand() < DeltaSeconds * 0.15f) PauseTime = 1.f + FMath::FRand() * 2.f;
	}
	CurrentSpeed = Moving;
}

// ============================================================================================ ALTGift

ALTGift::ALTGift()
{
	PrimaryActorTick.bCanEverTick = true;
	USceneComponent* Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	Root->SetMobility(EComponentMobility::Movable);
	RootComponent = Root;
	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(Root);
	Mesh->SetMobility(EComponentMobility::Movable);
	Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Mesh->SetGenerateOverlapEvents(false);
}

void ALTGift::Init(const FLTPropSpawn& S, const FLinearColor& Ribbon)
{
	// the box floats a little above the teacher's desk (76 cm)
	Base = LT::MapToWorld(S.X, S.Y) + FVector(0, 0, FMath::Max(S.Z * Cs, 96.0));
	SetActorLocationAndRotation(Base, FRotator(0, LT::MapYawToDegrees(S.Yaw), 0));
	Time = FMath::FRand() * 6.f;

	if (UStaticMesh* Art = ArtMesh(S.Key))
	{
		Mesh->SetStaticMesh(Art);
		Mesh->SetRelativeScale3D(FVector(S.Scale));
		return;
	}
	// Mesh stays an unscaled pivot so the parts keep their sizes while it spins
	FTints T(this);
	Part(this, Mesh, Cube(), FVector::ZeroVector, FRotator::ZeroRotator, FVector(30, 30, 28), T(Ribbon));
	Part(this, Mesh, Cube(), FVector::ZeroVector, FRotator::ZeroRotator, FVector(31, 6, 29), T(Gold));
	Part(this, Mesh, Cube(), FVector::ZeroVector, FRotator::ZeroRotator, FVector(6, 31, 29), T(Gold));
	Part(this, Mesh, Sphere(), FVector(4, 4, 17), FRotator(0, 45, 0), FVector(14, 6, 9), T(Gold));
	Part(this, Mesh, Sphere(), FVector(-4, -4, 17), FRotator(0, 45, 0), FVector(14, 6, 9), T(Gold));
}

void ALTGift::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	Time += DeltaSeconds;
	Mesh->SetRelativeLocationAndRotation(FVector(0, 0, FMath::Sin(Time * 2.5f) * 4.f), FRotator(0, Time * 50.f, 0));
}

void ALTGift::Collect()
{
	SetActorHiddenInGame(true);
	SetActorEnableCollision(false);
	SetActorTickEnabled(false);
}

// ============================================================================================ ALTMarker

ALTMarker::ALTMarker()
{
	PrimaryActorTick.bCanEverTick = true;
	USceneComponent* Root = CreateDefaultSubobject<USceneComponent>(TEXT("Root"));
	Root->SetMobility(EComponentMobility::Movable);
	RootComponent = Root;
	Text = CreateDefaultSubobject<UTextRenderComponent>(TEXT("Text"));
	Text->SetupAttachment(Root);
	Text->SetText(FText::FromString(TEXT("!")));
	Text->SetHorizontalAlignment(EHTA_Center);
	Text->SetVerticalAlignment(EVRTA_TextCenter);
	Text->SetTextRenderColor(FColor(255, 205, 40));
	Text->SetWorldSize(70.f);
	Text->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Text->SetCastShadow(false);
}

void ALTMarker::EnsureCheck()
{
	if (Check) return;
	Check = NewObject<USceneComponent>(this);
	Check->SetMobility(EComponentMobility::Movable);
	Check->SetupAttachment(RootComponent);
	RegisterPart(this, Check);
	UMaterialInterface* Green = FLTTurtleVisual::Tinted(this, Lin(40, 200, 70));
	// a check mark in the marker's YZ plane, seen from +X (the viewer's right is local -Y)
	auto Stroke = [&](const FVector2D& A, const FVector2D& B)
	{
		const FVector PA(0, -A.X, A.Y), PB(0, -B.X, B.Y);
		const FVector Dir = (PB - PA).GetSafeNormal();
		const double Len = FVector::Dist(PA, PB) + 7.0;
		Part(this, Check, Cube(), (PA + PB) * 0.5, FRotationMatrix::MakeFromZX(Dir, FVector::ForwardVector).Rotator(), FVector(6, 8, Len), Green);
	};
	Stroke(FVector2D(-16, 2), FVector2D(-5, -12));
	Stroke(FVector2D(-5, -12), FVector2D(18, 20));
}

void ALTMarker::SetDone(bool bInDone)
{
	bDone = bInDone;
	if (bDone) EnsureCheck();
	Text->SetVisibility(!bDone);
	if (Check) Check->SetVisibility(bDone, true);
}

void ALTMarker::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (!bBaseSet)
	{
		Base = GetActorLocation();
		bBaseSet = true;
	}
	Time += DeltaSeconds;
	double Yaw = GetActorRotation().Yaw;
	if (APlayerCameraManager* Cam = UGameplayStatics::GetPlayerCameraManager(this, 0))
	{
		FVector To = Cam->GetCameraLocation() - Base;
		To.Z = 0;
		if (!To.IsNearlyZero()) Yaw = To.Rotation().Yaw;
	}
	SetActorLocationAndRotation(Base + FVector(0, 0, FMath::Sin(Time * 3.f) * 6.f), FRotator(0, Yaw, 0));
}

// ============================================================================================ ALTCoin

ALTCoin::ALTCoin()
{
	PrimaryActorTick.bCanEverTick = true;
	Trigger = CreateDefaultSubobject<USphereComponent>(TEXT("Trigger"));
	Trigger->SetMobility(EComponentMobility::Movable);
	Trigger->InitSphereRadius(40.f);
	Trigger->SetCollisionEnabled(ECollisionEnabled::QueryOnly);
	Trigger->SetCollisionObjectType(ECC_WorldDynamic);
	Trigger->SetCollisionResponseToAllChannels(ECR_Ignore);
	Trigger->SetCollisionResponseToChannel(ECC_Pawn, ECR_Overlap);
	Trigger->SetGenerateOverlapEvents(true);
	RootComponent = Trigger;
	Mesh = CreateDefaultSubobject<UStaticMeshComponent>(TEXT("Mesh"));
	Mesh->SetupAttachment(Trigger);
	Mesh->SetMobility(EComponentMobility::Movable);
	Mesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	Mesh->SetGenerateOverlapEvents(false);
	Trigger->OnComponentBeginOverlap.AddDynamic(this, &ALTCoin::OnOverlap);
}

void ALTCoin::Setup(bool bFloating)
{
	// a floating coin's trigger starts above an 85 cm turtle's head: it needs a jump
	Trigger->SetSphereRadius(bFloating ? 24.f : 40.f);
	if (UStaticMesh* Art = ArtMesh(TEXT("coin")))
	{
		Mesh->SetStaticMesh(Art);
		return;
	}
	Part(this, Mesh, Cylinder(), FVector::ZeroVector, FRotator(0, 0, 90), FVector(30, 30, 4), FLTTurtleVisual::Tinted(this, Lin(255, 190, 40)));
	Part(this, Mesh, Cylinder(), FVector::ZeroVector, FRotator(0, 0, 90), FVector(22, 22, 4.6), FLTTurtleVisual::Tinted(this, Lin(235, 165, 30)));
}

void ALTCoin::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	if (!bCollected) Mesh->AddRelativeRotation(FRotator(0, 140.f * DeltaSeconds, 0));
}

void ALTCoin::OnOverlap(UPrimitiveComponent* OverlappedComp, AActor* Other, UPrimitiveComponent* OtherComp,
	int32 OtherBodyIndex, bool bFromSweep, const FHitResult& Sweep)
{
	if (bCollected) return;
	const APawn* Pawn = Cast<APawn>(Other);
	if (!Pawn || !Pawn->IsPlayerControlled()) return;
	bCollected = true;
	SetActorHiddenInGame(true);
	SetActorEnableCollision(false);
	SetActorTickEnabled(false);
	OnCollected.Broadcast(this);
}

// ============================================================================================ ALTFloor: helpers

ALTFloor::ALTFloor()
{
	PrimaryActorTick.bCanEverTick = true;
	FloorRoot = CreateDefaultSubobject<USceneComponent>(TEXT("FloorRoot"));
	FloorRoot->SetMobility(EComponentMobility::Movable);
	RootComponent = FloorRoot;
}

UMaterialInterface* ALTFloor::Mat(const FLinearColor& Color, bool bEmissive)
{
	FColor Key = Color.ToFColor(false);
	Key.A = bEmissive ? 1 : 0;
	if (TObjectPtr<UMaterialInterface>* Found = MaterialCache.Find(Key.ToPackedARGB())) return *Found;
	UMaterialInterface* M = nullptr;
	if (bEmissive)
		if (UMaterialInterface* Base = EmissiveBase())
		{
			UMaterialInstanceDynamic* MID = UMaterialInstanceDynamic::Create(Base, this);
			MID->SetVectorParameterValue(TEXT("Color"), Color);
			MID->SetVectorParameterValue(TEXT("EmissiveColor"), Color);
			M = MID;
		}
	if (!M) M = FLTTurtleVisual::Tinted(this, Color);
	MaterialCache.Add(Key.ToPackedARGB(), M);
	return M;
}

UMaterialInterface* ALTFloor::ArtMat(const TSoftObjectPtr<UMaterialInterface>& Art, const FLinearColor& Fallback)
{
	if (!Art.IsNull())
		if (UMaterialInterface* M = Art.LoadSynchronous()) return M;
	return Mat(Fallback);
}

template <class T>
T* ALTFloor::NewComp(USceneComponent* Parent)
{
	T* C = NewObject<T>(this);
	C->SetMobility(EComponentMobility::Movable);
	C->SetupAttachment(Parent ? Parent : FloorRoot.Get());
	AddInstanceComponent(C);
	return C;
}

UStaticMeshComponent* ALTFloor::AddShape(UStaticMesh* ShapeMesh, const FVector& Center, const FRotator& Rot, const FVector& SizeCm,
	UMaterialInterface* Material, USceneComponent* Parent)
{
	UStaticMeshComponent* C = NewComp<UStaticMeshComponent>(Parent);
	C->SetStaticMesh(ShapeMesh);
	C->SetRelativeLocationAndRotation(Center, Rot);
	C->SetRelativeScale3D(SizeCm / 100.0);
	if (Material) C->SetMaterial(0, Material);
	C->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	C->SetGenerateOverlapEvents(false);
	C->RegisterComponent();
	return C;
}

UStaticMeshComponent* ALTFloor::AddBox(const FVector& Center, const FRotator& Rot, const FVector& SizeCm, UMaterialInterface* Material,
	USceneComponent* Parent)
{
	return AddShape(Cube(), Center, Rot, SizeCm, Material, Parent);
}

UInstancedStaticMeshComponent* ALTFloor::AddInstanced(UStaticMesh* Mesh, UMaterialInterface* Material, bool bBlock)
{
	UInstancedStaticMeshComponent* C = NewComp<UInstancedStaticMeshComponent>();
	C->SetStaticMesh(Mesh);
	if (Material) C->SetMaterial(0, Material);
	if (bBlock) C->SetCollisionProfileName(UCollisionProfile::BlockAll_ProfileName);
	else C->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	C->SetGenerateOverlapEvents(false);
	C->RegisterComponent();
	return C;
}

UTextRenderComponent* ALTFloor::AddText(const FString& S, const FVector& Loc, const FRotator& Rot, float Size, const FColor& Color,
	USceneComponent* Parent)
{
	UTextRenderComponent* T = NewComp<UTextRenderComponent>(Parent);
	T->SetRelativeLocationAndRotation(Loc, Rot);
	SetLabelText(T, S);
	T->SetWorldSize(Size);
	T->SetTextRenderColor(Color);
	T->SetHorizontalAlignment(EHTA_Center);
	T->SetVerticalAlignment(EVRTA_TextCenter);
	T->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	T->SetCastShadow(false);
	T->RegisterComponent();
	return T;
}

UStaticMeshComponent* ALTFloor::AddArtMesh(UStaticMesh* Mesh, const FVector& Loc, const FRotator& Rot, bool bBlock)
{
	UStaticMeshComponent* C = NewComp<UStaticMeshComponent>();
	C->SetStaticMesh(Mesh);
	C->SetRelativeLocationAndRotation(Loc, Rot);
	if (bBlock) C->SetCollisionProfileName(UCollisionProfile::BlockAll_ProfileName);
	else C->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	C->SetGenerateOverlapEvents(false);
	C->RegisterComponent();
	return C;
}

template <class T>
T* ALTFloor::SpawnPart(const FVector& Loc, double YawDeg)
{
	FActorSpawnParameters SP;
	SP.Owner = this;
	SP.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	T* A = GetWorld()->SpawnActor<T>(T::StaticClass(), Loc, FRotator(0, YawDeg, 0), SP);
	if (A) Spawned.Add(A);
	return A;
}

// ============================================================================================ ALTFloor: build

void ALTFloor::Build(int32 Floor, int32 DoneMask, bool bMasterDone, bool bInPolish)
{
	if (!GetWorld()) return;
	FloorIndex = FMath::Clamp(Floor, 0, LT::Floors - 1);
	bPolish = bInPolish;
	Layout = FLTSchool::BuildLayout(FloorIndex, bPolish);
	if (Layout.Cells.Num() != LT::MapW * LT::MapH || Layout.Surfaces.Num() != LT::MapW * LT::MapH)
	{
		UE_LOG(LogLazyTurtle, Error, TEXT("LazyTurtle: floor layout %d has the wrong size"), FloorIndex);
		return;
	}
	const FLTFloorTheme& T = Theme(FloorIndex);

	BuildEnvironment(T);
	BuildShell(T);
	for (const FLTWallDecor& D : Layout.Decor) BuildDecor(D, T);
	BuildMasterDoor(T);
	BuildStairs(T);
	BuildProps(DoneMask, T);

	if (bMasterDone)
	{
		MarkMasterDone();
		OpenStairs();
	}
	if (bMasterDone || GiftsDone() >= LT::Classes) OpenMasterDoor();
}

void ALTFloor::BuildEnvironment(const FLTFloorTheme& Th)
{
	const ULTArtSettings* Art = ULTArtSettings::Get();

	USkyAtmosphereComponent* Atmosphere = NewComp<USkyAtmosphereComponent>();
	Atmosphere->RegisterComponent();

	UDirectionalLightComponent* Sun = NewComp<UDirectionalLightComponent>();
	Sun->Intensity = Art ? Art->SunIntensity : 8.f;
	Sun->bAtmosphereSunLight = true;
	Sun->bUseTemperature = true;
	Sun->Temperature = 5800.f;
	Sun->SetRelativeRotation(FRotator(-38, 130, 0));
	Sun->RegisterComponent();

	USkyLightComponent* Sky = NewComp<USkyLightComponent>();
	Sky->SourceType = ESkyLightSourceType::SLS_CapturedScene;
	Sky->bRealTimeCapture = true;
	Sky->Intensity = 1.f;
	Sky->RegisterComponent();

	UExponentialHeightFogComponent* Fog = NewComp<UExponentialHeightFogComponent>();
	Fog->SetRelativeLocation(FVector(0, 0, -50));
	Fog->RegisterComponent();
	Fog->SetFogDensity(0.0015f);
	Fog->SetFogHeightFalloff(0.2f);
	Fog->SetFogMaxOpacity(0.35f);
	Fog->SetStartDistance(600.f);
	Fog->SetFogInscatteringColor(FMath::Lerp(Lin(150, 160, 175), Th.Fog, 0.3f));

	UPostProcessComponent* PP = NewComp<UPostProcessComponent>();
	PP->bUnbound = true;
	PP->Priority = 0.f;
	FPostProcessSettings& S = PP->Settings;
	S.bOverride_DynamicGlobalIlluminationMethod = true;
	S.DynamicGlobalIlluminationMethod = EDynamicGlobalIlluminationMethod::Lumen;
	S.bOverride_ReflectionMethod = true;
	S.ReflectionMethod = EReflectionMethod::Lumen;
	// interiors lit by ceiling lamps: let the histogram adapt low, react quickly when walking between rooms
	bool bExtendedRange = false;
	if (IConsoleVariable* CVar = IConsoleManager::Get().FindConsoleVariable(TEXT("r.DefaultFeature.AutoExposure.ExtendDefaultLuminanceRange")))
		bExtendedRange = CVar->GetInt() != 0;
	S.bOverride_AutoExposureMethod = true;
	S.AutoExposureMethod = AEM_Histogram;
	S.bOverride_AutoExposureMinBrightness = true;
	S.AutoExposureMinBrightness = bExtendedRange ? -2.f : 0.03f;     // EV100 with the extended range, luminance otherwise
	S.bOverride_AutoExposureMaxBrightness = true;
	S.AutoExposureMaxBrightness = bExtendedRange ? 10.f : 8.f;
	S.bOverride_AutoExposureBias = true;
	S.AutoExposureBias = 0.5f;
	S.bOverride_AutoExposureSpeedUp = true;
	S.AutoExposureSpeedUp = 2.5f;
	S.bOverride_AutoExposureSpeedDown = true;
	S.AutoExposureSpeedDown = 1.5f;
	S.bOverride_BloomIntensity = true;
	S.BloomIntensity = 0.4f;
	S.bOverride_VignetteIntensity = true;
	S.VignetteIntensity = 0.3f;
	PP->RegisterComponent();
}

void ALTFloor::BuildShell(const FLTFloorTheme& T)
{
	const ULTArtSettings* Art = ULTArtSettings::Get();
	check(Art);
	UStaticMesh* Box = Cube();

	auto Walkable = [this](int32 X, int32 Y) { return Layout.InBounds(X, Y) && !Layout.IsSolid(X, Y); };
	auto Special = [this](int32 X, int32 Y) { const ELTCell C = Layout.At(X, Y); return C == ELTCell::MasterDoor || C == ELTCell::Stairs; };

	// royal wallpaper: the master hall walls (x >= 42) and the hall side of x = 41, plus any RoyalWall decor
	TSet<int32> Royal;
	auto FaceKey = [](int32 X, int32 Y, ELTFace F) { return (Y * LT::MapW + X) * 4 + (int32)F; };
	for (const FLTWallDecor& D : Layout.Decor)
		if (D.Kind == ELTWallDecor::RoyalWall) Royal.Add(FaceKey(D.X, D.Y, D.Face));

	UInstancedStaticMeshComponent* Walls = AddInstanced(Box, ArtMat(Art->WallMaterial, T.Paint), true);
	UInstancedStaticMeshComponent* Wainscot = AddInstanced(Box, Mat(Scaled(FMath::Lerp(T.Paint, T.Accent, 0.35f), 0.85f)), false);
	UInstancedStaticMeshComponent* Skirting = AddInstanced(Box, Mat(Scaled(T.Wood, 0.6f)), false);
	UInstancedStaticMeshComponent* RoyalPanels = nullptr;
	UInstancedStaticMeshComponent* RoyalTrim = nullptr;

	for (int32 Y = 0; Y < LT::MapH; Y++)
		for (int32 X = 0; X < LT::MapW; X++)
		{
			if (Layout.At(X, Y) != ELTCell::Wall) continue;
			bool bNear = false;
			for (int32 DY = -1; DY <= 1 && !bNear; DY++)
				for (int32 DX = -1; DX <= 1 && !bNear; DX++)
					bNear = Walkable(X + DX, Y + DY) || Special(X + DX, Y + DY);
			if (!bNear) continue;

			Walls->AddInstance(FTransform(FRotator::ZeroRotator, FVector((X + 0.5) * Cs, (Y + 0.5) * Cs, Wh * 0.5), FVector(Cs / 100.0, Cs / 100.0, Wh / 100.0)));

			for (int32 Fi = 0; Fi < 4; Fi++)
			{
				const ELTFace F = (ELTFace)Fi;
				const FIntPoint N = FaceNeighbour(X, Y, F);
				if (!Walkable(N.X, N.Y)) continue;
				const FRotator R = FaceRot(F);
				const bool bRoyal = Royal.Contains(FaceKey(X, Y, F)) || X >= 42 || (X == 41 && F == ELTFace::E);
				if (bRoyal)
				{
					if (!RoyalPanels)
					{
						RoyalPanels = AddInstanced(Box, ArtMat(Art->RoyalWallMaterial, Lin(120, 25, 38)), false);
						RoyalTrim = AddInstanced(Box, Mat(Gold), false);
					}
					RoyalPanels->AddInstance(FTransform(R, FacePoint(X, Y, F, 0.5, 0, Wh * 0.5), FVector(0.01, Cs / 100.0, Wh / 100.0)));
					RoyalTrim->AddInstance(FTransform(R, FacePoint(X, Y, F, 1.5, 0, 95), FVector(0.03, Cs / 100.0, 0.05)));
					RoyalTrim->AddInstance(FTransform(R, FacePoint(X, Y, F, 1.5, 0, Wh - 14), FVector(0.03, Cs / 100.0, 0.08)));
				}
				else
				{
					// "lamperia": the glossy lower band of Polish school corridors
					Wainscot->AddInstance(FTransform(R, FacePoint(X, Y, F, 0.3, 0, 65), FVector(0.006, Cs / 100.0, 1.3)));
				}
				Skirting->AddInstance(FTransform(R, FacePoint(X, Y, F, 1.0, 0, 6), FVector(0.02, Cs / 100.0, 0.12)));
			}
		}

	// ---- floors, by surface (fallback colours alternate for checker and mosaic)
	TMap<int32, UInstancedStaticMeshComponent*> Floors;
	auto FloorIsm = [&](ELTSurface S, int32 Alt) -> UInstancedStaticMeshComponent*
	{
		const int32 Key = (int32)S * 2 + Alt;
		if (UInstancedStaticMeshComponent** Found = Floors.Find(Key)) return *Found;
		UMaterialInterface* M;
		switch (S)
		{
		case ELTSurface::Checker: M = ArtMat(Art->CheckerFloor, Alt ? T.Floor2 : T.Floor1); break;
		case ELTSurface::Mosaic: M = ArtMat(Art->CheckerFloor, Alt ? T.Accent : T.Floor2); break;
		case ELTSurface::Parquet: M = ArtMat(Art->ParquetFloor, T.Wood); break;
		case ELTSurface::Carpet: M = ArtMat(Art->CarpetFloor, Scaled(T.Accent, 0.55f)); break;
		case ELTSurface::Runner: M = ArtMat(Art->RunnerFloor, T.Accent); break;
		default: M = ArtMat(Art->MarbleFloor, T.Floor1); break;
		}
		UInstancedStaticMeshComponent* C = AddInstanced(Box, M, true);
		Floors.Add(Key, C);
		return C;
	};
	UInstancedStaticMeshComponent* Ceiling = AddInstanced(Box, ArtMat(Art->CeilingMaterial, Lin(236, 236, 230)), true);
	UInstancedStaticMeshComponent* Lintels = AddInstanced(Box, ArtMat(Art->WallMaterial, T.Paint), true);
	UInstancedStaticMeshComponent* LampHousings = nullptr;
	UStaticMesh* LampMesh = ArtMesh(TEXT("lamp"));
	const float LampCd = Art->LampIntensity;

	for (int32 Y = 0; Y < LT::MapH; Y++)
		for (int32 X = 0; X < LT::MapW; X++)
		{
			const bool bWalk = Walkable(X, Y);
			if (!bWalk && !Special(X, Y)) continue;
			const int32 I = Y * LT::MapW + X;
			const ELTSurface S = Layout.Surfaces[I];
			const bool bAltSurface = S == ELTSurface::Checker || S == ELTSurface::Mosaic;
			const bool bArtSurface = (S == ELTSurface::Checker || S == ELTSurface::Mosaic) && !Art->CheckerFloor.IsNull();
			const int32 Alt = bAltSurface && !bArtSurface ? (X + Y) % 2 : 0;
			const FVector C((X + 0.5) * Cs, (Y + 0.5) * Cs, 0);
			FloorIsm(S, Alt)->AddInstance(FTransform(FRotator::ZeroRotator, C - FVector(0, 0, 5), FVector(Cs / 100.0, Cs / 100.0, 0.1)));
			Ceiling->AddInstance(FTransform(FRotator::ZeroRotator, C + FVector(0, 0, Wh + 5), FVector(Cs / 100.0, Cs / 100.0, 0.1)));

			if (Layout.At(X, Y) == ELTCell::Doorway)
				Lintels->AddInstance(FTransform(FRotator::ZeroRotator, C + FVector(0, 0, (Wh + 235) * 0.5), FVector(Cs / 100.0, Cs / 100.0, (Wh - 235) / 100.0)));

			// lamps on the original grid (x % 3 == 1 && y % 3 == 1); about every fourth one casts shadows
			if (!bWalk || Layout.At(X, Y) == ELTCell::Doorway || X % 3 != 1 || Y % 3 != 1) continue;
			if (LampMesh)
			{
				const FBoxSphereBounds B = LampMesh->GetBounds();
				AddArtMesh(LampMesh, C + FVector(0, 0, Wh - (B.Origin.Z + B.BoxExtent.Z)), FRotator::ZeroRotator, false);
			}
			else
			{
				if (!LampHousings) LampHousings = AddInstanced(Box, Mat(Lin(225, 225, 220)), false);
				LampHousings->AddInstance(FTransform(FRotator::ZeroRotator, C + FVector(0, 0, Wh - 1.5), FVector(0.66, 0.66, 0.03)));
				AddBox(C + FVector(0, 0, Wh - 3.5), FRotator::ZeroRotator, FVector(60, 60, 1), Mat(Lin(255, 250, 240), true))->SetCastShadow(false);
			}
			URectLightComponent* L = NewComp<URectLightComponent>();
			L->IntensityUnits = ELightUnits::Candelas;
			L->Intensity = LampCd;
			L->bUseTemperature = true;
			L->Temperature = Art->LampTemperature;
			L->SourceWidth = 60.f;
			L->SourceHeight = 60.f;
			L->AttenuationRadius = 900.f;
			L->CastShadows = ((X / 3) + (Y / 3)) % 4 == 0;
			L->SetRelativeLocationAndRotation(C + FVector(0, 0, Wh - 6), FRotator(-90, 0, 0));
			L->RegisterComponent();
		}
}

void ALTFloor::AddPicture(const FLTWallDecor& D, double W, double H, double Z, const FLinearColor& FrameColor, double FrameW,
	const FLinearColor& Placeholder)
{
	const FRotator R = FaceRot(D.Face);
	const double O = 1.5;     // in front of the royal wallpaper / lamperia
	AddBox(FacePoint(D.X, D.Y, D.Face, O + 1.25, 0, Z), R, FVector(2.5, W + 2 * FrameW, H + 2 * FrameW), Mat(FrameColor));

	UTexture2D* Tex = nullptr;
	if (D.Caption >= 0)
		if (const ULTArtSettings* Art = ULTArtSettings::Get())
			if (const TSoftObjectPtr<UTexture2D>* P = Art->Pictures.Find(D.Caption)) Tex = P->LoadSynchronous();

	UMaterialInterface* PicMat = nullptr;
	if (Tex)
		if (UMaterialInterface* Base = PictureBase())
		{
			UMaterialInstanceDynamic* MID = UMaterialInstanceDynamic::Create(Base, this);
			MID->SetTextureParameterValue(TEXT("SlateUI"), Tex);
			MID->SetTextureParameterValue(TEXT("Texture"), Tex);
			MID->SetVectorParameterValue(TEXT("TintColorAndOpacity"), FLinearColor::White);
			PicMat = MID;
		}
	const FRotator PlaneRot = Compose(R, PictureLocalRot());
	AddShape(Plane(), FacePoint(D.X, D.Y, D.Face, O + 2.7, 0, Z), PlaneRot, FVector(W, H, 100), PicMat ? PicMat : Mat(Placeholder));

	const FString Title = D.Caption >= 0 ? FLTSchool::CaptionTitle(D.Caption, bPolish) : D.Text;
	if (Title.IsEmpty()) return;
	if (PicMat)
		AddText(Title, FacePoint(D.X, D.Y, D.Face, O + 0.5, 0, Z - H * 0.5 - FrameW - 9), R, 6.f, FColor(40, 36, 32));
	else
		AddText(Title, FacePoint(D.X, D.Y, D.Face, O + 3.2, 0, Z), R, (float)FMath::Min(W / 9.0, 9.0), FColor(255, 252, 240));
}

void ALTFloor::BuildDecor(const FLTWallDecor& D, const FLTFloorTheme& T)
{
	const ULTArtSettings* Art = ULTArtSettings::Get();
	const FRotator R = FaceRot(D.Face);
	const double O = 1.5;
	auto At = [&](double Out, double Lateral, double Z) { return FacePoint(D.X, D.Y, D.Face, O + Out, Lateral, Z); };
	auto Box = [&](double Out, double Lateral, double Z, const FVector& Size, UMaterialInterface* M)
	{
		// Out/Lateral/Z = the box's back-centre; Size = depth, width, height
		return AddBox(At(Out + Size.X * 0.5, Lateral, Z), R, Size, M);
	};
	// art meshes hang with their back (min X) on the wall
	auto PlaceArt = [&](UStaticMesh* M, bool bBlock)
	{
		const FBoxSphereBounds B = M->GetBounds();
		return AddArtMesh(M, At(-(B.Origin.X - B.BoxExtent.X), 0, 0), R, bBlock);
	};
	auto Block = [&](double Depth, double Width, double Height)
	{
		UBoxComponent* C = NewComp<UBoxComponent>();
		C->SetBoxExtent(FVector(Depth * 0.5, Width * 0.5, Height * 0.5));
		C->SetRelativeLocationAndRotation(At(Depth * 0.5, 0, Height * 0.5), R);
		C->SetCollisionProfileName(UCollisionProfile::BlockAllDynamic_ProfileName);
		C->SetGenerateOverlapEvents(false);
		C->RegisterComponent();
	};
	const int32 Subject = D.Room >= 0 ? FLTSchool::SubjectOf(FloorIndex, D.Room) : -1;

	switch (D.Kind)
	{
	case ELTWallDecor::Window:
	{
		if (UStaticMesh* M = ArtMesh(TEXT("window"))) PlaceArt(M, false);
		else
		{
			UMaterialInterface* Frame = Mat(Lin(240, 240, 236));
			AddBox(At(0.5, 0, 170), R, FVector(1, 110, 150), Mat(Daylight, true))->SetCastShadow(false);
			if (!Art->GlassMaterial.IsNull()) Box(2.5, 0, 170, FVector(0.5, 110, 150), ArtMat(Art->GlassMaterial, Daylight));
			Box(0, 0, 248.5, FVector(6, 124, 7), Frame);
			Box(0, 0, 91.5, FVector(6, 124, 7), Frame);
			Box(0, 58.5, 170, FVector(6, 7, 164), Frame);
			Box(0, -58.5, 170, FVector(6, 7, 164), Frame);
			Box(0, 0, 170, FVector(4, 4, 150), Frame);
			Box(0, 0, 205, FVector(4, 110, 4), Frame);
			Box(0, 0, 86, FVector(14, 134, 4), Frame);
		}
		// daylight coming in through the window
		URectLightComponent* L = NewComp<URectLightComponent>();
		L->IntensityUnits = ELightUnits::Candelas;
		L->Intensity = Art->LampIntensity * 1.5f;
		L->bUseTemperature = true;
		L->Temperature = 6500.f;
		L->SourceWidth = 110.f;
		L->SourceHeight = 150.f;
		L->AttenuationRadius = 650.f;
		L->CastShadows = false;
		L->SetRelativeLocationAndRotation(At(12, 0, 170), R);
		L->RegisterComponent();
		break;
	}

	case ELTWallDecor::Bookshelf:
	{
		if (UStaticMesh* M = ArtMesh(TEXT("bookshelf"))) { PlaceArt(M, true); break; }
		UMaterialInterface* W = Mat(Scaled(T.Wood, 0.75f));
		Box(0, 0, 100, FVector(2, 120, 200), Mat(Scaled(T.Wood, 0.5f)));
		Box(0, 58.5, 100, FVector(35, 3, 200), W);
		Box(0, -58.5, 100, FVector(35, 3, 200), W);
		for (int32 I = 0; I < 5; I++) Box(0, 0, 1.5 + I * 49, FVector(35, 114, 3), W);
		for (int32 Row = 0; Row < 4; Row++)
			for (int32 K = 0; K < 2; K++)
			{
				const int32 Seed = D.X * 7 + D.Y * 13 + Row * 3 + K;
				const double BW = 40 + (Seed % 4) * 4, BH = 28 + (Seed % 3) * 4;
				Box(4, (K == 0 ? 1 : -1) * (28 + (Seed % 5) * 1.5), 3 + Row * 49 + BH * 0.5, FVector(22, BW, BH),
					Mat(Scaled(Palette(Seed), 0.8f)));
			}
		Block(36, 120, 200);
		break;
	}

	case ELTWallDecor::Notice:
	{
		Box(0, 0, 150, FVector(2, 114, 79), Mat(Scaled(T.Wood, 0.6f)));
		Box(2, 0, 150, FVector(1, 108, 73), Mat(Lin(190, 145, 95)));
		const FVector2D Sheets[] = { FVector2D(-35, 160), FVector2D(-5, 168), FVector2D(28, 158), FVector2D(-22, 128), FVector2D(18, 130) };
		for (int32 I = 0; I < 5; I++)
			Box(3, Sheets[I].X, Sheets[I].Y, FVector(0.3, 20, 25), Mat(I % 2 ? Lin(250, 250, 245) : FMath::Lerp(Palette(I + D.X), FLinearColor::White, 0.6f)));
		break;
	}

	case ELTWallDecor::Poster:
		AddPicture(D, 60, 85, 165, Lin(30, 30, 30), 1.5, Palette(D.Index + 2));
		break;

	case ELTWallDecor::KornikPoster:
		AddPicture(D, 70, 100, 160, T.Wood, 3, FMath::Lerp(Lin(70, 120, 170), Palette(D.Index), 0.35f));
		break;

	case ELTWallDecor::SubjectPoster:
		AddPicture(D, 70, 100, 160, Lin(40, 40, 45), 1.5, FMath::Lerp(SubjectColor(Subject), FLinearColor::White, 0.15f * D.Index));
		break;

	case ELTWallDecor::Painting:
		AddPicture(D, 100, 75, 170, Gold, 8, FMath::Lerp(Lin(70, 55, 40), Palette(D.Index), 0.25f));
		break;

	case ELTWallDecor::RoomSign:
		Box(0, 0, 225, FVector(1.5, 84, 38), Mat(SubjectColor(Subject)));
		Box(1.5, 0, 225, FVector(1, 80, 34), Mat(Lin(248, 248, 244)));
		AddText(D.Text, At(2.8, 0, 225), R, 10.f, FColor(30, 30, 40));
		break;

	case ELTWallDecor::Blackboard:
	{
		Box(0, 0, 152, FVector(3, 148, 115), ArtMat(Art->BlackboardMaterial, Lin(32, 58, 46)));
		Box(0, 0, 212, FVector(4, 150, 5), Mat(T.Wood));
		Box(0, 0, 92, FVector(9, 150, 3), Mat(T.Wood));
		if (D.Index != 0 || D.Text.IsEmpty()) break;
		// the text goes once across both halves: centre between this half and its partner
		FVector TextAt = At(3.2, 0, 175);
		for (const FLTWallDecor& Other : Layout.Decor)
			if (Other.Kind == ELTWallDecor::Blackboard && Other.Room == D.Room && Other.Face == D.Face && Other.Index == 1)
			{
				TextAt = (TextAt + FacePoint(Other.X, Other.Y, Other.Face, O + 3.2, 0, 175)) * 0.5;
				break;
			}
		AddText(D.Text, TextAt, R, 22.f, FColor(235, 235, 225));
		break;
	}

	case ELTWallDecor::Banner:
	{
		Box(0, 0, 262, FVector(1, 152, 62), Mat(T.Accent));
		AddShape(Cylinder(), At(3, 0, 295), Compose(R, FRotator(0, 0, 90)), FVector(3, 3, 152), Mat(Gold));
		if (D.Index == 1 && !D.Text.IsEmpty()) AddText(D.Text, At(1.6, 0, 262), R, 10.f, FColor(255, 250, 235));
		break;
	}

	case ELTWallDecor::YardDoor:
	{
		if (UStaticMesh* M = ArtMesh(TEXT("door"))) PlaceArt(M, false);
		else
		{
			const FLinearColor Green = Lin(45, 140, 75);
			Box(0, 0, 122.5, FVector(4, 150, 245), Mat(Lin(30, 85, 50)));
			for (int32 S = -1; S <= 1; S += 2)
			{
				Box(4, S * 35, 115, FVector(4, 68, 228), Mat(Green));
				AddBox(At(8.3, S * 35, 160), R, FVector(0.6, 48, 95), Mat(Daylight, true))->SetCastShadow(false);
				Box(8, S * 35, 105, FVector(4, 55, 4), Mat(Metal));
			}
		}
		const FString Label = !D.Text.IsEmpty() ? D.Text : FString(bPolish ? TEXT("BOISKO") : TEXT("PLAYGROUND"));
		Box(0, 0, 272, FVector(2, 120, 30), Mat(Lin(20, 120, 60)));
		AddText(Label, At(2.3, 0, 272), R, 16.f, FColor(255, 255, 255));
		break;
	}

	case ELTWallDecor::Lockers:
	{
		if (UStaticMesh* M = ArtMesh(TEXT("lockers"))) { PlaceArt(M, true); break; }
		static const FLinearColor Colors[] = { Lin(70, 110, 165), Lin(80, 140, 95), Lin(200, 120, 60) };
		const FLinearColor C = Colors[((D.Index % 3) + 3) % 3];
		UMaterialInterface* Dark = Mat(Lin(35, 35, 40));
		Box(0, 0, 5, FVector(40, 148, 10), Dark);
		Box(0, 0, 102.5, FVector(45, 148, 185), Mat(C));
		for (int32 I = -1; I <= 1; I++)
		{
			const double Y = I * 49.3;
			if (I < 1) Box(45, Y + 24.6, 102.5, FVector(0.6, 1.5, 180), Dark);
			Box(45, Y, 178, FVector(0.6, 30, 8), Dark);
			Box(45, Y - 18, 110, FVector(1.5, 2, 12), Mat(Metal));
		}
		Block(46, 148, 195);
		break;
	}

	case ELTWallDecor::DisplayCase:
	{
		if (UStaticMesh* M = ArtMesh(TEXT("display_case"))) { PlaceArt(M, true); break; }
		UMaterialInterface* W = Mat(Scaled(T.Wood, 0.7f));
		Box(0, 0, 42.5, FVector(45, 130, 85), W);
		Box(0, 0, 176, FVector(45, 130, 3), W);
		for (int32 S = -1; S <= 1; S += 2)
		{
			Box(41, S * 63, 130, FVector(4, 4, 90), W);
			Box(0, S * 63, 130, FVector(4, 4, 90), W);
		}
		if (!Art->GlassMaterial.IsNull()) Box(0, 0, 130, FVector(44, 126, 89), ArtMat(Art->GlassMaterial, Daylight));
		UMaterialInterface* G = Mat(Gold);
		for (int32 I = 0; I < 3; I++)
		{
			const double Y = (I - 1) * 38, H = 18 + (I % 2) * 10;
			AddShape(Cylinder(), At(22, Y, 85 + H * 0.5), R, FVector(8, 8, H), G);
			AddShape(Sphere(), At(22, Y, 85 + H + 6), R, FVector(14, 14, 12), G);
		}
		Block(46, 130, 178);
		break;
	}

	case ELTWallDecor::RoyalWall:
	default:
		break;     // the wallpaper is part of the wall shell
	}
}

void ALTFloor::BuildMasterDoor(const FLTFloorTheme& T)
{
	const ULTArtSettings* Art = ULTArtSettings::Get();
	const FIntPoint P = Layout.MasterDoor;
	const FVector C((P.X + 0.5) * Cs, (P.Y + 0.5) * Cs, 0);
	UMaterialInterface* WallM = ArtMat(Art->WallMaterial, T.Paint);

	// lintel and jambs make a 120 cm doorway in the wall cell
	AddBox(C + FVector(0, 0, (Wh + 240) * 0.5), FRotator::ZeroRotator, FVector(Cs, Cs, Wh - 240), WallM)
		->SetCollisionProfileName(UCollisionProfile::BlockAll_ProfileName);
	for (int32 S = -1; S <= 1; S += 2)
		AddBox(C + FVector(0, S * (Cs * 0.5 - 7.5), 120), FRotator::ZeroRotator, FVector(Cs, 15, 240), WallM)
			->SetCollisionProfileName(UCollisionProfile::BlockAll_ProfileName);

	// the door: an unscaled pivot with the parts under it, so OpenMasterDoor can hide it all
	MasterDoorMesh = NewComp<UStaticMeshComponent>();
	MasterDoorMesh->SetRelativeLocation(C);
	MasterDoorMesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	MasterDoorMesh->RegisterComponent();
	if (UStaticMesh* M = ArtMesh(TEXT("master_door")))
	{
		UStaticMeshComponent* D = AddArtMesh(M, FVector::ZeroVector, FRotator::ZeroRotator, true);
		D->AttachToComponent(MasterDoorMesh, FAttachmentTransformRules::KeepRelativeTransform);
		return;
	}
	AddBox(FVector(0, 0, 120), FRotator::ZeroRotator, FVector(10, 120, 240), Mat(Lin(90, 45, 25)), MasterDoorMesh)
		->SetCollisionProfileName(UCollisionProfile::BlockAll_ProfileName);
	UMaterialInterface* G = Mat(Gold);
	for (int32 SX = -1; SX <= 1; SX += 2)
	{
		for (int32 SY = -1; SY <= 1; SY += 2)
			for (int32 Row = 0; Row < 2; Row++)
				AddBox(FVector(SX * 5.5, SY * 29, 70 + Row * 100), FRotator::ZeroRotator, FVector(1, 46, Row ? 80 : 90), Mat(Lin(120, 62, 32)), MasterDoorMesh);
		AddShape(Sphere(), FVector(SX * 8, 0, 110), FRotator::ZeroRotator, FVector(7, 7, 7), G, MasterDoorMesh);
		AddShape(Cylinder(), FVector(SX * 5.5, 0, 225), FRotator(90, 0, 0), FVector(16, 16, 1), G, MasterDoorMesh);
	}
	AddBox(FVector(0, 0, 120), FRotator::ZeroRotator, FVector(11, 1.5, 236), G, MasterDoorMesh);
}

void ALTFloor::BuildStairs(const FLTFloorTheme& T)
{
	const ULTArtSettings* Art = ULTArtSettings::Get();
	const FIntPoint P = Layout.Stairs;
	const double X0 = P.X * Cs, YC = (P.Y + 0.5) * Cs;
	UMaterialInterface* WallM = ArtMat(Art->WallMaterial, T.Paint);
	auto Solid = [](UStaticMeshComponent* C) { C->SetCollisionProfileName(UCollisionProfile::BlockAll_ProfileName); return C; };

	// enclosure behind the stairs (beyond the map edge), steps going up east
	Solid(AddBox(FVector(X0 + Cs * 1.5, YC, Wh * 0.5), FRotator::ZeroRotator, FVector(Cs, Cs, Wh), WallM));
	UMaterialInterface* StepM = ArtMat(Art->MarbleFloor, T.Floor1);
	const int32 Steps = 6;
	const double Run = Cs / Steps, Rise = 20;
	for (int32 I = 0; I < Steps; I++)
	{
		const double From = X0 + I * Run, Len = Cs - I * Run, H = (I + 1) * Rise;
		Solid(AddBox(FVector(From + Len * 0.5, YC, H * 0.5), FRotator::ZeroRotator, FVector(Len, 130, H), StepM));
		AddBox(FVector(From + 2, YC, H - 0.5), FRotator::ZeroRotator, FVector(4, 130, 1.2), Mat(Scaled(T.Wood, 0.5f)));
	}
	for (int32 S = -1; S <= 1; S += 2)
		AddShape(Cylinder(), FVector(X0 + Cs * 0.5, YC + S * 62, 140), FRotator(-90 + FMath::RadiansToDegrees(FMath::Atan2(Rise * Steps, Cs)), 0, 0),
			FVector(5, 5, Cs * 1.1), Mat(Scaled(T.Wood, 0.7f)));

	// lintel with the sign and the locked gate in front
	Solid(AddBox(FVector(X0 + 10, YC, (Wh + 250) * 0.5), FRotator::ZeroRotator, FVector(20, Cs, Wh - 250), WallM));
	AddText(bPolish ? TEXT("SCHODY") : TEXT("STAIRS"), FVector(X0 - 1, YC, 290), FRotator(0, 180, 0), 18.f, FColor(70, 50, 30));

	StairsMesh = NewComp<UStaticMeshComponent>();
	StairsMesh->SetRelativeLocation(FVector(X0 + 6, YC, 0));
	StairsMesh->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	StairsMesh->RegisterComponent();
	UMaterialInterface* Iron = Mat(Lin(45, 45, 50));
	Solid(AddBox(FVector(0, 0, 125), FRotator::ZeroRotator, FVector(4, 150, 250), Mat(Scaled(T.Wood, 0.55f)), StairsMesh));
	for (int32 I = -3; I <= 3; I++)
		AddBox(FVector(-3, I * 19, 125), FRotator::ZeroRotator, FVector(2, 3, 246), Iron, StairsMesh);
	AddBox(FVector(-3, 0, 125), FRotator::ZeroRotator, FVector(2, 146, 4), Iron, StairsMesh);
	AddBox(FVector(-5, 0, 115), FRotator::ZeroRotator, FVector(4, 12, 14), Mat(Gold), StairsMesh);

	StairsLight = NewComp<UPointLightComponent>();
	StairsLight->IntensityUnits = ELightUnits::Candelas;
	StairsLight->Intensity = Art->LampIntensity * 3.f;
	StairsLight->LightColor = FColor(255, 214, 150);
	StairsLight->AttenuationRadius = 600.f;
	StairsLight->CastShadows = false;
	StairsLight->SetRelativeLocation(FVector(X0 + Cs * 0.7, YC, 260));
	StairsLight->SetVisibility(false);
	StairsLight->RegisterComponent();
}

void ALTFloor::BuildProps(int32 DoneMask, const FLTFloorTheme& T)
{
	Rooms.SetNum(Layout.Rooms.Num());
	for (int32 I = 0; I < Layout.Rooms.Num(); I++)
	{
		Rooms[I].Layout = Layout.Rooms[I];
		Rooms[I].bDone = (DoneMask >> Layout.Rooms[I].Index & 1) != 0;
	}

	for (const FLTPropSpawn& P : Layout.Props)
	{
		const FVector Loc = LT::MapToWorld(P.X, P.Y, P.Z);
		const double Yaw = LT::MapYawToDegrees(P.Yaw);
		FLTRoomRuntime* Room = Rooms.IsValidIndex(P.Room) ? &Rooms[P.Room] : nullptr;
		const FLinearColor RoomColor = Room ? SubjectColor(Room->Layout.Subject) : T.Accent;

		switch (P.Kind)
		{
		case ELTProp::Teacher:
			if (ALTTurtleNPC* NPC = SpawnPart<ALTTurtleNPC>(Loc, Yaw))
			{
				NPC->Init(P, SubjectColor(P.Variant));
				if (Room) Room->Teacher = NPC;
			}
			break;

		case ELTProp::Master:
			if (ALTTurtleNPC* NPC = SpawnPart<ALTTurtleNPC>(Loc, Yaw))
			{
				FLTPropSpawn Copy = P;
				if (Copy.Name.IsEmpty()) Copy.Name = bPolish ? TEXT("Mistrz Żółw") : TEXT("Master Turtle");
				NPC->Init(Copy, Lin(95, 40, 150));
				Master = NPC;
			}
			break;

		case ELTProp::Kid:
			if (ALTKid* Kid = SpawnPart<ALTKid>(Loc, Yaw))
			{
				static const FLinearColor Caps[] = { Lin(220, 50, 50), Lin(50, 110, 220), Lin(240, 150, 40) };
				Kid->Init(P, Caps[((P.Variant % 3) + 3) % 3]);
				Kid->MinX = Layout.KidMinX;
				Kid->MaxX = Layout.KidMaxX;
				Kids.Add(Kid);
			}
			break;

		case ELTProp::Gift:
			if (ALTGift* Gift = SpawnPart<ALTGift>(Loc, Yaw))
			{
				Gift->Init(P, RoomColor);
				if (Room)
				{
					Room->Gift = Gift;
					if (Room->bDone) Gift->Collect();
				}
			}
			break;

		case ELTProp::Marker:
		case ELTProp::MasterMarker:
		{
			// above the head: teachers are 155 cm, the Master Turtle 190 cm
			const bool bMaster = P.Kind == ELTProp::MasterMarker;
			const FVector At = LT::MapToWorld(P.X, P.Y) + FVector(0, 0, FMath::Max(P.Z * Cs, bMaster ? 245.0 : 205.0));
			if (ALTMarker* M = SpawnPart<ALTMarker>(At, Yaw))
			{
				if (bMaster) MasterMarker = M;
				else if (Room)
				{
					Room->Marker = M;
					M->SetDone(Room->bDone);
				}
			}
			break;
		}

		case ELTProp::Coin:
		{
			const bool bFloating = P.Z > 0.2;
			const FVector At = LT::MapToWorld(P.X, P.Y) + FVector(0, 0, bFloating ? 135.0 : 35.0);
			if (ALTCoin* Coin = SpawnPart<ALTCoin>(At, Yaw))
			{
				Coin->Setup(bFloating);
				Coin->OnCollected.AddUObject(this, &ALTFloor::HandleCoin);
			}
			break;
		}

		default:
			if (ALTProp* Prop = SpawnPart<ALTProp>(Loc, Yaw))
			{
				FLinearColor Tint = T.Wood;
				switch (P.Kind)
				{
				case ELTProp::Plant: Tint = Lin(60, 130, 55); break;
				case ELTProp::Artifact: Tint = RoomColor; break;
				case ELTProp::Clock: Tint = Scaled(T.Wood, 0.7f); break;
				default: break;
				}
				Prop->Init(P, Tint);
			}
			break;
		}
	}
}

// ============================================================================================ ALTFloor: runtime

int32 ALTFloor::GiftsDone() const
{
	int32 N = 0;
	for (const FLTRoomRuntime& R : Rooms) N += R.bDone ? 1 : 0;
	return N;
}

void ALTFloor::MarkRoomDone(int32 RoomIndex)
{
	if (!Rooms.IsValidIndex(RoomIndex)) return;
	FLTRoomRuntime& R = Rooms[RoomIndex];
	R.bDone = true;
	if (R.Gift && !R.Gift->IsHidden())
	{
		SpawnSparkles(R.Gift->GetActorLocation(), 14);
		R.Gift->Collect();
	}
	if (R.Marker) R.Marker->SetDone(true);
}

void ALTFloor::MarkMasterDone()
{
	if (MasterMarker) MasterMarker->SetDone(true);
}

void ALTFloor::OpenMasterDoor()
{
	if (bMasterDoorOpen) return;
	bMasterDoorOpen = true;
	if (!MasterDoorMesh) return;
	MasterDoorMesh->SetVisibility(false, true);
	TArray<USceneComponent*> DoorParts;
	MasterDoorMesh->GetChildrenComponents(true, DoorParts);
	for (USceneComponent* C : DoorParts)
		if (UPrimitiveComponent* Prim = Cast<UPrimitiveComponent>(C)) Prim->SetCollisionEnabled(ECollisionEnabled::NoCollision);
}

void ALTFloor::OpenStairs()
{
	if (bStairsOpen) return;
	bStairsOpen = true;
	if (StairsMesh)
	{
		StairsMesh->SetVisibility(false, true);
		TArray<USceneComponent*> GateParts;
		StairsMesh->GetChildrenComponents(true, GateParts);
		for (USceneComponent* C : GateParts)
			if (UPrimitiveComponent* Prim = Cast<UPrimitiveComponent>(C)) Prim->SetCollisionEnabled(ECollisionEnabled::NoCollision);
	}
	if (StairsLight) StairsLight->SetVisibility(true);
}

void ALTFloor::HandleCoin(ALTCoin* Coin)
{
	// the game mode adds the coin to the save, plays the sound and spawns the sparkles
	OnCoinCollected.Broadcast(Coin);
}

void ALTFloor::SpawnSparkles(const FVector& At, int32 Count)
{
	UMaterialInterface* M = Mat(Lin(255, 215, 90), true);
	const FVector Local = At - GetActorLocation();
	for (int32 I = 0; I < Count; I++)
	{
		// Game.Sparkles: 0.8-2 cells/s sideways, 1-2.5 cells/s up, 0.5-0.8 s
		const double A = FMath::FRand() * UE_DOUBLE_PI * 2, S = (0.8 + FMath::FRand() * 1.2) * Cs;
		UStaticMeshComponent* C = AddShape(Sphere(), Local, FRotator::ZeroRotator, FVector(8), M);
		C->SetCastShadow(false);
		FSparkle& Sp = Sparkles.AddDefaulted_GetRef();
		Sp.Comp = C;
		Sp.Vel = FVector(FMath::Cos(A) * S, FMath::Sin(A) * S, (1.0 + FMath::FRand() * 1.5) * Cs);
		Sp.Life = 0.5f + FMath::FRand() * 0.3f;
	}
}

void ALTFloor::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	for (int32 I = Sparkles.Num() - 1; I >= 0; I--)
	{
		FSparkle& S = Sparkles[I];
		UStaticMeshComponent* C = S.Comp.Get();
		S.Life -= DeltaSeconds;
		if (!C || S.Life <= 0)
		{
			if (C) C->DestroyComponent();
			Sparkles.RemoveAtSwap(I);
			continue;
		}
		FVector P = C->GetRelativeLocation() + S.Vel * DeltaSeconds;
		S.Vel.Z -= 4.0 * Cs * DeltaSeconds;
		if (P.Z < 3) { P.Z = 3; S.Vel.Z = 0; }
		C->SetRelativeLocation(P);
		C->SetRelativeScale3D(FVector(0.08 * FMath::Clamp(S.Life / 0.25f, 0.2f, 1.f)));
	}
}

void ALTFloor::Destroyed()
{
	for (AActor* A : Spawned)
		if (IsValid(A)) A->Destroy();
	Spawned.Empty();
	Sparkles.Empty();
	Super::Destroyed();
}
