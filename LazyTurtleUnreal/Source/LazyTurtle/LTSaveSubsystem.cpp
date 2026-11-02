// Port of SaveData.cs: progress in the engine's SaveGame slot, with a one-time import of the old save.txt.

#include "LTServices.h"
#include "LazyTurtle.h"
#include "Kismet/GameplayStatics.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "HAL/PlatformMisc.h"
#include "HAL/FileManager.h"

namespace LTSaveDetail
{
	const TCHAR* SlotName = TEXT("LazyTurtle");
	constexpr int32 UserIndex = 0;

	/** C# int.TryParse: the number, or 0 when the text is not a whole number. */
	int32 ParseInt(const FString& Text)
	{
		int32 N = 0;
		if (!LexTryParseString(N, *Text)) N = 0;
		return N;
	}

	/** Keeps the per-floor arrays the right size and the values in range whatever was loaded. */
	void Sanitize(ULTSaveGame* S)
	{
		S->Classes.SetNum(LT::Floors);
		S->Master.SetNum(LT::Floors);
		S->Stars.SetNum(LT::Floors);
		S->Unlocked = FMath::Clamp(S->Unlocked, 1, LT::Floors);
		S->Coins = FMath::Max(0, S->Coins);
		for (int32 F = 0; F < LT::Floors; F++)
		{
			S->Classes[F] &= 0x3FF;
			S->Stars[F] = FMath::Clamp(S->Stars[F], 0, 3);
		}
	}
}

ULTSaveGame::ULTSaveGame()
{
	Classes.Init(0, LT::Floors);
	Master.Init(false, LT::Floors);
	Stars.Init(0, LT::Floors);
}

void ULTSaveSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);
	using namespace LTSaveDetail;

	if (UGameplayStatics::DoesSaveGameExist(SlotName, UserIndex))
		Save = Cast<ULTSaveGame>(UGameplayStatics::LoadGameFromSlot(SlotName, UserIndex));

	if (!Save)
	{
		Save = Cast<ULTSaveGame>(UGameplayStatics::CreateSaveGameObject(ULTSaveGame::StaticClass()));
		if (!Save) Save = NewObject<ULTSaveGame>(this);
		if (ImportLegacy(Save))
			UE_LOG(LogLazyTurtle, Log, TEXT("Imported progress from the old save.txt"));
		Sanitize(Save);
		Write();
		return;
	}
	Sanitize(Save);
}

void ULTSaveSubsystem::Write()
{
	if (!Save) return;
	LTSaveDetail::Sanitize(Save);
	if (!UGameplayStatics::SaveGameToSlot(Save, LTSaveDetail::SlotName, LTSaveDetail::UserIndex))
		UE_LOG(LogLazyTurtle, Warning, TEXT("Could not write the save game"));
}

bool ULTSaveSubsystem::ImportLegacy(ULTSaveGame* Into) const
{
	if (!Into) return false;
	const FString AppData = FPlatformMisc::GetEnvironmentVariable(TEXT("APPDATA"));
	if (AppData.IsEmpty()) return false;
	const FString Path = FPaths::Combine(AppData, TEXT("LazyTurtle"), TEXT("save.txt"));
	if (!IFileManager::Get().FileExists(*Path)) return false;

	TArray<FString> Lines;
	if (!FFileHelper::LoadFileToStringArray(Lines, *Path)) return false;

	Into->Classes.SetNum(LT::Floors);
	Into->Master.SetNum(LT::Floors);
	Into->Stars.SetNum(LT::Floors);
	// keys are case-sensitive as in the original (FString == ignores case)
	auto Is = [](const FString& A, const TCHAR* B) { return A.Equals(B, ESearchCase::CaseSensitive); };
	for (const FString& Line : Lines)
	{
		int32 Eq = INDEX_NONE;
		if (!Line.FindChar(TEXT('='), Eq) || Eq <= 0) continue;
		const FString Key = Line.Left(Eq).TrimStartAndEnd();
		const FString Val = Line.Mid(Eq + 1).TrimStartAndEnd();
		const int32 N = LTSaveDetail::ParseInt(Val);
		if (Is(Key, TEXT("unlocked"))) Into->Unlocked = FMath::Max(1, FMath::Min(LT::Floors, N));
		else if (Is(Key, TEXT("coins"))) Into->Coins = FMath::Max(0, N);
		else if (Is(Key, TEXT("lang"))) Into->Lang = Val;
		else if (Is(Key, TEXT("sound"))) Into->bSound = Val != TEXT("0");
		else if (Is(Key, TEXT("speech"))) Into->bSpeech = Val != TEXT("0");
		else if (Key.Len() > 1 && FChar::IsDigit(Key[Key.Len() - 1]))
		{
			const int32 F = Key[Key.Len() - 1] - TEXT('0');
			if (F < 0 || F >= LT::Floors) continue;
			const FString K = Key.Left(Key.Len() - 1);
			if (Is(K, TEXT("classes"))) Into->Classes[F] = N & 0x3FF;
			else if (Is(K, TEXT("master"))) Into->Master[F] = N != 0;
			else if (Is(K, TEXT("stars"))) Into->Stars[F] = FMath::Max(0, FMath::Min(3, N));
		}
	}
	return true;
}
