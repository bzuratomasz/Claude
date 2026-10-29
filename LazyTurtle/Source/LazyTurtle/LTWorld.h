#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "LTSchool.h"
#include "LTWorld.generated.h"

class UStaticMeshComponent;
class USkeletalMeshComponent;
class UTextRenderComponent;
class USphereComponent;
class UInstancedStaticMeshComponent;
class UMaterialInterface;
class UMaterialInstanceDynamic;
class UStaticMesh;
class UPointLightComponent;
class ALTCoin;
class ALTTurtleNPC;
class ALTKid;
class ALTGift;
class ALTMarker;

/**
 * Builds turtles out of engine basic shapes (shell, head, legs, optional yellow backpack) when no skeletal mesh
 * is set in the art settings. Used for the player, teachers, the Master Turtle and classmates.
 */
struct LAZYTURTLE_API FLTTurtleVisual
{
	/**
	 * Adds the parts as static mesh components under Parent. Height = total height in cm (~85 for the player,
	 * ~155 for teachers). Faces +X. Returns the created components.
	 */
	static TArray<UStaticMeshComponent*> Build(AActor* Owner, USceneComponent* Parent, float Height,
		const FLinearColor& ShellColor, const FLinearColor& SkinColor, bool bBackpack, const FLinearColor& AccessoryColor);

	/** A dynamic instance of the engine basic shape material tinted with Color (parameter "Color"). */
	static UMaterialInstanceDynamic* Tinted(UObject* Outer, const FLinearColor& Color);
};

/** Furniture and subject artifacts: a mesh from the art settings (by key) or a tinted basic shape. */
UCLASS()
class LAZYTURTLE_API ALTProp : public AActor
{
	GENERATED_BODY()

public:
	ALTProp();
	void Init(const FLTPropSpawn& Spawn, const FLinearColor& Tint);

protected:
	UPROPERTY(VisibleAnywhere) TObjectPtr<USceneComponent> Root;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UStaticMeshComponent> Mesh;
};

/** A turtle character that is not the player: teacher, Master Turtle or classmate. */
UCLASS()
class LAZYTURTLE_API ALTTurtleNPC : public AActor
{
	GENERATED_BODY()

public:
	ALTTurtleNPC();
	/** Kind is Teacher, Master or Kid; Variant = subject for teachers, look for kids. */
	virtual void Init(const FLTPropSpawn& Spawn, const FLinearColor& Accent);

	const FString& GetDisplayName() const { return DisplayName; }
	/** Turns smoothly to look at a world location (yaw only). */
	void FaceTowards(const FVector& Target);

	virtual void Tick(float DeltaSeconds) override;

protected:
	UPROPERTY(VisibleAnywhere) TObjectPtr<USceneComponent> Root;
	UPROPERTY(VisibleAnywhere) TObjectPtr<USkeletalMeshComponent> Body;     // used when a skeletal mesh is set
	UPROPERTY(VisibleAnywhere) TObjectPtr<UTextRenderComponent> NameLabel;
	FString DisplayName;
	double WantYaw = 0;
	bool bTurning = false;

	/** Parent of the basic-shape turtle (waddles while walking); empty when a skeletal mesh is used. */
	UPROPERTY() TObjectPtr<USceneComponent> Visual;
	float Height = 155.f;           // cm
	float CurrentSpeed = 0.f;       // cm/s, fed to the anim instance ("Speed") or the waddle
	float WalkPhase = 0.f;
	/** Sets a float/double "Speed" variable on the anim instance if it has one. */
	void SetAnimSpeed(float Speed);
};

/** A classmate walking back and forth along the corridor; stops now and then and when talked to. */
UCLASS()
class LAZYTURTLE_API ALTKid : public ALTTurtleNPC
{
	GENERATED_BODY()

public:
	virtual void Init(const FLTPropSpawn& Spawn, const FLinearColor& Accent) override;
	virtual void Tick(float DeltaSeconds) override;

	/** Stops walking for Seconds (talking to the player). */
	void PauseWalking(float Seconds) { PauseTime = FMath::Max(PauseTime, Seconds); }

	double MinX = 2, MaxX = 39;     // map cells

private:
	int32 Dir = 1;
	float PauseTime = 0;
	float Speed = 0.7f;             // cells per second
};

/** The gift on a teacher's desk: bobs and spins until won. */
UCLASS()
class LAZYTURTLE_API ALTGift : public AActor
{
	GENERATED_BODY()

public:
	ALTGift();
	void Init(const FLTPropSpawn& Spawn, const FLinearColor& Ribbon);
	virtual void Tick(float DeltaSeconds) override;
	/** Hides the gift (with sparkles spawned by the floor). */
	void Collect();

private:
	UPROPERTY(VisibleAnywhere) TObjectPtr<UStaticMeshComponent> Mesh;
	FVector Base;
	float Time = 0;
};

/** The "!" (question waiting) / check mark (done) floating above a teacher or the Master Turtle. */
UCLASS()
class LAZYTURTLE_API ALTMarker : public AActor
{
	GENERATED_BODY()

public:
	ALTMarker();
	void SetDone(bool bInDone);
	virtual void Tick(float DeltaSeconds) override;

private:
	UPROPERTY(VisibleAnywhere) TObjectPtr<UTextRenderComponent> Text;
	/** Green check mark built from two bars (the default text font has no check glyph). */
	UPROPERTY() TObjectPtr<USceneComponent> Check;
	FVector Base;
	float Time = 0;
	bool bBaseSet = false;
	bool bDone = false;
	void EnsureCheck();
};

DECLARE_MULTICAST_DELEGATE_OneParam(FLTOnCoinCollected, ALTCoin*);

/** A gold coin; some float in the air and need a jump. Collected by overlapping the player pawn. */
UCLASS()
class LAZYTURTLE_API ALTCoin : public AActor
{
	GENERATED_BODY()

public:
	ALTCoin();
	virtual void Tick(float DeltaSeconds) override;

	FLTOnCoinCollected OnCollected;

private:
	UPROPERTY(VisibleAnywhere) TObjectPtr<USphereComponent> Trigger;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UStaticMeshComponent> Mesh;
	bool bCollected = false;

	friend class ALTFloor;
	/** Art mesh / fallback shape and trigger size; floating coins get a small trigger above the pawn's head. */
	void Setup(bool bFloating);

	UFUNCTION() void OnOverlap(UPrimitiveComponent* OverlappedComp, AActor* Other, UPrimitiveComponent* OtherComp,
		int32 OtherBodyIndex, bool bFromSweep, const FHitResult& Sweep);
};

/** Runtime state of one classroom. */
USTRUCT()
struct FLTRoomRuntime
{
	GENERATED_BODY()

	FLTRoomLayout Layout;
	bool bDone = false;
	UPROPERTY() TObjectPtr<ALTTurtleNPC> Teacher;
	UPROPERTY() TObjectPtr<ALTGift> Gift;
	UPROPERTY() TObjectPtr<ALTMarker> Marker;
};

/**
 * One school floor in the world: walls, floors, ceilings with lamps, wall decorations, furniture, people and
 * coins built from FLTSchool::BuildLayout, plus the sky / sun / post-process environment.
 * The game mode destroys it and spawns a new one when changing floors.
 */
UCLASS()
class LAZYTURTLE_API ALTFloor : public AActor
{
	GENERATED_BODY()

public:
	ALTFloor();

	/** Builds everything. DoneMask = classrooms whose gift is already collected; bMasterDone opens the stairs. */
	void Build(int32 Floor, int32 DoneMask, bool bMasterDone, bool bInPolish);

	const FLTFloorLayout& GetLayout() const { return Layout; }
	TArray<FLTRoomRuntime>& GetRooms() { return Rooms; }
	const TArray<FLTRoomRuntime>& GetRooms() const { return Rooms; }
	int32 GiftsDone() const;

	/** Gift won: hides the gift with sparkles and turns the marker into a check mark. */
	void MarkRoomDone(int32 RoomIndex);
	/** Master Turtle answered: marker becomes a check mark. */
	void MarkMasterDone();

	/** Removes the Master hall door (collision and mesh); idempotent. */
	void OpenMasterDoor();
	/** Shows the open stairs (they are a trigger area; the game mode checks the distance). */
	void OpenStairs();
	bool IsMasterDoorOpen() const { return bMasterDoorOpen; }
	bool AreStairsOpen() const { return bStairsOpen; }

	ALTTurtleNPC* GetMaster() const { return Master; }
	const TArray<TObjectPtr<ALTKid>>& GetKids() const { return Kids; }

	/** World position of the centre of a map cell / point at floor level. */
	static FVector CellCenter(FIntPoint Cell) { return LT::MapToWorld(Cell.X + 0.5, Cell.Y + 0.5); }
	FVector GetStartLocation() const { return LT::MapToWorld(Layout.Start.X, Layout.Start.Y); }
	double GetStartYawDegrees() const { return LT::MapYawToDegrees(Layout.StartYaw); }

	/** A little burst of golden sparkles (Niagara-free: short-lived tinted spheres). */
	void SpawnSparkles(const FVector& At, int32 Count);

	/** Fired for every collected coin (the game mode adds it to the save and plays a sound). */
	FLTOnCoinCollected OnCoinCollected;

	virtual void Tick(float DeltaSeconds) override;

private:
	FLTFloorLayout Layout;
	UPROPERTY() TArray<FLTRoomRuntime> Rooms;
	UPROPERTY() TObjectPtr<ALTTurtleNPC> Master;
	UPROPERTY() TObjectPtr<ALTMarker> MasterMarker;
	UPROPERTY() TArray<TObjectPtr<ALTKid>> Kids;
	UPROPERTY() TArray<TObjectPtr<AActor>> Spawned;          // everything spawned by Build, destroyed with the floor
	UPROPERTY() TObjectPtr<UStaticMeshComponent> MasterDoorMesh;
	UPROPERTY() TObjectPtr<UStaticMeshComponent> StairsMesh;
	bool bMasterDoorOpen = false;
	bool bStairsOpen = false;

	struct FSparkle { TWeakObjectPtr<UStaticMeshComponent> Comp; FVector Vel; float Life; };
	TArray<FSparkle> Sparkles;

	virtual void Destroyed() override;

	// ---- building helpers (LTWorld.cpp). The floor sits at the world origin; component transforms are relative.
	int32 FloorIndex = 0;
	bool bPolish = true;
	UPROPERTY() TObjectPtr<USceneComponent> FloorRoot;
	UPROPERTY() TObjectPtr<UPointLightComponent> StairsLight;
	UPROPERTY() TMap<uint32, TObjectPtr<UMaterialInterface>> MaterialCache;

	/** Tinted basic shape material (bEmissive: glowing variant if the engine has one), cached per colour. */
	UMaterialInterface* Mat(const FLinearColor& Color, bool bEmissive = false);
	/** The art-settings material if set, otherwise the tinted fallback. */
	UMaterialInterface* ArtMat(const TSoftObjectPtr<UMaterialInterface>& Art, const FLinearColor& Fallback);
	template <class T> T* NewComp(USceneComponent* Parent = nullptr);
	/** A basic shape (100 cm) scaled to SizeCm. */
	UStaticMeshComponent* AddShape(UStaticMesh* Shape, const FVector& Center, const FRotator& Rot, const FVector& SizeCm,
		UMaterialInterface* Material, USceneComponent* Parent = nullptr);
	UStaticMeshComponent* AddBox(const FVector& Center, const FRotator& Rot, const FVector& SizeCm, UMaterialInterface* Material,
		USceneComponent* Parent = nullptr);
	UInstancedStaticMeshComponent* AddInstanced(UStaticMesh* Mesh, UMaterialInterface* Material, bool bBlock);
	UTextRenderComponent* AddText(const FString& S, const FVector& Loc, const FRotator& Rot, float Size, const FColor& Color,
		USceneComponent* Parent = nullptr);
	/** Art mesh placed so it hangs flush on a wall face (pivot on the floor, facing out of the wall). */
	UStaticMeshComponent* AddArtMesh(UStaticMesh* Mesh, const FVector& Loc, const FRotator& Rot, bool bBlock);

	void BuildEnvironment(const FLTFloorTheme& Theme);
	void BuildShell(const FLTFloorTheme& Theme);
	void BuildDecor(const FLTWallDecor& D, const FLTFloorTheme& Theme);
	void BuildMasterDoor(const FLTFloorTheme& Theme);
	void BuildStairs(const FLTFloorTheme& Theme);
	void BuildProps(int32 DoneMask, const FLTFloorTheme& Theme);
	void AddPicture(const FLTWallDecor& D, double W, double H, double Z, const FLinearColor& FrameColor, double FrameW,
		const FLinearColor& Placeholder);
	void HandleCoin(ALTCoin* Coin);
	template <class T> T* SpawnPart(const FVector& Loc, double YawDeg);
};
