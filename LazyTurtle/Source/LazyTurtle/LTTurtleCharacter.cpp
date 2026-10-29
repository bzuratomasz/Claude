#include "LTTurtleCharacter.h"

#include "Animation/AnimInstance.h"
#include "Camera/CameraComponent.h"
#include "Components/CapsuleComponent.h"
#include "Components/SkeletalMeshComponent.h"
#include "Components/StaticMeshComponent.h"
#include "Engine/SkeletalMesh.h"
#include "Engine/World.h"
#include "GameFramework/Controller.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "GameFramework/SpringArmComponent.h"
#include "LTArtSettings.h"
#include "LTWorld.h"

namespace
{
	constexpr float TurtleCell = (float)LT::CellSize;
	constexpr float TurtleCapsuleRadius = ALTTurtleCharacter::Radius * TurtleCell;              // 37.5 cm
	constexpr float TurtleCapsuleHalfHeight = ALTTurtleCharacter::VisualHeight * 0.5f;    // 42.5 cm
	/** The camera looks at a point this high above the floor (the original 0.42 cells). */
	constexpr float TurtlePivotHeight = 0.42f * TurtleCell;
	constexpr float TurtleEngineGravity = 980.f;
}

ALTTurtleCharacter::ALTTurtleCharacter()
{
	PrimaryActorTick.bCanEverTick = true;

	GetCapsuleComponent()->InitCapsuleSize(TurtleCapsuleRadius, TurtleCapsuleHalfHeight);

	// tank controls: the game turns the actor itself, the controller rotation is not used
	bUseControllerRotationYaw = false;
	bUseControllerRotationPitch = false;
	bUseControllerRotationRoll = false;

	UCharacterMovementComponent* Move = GetCharacterMovement();
	Move->bOrientRotationToMovement = false;
	Move->bUseControllerDesiredRotation = false;
	Move->MaxWalkSpeed = WalkSpeed * TurtleCell;
	Move->MaxAcceleration = 3000.f;
	Move->BrakingDecelerationWalking = 3000.f;
	Move->GroundFriction = 8.f;
	Move->JumpZVelocity = JumpVelocity * TurtleCell;
	Move->GravityScale = Gravity * TurtleCell / TurtleEngineGravity;
	// the original kept full control in the air; keep it close to that without sliding around
	Move->AirControl = 0.9f;
	Move->AirControlBoostMultiplier = 1.f;
	Move->BrakingDecelerationFalling = 1500.f;
	Move->FallingLateralFriction = 0.f;
	Move->MaxStepHeight = 30.f;
	Move->bCanWalkOffLedges = true;

	CameraArm = CreateDefaultSubobject<USpringArmComponent>(TEXT("CameraArm"));
	CameraArm->SetupAttachment(GetCapsuleComponent());
	CameraArm->SetRelativeLocation(FVector(0, 0, TurtlePivotHeight - TurtleCapsuleHalfHeight));
	CameraArm->SetRelativeRotation(FRotator(-FMath::RadiansToDegrees(CamPitch), 0, 0));
	CameraArm->TargetArmLength = CamDistance * TurtleCell;
	CameraArm->bUsePawnControlRotation = false;
	CameraArm->bInheritPitch = false;
	CameraArm->bInheritRoll = false;
	CameraArm->bInheritYaw = true;
	CameraArm->bDoCollisionTest = true;
	CameraArm->ProbeSize = 18.f;
	CameraArm->ProbeChannel = ECC_Camera;
	CameraArm->bEnableCameraLag = true;
	CameraArm->CameraLagSpeed = 10.f;
	CameraArm->bEnableCameraRotationLag = true;
	CameraArm->CameraRotationLagSpeed = 8.f;

	Camera = CreateDefaultSubobject<UCameraComponent>(TEXT("Camera"));
	Camera->SetupAttachment(CameraArm, USpringArmComponent::SocketName);
	Camera->bUsePawnControlRotation = false;
	Camera->SetFieldOfView(75.f);

	VisualRoot = CreateDefaultSubobject<USceneComponent>(TEXT("VisualRoot"));
	VisualRoot->SetupAttachment(GetCapsuleComponent());
	VisualRoot->SetRelativeLocation(FVector(0, 0, -TurtleCapsuleHalfHeight));

	GetMesh()->SetRelativeLocation(FVector(0, 0, -TurtleCapsuleHalfHeight));
	GetMesh()->SetCollisionEnabled(ECollisionEnabled::NoCollision);
}

void ALTTurtleCharacter::BeginPlay()
{
	Super::BeginPlay();

	const ULTArtSettings* Art = ULTArtSettings::Get();
	USkeletalMesh* SkMesh = Art ? Art->PlayerMesh.LoadSynchronous() : nullptr;
	if (SkMesh)
	{
		bUsesSkeletalMesh = true;
		GetMesh()->SetSkeletalMeshAsset(SkMesh);
		if (UClass* AnimClass = Art->PlayerAnim.LoadSynchronous())
			GetMesh()->SetAnimInstanceClass(AnimClass);
	}
	else
	{
		// green shell, yellow school backpack
		const TArray<UStaticMeshComponent*> Parts = FLTTurtleVisual::Build(this, VisualRoot, VisualHeight,
			FLinearColor(0.10f, 0.42f, 0.08f), FLinearColor(0.50f, 0.75f, 0.28f), true, FLinearColor(1.0f, 0.78f, 0.05f));
		for (UStaticMeshComponent* Part : Parts)
		{
			if (!Part) continue;
			Part->SetCollisionEnabled(ECollisionEnabled::NoCollision);
			VisualParts.Add(Part);
		}
	}
}

void ALTTurtleCharacter::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);

	if (SnapFrames > 0 && --SnapFrames == 0) SetCameraLag(true);

	// zoom eases out (walls pull the camera in instantly through the spring arm's collision probe)
	CameraArm->TargetArmLength = FMath::FInterpTo(CameraArm->TargetArmLength, CamWant * TurtleCell, DeltaSeconds, 4.f);
	CameraArm->SetRelativeRotation(FRotator(-FMath::RadiansToDegrees(CamPitch), 0, 0));

	// hide the turtle when the camera is squeezed right behind it
	const float CamDist = FVector::Dist(Camera->GetComponentLocation(), CameraArm->GetComponentLocation());
	const bool bVisible = CamDist > 0.75f * TurtleCell;
	if (VisualRoot->IsVisible() != bVisible) VisualRoot->SetVisibility(bVisible, true);
	if (bUsesSkeletalMesh && GetMesh()->IsVisible() != bVisible) GetMesh()->SetVisibility(bVisible, true);

	// a little waddle for the basic-shape turtle
	if (!bUsesSkeletalMesh)
	{
		const float Speed = GetSpeed();
		const bool bWalking = Speed > 10.f && !IsInAir();
		if (bWalking) BobTime += DeltaSeconds * Speed / TurtleCell * 4.f;
		else BobTime = FMath::FInterpTo(BobTime, FMath::RoundToFloat(BobTime / UE_PI) * UE_PI, DeltaSeconds, 8.f);
		const float Bob = FMath::Abs(FMath::Sin(BobTime)) * 3.f;
		const float Roll = FMath::Sin(BobTime) * 4.f;
		VisualRoot->SetRelativeLocationAndRotation(FVector(0, 0, -TurtleCapsuleHalfHeight + Bob), FRotator(0, 0, bWalking ? Roll : 0.f));
	}
}

void ALTTurtleCharacter::SetCameraLag(bool bEnable)
{
	CameraArm->bEnableCameraLag = bEnable;
	CameraArm->bEnableCameraRotationLag = bEnable;
}

void ALTTurtleCharacter::SetMapPose(const FVector2D& MapPos, double YawRadians)
{
	const FVector Location = LT::MapToWorld(MapPos.X, MapPos.Y) + FVector(0, 0, TurtleCapsuleHalfHeight + 2.0);
	SetActorLocationAndRotation(Location, FRotator(0, LT::MapYawToDegrees(YawRadians), 0), false, nullptr, ETeleportType::TeleportPhysics);
	GetCharacterMovement()->StopMovementImmediately();
	if (Controller) Controller->SetControlRotation(GetActorRotation());
	SetCameraLag(false);
	SnapFrames = 2;
}

FVector2D ALTTurtleCharacter::GetMapPos() const
{
	return LT::WorldToMap(GetActorLocation());
}

double ALTTurtleCharacter::GetMapYaw() const
{
	return FMath::DegreesToRadians(GetActorRotation().Yaw);
}

void ALTTurtleCharacter::AddMapYaw(double Radians)
{
	AddActorWorldRotation(FRotator(0, FMath::RadiansToDegrees(Radians), 0));
}

void ALTTurtleCharacter::MoveForward(float Value)
{
	if (Value != 0.f) AddMovementInput(GetActorForwardVector(), Value);
}

void ALTTurtleCharacter::Turn(float Value)
{
	if (Value == 0.f || !GetWorld()) return;
	AddMapYaw(Value * TurnSpeed * GetWorld()->GetDeltaSeconds());
}

void ALTTurtleCharacter::Strafe(float Value)
{
	if (Value != 0.f) AddMovementInput(GetActorRightVector(), Value);
}

void ALTTurtleCharacter::SetRunning(bool bRun)
{
	bRunning = bRun;
	GetCharacterMovement()->MaxWalkSpeed = (bRunning ? RunSpeed : WalkSpeed) * TurtleCell;
}

bool ALTTurtleCharacter::TryJump()
{
	if (!CanJump()) return false;
	Jump();
	return true;
}

void ALTTurtleCharacter::MouseLook(float DX, float DY)
{
	AddMapYaw(DX * 0.006);
	// mouse Y is positive upwards; dragging down looks down more (the original + dy * 0.004)
	CamPitch = FMath::Clamp(CamPitch - DY * 0.004f, 0.05f, 1.0f);
}

void ALTTurtleCharacter::Zoom(float Notches)
{
	CamWant = FMath::Clamp(CamWant - Notches * 0.25f, 1.2f, 4.0f);
}

float ALTTurtleCharacter::GetCameraYawRadians() const
{
	return FMath::DegreesToRadians(Camera->GetComponentRotation().Yaw);
}

float ALTTurtleCharacter::GetSpeed() const
{
	return GetVelocity().Size2D();
}

bool ALTTurtleCharacter::IsInAir() const
{
	return GetCharacterMovement() && GetCharacterMovement()->IsFalling();
}
