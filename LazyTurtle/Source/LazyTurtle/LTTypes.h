#pragma once

#include "CoreMinimal.h"
#include "LTTypes.generated.h"

/**
 * Shared constants and small types. The game logic still thinks in the original 2D floor plan
 * (map cells: x grows east, y grows south, angles in radians with 0 = +x and PI/2 = +y), and
 * everything is placed in the world with MapToWorld (1 cell = CellSize cm, UE X = map x, UE Y = map y).
 */
namespace LT
{
	constexpr int32 Floors = 8;
	constexpr int32 Classes = 10;
	constexpr int32 MapW = 54;
	constexpr int32 MapH = 21;
	constexpr int32 MaxDifficulty = 9;

	/** One cell of the floor plan in centimetres (a classroom of 6 x 7 cells is about 9 x 10.5 m). */
	constexpr double CellSize = 150.0;
	/** Height of walls and ceilings in centimetres. */
	constexpr double WallHeight = 330.0;

	inline FVector MapToWorld(double X, double Y, double Z = 0.0) { return FVector(X * CellSize, Y * CellSize, Z * CellSize); }
	inline FVector2D WorldToMap(const FVector& W) { return FVector2D(W.X / CellSize, W.Y / CellSize); }

	/** Map angle (radians) to UE yaw (degrees). Both rotate from +X towards +Y. */
	inline double MapYawToDegrees(double Radians) { return FMath::RadiansToDegrees(Radians); }

	/** Names of the synthesized sound effects (ULTAudioSubsystem::Play). */
	namespace Sfx
	{
		inline const FName Coin = TEXT("coin");
		inline const FName Jump = TEXT("jump");
		inline const FName Correct = TEXT("correct");
		inline const FName Wrong = TEXT("wrong");
		inline const FName Gift = TEXT("gift");
		inline const FName Talk = TEXT("talk");
		inline const FName Door = TEXT("door");
		inline const FName Stairs = TEXT("stairs");
		inline const FName Win = TEXT("win");
		inline const FName Bump = TEXT("bump");
		inline const FName Click = TEXT("click");
		inline const FName Select = TEXT("select");
	}
}

UENUM(BlueprintType)
enum class ELTScreen : uint8
{
	Title, Floors, Help, Intro, Playing, Dialog, Paused, FloorDone, Victory
};

UENUM(BlueprintType)
enum class ELTDialogPhase : uint8
{
	Ask, Correct, Wrong, Reward, Info
};

/** A quiz question: text, 2-4 answers and the index of the right one. */
USTRUCT(BlueprintType)
struct FLTQuestion
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly) FString Text;
	UPROPERTY(BlueprintReadOnly) TArray<FString> Answers;
	UPROPERTY(BlueprintReadOnly) int32 Correct = 0;

	bool IsValid() const { return Answers.IsValidIndex(Correct); }
	FString CorrectText() const { return IsValid() ? Answers[Correct] : FString(); }
};
