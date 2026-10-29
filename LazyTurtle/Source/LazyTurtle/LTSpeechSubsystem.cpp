// Port of Speech.cs: reads questions aloud with a Windows SAPI 5 voice in the game's language.
// Without a matching voice it stays silent rather than mispronouncing. Other platforms: silent.

#include "LTServices.h"
#include "LazyTurtle.h"

#if PLATFORM_WINDOWS
#include "Windows/WindowsHWrapper.h"
#include "Windows/AllowWindowsPlatformTypes.h"
#include "Windows/PreWindowsApi.h"
THIRD_PARTY_INCLUDES_START
#include <sapi.h>
THIRD_PARTY_INCLUDES_END
#include "Windows/PostWindowsApi.h"
#include "Windows/HideWindowsPlatformTypes.h"

namespace LTSpeechDetail
{
	/** Voice tokens per language (0 = pl, 1 = en), looked up once and shared by every game instance. */
	struct FState
	{
		int32 Users = 0;
		bool bComInitialized = false;
		ISpObjectToken* Tokens[2] = { nullptr, nullptr };
		bool bSearched[2] = { false, false };
	};

	FState& State()
	{
		static FState S;
		return S;
	}

	/** First enabled voice in a token category matching an attribute such as "Language=415" (AddRef'd), or null. */
	ISpObjectToken* FindIn(const wchar_t* Category, const wchar_t* Attributes)
	{
		ISpObjectTokenCategory* Cat = nullptr;
		if (FAILED(CoCreateInstance(__uuidof(SpObjectTokenCategory), nullptr, CLSCTX_ALL, __uuidof(ISpObjectTokenCategory), (void**)&Cat)) || !Cat)
			return nullptr;
		ISpObjectToken* Found = nullptr;
		if (SUCCEEDED(Cat->SetId(Category, 0)))
		{
			IEnumSpObjectTokens* Enum = nullptr;
			if (SUCCEEDED(Cat->EnumTokens(Attributes, nullptr, &Enum)) && Enum)
			{
				ISpObjectToken* Token = nullptr;
				ULONG Fetched = 0;
				if (Enum->Next(1, &Token, &Fetched) == S_OK && Fetched == 1 && Token)
					Found = Token;
				Enum->Release();
			}
		}
		Cat->Release();
		return Found;
	}

	/** A voice for the language: classic SAPI voices first, then the Windows 10/11 OneCore voices (often the only Polish one). */
	ISpObjectToken* FindVoice(bool bPolish)
	{
		static const wchar_t* Categories[] = { SPCAT_VOICES, L"HKEY_LOCAL_MACHINE\\SOFTWARE\\Microsoft\\Speech_OneCore\\Voices" };
		static const wchar_t* Pl[] = { L"Language=415" };
		static const wchar_t* En[] = { L"Language=409", L"Language=809" };
		for (const wchar_t* Category : Categories)
		{
			if (bPolish)
			{
				for (const wchar_t* A : Pl)
					if (ISpObjectToken* T = FindIn(Category, A)) return T;
			}
			else
			{
				for (const wchar_t* A : En)
					if (ISpObjectToken* T = FindIn(Category, A)) return T;
			}
		}
		return nullptr;
	}

	ISpObjectToken* Token(bool bPolish)
	{
		FState& S = State();
		const int32 L = bPolish ? 0 : 1;
		if (!S.bSearched[L])
		{
			S.bSearched[L] = true;
			S.Tokens[L] = FindVoice(bPolish);
		}
		return S.Tokens[L];
	}
}
#endif

void ULTSpeechSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);
	if (ULTSaveSubsystem* SaveSys = Collection.InitializeDependency<ULTSaveSubsystem>())
		if (const ULTSaveGame* S = SaveSys->Get())
			bEnabled = S->bSpeech;

#if PLATFORM_WINDOWS
	using namespace LTSpeechDetail;
	FState& S = State();
	if (S.Users++ == 0)
	{
		// The game thread usually has COM already; S_FALSE / RPC_E_CHANGED_MODE are fine, only our own S_OK/S_FALSE need balancing.
		const HRESULT Hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
		S.bComInitialized = Hr == S_OK || Hr == S_FALSE;
	}
	ISpVoice* V = nullptr;
	if (SUCCEEDED(CoCreateInstance(__uuidof(SpVoice), nullptr, CLSCTX_ALL, __uuidof(ISpVoice), (void**)&V)) && V)
	{
		V->SetRate(-1);
		Voice = V;
	}
	else
	{
		UE_LOG(LogLazyTurtle, Log, TEXT("Speech: SAPI voice not available"));
	}
#endif
}

void ULTSpeechSubsystem::Deinitialize()
{
	Stop();
#if PLATFORM_WINDOWS
	using namespace LTSpeechDetail;
	if (Voice)
	{
		static_cast<ISpVoice*>(Voice)->Release();
		Voice = nullptr;
	}
	FState& S = State();
	if (S.Users > 0 && --S.Users == 0)
	{
		for (int32 L = 0; L < 2; L++)
		{
			if (S.Tokens[L]) S.Tokens[L]->Release();
			S.Tokens[L] = nullptr;
			S.bSearched[L] = false;
		}
		if (S.bComInitialized) CoUninitialize();
		S.bComInitialized = false;
	}
#endif
	Super::Deinitialize();
}

bool ULTSpeechSubsystem::IsAvailable(bool bPolish) const
{
#if PLATFORM_WINDOWS
	return Voice != nullptr && LTSpeechDetail::Token(bPolish) != nullptr;
#else
	return false;
#endif
}

void ULTSpeechSubsystem::Say(const FString& Text, bool bPolish)
{
#if PLATFORM_WINDOWS
	if (!bEnabled || !Voice || Text.IsEmpty()) return;
	ISpVoice* V = static_cast<ISpVoice*>(Voice);
	V->Speak(nullptr, SPF_PURGEBEFORESPEAK, nullptr);
	ISpObjectToken* T = LTSpeechDetail::Token(bPolish);
	if (!T || FAILED(V->SetVoice(T))) return;
	V->Speak(*Text, SPF_ASYNC | SPF_PURGEBEFORESPEAK | SPF_IS_NOT_XML, nullptr);
#endif
}

void ULTSpeechSubsystem::Stop()
{
#if PLATFORM_WINDOWS
	if (Voice) static_cast<ISpVoice*>(Voice)->Speak(nullptr, SPF_PURGEBEFORESPEAK, nullptr);
#endif
}
