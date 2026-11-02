#pragma once

#include "CoreMinimal.h"
#include "LTTypes.h"
#include "LTSchool.generated.h"

/** What a floor plan cell is. Everything that is not solid can be walked on. */
UENUM()
enum class ELTCell : uint8
{
	Wall, Lobby, Corridor, Classroom, Doorway, MasterHall,
	MasterDoor,     // wall cell until all 10 gifts are collected, then carpet
	Stairs,         // wall cell with the stairs up; walkable trigger once the Master Turtle opens them
};

/** Floor covering of a walkable cell (the original marble / checker / parquet / carpet / runner / mosaic). */
UENUM()
enum class ELTSurface : uint8
{
	None, Marble, Checker, Parquet, Carpet, Runner, Mosaic
};

/** The direction a wall face points to (the side of the wall cell it is seen from). Same order as World.FaceN..FaceE. */
UENUM()
enum class ELTFace : uint8
{
	N,  // faces -y
	S,  // faces +y
	W,  // faces -x
	E,  // faces +x
};

/** Something mounted on one face of a wall cell. */
UENUM()
enum class ELTWallDecor : uint8
{
	Window, Bookshelf, Notice, Poster, Lockers, KornikPoster, SubjectPoster, Painting,
	RoomSign, Blackboard, Banner, YardDoor, DisplayCase, RoyalWall,
};

USTRUCT()
struct FLTWallDecor
{
	GENERATED_BODY()

	int32 X = 0, Y = 0;
	ELTFace Face = ELTFace::N;
	ELTWallDecor Kind = ELTWallDecor::Poster;
	/** Variant: poster / Kórnik poster / painting kind, subject poster variant, banner part 0-2, blackboard half 0-1. */
	int32 Index = 0;
	/** Classroom index 0-9 for signs, blackboards and subject posters, -1 otherwise. */
	int32 Room = -1;
	/** Caption id (FLTSchool::CaptionText) when the player can walk up and read it, -1 = none. */
	int32 Caption = -1;
	/** Text written on it (room sign label + subject, banner, blackboard), already in the build language. */
	FString Text;
};

UENUM()
enum class ELTProp : uint8
{
	Teacher, TeacherDesk, StudentDesk, Gift, Marker, Master, MasterMarker, Throne, Plant, Bench,
	Globe, Clock, Fountain, Artifact, Coin, Kid,
};

/** A thing placed on the floor (people, furniture, subject artifacts, coins). Map coordinates. */
USTRUCT()
struct FLTPropSpawn
{
	GENERATED_BODY()

	ELTProp Kind = ELTProp::Plant;
	/** Model key from the original game ("tdesk", "desk2", "teacher3", "m_abacus", "plant", "gift", ...). Art settings map keys to meshes. */
	FName Key;
	double X = 0, Y = 0, Z = 0;     // Z = height above the floor in cells
	double Yaw = 0;                 // map radians; models face +x at 0
	double Scale = 1;
	double W = 0.5, H = 0.5;        // rough size in cells (used to size fallback shapes)
	double Radius = 0;              // round obstacle (> 0 = solid)
	double HalfX = 0, HalfY = 0;    // box obstacle (> 0 = solid), in the prop's local frame before yaw
	int32 Room = -1;                // classroom this belongs to
	int32 Variant = 0;              // subject for teachers, look for kids / desks
	int32 Walk = 0;                 // kids: +1 / -1 walking direction along the corridor, 0 = standing
	FString Name;                   // teacher / kid display name
};

USTRUCT()
struct FLTRoomLayout
{
	GENERATED_BODY()

	int32 Index = 0;
	int32 Subject = 0;
	FString Label;                  // "1.01"
	FString TeacherName;            // "Pani Zofia Kowalska"
	int32 X0 = 0, Y0 = 0, X1 = 0, Y1 = 0;
	FVector2D TeacherPos = FVector2D::ZeroVector;
	FVector2D GiftPos = FVector2D::ZeroVector;

	bool Contains(double X, double Y) const { return X >= X0 && X < X1 + 1 && Y >= Y0 && Y < Y1 + 1; }
};

/** A spot in front of a painting or poster where its caption is shown. */
USTRUCT()
struct FLTCaptionSpot
{
	GENERATED_BODY()

	double X = 0, Y = 0;
	int32 Caption = -1;
};

/** Everything needed to build one school floor; a pure-data port of the original School.Build. */
USTRUCT()
struct FLTFloorLayout
{
	GENERATED_BODY()

	int32 Floor = 0;
	TArray<ELTCell> Cells;          // MapW * MapH, index y * MapW + x
	TArray<ELTSurface> Surfaces;    // same indexing
	TArray<FLTWallDecor> Decor;
	TArray<FLTPropSpawn> Props;
	TArray<FLTRoomLayout> Rooms;    // ordered by room index 0-9
	TArray<FLTCaptionSpot> Captions;
	FIntPoint MasterDoor = FIntPoint(41, 10);
	FIntPoint Stairs = FIntPoint(53, 10);
	FIntPoint YardDoor = FIntPoint(0, 10);
	FVector2D MasterPos = FVector2D(48.5, 10.5);
	FVector2D Start = FVector2D(3, 10.5);
	double StartYaw = 0;
	/** Classmates walk back and forth along the corridor between these x values. */
	double KidMinX = 2, KidMaxX = 39;

	bool InBounds(int32 X, int32 Y) const { return X >= 0 && Y >= 0 && X < LT::MapW && Y < LT::MapH; }
	ELTCell At(int32 X, int32 Y) const { return InBounds(X, Y) ? Cells[Y * LT::MapW + X] : ELTCell::Wall; }
	/** Solid for walking (MasterDoor and Stairs are solid until opened at runtime). */
	bool IsSolid(int32 X, int32 Y) const
	{
		const ELTCell C = At(X, Y);
		return C == ELTCell::Wall || C == ELTCell::MasterDoor || C == ELTCell::Stairs;
	}
};

struct FLTSubject
{
	FString Pl, En;
	FLinearColor Color;
	const FString& Name(bool bPolish) const { return bPolish ? Pl : En; }
};

struct FLTFloorTheme
{
	FString NamePl, NameEn;
	FLinearColor Paint, Accent, Wood, Floor1, Floor2, Fog;
	const FString& Name(bool bPolish) const { return bPolish ? NamePl : NameEn; }
};

/**
 * The school: 8 floors, each with a lobby, a long corridor, 10 classrooms (5 on each side, one subject
 * each) and the Master Turtle's hall at the end of the corridor with the stairs up. Port of School.cs.
 */
struct LAZYTURTLE_API FLTSchool
{
	static const TArray<FLTSubject>& Subjects();
	static const TArray<FLTFloorTheme>& Themes();

	/** Subject taught in classroom Room on Floor (the order changes from floor to floor). */
	static int32 SubjectOf(int32 Floor, int32 Room) { return (Room + Floor * 3) % LT::Classes; }
	static FString RoomLabel(int32 Floor, int32 Room);
	/** e.g. "Pani Zofia Kowalska" / "Mrs Smith". Even subjects: ladies, odd: gentlemen. */
	static FString TeacherFullName(int32 Floor, int32 Subject, bool bPolish);
	/** Model keys of the subject artifacts in a subject's classroom. */
	static TArray<FName> ArtifactKeys(int32 Subject);

	/** Caption shown when standing in front of a painting / Kórnik poster / subject poster (ids as in the original). */
	static FString CaptionText(int32 Caption, bool bPolish);
	/** Short title of a painting / poster (used as the picture's fallback text). */
	static FString CaptionTitle(int32 Caption, bool bPolish);

	static constexpr int32 KornikCaption = 100;
	static constexpr int32 SubjectCaption = 200;

	/** The floor plan with all decorations, furniture, people and coins. Deterministic for a floor. */
	static FLTFloorLayout BuildLayout(int32 Floor, bool bPolish);
};
