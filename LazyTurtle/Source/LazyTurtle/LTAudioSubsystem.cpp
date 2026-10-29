// Port of Sounds.cs: retro sound effects synthesized in code (22050 Hz mono 16-bit) and played as 2D UI sounds.

#include "LTServices.h"
#include "LazyTurtle.h"
#include "Components/AudioComponent.h"
#include "Engine/Engine.h"
#include "Engine/GameInstance.h"
#include "Engine/World.h"
#include "Kismet/GameplayStatics.h"
#include "Sound/SoundGroups.h"
#include "Sound/SoundWaveProcedural.h"
#include "TimerManager.h"
#include "UObject/Package.h"

namespace LTAudioDetail
{
	constexpr int32 Rate = 22050;
	enum class EWave : uint8 { Sine, Square, Triangle };

	/** PCM bytes (16-bit little-endian mono) per sound name; the same for every game instance. */
	TMap<FName, TArray<uint8>>& Cache()
	{
		static TMap<FName, TArray<uint8>> Map;
		return Map;
	}

	double Osc(EWave W, double Phase)
	{
		const double S = FMath::Sin(Phase);
		switch (W)
		{
		case EWave::Square: return (S >= 0 ? 0.7 : -0.7) + S * 0.3;
		case EWave::Triangle: return 2 / UE_DOUBLE_PI * FMath::Asin(S);
		default: return S;
		}
	}

	/** A melody given as pairs: frequency (Hz), duration (s). */
	TArray<int16> Notes(EWave W, double Vol, std::initializer_list<double> FD)
	{
		const TArray<double> Pairs(FD);
		TArray<int16> Out;
		for (int32 I = 0; I + 1 < Pairs.Num(); I += 2)
		{
			const double F = Pairs[I];
			const int32 N = (int32)(Pairs[I + 1] * Rate);
			double Phase = 0;
			for (int32 K = 0; K < N; K++)
			{
				const double T = K / (double)N;
				const double Env = FMath::Min(1.0, K / (0.005 * Rate)) * (T < 0.6 ? 1.0 : (1 - T) / 0.4);
				Phase += 2 * UE_DOUBLE_PI * F / Rate;
				Out.Add((int16)(Osc(W, Phase) * Env * Vol * 32000));
			}
		}
		return Out;
	}

	TArray<int16> Sweep(EWave W, double Vol, double F0, double F1, double Dur, bool bVibrato)
	{
		const int32 N = (int32)(Dur * Rate);
		TArray<int16> Out;
		Out.SetNumZeroed(N);
		double Phase = 0;
		for (int32 K = 0; K < N; K++)
		{
			const double T = K / (double)N;
			double F = F0 + (F1 - F0) * T;
			if (bVibrato) F *= 1 + 0.08 * FMath::Sin(T * 60);
			Phase += 2 * UE_DOUBLE_PI * F / Rate;
			const double Env = FMath::Min(1.0, K / (0.004 * Rate)) * (1 - T);
			Out[K] = (int16)(Osc(W, Phase) * Env * Vol * 32000);
		}
		return Out;
	}

	void Add(FName Name, const TArray<int16>& Samples)
	{
		TArray<uint8>& Bytes = Cache().FindOrAdd(Name);
		Bytes.SetNumUninitialized(Samples.Num() * sizeof(int16));
		for (int32 I = 0; I < Samples.Num(); I++)
		{
			const uint16 V = (uint16)Samples[I];
			Bytes[I * 2] = (uint8)(V & 0xFF);
			Bytes[I * 2 + 1] = (uint8)(V >> 8);
		}
	}

	void Synthesize()
	{
		if (Cache().Num() > 0) return;
		Add(LT::Sfx::Coin, Notes(EWave::Square, 0.16, { 988, 0.07, 1319, 0.28 }));
		Add(LT::Sfx::Jump, Sweep(EWave::Square, 0.12, 260, 640, 0.2, false));
		Add(LT::Sfx::Correct, Notes(EWave::Triangle, 0.45, { 784, 0.09, 988, 0.09, 1175, 0.09, 1568, 0.3 }));
		Add(LT::Sfx::Wrong, Notes(EWave::Sine, 0.4, { 392, 0.18, 330, 0.32 }));
		Add(LT::Sfx::Gift, Notes(EWave::Square, 0.13, { 1047, 0.07, 1319, 0.07, 1568, 0.07, 2093, 0.07, 1568, 0.07, 2093, 0.07, 2637, 0.35 }));
		Add(LT::Sfx::Talk, Notes(EWave::Triangle, 0.3, { 660, 0.04, 880, 0.05 }));
		Add(LT::Sfx::Door, Notes(EWave::Sine, 0.45, { 784, 0.12, 1047, 0.12, 1319, 0.12, 1568, 0.45 }));
		Add(LT::Sfx::Stairs, Notes(EWave::Triangle, 0.4, { 523, 0.08, 659, 0.08, 784, 0.08, 1047, 0.08, 1319, 0.08, 1568, 0.3 }));
		Add(LT::Sfx::Win, Notes(EWave::Square, 0.14, { 523, 0.13, 659, 0.13, 784, 0.13, 1047, 0.3, 784, 0.13, 1047, 0.6 }));
		Add(LT::Sfx::Bump, Sweep(EWave::Triangle, 0.35, 220, 140, 0.1, false));
		Add(LT::Sfx::Click, Notes(EWave::Triangle, 0.3, { 880, 0.035 }));
		Add(LT::Sfx::Select, Notes(EWave::Triangle, 0.35, { 660, 0.05, 990, 0.09 }));
	}
}

void ULTAudioSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);
	if (ULTSaveSubsystem* SaveSys = Collection.InitializeDependency<ULTSaveSubsystem>())
		if (const ULTSaveGame* S = SaveSys->Get())
			bEnabled = S->bSound;
	LTAudioDetail::Synthesize();
}

void ULTAudioSubsystem::Play(FName Name)
{
	if (!bEnabled) return;
	const TArray<uint8>* Pcm = LTAudioDetail::Cache().Find(Name);
	if (!Pcm || Pcm->Num() == 0) return;

	// No audio (dedicated server, -nosound, no device, no world yet): the game simply stays silent.
	if (!GEngine || !GEngine->UseSound() || IsRunningDedicatedServer()) return;
	const UGameInstance* GI = GetGameInstance();
	UWorld* World = GI ? GI->GetWorld() : nullptr;
	if (!World || !World->bAllowAudioPlayback) return;

	// A procedural wave is consumed by playing it, so every play gets a fresh one fed with the cached samples.
	USoundWaveProcedural* Wave = NewObject<USoundWaveProcedural>(GetTransientPackage());
	if (!Wave) return;
	const float Seconds = Pcm->Num() / (2.f * LTAudioDetail::Rate);
	Wave->NumChannels = 1;
	Wave->SetSampleRate(LTAudioDetail::Rate);
	Wave->Duration = Seconds;
	Wave->bLooping = false;
	Wave->SoundGroup = SOUNDGROUP_UI;
	Wave->QueueAudio(Pcm->GetData(), Pcm->Num());

	UAudioComponent* Comp = UGameplayStatics::SpawnSound2D(World, Wave, 1.f, 1.f, 0.f, nullptr, false, true);
	if (!Comp) return;
	// A procedural source never runs out by itself; stop it once the samples have been played (auto-destroys).
	FTimerHandle Handle;
	World->GetTimerManager().SetTimer(Handle, FTimerDelegate::CreateWeakLambda(Comp, [Comp]()
	{
		Comp->Stop();
	}), Seconds + 0.1f, false);
}
