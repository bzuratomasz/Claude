#pragma once

#include "CoreMinimal.h"
#include "GameFramework/PlayerController.h"
#include "LTPlayerController.generated.h"

class UInputAction;
class UInputMappingContext;
class ALTGameMode;
class ALTTurtleCharacter;
struct FInputActionValue;
struct FKey;

/**
 * Keyboard and mouse: Enhanced Input actions and the mapping context are created in code (no input assets).
 * Movement goes to the turtle while playing; everything UI-related goes to the game mode. The mouse cursor
 * stays visible so menus, answers and the talk button can be clicked.
 */
UCLASS()
class LAZYTURTLE_API ALTPlayerController : public APlayerController
{
	GENERATED_BODY()

public:
	ALTPlayerController();

protected:
	virtual void BeginPlay() override;
	virtual void SetupInputComponent() override;

private:
	UPROPERTY() TObjectPtr<UInputMappingContext> Mapping;
	/** Keeps every created action alive. */
	UPROPERTY() TArray<TObjectPtr<UInputAction>> Actions;

	UInputAction* MoveAction = nullptr;
	UInputAction* TurnAction = nullptr;
	UInputAction* StrafeAction = nullptr;
	UInputAction* RunAction = nullptr;
	UInputAction* SpaceAction = nullptr;
	UInputAction* EnterAction = nullptr;
	UInputAction* UpAction = nullptr;
	UInputAction* DownAction = nullptr;
	UInputAction* LeftAction = nullptr;
	UInputAction* RightAction = nullptr;
	UInputAction* EscapeAction = nullptr;
	UInputAction* PauseAction = nullptr;
	UInputAction* ReadAction = nullptr;
	UInputAction* LookAction = nullptr;
	UInputAction* ZoomAction = nullptr;
	UInputAction* FullscreenAction = nullptr;
	UInputAction* FpsAction = nullptr;
	UInputAction* AnswerActions[4] = {};
	UInputAction* DebugActions[3] = {};

	void CreateInput();
	UInputAction* NewAction(const TCHAR* Name, bool bAxis1D, bool bAxis2D = false);
	void Map(UInputAction* Action, const FKey& Key, bool bNegate = false, float Scale = 1.f);

	ALTGameMode* GameMode() const;
	ALTTurtleCharacter* Turtle() const;
	bool CanMove() const;
	void ToggleFullscreen();

	void OnMove(const FInputActionValue& Value);
	void OnTurn(const FInputActionValue& Value);
	void OnStrafe(const FInputActionValue& Value);
	void OnRunStart(const FInputActionValue& Value);
	void OnRunStop(const FInputActionValue& Value);
	void OnSpace(const FInputActionValue& Value);
	void OnEnter(const FInputActionValue& Value);
	void OnUp(const FInputActionValue& Value);
	void OnDown(const FInputActionValue& Value);
	void OnLeft(const FInputActionValue& Value);
	void OnRight(const FInputActionValue& Value);
	void OnEscape(const FInputActionValue& Value);
	void OnPause(const FInputActionValue& Value);
	void OnRead(const FInputActionValue& Value);
	void OnLook(const FInputActionValue& Value);
	void OnZoom(const FInputActionValue& Value);
	void OnFullscreen(const FInputActionValue& Value);
	void OnFps(const FInputActionValue& Value);
	void OnAnswer1(const FInputActionValue& Value);
	void OnAnswer2(const FInputActionValue& Value);
	void OnAnswer3(const FInputActionValue& Value);
	void OnAnswer4(const FInputActionValue& Value);
	void OnDebug5(const FInputActionValue& Value);
	void OnDebug6(const FInputActionValue& Value);
	void OnDebug7(const FInputActionValue& Value);
};
