#include "LTGameMode.h"

#include "Engine/Engine.h"
#include "Engine/GameInstance.h"
#include "Engine/GameViewportClient.h"
#include "Engine/World.h"
#include "GameFramework/PlayerController.h"
#include "Kismet/KismetSystemLibrary.h"
#include "Misc/App.h"
#include "Misc/CommandLine.h"
#include "Misc/Parse.h"
#include "LazyTurtle.h"
#include "LTGameUI.h"
#include "LTPlayerController.h"
#include "LTSchool.h"
#include "LTServices.h"
#include "LTTurtleCharacter.h"
#include "LTWorld.h"

namespace
{
	double GMDist(const FVector2D& A, const FVector2D& B) { return FVector2D::Distance(A, B); }
	FVector2D GMCellMid(const FIntPoint& C) { return FVector2D(C.X + 0.5, C.Y + 0.5); }
	FLinearColor GMRgb(int32 R, int32 G, int32 B) { return FLinearColor(FColor((uint8)R, (uint8)G, (uint8)B)); }
}

ALTGameMode::ALTGameMode()
{
	PrimaryActorTick.bCanEverTick = true;
	PrimaryActorTick.bTickEvenWhenPaused = true;
	DefaultPawnClass = ALTTurtleCharacter::StaticClass();
	PlayerControllerClass = ALTPlayerController::StaticClass();
	// HUDClass stays the default AHUD: a null class makes ClientSetHUD log a failed spawn (the UI is Slate anyway)
}

// ------------------------------------------------------------------ services

ULTQuizSubsystem* ALTGameMode::Quiz() const
{
	return GetGameInstance() ? GetGameInstance()->GetSubsystem<ULTQuizSubsystem>() : nullptr;
}

ULTAudioSubsystem* ALTGameMode::Audio() const
{
	return GetGameInstance() ? GetGameInstance()->GetSubsystem<ULTAudioSubsystem>() : nullptr;
}

ULTSpeechSubsystem* ALTGameMode::Speech() const
{
	return GetGameInstance() ? GetGameInstance()->GetSubsystem<ULTSpeechSubsystem>() : nullptr;
}

ULTSaveGame* ALTGameMode::Save() const
{
	if (const UGameInstance* GI = GetGameInstance())
		if (const ULTSaveSubsystem* Sub = GI->GetSubsystem<ULTSaveSubsystem>())
			if (ULTSaveGame* S = Sub->Get()) return S;
	return FallbackSave;
}

void ALTGameMode::WriteSave()
{
	if (const UGameInstance* GI = GetGameInstance())
		if (ULTSaveSubsystem* Sub = GI->GetSubsystem<ULTSaveSubsystem>())
			Sub->Write();
}

void ALTGameMode::Sfx(FName Name)
{
	if (ULTAudioSubsystem* A = Audio()) A->Play(Name);
}

ALTTurtleCharacter* ALTGameMode::Turtle() const
{
	const APlayerController* PC = GetWorld() ? GetWorld()->GetFirstPlayerController() : nullptr;
	return PC ? Cast<ALTTurtleCharacter>(PC->GetPawn()) : nullptr;
}

FVector2D ALTGameMode::PlayerMapPos() const
{
	if (const ALTTurtleCharacter* P = Turtle()) return P->GetMapPos();
	return bPlacePending ? PendingPos : FVector2D::ZeroVector;
}

bool ALTGameMode::IsPolish() const
{
	const ULTQuizSubsystem* Q = Quiz();
	return !Q || Q->IsPolish();
}

FString ALTGameMode::T(const FString& Key) const
{
	const ULTQuizSubsystem* Q = Quiz();
	return Q ? Q->T(Key) : Key;
}

FString ALTGameMode::F(const FString& Key, const TArray<FString>& Args) const
{
	const ULTQuizSubsystem* Q = Quiz();
	return Q ? Q->F(Key, Args) : Key;
}

FString ALTGameMode::TextOr(const FString& Key, const TCHAR* Pl, const TCHAR* En) const
{
	const FString S = T(Key);
	return S.IsEmpty() || S == Key ? FString(IsPolish() ? Pl : En) : S;
}

// ------------------------------------------------------------------ start / end

void ALTGameMode::BeginPlay()
{
	Super::BeginPlay();
	Rng.GenerateNewSeed();

	if (!Save()) FallbackSave = NewObject<ULTSaveGame>(this);
	ULTSaveGame* S = Save();
	S->Classes.SetNum(LT::Floors);
	S->Master.SetNum(LT::Floors);
	S->Stars.SetNum(LT::Floors);
	S->Unlocked = FMath::Clamp(S->Unlocked, 1, LT::Floors);

	if (ULTQuizSubsystem* Q = Quiz()) Q->SetPolish(S->Lang != TEXT("en"));
	if (ULTAudioSubsystem* A = Audio()) A->SetEnabled(S->bSound);
	if (ULTSpeechSubsystem* Sp = Speech()) Sp->SetEnabled(S->bSpeech);

	// developer switches: "--floor 3" / "-floor=3" jumps straight onto floor 3, "--debug" enables F5-F7
	int32 StartLevel = -1;
	TArray<FString> Tokens;
	FString(FCommandLine::Get()).ParseIntoArrayWS(Tokens);
	for (int32 i = 0; i < Tokens.Num(); i++)
	{
		const FString& Tok = Tokens[i];
		if (Tok.Equals(TEXT("--debug"), ESearchCase::IgnoreCase) || Tok.Equals(TEXT("-ltdebug"), ESearchCase::IgnoreCase)) bDebug = true;
		const bool bFloorSwitch = Tok.Equals(TEXT("--floor"), ESearchCase::IgnoreCase) || Tok.Equals(TEXT("--level"), ESearchCase::IgnoreCase) || Tok.Equals(TEXT("-floor"), ESearchCase::IgnoreCase);
		if (bFloorSwitch && i + 1 < Tokens.Num() && Tokens[i + 1].IsNumeric()) StartLevel = FCString::Atoi(*Tokens[i + 1]) - 1;
	}
	int32 FloorArg = 0;
	if (FParse::Value(FCommandLine::Get(), TEXT("floor="), FloorArg)) StartLevel = FloorArg - 1;
	if (FParse::Param(FCommandLine::Get(), TEXT("ltdebug"))) bDebug = true;

	LoadFloor(0);
	SetScreen(ELTScreen::Title);
	if (StartLevel >= 0) StartFloor(FMath::Clamp(StartLevel, 0, LT::Floors - 1));

	if (UGameViewportClient* Viewport = GetWorld()->GetGameViewport())
	{
		UI = SNew(SLTGameUI).GameMode(this);
		Viewport->AddViewportWidgetContent(UI.ToSharedRef(), 10);
	}
	UE_LOG(LogLazyTurtle, Log, TEXT("Lazy Turtle started (floor %d, debug %d)"), FloorIndex + 1, bDebug ? 1 : 0);
}

void ALTGameMode::EndPlay(const EEndPlayReason::Type EndPlayReason)
{
	if (UI.IsValid())
	{
		if (UWorld* World = GetWorld())
			if (UGameViewportClient* Viewport = World->GetGameViewport())
				Viewport->RemoveViewportWidgetContent(UI.ToSharedRef());
		UI.Reset();
	}
	if (ULTSpeechSubsystem* Sp = Speech()) Sp->Stop();
	WriteSave();
	Super::EndPlay(EndPlayReason);
}

// ------------------------------------------------------------------ floors

void ALTGameMode::LoadFloor(int32 InFloor)
{
	FloorIndex = FMath::Clamp(InFloor, 0, LT::Floors - 1);
	if (Floor)
	{
		Floor->OnCoinCollected.RemoveAll(this);
		Floor->Destroy();
		Floor = nullptr;
	}
	NearRoom = -1;
	bNearMaster = false;
	NearKid.Reset();
	NearCaption = -1;

	ULTSaveGame* S = Save();
	FActorSpawnParameters Params;
	Params.SpawnCollisionHandlingOverride = ESpawnActorCollisionHandlingMethod::AlwaysSpawn;
	Floor = GetWorld()->SpawnActor<ALTFloor>(ALTFloor::StaticClass(), FTransform::Identity, Params);
	if (!Floor)
	{
		UE_LOG(LogLazyTurtle, Error, TEXT("Could not spawn the floor"));
		return;
	}
	Floor->Build(FloorIndex, S->Classes[FloorIndex], S->Master[FloorIndex], IsPolish());
	if (Floor->GiftsDone() >= LT::Classes) Floor->OpenMasterDoor();
	Floor->OnCoinCollected.AddUObject(this, &ALTGameMode::OnCoinCollected);

	const FLTFloorLayout& L = Floor->GetLayout();
	PlacePlayer(L.Start, L.StartYaw);
}

void ALTGameMode::PlacePlayer(const FVector2D& Pos, double YawRadians)
{
	if (ALTTurtleCharacter* P = Turtle())
	{
		P->SetMapPose(Pos, YawRadians);
		bPlacePending = false;
	}
	else
	{
		bPlacePending = true;
		PendingPos = Pos;
		PendingYaw = YawRadians;
	}
}

void ALTGameMode::StartFloor(int32 InFloor)
{
	ULTSaveGame* S = Save();
	if (S->Master[InFloor])
	{
		// replaying a finished floor starts it fresh
		S->Master[InFloor] = false;
		S->Classes[InFloor] = 0;
		WriteSave();
	}
	LoadFloor(InFloor);
	Mistakes = 0;
	Popups.Empty();
	Message.Empty();
	MessageTime = 0;
	SetScreen(ELTScreen::Intro);
}

int32 ALTGameMode::ContinueFloor() const
{
	const ULTSaveGame* S = Save();
	for (int32 Fl = 0; Fl < S->Unlocked && Fl < LT::Floors; Fl++)
		if (!S->Master[Fl]) return Fl;
	return S->Unlocked >= LT::Floors ? 0 : S->Unlocked - 1;
}

void ALTGameMode::FinishFloor()
{
	ULTSaveGame* S = Save();
	LastStars = StarsFor(Mistakes);
	S->Stars[FloorIndex] = FMath::Max(S->Stars[FloorIndex], LastStars);
	WriteSave();
	Sfx(LT::Sfx::Win);
	SetScreen(ELTScreen::FloorDone);
}

void ALTGameMode::OnCoinCollected(ALTCoin* Coin)
{
	Save()->Coins++;
	Sfx(LT::Sfx::Coin);
	if (Floor && Coin) Floor->SpawnSparkles(Coin->GetActorLocation(), 4);
}

// ------------------------------------------------------------------ screens

void ALTGameMode::SetScreen(ELTScreen NewScreen)
{
	Screen = NewScreen;
	StateTime = 0;
}

void ALTGameMode::ShowMessage(const FString& Text, float Seconds)
{
	Message = Text;
	MessageTime = Seconds;
}

void ALTGameMode::AddPopup(const FString& Text, const FLinearColor& Color)
{
	FLTPopup P;
	P.Text = Text;
	P.Color = Color;
	P.X = 0.5f + (Rng.FRand() - 0.5f) * 0.1f;
	P.Id = NextPopupId++;
	Popups.Add(P);
}

void ALTGameMode::ToggleLanguage()
{
	ULTSaveGame* S = Save();
	const bool bPolish = !IsPolish();
	if (ULTQuizSubsystem* Q = Quiz()) Q->SetPolish(bPolish);
	S->Lang = bPolish ? TEXT("pl") : TEXT("en");
	WriteSave();

	// signs, banners and blackboards contain text: rebuild the floor where the player stands
	const FVector2D Pos = PlayerMapPos();
	const ALTTurtleCharacter* P = Turtle();
	const double Yaw = P ? P->GetMapYaw() : 0.0;
	LoadFloor(FloorIndex);
	PlacePlayer(Pos, Yaw);
}

void ALTGameMode::ToggleSound()
{
	ULTSaveGame* S = Save();
	if (ULTAudioSubsystem* A = Audio())
	{
		A->SetEnabled(!A->IsEnabled());
		S->bSound = A->IsEnabled();
	}
	WriteSave();
	Sfx(LT::Sfx::Select);
}

void ALTGameMode::ToggleSpeech()
{
	ULTSpeechSubsystem* Sp = Speech();
	if (!Sp || !Sp->IsAvailable(IsPolish())) return;
	Sp->SetEnabled(!Sp->IsEnabled());
	Save()->bSpeech = Sp->IsEnabled();
	WriteSave();
	if (!Sp->IsEnabled()) Sp->Stop();
}

bool ALTGameMode::IsSpeechAvailable() const
{
	const ULTSpeechSubsystem* Sp = Speech();
	return Sp && Sp->IsAvailable(IsPolish());
}

TArray<FString> ALTGameMode::GetTitleItems() const
{
	const ULTAudioSubsystem* A = Audio();
	const ULTSpeechSubsystem* Sp = Speech();
	const bool bSound = A && A->IsEnabled();
	const FString SpeechLabel = !IsSpeechAvailable() ? T(TEXT("speech_none")) : T(Sp->IsEnabled() ? TEXT("speech_on") : TEXT("speech_off"));
	return { T(TEXT("play")), T(TEXT("floors")), T(TEXT("help")), T(TEXT("lang")), T(bSound ? TEXT("sound_on") : TEXT("sound_off")), SpeechLabel, T(TEXT("exit")) };
}

TArray<FString> ALTGameMode::GetPauseItems() const
{
	const ULTAudioSubsystem* A = Audio();
	const ULTSpeechSubsystem* Sp = Speech();
	const bool bSound = A && A->IsEnabled();
	const FString SpeechLabel = !IsSpeechAvailable() ? T(TEXT("speech_none")) : T(Sp->IsEnabled() ? TEXT("speech_on") : TEXT("speech_off"));
	return { T(TEXT("resume")), T(TEXT("restart")), T(bSound ? TEXT("sound_on") : TEXT("sound_off")), SpeechLabel, T(TEXT("menu")) };
}

void ALTGameMode::Pause()
{
	if (Screen != ELTScreen::Playing) return;
	Menu = 0;
	SetScreen(ELTScreen::Paused);
}

void ALTGameMode::GoToTitle()
{
	if (ULTSpeechSubsystem* Sp = Speech()) Sp->Stop();
	Menu = 0;
	LoadFloor(0);
	SetScreen(ELTScreen::Title);
}

// ------------------------------------------------------------------ update

void ALTGameMode::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	const float Dt = FMath::Min(DeltaSeconds, 0.05f);
	Time += Dt;
	StateTime += Dt;
	if (MessageTime > 0) MessageTime -= Dt;
	for (FLTPopup& P : Popups) P.Time += Dt;
	Popups.RemoveAll([](const FLTPopup& P) { return P.Time > 1.5f; });

	if (bPlacePending) PlacePlayer(PendingPos, PendingYaw);

	// the original paused when the window lost focus
	if (Screen == ELTScreen::Playing && !FApp::HasFocus()) Pause();

	ALTTurtleCharacter* P = Turtle();
	switch (Screen)
	{
	case ELTScreen::Title:
	case ELTScreen::Floors:
	case ELTScreen::Help:
	case ELTScreen::Victory:
		// slow camera orbit around the turtle behind the menus
		if (P) P->AddMapYaw(Dt * 0.15);
		break;
	case ELTScreen::Intro:
		if (StateTime > 6.f)
		{
			SetScreen(ELTScreen::Playing);
			ShowMessage(T(TEXT("find_class")), 5.f);
		}
		break;
	case ELTScreen::Playing:
		UpdatePlaying(Dt);
		break;
	case ELTScreen::Dialog:
		Dialog.Time += Dt;
		break;
	default:
		break;
	}
}

void ALTGameMode::UpdatePlaying(float DeltaSeconds)
{
	if (!Floor) return;
	FindNearby();
	CheckDoors();
}

void ALTGameMode::FindNearby()
{
	NearRoom = -1;
	bNearMaster = false;
	NearKid.Reset();
	NearCaption = -1;
	if (!Floor) return;

	const FVector2D P = PlayerMapPos();
	double BestKid = 1.5;
	for (ALTKid* Kid : Floor->GetKids())
	{
		if (!IsValid(Kid)) continue;
		const double D = GMDist(P, LT::WorldToMap(Kid->GetActorLocation()));
		if (D < BestKid)
		{
			BestKid = D;
			NearKid = Kid;
		}
	}
	for (const FLTRoomRuntime& R : Floor->GetRooms())
		if (R.Layout.Contains(P.X, P.Y) && GMDist(P, R.Layout.TeacherPos) < 2.1) NearRoom = R.Layout.Index;
	if (GMDist(P, Floor->GetLayout().MasterPos) < 2.4) bNearMaster = true;
	if (NearRoom >= 0 || bNearMaster) NearKid.Reset();   // teachers first

	double Best = 1.4;
	for (const FLTCaptionSpot& C : Floor->GetLayout().Captions)
	{
		const double D = GMDist(P, FVector2D(C.X, C.Y));
		if (D < Best)
		{
			Best = D;
			NearCaption = C.Caption;
		}
	}
}

void ALTGameMode::CheckDoors()
{
	const FLTFloorLayout& L = Floor->GetLayout();
	const FVector2D P = PlayerMapPos();

	// the schoolyard is not in this version yet
	if (L.YardDoor.X >= 0 && GMDist(P, GMCellMid(L.YardDoor)) < 1.0 && MessageTime < 1)
	{
		ShowMessage(T(TEXT("yard_soon")));
		Sfx(LT::Sfx::Bump);
		return;
	}
	if (!Floor->IsMasterDoorOpen() && GMDist(P, GMCellMid(L.MasterDoor)) < 0.95 && MessageTime < 1)
	{
		ShowMessage(F(TEXT("need_gifts"), { FString::FromInt(Floor->GiftsDone()) }));
		Sfx(LT::Sfx::Bump);
	}
	if (GMDist(P, GMCellMid(L.Stairs)) < 0.95)
	{
		if (Floor->AreStairsOpen()) FinishFloor();
		else if (MessageTime < 1) ShowMessage(T(TEXT("stairs_locked")));
	}
}

// ------------------------------------------------------------------ input

bool ALTGameMode::MenuNav(ELTInput Input, int32 Count)
{
	if (Count <= 0) return false;
	if (Input == ELTInput::Up) { Menu = (Menu + Count - 1) % Count; Sfx(LT::Sfx::Click); }
	if (Input == ELTInput::Down) { Menu = (Menu + 1) % Count; Sfx(LT::Sfx::Click); }
	if (Input == ELTInput::Accept || Input == ELTInput::Space) { Sfx(LT::Sfx::Select); return true; }
	return false;
}

void ALTGameMode::HandleInput(ELTInput Input)
{
	const bool bAccept = Input == ELTInput::Accept || Input == ELTInput::Space;
	switch (Screen)
	{
	case ELTScreen::Title:
		if (MenuNav(Input, GetTitleItems().Num())) TitleChoose();
		break;

	case ELTScreen::Floors:
	{
		const int32 N = LT::Floors, PerRow = (N + 1) / 2;
		if (Input == ELTInput::Left) { Menu = (Menu + N - 1) % N; Sfx(LT::Sfx::Click); }
		if (Input == ELTInput::Right) { Menu = (Menu + 1) % N; Sfx(LT::Sfx::Click); }
		if (Input == ELTInput::Up || Input == ELTInput::Down) { Menu = (Menu + PerRow) % N; Sfx(LT::Sfx::Click); }
		if (Input == ELTInput::Back) { Sfx(LT::Sfx::Click); Menu = 1; SetScreen(ELTScreen::Title); return; }
		if (bAccept) FloorSelectGo();
		break;
	}

	case ELTScreen::Help:
		if (Input == ELTInput::Back || bAccept) { Sfx(LT::Sfx::Click); Menu = 2; SetScreen(ELTScreen::Title); }
		break;

	case ELTScreen::Intro:
		if (StateTime > 0.4f && bAccept)
		{
			SetScreen(ELTScreen::Playing);
			ShowMessage(T(TEXT("find_class")), 5.f);
		}
		break;

	case ELTScreen::Playing:
		if (Input == ELTInput::Back || Input == ELTInput::Pause) { Pause(); return; }
		if (Input == ELTInput::Accept) TryTalkOrJump(false);
		if (Input == ELTInput::Space) TryTalkOrJump(true);
		break;

	case ELTScreen::Dialog:
	{
		if (Input == ELTInput::Back)
		{
			if (ULTSpeechSubsystem* Sp = Speech()) Sp->Stop();
			SetScreen(ELTScreen::Playing);
			return;
		}
		if (Input == ELTInput::Read) { ReadAloud(); return; }
		if (Dialog.Phase == ELTDialogPhase::Ask && Dialog.bHasQuestion)
		{
			const int32 N = Dialog.Q.Answers.Num();
			if (N <= 0) return;
			if (Input == ELTInput::Left) Dialog.Selected = (Dialog.Selected + N - 1) % N;
			if (Input == ELTInput::Right) Dialog.Selected = (Dialog.Selected + 1) % N;
			if (Input == ELTInput::Up) Dialog.Selected = (Dialog.Selected + N - 2 + N) % N;
			if (Input == ELTInput::Down) Dialog.Selected = (Dialog.Selected + 2) % N;
			if (bAccept && Dialog.Time > 0.3f) Answer(Dialog.Selected);
			return;
		}
		if (bAccept) DialogContinue();
		break;
	}

	case ELTScreen::Paused:
		if (Input == ELTInput::Back || Input == ELTInput::Pause) { SetScreen(ELTScreen::Playing); return; }
		if (MenuNav(Input, GetPauseItems().Num())) PauseChoose();
		break;

	case ELTScreen::FloorDone:
		if (StateTime > 1.f && bAccept) FloorDoneContinue();
		break;

	case ELTScreen::Victory:
		if (StateTime > 1.5f && (bAccept || Input == ELTInput::Back)) GoToTitle();
		break;
	}
}

void ALTGameMode::HandleAnswerKey(int32 Index)
{
	if (Screen != ELTScreen::Dialog || Dialog.Phase != ELTDialogPhase::Ask || !Dialog.bHasQuestion) return;
	if (Dialog.Q.Answers.IsValidIndex(Index)) Answer(Index);
}

void ALTGameMode::HandleClick(int32 Action)
{
	switch (Screen)
	{
	case ELTScreen::Title:
		if (Action >= LTClick::Menu0 && Action < LTClick::Menu0 + GetTitleItems().Num())
		{
			Menu = Action - LTClick::Menu0;
			Sfx(LT::Sfx::Select);
			TitleChoose();
		}
		break;
	case ELTScreen::Floors:
		if (Action >= LTClick::Floor0 && Action < LTClick::Floor0 + LT::Floors)
		{
			Menu = Action - LTClick::Floor0;
			FloorSelectGo();
		}
		break;
	case ELTScreen::Help:
		Sfx(LT::Sfx::Click);
		Menu = 2;
		SetScreen(ELTScreen::Title);
		break;
	case ELTScreen::Intro:
		if (StateTime > 0.4f)
		{
			SetScreen(ELTScreen::Playing);
			ShowMessage(T(TEXT("find_class")), 5.f);
		}
		break;
	case ELTScreen::Playing:
		if (Action == LTClick::Talk) TryTalkOrJump(false);
		break;
	case ELTScreen::Dialog:
		if (Action == LTClick::Read) { ReadAloud(); return; }
		if (Dialog.Phase == ELTDialogPhase::Ask)
		{
			if (Dialog.bHasQuestion && Dialog.Q.Answers.IsValidIndex(Action)) Answer(Action);
		}
		else if (Action == LTClick::Continue) DialogContinue();
		break;
	case ELTScreen::Paused:
		if (Action >= LTClick::Menu0 && Action < LTClick::Menu0 + GetPauseItems().Num())
		{
			Menu = Action - LTClick::Menu0;
			Sfx(LT::Sfx::Select);
			PauseChoose();
		}
		break;
	case ELTScreen::FloorDone:
		if (StateTime > 1.f) FloorDoneContinue();
		break;
	case ELTScreen::Victory:
		if (StateTime > 1.5f) GoToTitle();
		break;
	}
}

void ALTGameMode::TitleChoose()
{
	switch (Menu)
	{
	case 0: StartFloor(ContinueFloor()); break;
	case 1: Menu = FMath::Min(Save()->Unlocked, LT::Floors) - 1; SetScreen(ELTScreen::Floors); break;
	case 2: SetScreen(ELTScreen::Help); break;
	case 3: ToggleLanguage(); break;
	case 4: ToggleSound(); break;
	case 5: ToggleSpeech(); break;
	case 6:
		WriteSave();
		UKismetSystemLibrary::QuitGame(this, GetWorld()->GetFirstPlayerController(), EQuitPreference::Quit, false);
		break;
	default: break;
	}
}

void ALTGameMode::PauseChoose()
{
	ULTSaveGame* S = Save();
	switch (Menu)
	{
	case 0: SetScreen(ELTScreen::Playing); break;
	case 1:
		S->Classes[FloorIndex] = 0;
		S->Master[FloorIndex] = false;
		WriteSave();
		StartFloor(FloorIndex);
		break;
	case 2: ToggleSound(); break;
	case 3: ToggleSpeech(); break;
	case 4: GoToTitle(); break;
	default: break;
	}
}

void ALTGameMode::FloorSelectGo()
{
	if (Menu < Save()->Unlocked)
	{
		Sfx(LT::Sfx::Select);
		StartFloor(Menu);
	}
	else Sfx(LT::Sfx::Bump);
}

void ALTGameMode::FloorDoneContinue()
{
	Sfx(LT::Sfx::Select);
	if (FloorIndex + 1 < LT::Floors) StartFloor(FloorIndex + 1);
	else SetScreen(ELTScreen::Victory);
}

void ALTGameMode::TryTalkOrJump(bool bJumpIfNothing)
{
	FindNearby();
	if (CanTalk())
	{
		StartDialog();
		return;
	}
	if (bJumpIfNothing)
		if (ALTTurtleCharacter* P = Turtle())
			if (P->TryJump()) Sfx(LT::Sfx::Jump);
}

void ALTGameMode::MouseLook(float DX, float DY)
{
	if (Screen != ELTScreen::Playing) return;
	if (ALTTurtleCharacter* P = Turtle()) P->MouseLook(DX, DY);
}

void ALTGameMode::HandleDebugKey(int32 FunctionKey)
{
	if (!bDebug || Screen != ELTScreen::Playing || !Floor) return;
	const FLTFloorLayout& L = Floor->GetLayout();

	if (FunctionKey == 5)
	{
		// in front of the next teacher still to visit
		for (const FLTRoomRuntime& R : Floor->GetRooms())
		{
			if (R.bDone) continue;
			const double Dir = R.Layout.TeacherPos.Y < 10 ? 1.0 : -1.0;
			PlacePlayer(FVector2D(R.Layout.TeacherPos.X, R.Layout.TeacherPos.Y + Dir * 1.9), Dir > 0 ? -UE_DOUBLE_HALF_PI : UE_DOUBLE_HALF_PI);
			break;
		}
	}
	else if (FunctionKey == 6)
	{
		// straight to the Master Turtle
		Floor->OpenMasterDoor();
		PlacePlayer(FVector2D(46.5, 10.5), 0.0);
	}
	else if (FunctionKey == 7 && L.Captions.Num() > 0)
	{
		// cycle through the paintings, facing the wall they hang on
		const FLTCaptionSpot& C = L.Captions[DebugCaption++ % L.Captions.Num()];
		const int32 CX = FMath::FloorToInt(C.X), CY = FMath::FloorToInt(C.Y);
		double Yaw = 0;
		if (L.IsSolid(CX, CY - 1)) Yaw = -UE_DOUBLE_HALF_PI;
		else if (L.IsSolid(CX, CY + 1)) Yaw = UE_DOUBLE_HALF_PI;
		else if (L.IsSolid(CX - 1, CY)) Yaw = UE_DOUBLE_PI;
		PlacePlayer(FVector2D(C.X, C.Y), Yaw);
	}
}

// ------------------------------------------------------------------ quiz dialogs

void ALTGameMode::StartDialog()
{
	Dialog = FLTDialogState();
	const FVector PlayerLoc = Turtle() ? Turtle()->GetActorLocation() : FVector::ZeroVector;

	if (NearKid.IsValid() && NearRoom < 0 && !bNearMaster)
	{
		StartKidDialog(NearKid.Get());
	}
	else
	{
		Dialog.bMaster = bNearMaster && NearRoom < 0;
		Dialog.Room = Dialog.bMaster ? -1 : NearRoom;
		if (Dialog.bMaster)
		{
			if (ALTTurtleNPC* M = Floor->GetMaster()) M->FaceTowards(PlayerLoc);
			if (Save()->Master[FloorIndex]) SetLine(ELTDialogPhase::Info, T(TEXT("master_already")));
			else AskNew();
		}
		else
		{
			const FLTRoomRuntime* Room = Floor->GetRooms().FindByPredicate([this](const FLTRoomRuntime& R) { return R.Layout.Index == Dialog.Room; });
			if (!Room) return;
			if (Room->Teacher) Room->Teacher->FaceTowards(PlayerLoc);
			if (Room->bDone) SetLine(ELTDialogPhase::Info, T(TEXT("already")));
			else AskNew();
		}
	}
	Sfx(LT::Sfx::Talk);
	SetScreen(ELTScreen::Dialog);
}

void ALTGameMode::StartKidDialog(ALTKid* Kid)
{
	Dialog.bKid = true;
	Dialog.Kid = Kid;
	Dialog.KidName = Kid->GetDisplayName();
	Kid->PauseWalking(4.f);   // stop walking and look at us
	if (const ALTTurtleCharacter* P = Turtle()) Kid->FaceTowards(P->GetActorLocation());

	const FString Greeting = F(TEXT("chat_greeting"), { Dialog.KidName });
	ULTQuizSubsystem* Q = Quiz();
	if (Q && Rng.RandRange(0, 1) == 0)
	{
		Dialog.bRiddle = true;
		Dialog.Q = Q->Riddle();
		Dialog.bHasQuestion = Dialog.Q.IsValid();
		if (Dialog.bHasQuestion)
		{
			SetLine(ELTDialogPhase::Ask, Greeting + TEXT(" ") + T(TEXT("chat_riddle_intro")));
			return;
		}
		Dialog.bRiddle = false;
	}
	SetLine(ELTDialogPhase::Info, Greeting + TEXT(" ") + (Q ? Q->ChatLine(false) : FString()));
}

void ALTGameMode::AskNew()
{
	ULTQuizSubsystem* Q = Quiz();
	if (!Q) return;
	if (Dialog.bMaster) Dialog.Q = Q->MasterQuestion(FloorIndex);
	else
	{
		const FLTRoomRuntime* Room = Floor->GetRooms().FindByPredicate([this](const FLTRoomRuntime& R) { return R.Layout.Index == Dialog.Room; });
		const int32 Subject = Room ? Room->Layout.Subject : FLTSchool::SubjectOf(FloorIndex, Dialog.Room);
		Dialog.Q = Q->MakeQuestion(Subject, FMath::Clamp(FloorIndex + 1, 1, LT::MaxDifficulty));
	}
	Dialog.bHasQuestion = Dialog.Q.IsValid();
	Dialog.Selected = 0;
	Dialog.Chosen = -1;
	SetLine(Dialog.bHasQuestion ? ELTDialogPhase::Ask : ELTDialogPhase::Info, T(Dialog.bMaster ? TEXT("master_greet") : TEXT("greet")));
}

void ALTGameMode::SetLine(ELTDialogPhase Phase, const FString& Line)
{
	Dialog.Phase = Phase;
	Dialog.Line = Line;
	Dialog.Time = 0;
	Dialog.Serial = ++LineSerial;
	ULTSpeechSubsystem* Sp = Speech();
	if (Sp && Sp->IsEnabled()) ReadAloud();
}

void ALTGameMode::ReadAloud()
{
	ULTSpeechSubsystem* Sp = Speech();
	if (!Sp || !Sp->IsEnabled()) return;
	if (Dialog.Phase != ELTDialogPhase::Ask || !Dialog.bHasQuestion)
	{
		Sp->Say(Dialog.Line, IsPolish());
		return;
	}
	FString Text = Dialog.Line + TEXT(" ") + Dialog.Q.Text + TEXT(" ");
	for (int32 i = 0; i < Dialog.Q.Answers.Num(); i++)
		Text += FString::Printf(TEXT("%d: %s. "), i + 1, *Dialog.Q.Answers[i]);
	Sp->Say(Text, IsPolish());
}

void ALTGameMode::DialogContinue()
{
	if (Dialog.Time < 0.5f) return;
	const bool bKid = Dialog.bKid;
	switch (Dialog.Phase)
	{
	case ELTDialogPhase::Correct:
		if (Dialog.bMaster || bKid) CloseDialog();
		else SetLine(ELTDialogPhase::Reward, F(TEXT("reward"), { T(FString::Printf(TEXT("g%d"), Dialog.Gift)) }));
		break;
	case ELTDialogPhase::Wrong:
		if (bKid) CloseDialog();
		else AskNew();
		break;
	default:
		CloseDialog();
		break;
	}
}

void ALTGameMode::CloseDialog()
{
	if (ULTSpeechSubsystem* Sp = Speech()) Sp->Stop();
	SetScreen(ELTScreen::Playing);
	if (Dialog.bMaster && Floor && Floor->AreStairsOpen()) ShowMessage(T(TEXT("stairs_open")), 5.f);
}

void ALTGameMode::Answer(int32 Choice)
{
	Dialog.Chosen = Choice;
	Dialog.Selected = Choice;
	ULTSaveGame* S = Save();

	if (Dialog.bRiddle)
	{
		if (Choice == Dialog.Q.Correct)
		{
			S->Coins += 3;
			WriteSave();
			Sfx(LT::Sfx::Correct);
			AddPopup(F(TEXT("coins_plus"), { FString(TEXT("3")) }), GMRgb(255, 230, 90));
			SetLine(ELTDialogPhase::Correct, T(TEXT("chat_riddle_right")));
		}
		else
		{
			Sfx(LT::Sfx::Wrong);
			SetLine(ELTDialogPhase::Wrong, F(TEXT("chat_riddle_wrong"), { Dialog.Q.CorrectText() }));
		}
		return;
	}

	if (Choice != Dialog.Q.Correct)
	{
		Mistakes++;
		Sfx(LT::Sfx::Wrong);
		SetLine(ELTDialogPhase::Wrong, F(Dialog.bMaster ? TEXT("master_wrong") : TEXT("wrong"), { Dialog.Q.CorrectText() }));
		return;
	}

	Sfx(LT::Sfx::Correct);
	if (Dialog.bMaster)
	{
		S->Master[FloorIndex] = true;
		S->Unlocked = FMath::Max(S->Unlocked, FMath::Min(LT::Floors, FloorIndex + 2));
		WriteSave();
		Floor->OpenStairs();
		Floor->MarkMasterDone();
		Sfx(LT::Sfx::Stairs);
		SetLine(ELTDialogPhase::Correct, T(FloorIndex == LT::Floors - 1 ? TEXT("master_last") : TEXT("master_correct")));
		return;
	}

	if (Dialog.Room < 0) return;
	// the gift is won right away, so leaving the dialog early cannot lose it
	FLTRoomRuntime* Room = Floor->GetRooms().FindByPredicate([this](const FLTRoomRuntime& R) { return R.Layout.Index == Dialog.Room; });
	if (Room) Room->bDone = true;
	Floor->MarkRoomDone(Dialog.Room);
	Dialog.Gift = Dialog.Room;
	S->Classes[FloorIndex] |= 1 << Dialog.Room;
	WriteSave();
	Sfx(LT::Sfx::Gift);
	SetLine(ELTDialogPhase::Correct, T(TEXT("correct")));
	if (Floor->GiftsDone() >= LT::Classes && !Floor->IsMasterDoorOpen())
	{
		Floor->OpenMasterDoor();
		Sfx(LT::Sfx::Door);
		ShowMessage(T(TEXT("master_open")), 6.f);
	}
}

// ------------------------------------------------------------------ helpers for the UI

const FLTFloorTheme& ALTGameMode::GetTheme() const
{
	const TArray<FLTFloorTheme>& Themes = FLTSchool::Themes();
	check(Themes.Num() > 0);
	return Themes[FMath::Clamp(FloorIndex, 0, Themes.Num() - 1)];
}

const ULTSaveGame* ALTGameMode::GetSave() const
{
	return Save();
}

int32 ALTGameMode::GetCoins() const
{
	const ULTSaveGame* S = Save();
	return S ? S->Coins : 0;
}

int32 ALTGameMode::GetGiftsDone() const
{
	return Floor ? Floor->GiftsDone() : 0;
}

int32 ALTGameMode::GetGiftMask() const
{
	int32 Mask = 0;
	if (Floor)
		for (const FLTRoomRuntime& R : Floor->GetRooms())
			if (R.bDone) Mask |= 1 << R.Layout.Index;
	return Mask;
}

int32 ALTGameMode::GetTotalStars() const
{
	int32 Sum = 0;
	if (const ULTSaveGame* S = Save())
		for (int32 V : S->Stars) Sum += V;
	return Sum;
}

FString ALTGameMode::GetTalkLabel() const
{
	if (NearKid.IsValid() && NearRoom < 0 && !bNearMaster) return F(TEXT("talk_to"), { NearKid->GetDisplayName() });
	return T(TEXT("talk"));
}

FString ALTGameMode::GetCaptionText() const
{
	return NearCaption >= 0 ? FLTSchool::CaptionText(NearCaption, IsPolish()) : FString();
}

FString ALTGameMode::GetSpeaker() const
{
	if (Dialog.bKid) return Dialog.KidName;
	if (Dialog.bMaster) return T(TEXT("master"));
	if (Floor)
		if (const FLTRoomRuntime* Room = Floor->GetRooms().FindByPredicate([this](const FLTRoomRuntime& R) { return R.Layout.Index == Dialog.Room; }))
		{
			const bool bPl = IsPolish();
			const int32 Subject = Room->Layout.Subject;
			const FString Name = Room->Layout.TeacherName.IsEmpty() ? FLTSchool::TeacherFullName(FloorIndex, Subject, bPl) : Room->Layout.TeacherName;
			const TArray<FLTSubject>& Subjects = FLTSchool::Subjects();
			return Subjects.IsValidIndex(Subject) ? Name + TEXT(" - ") + Subjects[Subject].Name(bPl) : Name;
		}
	return FString();
}

FLinearColor ALTGameMode::GetSpeakerColor() const
{
	if (Dialog.bKid) return GMRgb(240, 120, 60);
	if (Dialog.bMaster) return GMRgb(160, 90, 220);
	if (Floor)
		if (const FLTRoomRuntime* Room = Floor->GetRooms().FindByPredicate([this](const FLTRoomRuntime& R) { return R.Layout.Index == Dialog.Room; }))
			if (FLTSchool::Subjects().IsValidIndex(Room->Layout.Subject)) return FLTSchool::Subjects()[Room->Layout.Subject].Color;
	return GMRgb(60, 140, 230);
}

bool ALTGameMode::GetCompass(float& OutRelative, bool& bOutMaster) const
{
	OutRelative = 0;
	bOutMaster = false;
	const ALTTurtleCharacter* P = Turtle();
	if (!Floor || !P) return false;

	// the nearest classroom still to visit, then the Master Turtle, then the stairs
	const FVector2D Pos = P->GetMapPos();
	FVector2D Target = FVector2D::ZeroVector;
	double Best = TNumericLimits<double>::Max();
	for (const FLTRoomRuntime& R : Floor->GetRooms())
	{
		if (R.bDone) continue;
		const double D = GMDist(Pos, R.Layout.TeacherPos);
		if (D < Best)
		{
			Best = D;
			Target = R.Layout.TeacherPos;
		}
	}
	if (Best == TNumericLimits<double>::Max())
	{
		bOutMaster = true;
		const ULTSaveGame* S = Save();
		Target = S && S->Master[FloorIndex] ? GMCellMid(Floor->GetLayout().Stairs) : Floor->GetLayout().MasterPos;
	}
	const double Angle = FMath::Atan2(Target.Y - Pos.Y, Target.X - Pos.X) - P->GetCameraYawRadians();
	OutRelative = (float)FMath::UnwindRadians(Angle);
	return true;
}
