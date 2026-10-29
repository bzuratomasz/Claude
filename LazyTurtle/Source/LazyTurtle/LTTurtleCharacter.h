#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Character.h"
#include "LTTypes.h"
#include "LTTurtleCharacter.generated.h"

class USpringArmComponent;
class UCameraComponent;
class UStaticMeshComponent;

/**
 * The Lazy Turtle: tank controls like the original (A/D turn, W/S forward/back, Q/E sideways, Shift runs, Space jumps)
 * and a third-person camera behind it that slides closer instead of going through walls.
 * Speeds are the original ones in cells per second converted to centimetres.
 */
UCLASS()
class LAZYTURTLE_API ALTTurtleCharacter : public ACharacter
{
	GENERATED_BODY()

public:
	ALTTurtleCharacter();

	virtual void BeginPlay() override;
	virtual void Tick(float DeltaSeconds) override;

	/** Teleports to a map position (cells) facing YawRadians; the camera snaps behind. */
	void SetMapPose(const FVector2D& MapPos, double YawRadians);
	FVector2D GetMapPos() const;
	double GetMapYaw() const;
	/** Turns the turtle (title screen orbit, mouse look). */
	void AddMapYaw(double Radians);

	/** Called every frame while held. Forward: +1 forward, negative backwards (the mapping scales back to 0.7). */
	void MoveForward(float Value);
	/** -1 left .. +1 right, turns at TurnSpeed. */
	void Turn(float Value);
	/** -1 left (Q) .. +1 right (E). */
	void Strafe(float Value);
	void SetRunning(bool bRun);
	/** Jumps when standing on the ground; returns true when it did. */
	bool TryJump();
	/** Right mouse drag: turn and tilt the camera (pixels). */
	void MouseLook(float DX, float DY);
	/** Mouse wheel notches: + = closer. */
	void Zoom(float Notches);
	/** Yaw of the camera in map radians (the compass is relative to it). */
	float GetCameraYawRadians() const;

	/** Horizontal speed in cm/s (for an animation blueprint). */
	UFUNCTION(BlueprintPure, Category = "LazyTurtle") float GetSpeed() const;
	UFUNCTION(BlueprintPure, Category = "LazyTurtle") bool IsInAir() const;

	static constexpr float Radius = 0.25f;          // cells
	static constexpr float WalkSpeed = 2.3f;        // cells / s
	static constexpr float RunSpeed = 3.6f;
	static constexpr float TurnSpeed = 2.4f;        // rad / s
	static constexpr float JumpVelocity = 2.8f;     // cells / s
	static constexpr float Gravity = 9.5f;          // cells / s^2
	static constexpr float CamDistance = 2.1f;      // cells
	static constexpr float VisualHeight = 85.f;     // cm

private:
	UPROPERTY(VisibleAnywhere) TObjectPtr<USpringArmComponent> CameraArm;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UCameraComponent> Camera;
	/** Parent of the basic-shape turtle (used when no player skeletal mesh is set). */
	UPROPERTY(VisibleAnywhere) TObjectPtr<USceneComponent> VisualRoot;
	UPROPERTY() TArray<TObjectPtr<UStaticMeshComponent>> VisualParts;

	float CamPitch = 0.32f;         // radians the camera looks down
	float CamWant = CamDistance;    // cells
	float BobTime = 0;
	int32 SnapFrames = 0;           // frames with camera lag off after a teleport
	bool bRunning = false;
	bool bUsesSkeletalMesh = false;

	void SetCameraLag(bool bEnable);
};
