#include "LTPlayerController.h"

#include "Engine/Engine.h"
#include "Engine/LocalPlayer.h"
#include "Engine/World.h"
#include "EnhancedInputComponent.h"
#include "EnhancedInputSubsystems.h"
#include "GameFramework/GameUserSettings.h"
#include "GameFramework/InputSettings.h"
#include "InputAction.h"
#include "InputActionValue.h"
#include "InputCoreTypes.h"
#include "InputMappingContext.h"
#include "InputModifiers.h"
#include "LTGameMode.h"
#include "LTTurtleCharacter.h"

ALTPlayerController::ALTPlayerController()
{
	bShowMouseCursor = true;
	bEnableClickEvents = false;
	bEnableMouseOverEvents = false;
}

void ALTPlayerController::BeginPlay()
{
	Super::BeginPlay();
	if (!IsLocalController()) return;

	CreateInput();
	if (ULocalPlayer* LP = GetLocalPlayer())
		if (UEnhancedInputLocalPlayerSubsystem* Sub = LP->GetSubsystem<UEnhancedInputLocalPlayerSubsystem>())
			Sub->AddMappingContext(Mapping, 0);

	// keyboard to the game, mouse also to the Slate UI (clickable menus and answers); the cursor hides while
	// the right mouse button is held for looking around
	bShowMouseCursor = true;
	FInputModeGameAndUI Mode;
	Mode.SetLockMouseToViewportBehavior(EMouseLockMode::DoNotLock);
	Mode.SetHideCursorDuringCapture(true);
	SetInputMode(Mode);
}

UInputAction* ALTPlayerController::NewAction(const TCHAR* Name, bool bAxis1D, bool bAxis2D)
{
	UInputAction* Action = NewObject<UInputAction>(this, FName(Name));
	Action->ValueType = bAxis2D ? EInputActionValueType::Axis2D : bAxis1D ? EInputActionValueType::Axis1D : EInputActionValueType::Boolean;
	// several actions share keys (W moves and navigates menus): never swallow a key
	Action->bConsumeInput = false;
	Actions.Add(Action);
	return Action;
}

void ALTPlayerController::Map(UInputAction* Action, const FKey& Key, bool bNegate, float Scale)
{
	FEnhancedActionKeyMapping& M = Mapping->MapKey(Action, Key);
	if (bNegate) M.Modifiers.Add(NewObject<UInputModifierNegate>(Mapping));
	if (Scale != 1.f)
	{
		UInputModifierScalar* Scalar = NewObject<UInputModifierScalar>(Mapping);
		Scalar->Scalar = FVector(Scale, Scale, Scale);
		M.Modifiers.Add(Scalar);
	}
}

void ALTPlayerController::CreateInput()
{
	if (Mapping) return;
	Mapping = NewObject<UInputMappingContext>(this, TEXT("LTMapping"));

	MoveAction = NewAction(TEXT("Move"), true);
	TurnAction = NewAction(TEXT("Turn"), true);
	StrafeAction = NewAction(TEXT("Strafe"), true);
	RunAction = NewAction(TEXT("Run"), false);
	SpaceAction = NewAction(TEXT("Space"), false);
	EnterAction = NewAction(TEXT("Enter"), false);
	UpAction = NewAction(TEXT("NavUp"), false);
	DownAction = NewAction(TEXT("NavDown"), false);
	LeftAction = NewAction(TEXT("NavLeft"), false);
	RightAction = NewAction(TEXT("NavRight"), false);
	EscapeAction = NewAction(TEXT("Escape"), false);
	PauseAction = NewAction(TEXT("PauseKey"), false);
	ReadAction = NewAction(TEXT("Read"), false);
	LookAction = NewAction(TEXT("Look"), false, true);
	ZoomAction = NewAction(TEXT("Zoom"), true);
	FullscreenAction = NewAction(TEXT("Fullscreen"), false);
	FpsAction = NewAction(TEXT("Fps"), false);
	for (int32 i = 0; i < 4; i++) AnswerActions[i] = NewAction(*FString::Printf(TEXT("Answer%d"), i + 1), false);
	for (int32 i = 0; i < 3; i++) DebugActions[i] = NewAction(*FString::Printf(TEXT("DebugF%d"), i + 5), false);

	// walking (tank controls): backwards at 0.7 of the speed like the original
	Map(MoveAction, EKeys::W);
	Map(MoveAction, EKeys::Up);
	Map(MoveAction, EKeys::S, true, 0.7f);
	Map(MoveAction, EKeys::Down, true, 0.7f);
	Map(TurnAction, EKeys::D);
	Map(TurnAction, EKeys::Right);
	Map(TurnAction, EKeys::A, true);
	Map(TurnAction, EKeys::Left, true);
	Map(StrafeAction, EKeys::E);
	Map(StrafeAction, EKeys::Q, true);
	Map(RunAction, EKeys::LeftShift);
	Map(RunAction, EKeys::RightShift);
	Map(SpaceAction, EKeys::SpaceBar);

	// menus and dialogs
	Map(EnterAction, EKeys::Enter);
	Map(UpAction, EKeys::Up);
	Map(UpAction, EKeys::W);
	Map(DownAction, EKeys::Down);
	Map(DownAction, EKeys::S);
	Map(LeftAction, EKeys::Left);
	Map(LeftAction, EKeys::A);
	Map(RightAction, EKeys::Right);
	Map(RightAction, EKeys::D);
	Map(EscapeAction, EKeys::Escape);
	Map(PauseAction, EKeys::P);
	Map(ReadAction, EKeys::R);
	const FKey Numbers[4] = { EKeys::One, EKeys::Two, EKeys::Three, EKeys::Four };
	const FKey NumPad[4] = { EKeys::NumPadOne, EKeys::NumPadTwo, EKeys::NumPadThree, EKeys::NumPadFour };
	for (int32 i = 0; i < 4; i++)
	{
		Map(AnswerActions[i], Numbers[i]);
		Map(AnswerActions[i], NumPad[i]);
	}

	// mouse: look with the right button held, wheel zooms
	Map(LookAction, EKeys::Mouse2D);
	Map(ZoomAction, EKeys::MouseWheelAxis);

	// window and developer keys
	Map(FullscreenAction, EKeys::F11);
	Map(FpsAction, EKeys::F3);
	Map(DebugActions[0], EKeys::F5);
	Map(DebugActions[1], EKeys::F6);
	Map(DebugActions[2], EKeys::F7);
}

void ALTPlayerController::SetupInputComponent()
{
	// the mapping only works with an Enhanced Input component (the 5.x default); make sure we have one
	if (!InputComponent || !InputComponent->IsA<UEnhancedInputComponent>())
	{
		InputComponent = NewObject<UEnhancedInputComponent>(this, TEXT("LTInputComponent"));
		InputComponent->RegisterComponent();
	}
	Super::SetupInputComponent();
	CreateInput();

	UEnhancedInputComponent* Input = Cast<UEnhancedInputComponent>(InputComponent);
	if (!Input) return;

	Input->BindAction(MoveAction, ETriggerEvent::Triggered, this, &ALTPlayerController::OnMove);
	Input->BindAction(TurnAction, ETriggerEvent::Triggered, this, &ALTPlayerController::OnTurn);
	Input->BindAction(StrafeAction, ETriggerEvent::Triggered, this, &ALTPlayerController::OnStrafe);
	Input->BindAction(RunAction, ETriggerEvent::Started, this, &ALTPlayerController::OnRunStart);
	Input->BindAction(RunAction, ETriggerEvent::Completed, this, &ALTPlayerController::OnRunStop);
	Input->BindAction(SpaceAction, ETriggerEvent::Started, this, &ALTPlayerController::OnSpace);
	Input->BindAction(EnterAction, ETriggerEvent::Started, this, &ALTPlayerController::OnEnter);
	Input->BindAction(UpAction, ETriggerEvent::Started, this, &ALTPlayerController::OnUp);
	Input->BindAction(DownAction, ETriggerEvent::Started, this, &ALTPlayerController::OnDown);
	Input->BindAction(LeftAction, ETriggerEvent::Started, this, &ALTPlayerController::OnLeft);
	Input->BindAction(RightAction, ETriggerEvent::Started, this, &ALTPlayerController::OnRight);
	Input->BindAction(EscapeAction, ETriggerEvent::Started, this, &ALTPlayerController::OnEscape);
	Input->BindAction(PauseAction, ETriggerEvent::Started, this, &ALTPlayerController::OnPause);
	Input->BindAction(ReadAction, ETriggerEvent::Started, this, &ALTPlayerController::OnRead);
	Input->BindAction(LookAction, ETriggerEvent::Triggered, this, &ALTPlayerController::OnLook);
	Input->BindAction(ZoomAction, ETriggerEvent::Triggered, this, &ALTPlayerController::OnZoom);
	Input->BindAction(FullscreenAction, ETriggerEvent::Started, this, &ALTPlayerController::OnFullscreen);
	Input->BindAction(FpsAction, ETriggerEvent::Started, this, &ALTPlayerController::OnFps);
	Input->BindAction(AnswerActions[0], ETriggerEvent::Started, this, &ALTPlayerController::OnAnswer1);
	Input->BindAction(AnswerActions[1], ETriggerEvent::Started, this, &ALTPlayerController::OnAnswer2);
	Input->BindAction(AnswerActions[2], ETriggerEvent::Started, this, &ALTPlayerController::OnAnswer3);
	Input->BindAction(AnswerActions[3], ETriggerEvent::Started, this, &ALTPlayerController::OnAnswer4);
	Input->BindAction(DebugActions[0], ETriggerEvent::Started, this, &ALTPlayerController::OnDebug5);
	Input->BindAction(DebugActions[1], ETriggerEvent::Started, this, &ALTPlayerController::OnDebug6);
	Input->BindAction(DebugActions[2], ETriggerEvent::Started, this, &ALTPlayerController::OnDebug7);
}

ALTGameMode* ALTPlayerController::GameMode() const
{
	return GetWorld() ? GetWorld()->GetAuthGameMode<ALTGameMode>() : nullptr;
}

ALTTurtleCharacter* ALTPlayerController::Turtle() const
{
	return Cast<ALTTurtleCharacter>(GetPawn());
}

bool ALTPlayerController::CanMove() const
{
	const ALTGameMode* GM = GameMode();
	return GM && GM->IsPlaying() && Turtle();
}

void ALTPlayerController::ToggleFullscreen()
{
	UGameUserSettings* Settings = GEngine ? GEngine->GetGameUserSettings() : nullptr;
	if (!Settings) return;
	const bool bFull = Settings->GetFullscreenMode() != EWindowMode::Windowed;
	if (bFull)
	{
		Settings->SetFullscreenMode(EWindowMode::Windowed);
		Settings->SetScreenResolution(FIntPoint(1280, 720));
	}
	else
	{
		Settings->SetFullscreenMode(EWindowMode::WindowedFullscreen);
		Settings->SetScreenResolution(Settings->GetDesktopResolution());
	}
	Settings->ApplySettings(false);
}

// ---------------------------------------------------------------- movement

void ALTPlayerController::OnMove(const FInputActionValue& Value)
{
	if (CanMove()) Turtle()->MoveForward(Value.Get<float>());
}

void ALTPlayerController::OnTurn(const FInputActionValue& Value)
{
	if (CanMove()) Turtle()->Turn(Value.Get<float>());
}

void ALTPlayerController::OnStrafe(const FInputActionValue& Value)
{
	if (CanMove()) Turtle()->Strafe(Value.Get<float>());
}

void ALTPlayerController::OnRunStart(const FInputActionValue& Value)
{
	if (ALTTurtleCharacter* T = Turtle()) T->SetRunning(true);
}

void ALTPlayerController::OnRunStop(const FInputActionValue& Value)
{
	if (ALTTurtleCharacter* T = Turtle()) T->SetRunning(false);
}

void ALTPlayerController::OnLook(const FInputActionValue& Value)
{
	if (!IsInputKeyDown(EKeys::RightMouseButton)) return;
	const FVector2D Delta = Value.Get<FVector2D>();
	if (ALTGameMode* GM = GameMode()) GM->MouseLook((float)Delta.X, (float)Delta.Y);
}

void ALTPlayerController::OnZoom(const FInputActionValue& Value)
{
	if (ALTTurtleCharacter* T = Turtle()) T->Zoom(Value.Get<float>());
}

// ---------------------------------------------------------------- game keys

void ALTPlayerController::OnSpace(const FInputActionValue& Value)
{
	if (ALTGameMode* GM = GameMode()) GM->HandleInput(ELTInput::Space);
}

void ALTPlayerController::OnEnter(const FInputActionValue& Value)
{
	if (IsInputKeyDown(EKeys::LeftAlt) || IsInputKeyDown(EKeys::RightAlt))
	{
		// Alt+Enter: window mode (unless the game viewport already handles it)
		if (!GetDefault<UInputSettings>()->bAltEnterTogglesFullscreen) ToggleFullscreen();
		return;
	}
	if (ALTGameMode* GM = GameMode()) GM->HandleInput(ELTInput::Accept);
}

void ALTPlayerController::OnUp(const FInputActionValue& Value)
{
	if (ALTGameMode* GM = GameMode()) GM->HandleInput(ELTInput::Up);
}

void ALTPlayerController::OnDown(const FInputActionValue& Value)
{
	if (ALTGameMode* GM = GameMode()) GM->HandleInput(ELTInput::Down);
}

void ALTPlayerController::OnLeft(const FInputActionValue& Value)
{
	if (ALTGameMode* GM = GameMode()) GM->HandleInput(ELTInput::Left);
}

void ALTPlayerController::OnRight(const FInputActionValue& Value)
{
	if (ALTGameMode* GM = GameMode()) GM->HandleInput(ELTInput::Right);
}

void ALTPlayerController::OnEscape(const FInputActionValue& Value)
{
	if (ALTGameMode* GM = GameMode()) GM->HandleInput(ELTInput::Back);
}

void ALTPlayerController::OnPause(const FInputActionValue& Value)
{
	if (ALTGameMode* GM = GameMode()) GM->HandleInput(ELTInput::Pause);
}

void ALTPlayerController::OnRead(const FInputActionValue& Value)
{
	if (ALTGameMode* GM = GameMode()) GM->HandleInput(ELTInput::Read);
}

void ALTPlayerController::OnAnswer1(const FInputActionValue& Value) { if (ALTGameMode* GM = GameMode()) GM->HandleAnswerKey(0); }
void ALTPlayerController::OnAnswer2(const FInputActionValue& Value) { if (ALTGameMode* GM = GameMode()) GM->HandleAnswerKey(1); }
void ALTPlayerController::OnAnswer3(const FInputActionValue& Value) { if (ALTGameMode* GM = GameMode()) GM->HandleAnswerKey(2); }
void ALTPlayerController::OnAnswer4(const FInputActionValue& Value) { if (ALTGameMode* GM = GameMode()) GM->HandleAnswerKey(3); }

// ---------------------------------------------------------------- window / developer

void ALTPlayerController::OnFullscreen(const FInputActionValue& Value)
{
	// the game viewport already toggles on F11 when the input settings say so; don't toggle twice
	if (!GetDefault<UInputSettings>()->bF11TogglesFullscreen) ToggleFullscreen();
}

void ALTPlayerController::OnFps(const FInputActionValue& Value)
{
	ConsoleCommand(TEXT("stat fps"), false);
}

void ALTPlayerController::OnDebug5(const FInputActionValue& Value) { if (ALTGameMode* GM = GameMode()) GM->HandleDebugKey(5); }
void ALTPlayerController::OnDebug6(const FInputActionValue& Value) { if (ALTGameMode* GM = GameMode()) GM->HandleDebugKey(6); }
void ALTPlayerController::OnDebug7(const FInputActionValue& Value) { if (ALTGameMode* GM = GameMode()) GM->HandleDebugKey(7); }
