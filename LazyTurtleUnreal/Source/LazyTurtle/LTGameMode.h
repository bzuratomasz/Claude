#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "LTTypes.h"
#include "LTGameMode.generated.h"

class ALTFloor;
class ALTKid;
class ALTCoin;
class ALTTurtleCharacter;
class ULTSaveGame;
class ULTQuizSubsystem;
class ULTAudioSubsystem;
class ULTSpeechSubsystem;
class SLTGameUI;
struct FLTFloorTheme;

/** Keys the game rules react to, forwarded by ALTPlayerController (the original Input.Up / Accept / Back ...). */
enum class ELTInput : uint8
{
	Up,         // Up arrow / W
	Down,       // Down arrow / S
	Left,       // Left arrow / A
	Right,      // Right arrow / D
	Accept,     // Enter
	Space,      // Space: accept in menus, talk or jump while playing
	Back,       // Esc
	Pause,      // P
	Read,       // R: read the question aloud
};

/** Click actions of the UI (the original HUD hit-area codes). */
namespace LTClick
{
	constexpr int32 Answer0 = 0;        // 0-3: answers
	constexpr int32 HelpPanel = 1;
	constexpr int32 Continue = 10;
	constexpr int32 Read = 11;
	constexpr int32 Talk = 20;
	constexpr int32 Menu0 = 100;        // + menu item
	constexpr int32 Floor0 = 200;       // + floor tile
}

/** A "+3 coins" text floating up the screen. */
struct FLTPopup
{
	FString Text;
	FLinearColor Color = FLinearColor::White;
	float Time = 0;
	float X = 0.5f;     // horizontal position as a fraction of the screen width
	int32 Id = 0;
};

/** A conversation with a teacher, the Master Turtle or a classmate. */
struct FLTDialogState
{
	bool bMaster = false;
	int32 Room = -1;                    // classroom index (teacher), -1 otherwise
	bool bKid = false;                  // talking to a classmate
	TWeakObjectPtr<ALTKid> Kid;
	FString KidName;
	bool bRiddle = false;               // classmate's riddle for 3 coins
	bool bHasQuestion = false;
	FLTQuestion Q;
	ELTDialogPhase Phase = ELTDialogPhase::Ask;
	int32 Selected = 0;
	int32 Chosen = -1;
	int32 Gift = -1;                    // gift won (classroom index) for the Reward phase
	FString Line;
	float Time = 0;
	int32 Serial = 0;                   // bumped on every new line (the UI rebuilds)
};

/**
 * Game rules and state machine (menus, walking around the school, quizzes, floors): port of the original Game.cs.
 * It is the global default game mode of the empty Entry map and spawns the floor, places the player and owns the UI.
 */
UCLASS()
class LAZYTURTLE_API ALTGameMode : public AGameModeBase
{
	GENERATED_BODY()

public:
	ALTGameMode();

	virtual void BeginPlay() override;
	virtual void EndPlay(const EEndPlayReason::Type EndPlayReason) override;
	virtual void Tick(float DeltaSeconds) override;

	// ---- input (from the player controller and the UI)

	void HandleInput(ELTInput Input);
	/** Number keys 1-4 (Index 0-3). */
	void HandleAnswerKey(int32 Index);
	/** A click on a UI element (LTClick codes). */
	void HandleClick(int32 Action);
	/** F5 / F6 / F7 developer teleports (only with --debug). */
	void HandleDebugKey(int32 FunctionKey);
	/** Right mouse button held: turn the turtle and tilt the camera. */
	void MouseLook(float DX, float DY);
	bool IsPlaying() const { return Screen == ELTScreen::Playing; }
	/** Started with --debug (F5-F7 teleports). */
	bool IsDebug() const { return bDebug; }

	// ---- state for the UI

	ELTScreen GetScreen() const { return Screen; }
	int32 GetMenu() const { return Menu; }
	float GetTime() const { return Time; }
	float GetStateTime() const { return StateTime; }
	int32 GetFloorIndex() const { return FloorIndex; }
	const FLTFloorTheme& GetTheme() const;
	bool IsPolish() const;
	FString T(const FString& Key) const;
	FString F(const FString& Key, const TArray<FString>& Args) const;
	TArray<FString> GetTitleItems() const;
	TArray<FString> GetPauseItems() const;
	const ULTSaveGame* GetSave() const;
	int32 GetCoins() const;
	int32 GetGiftsDone() const;
	/** Bit mask of classrooms done on the current floor. */
	int32 GetGiftMask() const;
	int32 GetMistakes() const { return Mistakes; }
	int32 GetLastStars() const { return LastStars; }
	/** Stars the floor would give now (3 / 2 / 1 by mistakes). */
	int32 GetProspectiveStars() const { return StarsFor(Mistakes); }
	int32 GetTotalStars() const;
	bool CanTalk() const { return NearRoom >= 0 || bNearMaster || NearKid.IsValid(); }
	FString GetTalkLabel() const;
	/** Caption id of the painting / poster the player stands in front of, -1 = none. */
	int32 GetNearCaption() const { return NearCaption; }
	FString GetCaptionText() const;
	/** Timed message and its opacity (fades out during the last second); empty when none. */
	FString GetMessage() const { return MessageTime > 0 ? Message : FString(); }
	float GetMessageAlpha() const { return FMath::Clamp(MessageTime, 0.f, 1.f); }
	const TArray<FLTPopup>& GetPopups() const { return Popups; }
	const FLTDialogState& GetDialog() const { return Dialog; }
	FString GetSpeaker() const;
	FLinearColor GetSpeakerColor() const;
	bool IsSpeechAvailable() const;
	/** Compass: angle of the target relative to the camera (radians, positive = to the right); bMaster = Master Turtle / stairs. */
	bool GetCompass(float& OutRelative, bool& bOutMaster) const;
	/** T(Key), or the given text when the key is missing in the data. */
	FString TextOr(const FString& Key, const TCHAR* Pl, const TCHAR* En) const;

private:
	// ---- services
	ULTQuizSubsystem* Quiz() const;
	ULTAudioSubsystem* Audio() const;
	ULTSpeechSubsystem* Speech() const;
	ULTSaveGame* Save() const;
	void WriteSave();
	void Sfx(FName Name);
	ALTTurtleCharacter* Turtle() const;
	FVector2D PlayerMapPos() const;

	// ---- floors
	void LoadFloor(int32 Floor);
	void StartFloor(int32 Floor);
	int32 ContinueFloor() const;
	void FinishFloor();
	void PlacePlayer(const FVector2D& Pos, double YawRadians);
	void OnCoinCollected(ALTCoin* Coin);

	// ---- screens
	void SetScreen(ELTScreen NewScreen);
	void ShowMessage(const FString& Text, float Seconds = 3.5f);
	void AddPopup(const FString& Text, const FLinearColor& Color);
	void ToggleLanguage();
	void ToggleSound();
	void ToggleSpeech();
	void Pause();
	void GoToTitle();
	/** Menu navigation; returns true when the selected item was chosen. */
	bool MenuNav(ELTInput Input, int32 Count);
	void TitleChoose();
	void PauseChoose();
	void FloorSelectGo();
	void FloorDoneContinue();

	// ---- playing
	void UpdatePlaying(float DeltaSeconds);
	void FindNearby();
	void CheckDoors();
	void TryTalkOrJump(bool bJumpIfNothing);

	// ---- dialogs
	void StartDialog();
	void StartKidDialog(ALTKid* Kid);
	void AskNew();
	void SetLine(ELTDialogPhase Phase, const FString& Line);
	void ReadAloud();
	void DialogContinue();
	void Answer(int32 Choice);
	void CloseDialog();

	static int32 StarsFor(int32 InMistakes) { return InMistakes <= 2 ? 3 : InMistakes <= 5 ? 2 : 1; }

	UPROPERTY() TObjectPtr<ALTFloor> Floor;
	UPROPERTY() TObjectPtr<ULTSaveGame> FallbackSave;

	TSharedPtr<SLTGameUI> UI;
	FRandomStream Rng;

	ELTScreen Screen = ELTScreen::Title;
	int32 FloorIndex = 0;
	float Time = 0;
	float StateTime = 0;
	int32 Menu = 0;
	FString Message;
	float MessageTime = 0;
	TArray<FLTPopup> Popups;
	int32 NextPopupId = 1;
	int32 LineSerial = 0;
	FLTDialogState Dialog;
	int32 Mistakes = 0;
	int32 LastStars = 0;

	int32 NearRoom = -1;
	bool bNearMaster = false;
	TWeakObjectPtr<ALTKid> NearKid;
	int32 NearCaption = -1;

	bool bDebug = false;
	int32 DebugCaption = 0;
	/** Where the player goes as soon as the pawn exists (it is spawned after the game mode's BeginPlay in some cases). */
	bool bPlacePending = false;
	FVector2D PendingPos = FVector2D::ZeroVector;
	double PendingYaw = 0;
};
