// Quiz questions, UI texts and classmates' chat. Port of Questions.cs / Chat.cs / Lang.cs: the question generators
// themselves stay in C# (tools/QuizExport) and are exported to Content/Data/quiz.json and strings.json.

#include "LTServices.h"
#include "LazyTurtle.h"
#include "Dom/JsonObject.h"
#include "Dom/JsonValue.h"
#include "Math/RandomStream.h"
#include "Misc/DateTime.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"

namespace
{
	/** Subjects served from a hand-written list (FromList in Questions.cs); their buckets hold only the items of exactly
	 * that difficulty and are searched outwards like FromList. All other subjects are generated: each bucket already
	 * holds everything the generator serves at that difficulty. Matches "listSubjects" in quiz.json. */
	bool IsListSubject(int32 Subject)
	{
		return Subject == 2 || Subject == 3 || Subject == 7 || Subject == 8 || Subject == 9;
	}

	/** FromList's default spread ("listSpread" in quiz.json). */
	constexpr int32 ListSpread = 2;

	/** The Master Turtle's subjects when not asking about Kórnik (Questions.Master). */
	const int32 MasterSubjects[] = { 0, 0, 5, 5, 6, 2, 9, 1, 3, 7, 8, 4 };

	/** Shown when the data files are missing, so the game keeps working. */
	FLTQuestion Placeholder(bool bPolish)
	{
		FLTQuestion Q;
		Q.Text = bPolish ? TEXT("Brak pytań (brak pliku Content/Data/quiz.json).") : TEXT("No questions (Content/Data/quiz.json is missing).");
		Q.Answers.Add(TEXT("OK"));
		Q.Correct = 0;
		return Q;
	}

	/** "text|correct answer", as Questions.Key: the same question never repeats in a session. */
	FString KeyOf(const FLTQuestion& Q)
	{
		return Q.Text + TEXT("|") + Q.CorrectText();
	}

	TSharedPtr<FJsonObject> LoadJson(const FString& Name)
	{
		const FString Path = FPaths::ProjectContentDir() / TEXT("Data") / Name;
		FString Text;
		if (!FFileHelper::LoadFileToString(Text, *Path))
		{
			UE_LOG(LogLazyTurtle, Error, TEXT("Quiz data not found: %s (regenerate it with tools/QuizExport)"), *Path);
			return nullptr;
		}
		TSharedPtr<FJsonObject> Root;
		const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(Text);
		if (!FJsonSerializer::Deserialize(Reader, Root) || !Root.IsValid())
		{
			UE_LOG(LogLazyTurtle, Error, TEXT("Quiz data is not valid JSON: %s"), *Path);
			return nullptr;
		}
		return Root;
	}

	/** {"q": text, "a": [answers], "c": index of the right one (optional, default 0 = first)} */
	bool ParseQuestion(const TSharedPtr<FJsonValue>& Value, FLTQuestion& Out)
	{
		const TSharedPtr<FJsonObject>* Obj = nullptr;
		if (!Value.IsValid() || !Value->TryGetObject(Obj) || Obj == nullptr || !Obj->IsValid())
		{
			return false;
		}
		const TArray<TSharedPtr<FJsonValue>>* Answers = nullptr;
		if (!(*Obj)->TryGetStringField(TEXT("q"), Out.Text) || !(*Obj)->TryGetArrayField(TEXT("a"), Answers) || Answers == nullptr)
		{
			return false;
		}
		Out.Answers.Reset();
		for (const TSharedPtr<FJsonValue>& A : *Answers)
		{
			FString S;
			if (A.IsValid() && A->TryGetString(S))
			{
				Out.Answers.Add(S);
			}
		}
		int32 Correct = 0;
		(*Obj)->TryGetNumberField(TEXT("c"), Correct);
		Out.Correct = Correct;
		return Out.IsValid();
	}

	void ParseQuestions(const TArray<TSharedPtr<FJsonValue>>* Values, TArray<FLTQuestion>& Out)
	{
		if (Values == nullptr)
		{
			return;
		}
		for (const TSharedPtr<FJsonValue>& V : *Values)
		{
			FLTQuestion Q;
			if (ParseQuestion(V, Q))
			{
				Out.Add(MoveTemp(Q));
			}
		}
	}

	/** An array of MaxDifficulty buckets (difficulty 1..9); missing ones stay empty. */
	void ParseBuckets(const TSharedPtr<FJsonValue>& Value, TArray<TArray<FLTQuestion>>& Out)
	{
		Out.Reset();
		Out.SetNum(LT::MaxDifficulty);
		const TArray<TSharedPtr<FJsonValue>>* Levels = nullptr;
		if (!Value.IsValid() || !Value->TryGetArray(Levels) || Levels == nullptr)
		{
			return;
		}
		for (int32 D = 0; D < LT::MaxDifficulty && D < Levels->Num(); D++)
		{
			const TArray<TSharedPtr<FJsonValue>>* Items = nullptr;
			if ((*Levels)[D].IsValid() && (*Levels)[D]->TryGetArray(Items))
			{
				ParseQuestions(Items, Out[D]);
			}
		}
	}

	void ParseStrings(const TSharedPtr<FJsonObject>& Obj, const TCHAR* Field, TArray<FString>& Out)
	{
		const TArray<TSharedPtr<FJsonValue>>* Values = nullptr;
		if (Obj.IsValid() && Obj->TryGetArrayField(Field, Values) && Values != nullptr)
		{
			for (const TSharedPtr<FJsonValue>& V : *Values)
			{
				FString S;
				if (V.IsValid() && V->TryGetString(S))
				{
					Out.Add(S);
				}
			}
		}
	}

	/**
	 * Questions.FromList over difficulty buckets: a fresh question of difficulty D if possible, otherwise a fresh one
	 * from nearby difficulties (up to Spread levels away), otherwise any one within one level again (or any at all).
	 */
	const FLTQuestion* FromList(const TArray<TArray<FLTQuestion>>& Levels, int32 D, int32 Spread, const TSet<FString>& Asked, FRandomStream& Rng)
	{
		TArray<const FLTQuestion*> Candidates;
		for (int32 S = 0; S <= Spread && Candidates.Num() == 0; S++)
		{
			for (int32 L = 0; L < Levels.Num(); L++)
			{
				if (FMath::Abs(L + 1 - D) > S)
				{
					continue;
				}
				for (const FLTQuestion& Q : Levels[L])
				{
					if (!Asked.Contains(KeyOf(Q)))
					{
						Candidates.Add(&Q);
					}
				}
			}
		}
		if (Candidates.Num() == 0)
		{
			for (int32 Pass = 0; Pass < 2 && Candidates.Num() == 0; Pass++)
			{
				for (int32 L = 0; L < Levels.Num(); L++)
				{
					if (Pass == 0 && FMath::Abs(L + 1 - D) > 1)
					{
						continue;
					}
					for (const FLTQuestion& Q : Levels[L])
					{
						Candidates.Add(&Q);
					}
				}
			}
		}
		return Candidates.Num() > 0 ? Candidates[Rng.RandRange(0, Candidates.Num() - 1)] : nullptr;
	}

	/** A random not-yet-asked question of one bucket, or any of it when all have been asked. */
	const FLTQuestion* FromBucket(const TArray<FLTQuestion>& Bucket, const TSet<FString>& Asked, FRandomStream& Rng)
	{
		if (Bucket.Num() == 0)
		{
			return nullptr;
		}
		TArray<const FLTQuestion*> Fresh;
		for (const FLTQuestion& Q : Bucket)
		{
			if (!Asked.Contains(KeyOf(Q)))
			{
				Fresh.Add(&Q);
			}
		}
		return Fresh.Num() > 0 ? Fresh[Rng.RandRange(0, Fresh.Num() - 1)] : &Bucket[Rng.RandRange(0, Bucket.Num() - 1)];
	}
}

void ULTQuizSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);
	Rng.Initialize(static_cast<int32>(FDateTime::Now().GetTicks() % MAX_int32));

	// strings.json: { "key": ["pl", "en"], ... }
	if (const TSharedPtr<FJsonObject> Root = LoadJson(TEXT("strings.json")))
	{
		for (const TPair<FString, TSharedPtr<FJsonValue>>& Pair : Root->Values)
		{
			TArray<FString> Texts;
			const TArray<TSharedPtr<FJsonValue>>* Values = nullptr;
			if (Pair.Value.IsValid() && Pair.Value->TryGetArray(Values) && Values != nullptr)
			{
				for (const TSharedPtr<FJsonValue>& V : *Values)
				{
					FString S;
					Texts.Add(V.IsValid() && V->TryGetString(S) ? S : FString());
				}
			}
			Strings.Add(Pair.Key, MoveTemp(Texts));
		}
	}

	// quiz.json: { "pl": {...}, "en": {...} }, see tools/QuizExport/README.md
	const TSharedPtr<FJsonObject> Quiz = LoadJson(TEXT("quiz.json"));
	const TCHAR* Codes[2] = { TEXT("pl"), TEXT("en") };
	for (int32 I = 0; I < 2; I++)
	{
		FLangData& Data = Lang[I];
		Data.Subjects.SetNum(10);
		for (TArray<TArray<FLTQuestion>>& Subject : Data.Subjects)
		{
			Subject.SetNum(LT::MaxDifficulty);
		}
		Data.Kornik.SetNum(LT::MaxDifficulty);

		const TSharedPtr<FJsonObject>* LangObj = nullptr;
		if (!Quiz.IsValid() || !Quiz->TryGetObjectField(Codes[I], LangObj) || LangObj == nullptr || !LangObj->IsValid())
		{
			if (Quiz.IsValid())
			{
				UE_LOG(LogLazyTurtle, Error, TEXT("quiz.json has no \"%s\" section"), Codes[I]);
			}
			continue;
		}
		const TSharedPtr<FJsonObject>& Obj = *LangObj;

		const TArray<TSharedPtr<FJsonValue>>* Subjects = nullptr;
		if (Obj->TryGetArrayField(TEXT("subjects"), Subjects) && Subjects != nullptr)
		{
			for (int32 S = 0; S < Data.Subjects.Num() && S < Subjects->Num(); S++)
			{
				ParseBuckets((*Subjects)[S], Data.Subjects[S]);
			}
		}
		ParseBuckets(Obj->TryGetField(TEXT("kornik")), Data.Kornik);

		const TArray<TSharedPtr<FJsonValue>>* Riddles = nullptr;
		Obj->TryGetArrayField(TEXT("riddles"), Riddles);
		ParseQuestions(Riddles, Data.Riddles);
		ParseStrings(Obj, TEXT("kids"), Data.Kids);
		ParseStrings(Obj, TEXT("school"), Data.SchoolLines);
		ParseStrings(Obj, TEXT("yard"), Data.YardLines);

		int32 Count = 0;
		for (const TArray<TArray<FLTQuestion>>& Subject : Data.Subjects)
		{
			for (const TArray<FLTQuestion>& Bucket : Subject)
			{
				Count += Bucket.Num();
			}
		}
		UE_LOG(LogLazyTurtle, Log, TEXT("Quiz data [%s]: %d questions, %d riddles, %d texts"), Codes[I], Count, Data.Riddles.Num(), Strings.Num());
	}
}

FString ULTQuizSubsystem::T(const FString& Key) const
{
	if (const TArray<FString>* Texts = Strings.Find(Key))
	{
		const int32 Index = bPolish ? 0 : 1;
		if (Texts->IsValidIndex(Index) && !(*Texts)[Index].IsEmpty())
		{
			return (*Texts)[Index];
		}
		if (Texts->Num() > 0 && !(*Texts)[0].IsEmpty())
		{
			return (*Texts)[0];
		}
	}
	return Key;
}

FString ULTQuizSubsystem::F(const FString& Key, const TArray<FString>& Args) const
{
	FString Result = T(Key);
	for (int32 I = 0; I < Args.Num(); I++)
	{
		Result.ReplaceInline(*FString::Printf(TEXT("{%d}"), I), *Args[I], ESearchCase::CaseSensitive);
	}
	return Result;
}

FLTQuestion ULTQuizSubsystem::MakeQuestion(int32 Subject, int32 Difficulty)
{
	const int32 D = FMath::Clamp(Difficulty, 1, LT::MaxDifficulty);
	// Out-of-range subjects fell through to a list in the original (Nature below 0, Science above 9).
	const int32 S = Subject < 0 ? 2 : FMath::Min(Subject, 9);
	const FLangData& Data = Current();
	if (!Data.Subjects.IsValidIndex(S))
	{
		return Placeholder(bPolish);
	}

	const FLTQuestion* Q = IsListSubject(S)
		? FromList(Data.Subjects[S], D, ListSpread, Asked, Rng)
		: (Data.Subjects[S].IsValidIndex(D - 1) ? FromBucket(Data.Subjects[S][D - 1], Asked, Rng) : nullptr);
	if (Q == nullptr)
	{
		UE_LOG(LogLazyTurtle, Warning, TEXT("No questions for subject %d, difficulty %d"), S, D);
		return Placeholder(bPolish);
	}
	Asked.Add(KeyOf(*Q));
	return Shuffled(*Q);
}

FLTQuestion ULTQuizSubsystem::MasterQuestion(int32 Floor)
{
	const int32 D = FMath::Clamp(Floor + 2, 1, LT::MaxDifficulty);
	if (Rng.RandRange(0, 3) == 0)
	{
		// About Kórnik and our school: any difficulty, the closest ones first.
		if (const FLTQuestion* Q = FromList(Current().Kornik, D, LT::MaxDifficulty, Asked, Rng))
		{
			Asked.Add(KeyOf(*Q));
			return Shuffled(*Q);
		}
	}
	const int32 Subject = Rng.RandRange(0, 3) == 0 ? 3 : MasterSubjects[Rng.RandRange(0, static_cast<int32>(UE_ARRAY_COUNT(MasterSubjects)) - 1)];
	return MakeQuestion(Subject, D);
}

FLTQuestion ULTQuizSubsystem::Riddle()
{
	const TArray<FLTQuestion>& Riddles = Current().Riddles;
	if (Riddles.Num() == 0)
	{
		return Placeholder(bPolish);
	}
	return Shuffled(Riddles[Rng.RandRange(0, Riddles.Num() - 1)]);
}

FString ULTQuizSubsystem::ChatLine(bool bInYard)
{
	const FLangData& Data = Current();
	const TArray<FString>* Set = bInYard && Rng.RandRange(0, 3) != 0 ? &Data.YardLines : &Data.SchoolLines;
	if (Set->Num() == 0)
	{
		Set = Set == &Data.YardLines ? &Data.SchoolLines : &Data.YardLines;
		if (Set->Num() == 0)
		{
			return FString();
		}
	}
	FString Line = (*Set)[Rng.RandRange(0, Set->Num() - 1)];
	Line.ReplaceInline(TEXT("{c}"), *KidName(0), ESearchCase::CaseSensitive);   // the football captain
	return Line;
}

FString ULTQuizSubsystem::KidName(int32 Index) const
{
	const TArray<FString>& Kids = Current().Kids;
	if (Kids.Num() == 0)
	{
		return FString();
	}
	const int32 N = Kids.Num();
	return Kids[((Index % N) + N) % N];
}

FLTQuestion ULTQuizSubsystem::Shuffled(const FLTQuestion& Q)
{
	FLTQuestion Result = Q;
	if (!Q.IsValid())
	{
		return Result;
	}
	TArray<int32> Order;
	for (int32 I = 0; I < Q.Answers.Num(); I++)
	{
		Order.Add(I);
	}
	for (int32 I = Order.Num() - 1; I > 0; I--)
	{
		Order.Swap(I, Rng.RandRange(0, I));
	}
	for (int32 I = 0; I < Order.Num(); I++)
	{
		Result.Answers[I] = Q.Answers[Order[I]];
		if (Order[I] == Q.Correct)
		{
			Result.Correct = I;
		}
	}
	return Result;
}
