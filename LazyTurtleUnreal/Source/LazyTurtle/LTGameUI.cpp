#include "LTGameUI.h"

#include "Brushes/SlateRoundedBoxBrush.h"
#include "Fonts/SlateFontInfo.h"
#include "Input/Events.h"
#include "InputCoreTypes.h"
#include "Layout/Geometry.h"
#include "LTGameMode.h"
#include "LTSchool.h"
#include "LTServices.h"
#include "Math/RandomStream.h"
#include "Rendering/DrawElements.h"
#include "Styling/CoreStyle.h"
#include "Widgets/Images/SImage.h"
#include "Widgets/Layout/SBorder.h"
#include "Widgets/Layout/SBox.h"
#include "Widgets/Layout/SScaleBox.h"
#include "Widgets/SBoxPanel.h"
#include "Widgets/SCanvas.h"
#include "Widgets/SLeafWidget.h"
#include "Widgets/SOverlay.h"
#include "Widgets/Text/STextBlock.h"

namespace
{
	FLinearColor UIRgb(int32 R, int32 G, int32 B, int32 A = 255) { return FLinearColor(FColor((uint8)R, (uint8)G, (uint8)B, (uint8)A)); }
	FLinearColor UIAlpha(FLinearColor C, float A) { C.A = A; return C; }
	FText UIText(const FString& S) { return FText::FromString(S); }

	FLinearColor UIYellow() { return UIRgb(255, 235, 80); }
	FLinearColor UIGold() { return UIRgb(220, 175, 70); }
	FLinearColor UIDark() { return UIRgb(60, 45, 35); }

	/** One colour per gift (teddy bear, beach ball, book, crayons, toy car, kite, balloon, cupcake, medal, robot). */
	FLinearColor UIGiftColor(int32 Index)
	{
		static const FColor Colors[10] = {
			FColor(190, 130, 70), FColor(240, 80, 80), FColor(70, 130, 230), FColor(250, 170, 40), FColor(230, 60, 60),
			FColor(80, 200, 220), FColor(240, 100, 190), FColor(250, 200, 140), FColor(255, 205, 40), FColor(150, 160, 175) };
		return FLinearColor(Colors[((Index % 10) + 10) % 10]);
	}

	ETextJustify::Type UIJustify(EHorizontalAlignment Align)
	{
		return Align == HAlign_Left ? ETextJustify::Left : Align == HAlign_Right ? ETextJustify::Right : ETextJustify::Center;
	}

	// ------------------------------------------------------------------ hand-drawn shapes

	enum class ELTUIShape : uint8 { Star, Arrow, Triangle, GiftBox, Orb, Rays };

	void UIPaintLine(FSlateWindowElementList& Out, int32 Layer, const FGeometry& G, const FVector2f& A, const FVector2f& B, const FLinearColor& Color, float Thickness)
	{
		TArray<FVector2f> Points;
		Points.Add(A);
		Points.Add(B);
		FSlateDrawElement::MakeLines(Out, Layer, G.ToPaintGeometry(), Points, ESlateDrawEffect::None, Color, true, Thickness);
	}

	/** Filled rectangle drawn as one thick line. */
	void UIPaintRect(FSlateWindowElementList& Out, int32 Layer, const FGeometry& G, float X0, float Y0, float X1, float Y1, const FLinearColor& Color)
	{
		const float Mid = (Y0 + Y1) * 0.5f;
		UIPaintLine(Out, Layer, G, FVector2f(X0, Mid), FVector2f(X1, Mid), Color, Y1 - Y0);
	}

	/** A polygon that is star-shaped around Center, filled with radial strokes, then outlined. */
	void UIPaintPolygon(FSlateWindowElementList& Out, int32 Layer, const FGeometry& G, const FVector2f& Center, const TArray<FVector2f>& Pts,
		const FLinearColor& Fill, const FLinearColor& Line, float LineWidth)
	{
		if (Pts.Num() < 3) return;
		float MaxR = 1.f;
		for (const FVector2f& P : Pts) MaxR = FMath::Max(MaxR, (P - Center).Size());
		const float Thick = FMath::Max(1.5f, MaxR * 0.06f);
		for (int32 i = 0; i < Pts.Num(); i++)
		{
			const FVector2f A = Pts[i], B = Pts[(i + 1) % Pts.Num()];
			const int32 Steps = FMath::Max(2, FMath::CeilToInt((B - A).Size() / (Thick * 0.6f)));
			for (int32 s = 0; s < Steps; s++)
			{
				const FVector2f P = A + (B - A) * ((float)s / Steps);
				UIPaintLine(Out, Layer, G, Center, Center + (P - Center) * 0.96f, Fill, Thick);
			}
		}
		if (LineWidth > 0.f && Line.A > 0.f)
		{
			TArray<FVector2f> Outline = Pts;
			Outline.Add(Pts[0]);
			FSlateDrawElement::MakeLines(Out, Layer + 1, G.ToPaintGeometry(), Outline, ESlateDrawEffect::None, Line, true, LineWidth);
		}
	}

	FVector2f UIRotate(const FVector2f& P, float Angle)
	{
		const float C = FMath::Cos(Angle), S = FMath::Sin(Angle);
		return FVector2f(P.X * C - P.Y * S, P.X * S + P.Y * C);
	}

	/** Stars, the compass arrow, menu arrows, gift boxes, the wisdom orb and the gift reveal rays. */
	class SLTShape : public SLeafWidget
	{
	public:
		SLATE_BEGIN_ARGS(SLTShape)
			: _Kind(ELTUIShape::Star), _Fill(FLinearColor::White), _Line(FLinearColor::Black), _Angle(0.f), _Amount(1.f)
			{}
			SLATE_ARGUMENT(ELTUIShape, Kind)
			SLATE_ATTRIBUTE(FLinearColor, Fill)
			SLATE_ATTRIBUTE(FLinearColor, Line)
			/** Rotation in radians (clockwise on screen). */
			SLATE_ATTRIBUTE(float, Angle)
			/** Orb: fill level 0-1. */
			SLATE_ATTRIBUTE(float, Amount)
		SLATE_END_ARGS()

		void Construct(const FArguments& InArgs)
		{
			Kind = InArgs._Kind;
			Fill = InArgs._Fill;
			Line = InArgs._Line;
			Angle = InArgs._Angle;
			Amount = InArgs._Amount;
		}

		virtual FVector2D ComputeDesiredSize(float) const override { return FVector2D(32, 32); }

		virtual int32 OnPaint(const FPaintArgs& Args, const FGeometry& G, const FSlateRect& Culling, FSlateWindowElementList& Out,
			int32 LayerId, const FWidgetStyle& Style, bool bParentEnabled) const override
		{
			const FVector2f Size = FVector2f(G.GetLocalSize());
			const FVector2f C = Size * 0.5f;
			const float R = FMath::Min(Size.X, Size.Y) * 0.5f;
			const FLinearColor Tint = Style.GetColorAndOpacityTint();
			const FLinearColor F = Fill.Get() * Tint, L = Line.Get() * Tint;
			const float A = Angle.Get();
			TArray<FVector2f> Pts;

			switch (Kind)
			{
			case ELTUIShape::Star:
				for (int32 k = 0; k < 10; k++)
				{
					const float Ang = -UE_HALF_PI + k * UE_PI / 5.f + A;
					const float Rad = (k % 2 == 0 ? R : R * 0.45f) * 0.95f;
					Pts.Add(C + FVector2f(FMath::Cos(Ang), FMath::Sin(Ang)) * Rad);
				}
				UIPaintPolygon(Out, LayerId, G, C, Pts, F, L, FMath::Max(1.5f, R * 0.1f));
				return LayerId + 2;

			case ELTUIShape::Arrow:
				for (const FVector2f& P : { FVector2f(0, -0.8f), FVector2f(0.45f, 0.35f), FVector2f(0, 0.12f), FVector2f(-0.45f, 0.35f) })
					Pts.Add(C + UIRotate(P * R, A));
				UIPaintPolygon(Out, LayerId, G, C, Pts, F, L, FMath::Max(1.5f, R * 0.07f));
				return LayerId + 2;

			case ELTUIShape::Triangle:
				for (const FVector2f& P : { FVector2f(-1, -1), FVector2f(1, 0), FVector2f(-1, 1) })
					Pts.Add(C + UIRotate(P * R * 0.9f, A));
				UIPaintPolygon(Out, LayerId, G, C, Pts, F, L, 0.f);
				return LayerId + 2;

			case ELTUIShape::GiftBox:
			{
				const float W = Size.X, H = Size.Y;
				UIPaintRect(Out, LayerId, G, W * 0.12f, H * 0.40f, W * 0.88f, H * 0.95f, F);                       // box
				UIPaintRect(Out, LayerId, G, W * 0.06f, H * 0.26f, W * 0.94f, H * 0.42f, F * 1.15f);               // lid
				UIPaintRect(Out, LayerId + 1, G, W * 0.44f, H * 0.26f, W * 0.56f, H * 0.95f, L);                  // ribbon
				UIPaintLine(Out, LayerId + 1, G, FVector2f(W * 0.5f, H * 0.26f), FVector2f(W * 0.28f, H * 0.08f), L, W * 0.09f);   // bow
				UIPaintLine(Out, LayerId + 1, G, FVector2f(W * 0.5f, H * 0.26f), FVector2f(W * 0.72f, H * 0.08f), L, W * 0.09f);
				return LayerId + 2;
			}

			case ELTUIShape::Orb:
			{
				// a glass ball (Diablo style) filled from the bottom
				const float Level = C.Y + R - FMath::Clamp(Amount.Get(), 0.f, 1.f) * 2.f * R;
				const float Step = FMath::Max(1.f, R / 40.f);
				for (float Y = C.Y - R + Step * 0.5f; Y < C.Y + R; Y += Step)
				{
					const float Half = FMath::Sqrt(FMath::Max(0.f, R * R - (Y - C.Y) * (Y - C.Y)));
					UIPaintLine(Out, LayerId, G, FVector2f(C.X - Half, Y), FVector2f(C.X + Half, Y), Y >= Level ? F : UIRgb(20, 15, 20, 220) * Tint, Step + 0.6f);
				}
				TArray<FVector2f> Ring;
				for (int32 k = 0; k <= 48; k++)
					Ring.Add(C + FVector2f(FMath::Cos(k * UE_TWO_PI / 48), FMath::Sin(k * UE_TWO_PI / 48)) * R);
				FSlateDrawElement::MakeLines(Out, LayerId + 1, G.ToPaintGeometry(), Ring, ESlateDrawEffect::None, L, true, FMath::Max(2.f, R * 0.07f));
				return LayerId + 2;
			}

			case ELTUIShape::Rays:
				for (int32 k = 0; k < 12; k++)
				{
					const float Ang = A + k * UE_TWO_PI / 12.f;
					TArray<FVector2f> Ray;
					Ray.Add(C + UIRotate(FVector2f(0.f, -R * 0.15f), Ang));
					Ray.Add(C + UIRotate(FVector2f(-R * 0.15f, -R), Ang));
					Ray.Add(C + UIRotate(FVector2f(R * 0.15f, -R), Ang));
					const FLinearColor Hue = FLinearColor(FMath::Fmod(k / 12.f * 360.f + A * 30.f, 360.f), 0.8f, 1.f).HSVToLinearRGB();
					UIPaintPolygon(Out, LayerId, G, C, Ray, UIAlpha(Hue, 0.45f * Tint.A), FLinearColor::Transparent, 0.f);
				}
				return LayerId + 2;
			}
			return LayerId;
		}

	private:
		ELTUIShape Kind = ELTUIShape::Star;
		TAttribute<FLinearColor> Fill, Line;
		TAttribute<float> Angle, Amount;
	};

	/** Falling confetti behind the diploma. */
	class SLTConfetti : public SLeafWidget
	{
	public:
		SLATE_BEGIN_ARGS(SLTConfetti) : _Time(0.f), _Unit(1.f) {}
			SLATE_ATTRIBUTE(float, Time)
			SLATE_ARGUMENT(float, Unit)
		SLATE_END_ARGS()

		void Construct(const FArguments& InArgs)
		{
			Time = InArgs._Time;
			Unit = InArgs._Unit;
		}

		virtual FVector2D ComputeDesiredSize(float) const override { return FVector2D(100, 100); }

		virtual int32 OnPaint(const FPaintArgs& Args, const FGeometry& G, const FSlateRect& Culling, FSlateWindowElementList& Out,
			int32 LayerId, const FWidgetStyle& Style, bool bParentEnabled) const override
		{
			const FVector2f Size = FVector2f(G.GetLocalSize());
			const float T = Time.Get();
			FRandomStream Rnd(7);
			for (int32 i = 0; i < 90; i++)
			{
				const float Speed = 60.f + Rnd.FRand() * 120.f;
				const float X = Rnd.FRand() * Size.X + FMath::Sin(T * 2.f + i) * 20.f * Unit;
				const float Y = FMath::Fmod(Rnd.FRand() * Size.Y + T * Speed * Unit, Size.Y + 20.f * Unit) - 10.f * Unit;
				const FVector2f Dir = UIRotate(FVector2f(6.f * Unit, 0.f), FMath::DegreesToRadians(T * 180.f + i * 37.f));
				const FLinearColor Color = FLinearColor(FMath::Fmod(i * 0.13f, 1.f) * 360.f, 0.85f, 1.f).HSVToLinearRGB();
				UIPaintLine(Out, LayerId, G, FVector2f(X, Y) - Dir, FVector2f(X, Y) + Dir, Color, 6.f * Unit);
			}
			return LayerId + 1;
		}

	private:
		TAttribute<float> Time;
		float Unit = 1.f;
	};
}

// ------------------------------------------------------------------ widget

void SLTGameUI::Construct(const FArguments& InArgs)
{
	GameMode = InArgs._GameMode;
	SetCanTick(true);
	ChildSlot
	[
		SAssignNew(Root, SBox)
	];
}

FString SLTGameUI::MakeSignature(const ALTGameMode& GM) const
{
	// everything that changes the layout; animated parts read the game mode through attributes instead
	FString S = FString::Printf(TEXT("%d|%d|%d|%d|%d|%d|%d|%d|%d"), (int32)GM.GetScreen(), GM.GetMenu(), GM.IsPolish() ? 1 : 0,
		GM.GetFloorIndex(), GM.GetGiftMask(), GM.GetMistakes(), GM.GetLastStars(), GM.GetNearCaption(), GM.IsSpeechAvailable() ? 1 : 0);
	if (GM.CanTalk()) S += TEXT("|talk:") + GM.GetTalkLabel();
	const FLTDialogState& D = GM.GetDialog();
	S += FString::Printf(TEXT("|d%d,%d,%d,%d"), D.Serial, D.Selected, D.Chosen, (int32)D.Phase);
	const ELTScreen Screen = GM.GetScreen();
	if (Screen == ELTScreen::Title) S += FString::Join(GM.GetTitleItems(), TEXT(","));
	if (Screen == ELTScreen::Paused) S += FString::Join(GM.GetPauseItems(), TEXT(","));
	for (const FLTPopup& P : GM.GetPopups()) S += FString::Printf(TEXT("|p%d"), P.Id);
	if (const ULTSaveGame* Save = GM.GetSave())
	{
		S += FString::Printf(TEXT("|u%d"), Save->Unlocked);
		if (Screen == ELTScreen::Floors)
			for (int32 i = 0; i < Save->Classes.Num(); i++)
				S += FString::Printf(TEXT(",%d:%d"), Save->Classes[i], Save->Stars.IsValidIndex(i) ? Save->Stars[i] : 0);
	}
	return S;
}

void SLTGameUI::Tick(const FGeometry& AllottedGeometry, const double InCurrentTime, const float InDeltaTime)
{
	SCompoundWidget::Tick(AllottedGeometry, InCurrentTime, InDeltaTime);
	const ALTGameMode* GM = GameMode.Get();
	if (!GM || !Root.IsValid()) return;
	const FVector2D Size = AllottedGeometry.GetLocalSize();
	if (Size.X < 16 || Size.Y < 16) return;

	const FString Sig = MakeSignature(*GM) + FString::Printf(TEXT("|%dx%d"), FMath::RoundToInt(Size.X), FMath::RoundToInt(Size.Y));
	if (Sig == Signature) return;
	Signature = Sig;
	View = Size;
	U = (float)FMath::Min(View.Y / 720.0, View.X / 1100.0);
	Rebuild();
}

void SLTGameUI::Rebuild()
{
	ALTGameMode* GM = GameMode.Get();
	if (!GM) return;
	TSharedRef<SCanvas> C = SNew(SCanvas).Visibility(EVisibility::SelfHitTestInvisible);
	int32 CatchAll = -1;   // a click anywhere does this

	switch (GM->GetScreen())
	{
	case ELTScreen::Title: BuildTitle(C); break;
	case ELTScreen::Floors: BuildFloors(C); break;
	case ELTScreen::Help: BuildHelp(C); CatchAll = LTClick::HelpPanel; break;
	case ELTScreen::Intro: BuildPlayHud(C); BuildIntro(C); CatchAll = LTClick::Continue; break;
	case ELTScreen::Playing: BuildPlayHud(C); break;
	case ELTScreen::Dialog:
		BuildDialog(C);
		if (GM->GetDialog().Phase != ELTDialogPhase::Ask || !GM->GetDialog().bHasQuestion) CatchAll = LTClick::Continue;
		break;
	case ELTScreen::Paused: BuildPlayHud(C); BuildPaused(C); break;
	case ELTScreen::FloorDone: BuildFloorDone(C); CatchAll = LTClick::Continue; break;
	case ELTScreen::Victory: BuildVictory(C); CatchAll = LTClick::Continue; break;
	}

	if (CatchAll >= 0) Root->SetContent(Clickable(CatchAll, C, false));
	else Root->SetContent(C);
}

// ------------------------------------------------------------------ building blocks

const FSlateBrush* SLTGameUI::Rounded(const FLinearColor& Fill, float Radius, const FLinearColor& Outline, float OutlineWidth)
{
	const FString Key = FString::Printf(TEXT("%s|%.1f|%s|%.1f"), *Fill.ToString(), Radius, *Outline.ToString(), OutlineWidth);
	if (const TSharedPtr<FSlateBrush>* Found = Brushes.Find(Key)) return Found->Get();
	TSharedPtr<FSlateBrush> Brush = MakeShared<FSlateRoundedBoxBrush>(Fill, Radius, Outline, OutlineWidth);
	Brushes.Add(Key, Brush);
	return Brush.Get();
}

FSlateFontInfo SLTGameUI::Font(float Px, bool bOutline) const
{
	// Px is the original GDI+ pixel size at u = 1; Slate font sizes are points (4/3 px)
	FSlateFontInfo F = FCoreStyle::GetDefaultFontStyle("Bold", FMath::Max(6, FMath::RoundToInt(Px * U * 0.75f)));
	if (bOutline)
	{
		F.OutlineSettings.OutlineSize = FMath::Max(1, FMath::RoundToInt(Px * U * 0.05f));
		F.OutlineSettings.OutlineColor = UIRgb(40, 28, 20);
	}
	return F;
}

void SLTGameUI::Place(const TSharedRef<SCanvas>& C, const TAttribute<FVector2D>& Pos, const TAttribute<FVector2D>& Size,
	const TSharedRef<SWidget>& Widget, bool bKeepVisibility) const
{
	if (!bKeepVisibility) Widget->SetVisibility(EVisibility::HitTestInvisible);
	C->AddSlot()
		.Position(Pos)
		.Size(Size)
	[
		Widget
	];
}

void SLTGameUI::Text(const TSharedRef<SCanvas>& C, const TAttribute<FText>& Str, float Px, float X, float Y, const TAttribute<FSlateColor>& Color,
	EHorizontalAlignment Align, bool bOutline)
{
	const float W = (float)View.X;
	const float BoxX = Align == HAlign_Center ? X - W * 0.5f : Align == HAlign_Right ? X - W : X;
	Place(C, FVector2D(BoxX, Y), FVector2D(W, Px * U * 1.6f),
		SNew(SBox)
		.HAlign(Align)
		.VAlign(VAlign_Top)
		[
			SNew(STextBlock)
			.Text(Str)
			.Font(Font(Px, bOutline))
			.ColorAndOpacity(Color)
			.Justification(UIJustify(Align))
		]);
}

TSharedRef<SWidget> SLTGameUI::TextBox(const TAttribute<FText>& Str, float Px, float BoxWidth, const TAttribute<FSlateColor>& Color,
	bool bOutline, EHorizontalAlignment Align) const
{
	return SNew(SScaleBox)
		.Stretch(EStretch::ScaleToFit)
		.StretchDirection(EStretchDirection::DownOnly)
		.HAlign(Align)
		.VAlign(VAlign_Center)
		[
			SNew(STextBlock)
			.Text(Str)
			.Font(Font(Px, bOutline))
			.ColorAndOpacity(Color)
			.WrapTextAt(FMath::Max(10.f, BoxWidth))
			.Justification(UIJustify(Align))
		];
}

TSharedRef<SWidget> SLTGameUI::Panel(const FLinearColor& Fill, const FLinearColor& Border, const TSharedRef<SWidget>& Content)
{
	return SNew(SBorder)
		.BorderImage(Rounded(Fill, 22.f * U, Border, 5.f * U))
		.Padding(0)
		.HAlign(HAlign_Fill)
		.VAlign(VAlign_Fill)
		[
			Content
		];
}

TSharedRef<SWidget> SLTGameUI::Clickable(int32 Action, const TSharedRef<SWidget>& Content, bool bHandCursor)
{
	const TWeakObjectPtr<ALTGameMode> WeakGM = GameMode;
	TSharedRef<SBorder> Border = SNew(SBorder)
		.BorderImage(&NoBrush)
		.Padding(0)
		.HAlign(HAlign_Fill)
		.VAlign(VAlign_Fill)
		.OnMouseButtonDown_Lambda([WeakGM, Action](const FGeometry&, const FPointerEvent& Event)
		{
			// only the left button: the right one drags the camera (it bubbles to the game viewport)
			if (Event.GetEffectingButton() != EKeys::LeftMouseButton || !WeakGM.IsValid()) return FReply::Unhandled();
			WeakGM->HandleClick(Action);
			return FReply::Handled();
		})
		[
			Content
		];
	if (bHandCursor) Border->SetCursor(EMouseCursor::Hand);
	return Border;
}

TSharedRef<SWidget> SLTGameUI::Dim(int32 Alpha)
{
	return SNew(SImage).Image(&WhiteBrush).ColorAndOpacity(UIRgb(10, 15, 25, Alpha));
}

TSharedRef<SWidget> SLTGameUI::CoinIcon(float Size)
{
	return SNew(SBox)
		.WidthOverride(Size)
		.HeightOverride(Size)
		[
			SNew(SBorder)
			.BorderImage(Rounded(UIRgb(255, 205, 40), Size * 0.5f, UIRgb(190, 120, 0), Size * 0.1f))
			.Padding(Size * 0.25f)
			[
				SNew(SBorder).BorderImage(Rounded(UIRgb(255, 235, 120), Size * 0.25f))
			]
		];
}

TSharedRef<SWidget> SLTGameUI::TurtleIcon(float S)
{
	// legs, head, shell and the yellow backpack, seen from above-ish: good enough for a logo
	TSharedRef<SCanvas> C = SNew(SCanvas);
	const FLinearColor Skin = UIRgb(130, 200, 80), Shell = UIRgb(40, 120, 30), ShellLight = UIRgb(90, 170, 60);
	auto Blob = [&](float X, float Y, float W, float H, const FLinearColor& Fill, const FLinearColor& Border)
	{
		C->AddSlot().Position(FVector2D(X * S, Y * S)).Size(FVector2D(W * S, H * S))
		[
			SNew(SBorder).BorderImage(Rounded(Fill, FMath::Min(W, H) * S * 0.5f, Border, S * 0.025f))
		];
	};
	const FLinearColor Outline = UIRgb(30, 50, 20);
	Blob(0.10f, 0.62f, 0.20f, 0.22f, Skin, Outline);     // legs
	Blob(0.62f, 0.62f, 0.20f, 0.22f, Skin, Outline);
	Blob(0.12f, 0.18f, 0.18f, 0.20f, Skin, Outline);
	Blob(0.60f, 0.18f, 0.18f, 0.20f, Skin, Outline);
	Blob(0.70f, 0.30f, 0.30f, 0.30f, Skin, Outline);     // head
	Blob(0.84f, 0.38f, 0.06f, 0.06f, UIRgb(20, 20, 20), FLinearColor::Transparent);
	Blob(0.08f, 0.16f, 0.72f, 0.66f, Shell, Outline);    // shell
	Blob(0.22f, 0.28f, 0.44f, 0.42f, ShellLight, FLinearColor::Transparent);
	Blob(0.26f, 0.36f, 0.30f, 0.26f, UIRgb(255, 200, 30), UIRgb(160, 110, 0));   // backpack
	return SNew(SBox).WidthOverride(S).HeightOverride(S)[C];
}

void SLTGameUI::Gift(const TSharedRef<SCanvas>& C, int32 Index, bool bHave, float X, float Y, float Size)
{
	Place(C, FVector2D(X, Y), FVector2D(Size, Size),
		SNew(SLTShape)
		.Kind(ELTUIShape::GiftBox)
		.Fill(bHave ? UIGiftColor(Index) : UIRgb(120, 120, 130, 110))
		.Line(bHave ? UIRgb(255, 225, 70) : UIRgb(200, 200, 200, 110)));
}

void SLTGameUI::MenuList(const TSharedRef<SCanvas>& C, const TArray<FString>& Items, float Top)
{
	const ALTGameMode* GM = GameMode.Get();
	const TWeakObjectPtr<ALTGameMode> WeakGM = GameMode;
	const float W = (float)View.X, Step = 56.f * U, Unit = U;
	for (int32 i = 0; i < Items.Num(); i++)
	{
		const bool bSel = i == GM->GetMenu();
		const float Y = Top + i * Step;
		// Roboto Bold is about 0.55 em per character
		const float Half = FMath::Max(Items[i].Len() * 38.f * U * 0.55f * 0.5f + 40.f * U, 160.f * U);
		const FVector2D Pos(W * 0.5f - Half, Y - 4.f * U), Size(Half * 2.f, 50.f * U);
		Place(C, Pos, Size, Clickable(LTClick::Menu0 + i,
			SNew(SBorder)
			.BorderImage(bSel ? Rounded(UIRgb(40, 120, 60, 170), 24.f * U) : static_cast<const FSlateBrush*>(&NoBrush))
			.Padding(0)
			.HAlign(HAlign_Center)
			.VAlign(VAlign_Center)
			[
				SNew(STextBlock)
				.Text(UIText(Items[i]))
				.Font(Font(bSel ? 38.f : 33.f))
				.ColorAndOpacity(bSel ? UIYellow() : FLinearColor::White)
			]), true);
		if (!bSel) continue;
		for (int32 Side = 0; Side < 2; Side++)
		{
			const float Base = Side == 0 ? Pos.X + 18.f * U : Pos.X + Size.X - 18.f * U;
			const float Dir = Side == 0 ? 1.f : -1.f;
			Place(C, TAttribute<FVector2D>::CreateLambda([WeakGM, Base, Dir, Y, Unit]()
				{
					const float Bounce = WeakGM.IsValid() ? FMath::Sin(WeakGM->GetTime() * 6.f) * 6.f * Unit : 0.f;
					return FVector2D(Base + Dir * Bounce - 11.f * Unit, Y + 10.f * Unit);
				}), FVector2D(22.f * U, 22.f * U),
				SNew(SLTShape).Kind(ELTUIShape::Triangle).Fill(UIYellow()).Angle(Side == 0 ? 0.f : UE_PI));
		}
	}
}

// ------------------------------------------------------------------ menus

void SLTGameUI::BuildTitle(const TSharedRef<SCanvas>& C)
{
	const ALTGameMode* GM = GameMode.Get();
	const TWeakObjectPtr<ALTGameMode> WeakGM = GameMode;
	const float W = (float)View.X, H = (float)View.Y, Unit = U;
	auto Bob = [WeakGM, Unit]() { return WeakGM.IsValid() ? FMath::Sin(WeakGM->GetTime() * 2.f) * 6.f * Unit : 0.f; };

	Place(C, FVector2D(0, 0), View, Dim(110));
	Place(C, TAttribute<FVector2D>::CreateLambda([W, Unit, Bob]() { return FVector2D(W * 0.5f - 340.f * Unit, 18.f * Unit + Bob()); }),
		FVector2D(115.f * U, 115.f * U), TurtleIcon(115.f * U));

	FSlateFontInfo TitleFont = Font(92.f);
	TitleFont.OutlineSettings.OutlineSize = FMath::Max(2, FMath::RoundToInt(6.f * U));
	TitleFont.OutlineSettings.OutlineColor = UIRgb(40, 70, 20);
	Place(C, TAttribute<FVector2D>::CreateLambda([W, Unit, Bob]() { return FVector2D(W * 0.5f + 40.f * Unit - W * 0.5f, 6.f * Unit + Bob()); }),
		FVector2D(W, 120.f * U),
		SNew(SBox).HAlign(HAlign_Center).VAlign(VAlign_Top)
		[
			SNew(STextBlock).Text(UIText(GM->T(TEXT("title")))).Font(TitleFont).ColorAndOpacity(UIRgb(215, 245, 110))
		]);
	Text(C, UIText(GM->T(TEXT("subtitle"))), 34.f, W * 0.5f, 122.f * U, UIRgb(255, 255, 220));
	Text(C, UIText(GM->T(TEXT("school_name"))), 22.f, W * 0.5f, 172.f * U, UIRgb(200, 235, 255));
	Text(C, UIText(GM->T(TEXT("school_patron"))), 18.f, W * 0.5f, 202.f * U, UIRgb(200, 235, 255));
	MenuList(C, GM->GetTitleItems(), 252.f * U);

	Place(C, FVector2D(16.f * U, H - 56.f * U), FVector2D(40.f * U, 40.f * U), CoinIcon(40.f * U));
	Text(C, TAttribute<FText>::CreateLambda([WeakGM]() { return WeakGM.IsValid() ? UIText(FString::Printf(TEXT("x %d"), WeakGM->GetCoins())) : FText(); }),
		26.f, 64.f * U, H - 54.f * U, UIRgb(255, 230, 90), HAlign_Left);
	Text(C, UIText(TEXT("UE 5")), 18.f, W - 20.f * U, H - 40.f * U, UIRgb(255, 255, 255, 220), HAlign_Right);
}

void SLTGameUI::BuildHelp(const TSharedRef<SCanvas>& C)
{
	const ALTGameMode* GM = GameMode.Get();
	const float W = (float)View.X;
	Place(C, FVector2D(0, 0), View, Dim(120));
	const FVector2D PanelPos(W * 0.5f - 540.f * U, 30.f * U), PanelSize(1080.f * U, 660.f * U);
	Place(C, PanelPos, PanelSize, Panel(UIRgb(25, 60, 35, 220), UIRgb(255, 215, 90, 230)));
	Text(C, UIText(GM->T(TEXT("help"))), 52.f, W * 0.5f, 44.f * U, UIYellow());
	const float InnerW = PanelSize.X - 60.f * U;
	Place(C, FVector2D(PanelPos.X + 30.f * U, 116.f * U), FVector2D(InnerW, 60.f * U), TextBox(UIText(GM->T(TEXT("help_goal"))), 24.f, InnerW, FLinearColor::White));
	float Y = 190.f * U;
	for (int32 i = 1; i <= 8; i++)
	{
		Text(C, UIText(GM->T(FString::Printf(TEXT("help%d"), i))), 25.f, W * 0.5f - 20.f * U, Y, UIYellow(), HAlign_Right);
		Text(C, UIText(GM->T(FString::Printf(TEXT("help%db"), i))), 25.f, W * 0.5f + 10.f * U, Y, FLinearColor::White, HAlign_Left);
		Y += 44.f * U;
	}
	Place(C, FVector2D(PanelPos.X + 30.f * U, Y + 4.f * U), FVector2D(InnerW, 56.f * U), TextBox(UIText(GM->T(TEXT("help_tip"))), 21.f, InnerW, UIRgb(200, 255, 200)));
	Text(C, UIText(GM->T(TEXT("back"))), 21.f, W * 0.5f, PanelPos.Y + PanelSize.Y - 44.f * U, UIRgb(220, 220, 220));
}

void SLTGameUI::BuildFloors(const TSharedRef<SCanvas>& C)
{
	const ALTGameMode* GM = GameMode.Get();
	const TWeakObjectPtr<ALTGameMode> WeakGM = GameMode;
	const ULTSaveGame* Save = GM->GetSave();
	const bool bPl = GM->IsPolish();
	const float W = (float)View.X, Unit = U;
	Place(C, FVector2D(0, 0), View, Dim(120));
	Text(C, UIText(GM->T(TEXT("floors"))), 60.f, W * 0.5f, 40.f * U, UIYellow());

	const int32 N = LT::Floors, PerRow = (N + 1) / 2;
	const float CW = 230.f * U, CH = 240.f * U, Gap = 20.f * U;
	const float Total = PerRow * CW + (PerRow - 1) * Gap;
	const float X0 = (W - Total) * 0.5f, Y0 = 130.f * U;
	const TArray<FLTFloorTheme>& Themes = FLTSchool::Themes();
	for (int32 i = 0; i < N; i++)
	{
		const bool bSel = i == GM->GetMenu();
		const bool bLocked = !Save || i >= Save->Unlocked;
		const FLTFloorTheme* Th = Themes.IsValidIndex(i) ? &Themes[i] : nullptr;
		int32 Done = 0;
		const int32 Mask = Save && Save->Classes.IsValidIndex(i) ? Save->Classes[i] : 0;
		for (int32 b = 0; b < LT::Classes; b++) if (Mask & (1 << b)) Done++;
		const int32 Stars = Save && Save->Stars.IsValidIndex(i) ? Save->Stars[i] : 0;
		const float TX = X0 + (i % PerRow) * (CW + Gap), TY = Y0 + (i / PerRow) * (CH + Gap);

		TSharedRef<SHorizontalBox> StarRow = SNew(SHorizontalBox);
		for (int32 s = 0; s < 3; s++)
			StarRow->AddSlot().AutoWidth().Padding(5.f * U, 0)
			[
				SNew(SBox).WidthOverride(36.f * U).HeightOverride(36.f * U)
				[
					SNew(SLTShape).Kind(ELTUIShape::Star)
					.Fill(s < Stars ? UIRgb(255, 220, 40) : UIRgb(60, 60, 70, 140))
					.Line(s < Stars ? UIRgb(200, 120, 0) : UIRgb(30, 30, 40, 180))
				]
			];

		TSharedRef<SOverlay> Tile = SNew(SOverlay)
			+ SOverlay::Slot()
			[
				SNew(SBorder)
				.BorderImage(Rounded(Th ? Th->Paint : UIRgb(80, 120, 90), 20.f * U, bSel ? UIYellow() : (Th ? Th->Accent : FLinearColor::White), (bSel ? 7.f : 4.f) * U))
				.Padding(FMargin(10.f * U, 4.f * U))
				[
					SNew(SVerticalBox)
					+ SVerticalBox::Slot().AutoHeight().HAlign(HAlign_Center)
					[
						SNew(STextBlock).Text(FText::AsNumber(i + 1)).Font(Font(56.f)).ColorAndOpacity(FLinearColor::White)
					]
					+ SVerticalBox::Slot().FillHeight(1.f).VAlign(VAlign_Center)
					[
						TextBox(UIText(Th ? Th->Name(bPl) : FString()), 20.f, CW - 20.f * U, FLinearColor::White)
					]
					+ SVerticalBox::Slot().AutoHeight().HAlign(HAlign_Center).Padding(0, 4.f * U)
					[
						SNew(SHorizontalBox)
						+ SHorizontalBox::Slot().AutoWidth().VAlign(VAlign_Center)
						[
							SNew(SBox).WidthOverride(36.f * U).HeightOverride(36.f * U)
							[
								SNew(SLTShape).Kind(ELTUIShape::GiftBox).Fill(UIRgb(240, 80, 80)).Line(UIRgb(255, 225, 70))
							]
						]
						+ SHorizontalBox::Slot().AutoWidth().VAlign(VAlign_Center).Padding(8.f * U, 0, 0, 0)
						[
							SNew(STextBlock).Text(UIText(FString::Printf(TEXT("%d/10"), Done))).Font(Font(24.f)).ColorAndOpacity(FLinearColor::White)
						]
					]
					+ SVerticalBox::Slot().AutoHeight().HAlign(HAlign_Center).Padding(0, 2.f * U, 0, 8.f * U)
					[
						StarRow
					]
				]
			];
		if (bLocked)
			Tile->AddSlot()
			[
				SNew(SBorder)
				.BorderImage(Rounded(UIRgb(20, 20, 30, 170), 20.f * U))
				.HAlign(HAlign_Center)
				.VAlign(VAlign_Center)
				[
					SNew(STextBlock).Text(UIText(GM->T(TEXT("locked")))).Font(Font(28.f)).ColorAndOpacity(UIRgb(255, 200, 60))
				]
			];

		Place(C, TAttribute<FVector2D>::CreateLambda([WeakGM, bSel, TX, TY, Unit]()
			{
				const float Lift = bSel && WeakGM.IsValid() ? FMath::Sin(WeakGM->GetTime() * 5.f) * 5.f * Unit - 8.f * Unit : 0.f;
				return FVector2D(TX, TY + Lift);
			}), FVector2D(CW, CH), Clickable(LTClick::Floor0 + i, Tile), true);
	}
	const float Bottom = Y0 + 2.f * CH + Gap;
	Text(C, UIText(GM->T(TEXT("select_hint"))), 24.f, W * 0.5f, Bottom + 14.f * U, FLinearColor::White);
	Text(C, UIText(GM->T(TEXT("back"))), 20.f, W * 0.5f, Bottom + 52.f * U, UIRgb(220, 220, 220));
}

// ------------------------------------------------------------------ in-game HUD

void SLTGameUI::BuildPlayHud(const TSharedRef<SCanvas>& C)
{
	const ALTGameMode* GM = GameMode.Get();
	const TWeakObjectPtr<ALTGameMode> WeakGM = GameMode;
	const float W = (float)View.X, H = (float)View.Y, Unit = U;
	const FLTFloorTheme& Theme = GM->GetTheme();
	const bool bPl = GM->IsPolish();

	// floor badge with the coins
	const FVector2D BadgePos(14.f * U, 12.f * U), BadgeSize(330.f * U, 86.f * U);
	Place(C, BadgePos, BadgeSize, Panel(UIRgb(20, 30, 25, 170), UIAlpha(Theme.Accent, 0.8f)));
	Text(C, UIText(GM->T(TEXT("floor")) + FString::Printf(TEXT(" %d"), GM->GetFloorIndex() + 1)), 26.f, BadgePos.X + 16.f * U, BadgePos.Y + 6.f * U, UIYellow(), HAlign_Left);
	Place(C, FVector2D(BadgePos.X + 14.f * U, BadgePos.Y + 40.f * U), FVector2D(210.f * U, 40.f * U),
		TextBox(UIText(Theme.Name(bPl)), 17.f, 210.f * U, FLinearColor::White, true, HAlign_Left));
	const float BadgeRight = BadgePos.X + BadgeSize.X;
	Place(C, FVector2D(BadgeRight - 106.f * U, BadgePos.Y + 12.f * U), FVector2D(34.f * U, 34.f * U), CoinIcon(34.f * U));
	Text(C, TAttribute<FText>::CreateLambda([WeakGM]() { return WeakGM.IsValid() ? FText::AsNumber(WeakGM->GetCoins()) : FText(); }),
		24.f, BadgeRight - 66.f * U, BadgePos.Y + 12.f * U, UIRgb(255, 230, 90), HAlign_Left);

	// stars the floor would give now
	const int32 Stars = GM->GetProspectiveStars();
	for (int32 s = 0; s < 3; s++)
		Place(C, FVector2D(BadgePos.X + 10.f * U + s * 38.f * U, BadgePos.Y + BadgeSize.Y + 6.f * U), FVector2D(34.f * U, 34.f * U),
			SNew(SLTShape).Kind(ELTUIShape::Star)
			.Fill(s < Stars ? UIRgb(255, 220, 40) : UIRgb(60, 60, 70, 140))
			.Line(s < Stars ? UIRgb(200, 120, 0) : UIRgb(30, 30, 40, 180)));

	// gift bar
	{
		const int32 Mask = GM->GetGiftMask();
		const float S = 46.f * U, Gap = 6.f * U, Total = 10.f * S + 9.f * Gap;
		float X = (W - Total) * 0.5f;
		const float Y = 14.f * U;
		Place(C, FVector2D(X - 12.f * U, Y - 6.f * U), FVector2D(Total + 24.f * U, S + 12.f * U),
			SNew(SBorder).BorderImage(Rounded(UIRgb(20, 25, 30, 160), 16.f * U, UIAlpha(UIGold(), 0.8f), 3.f * U)));
		for (int32 i = 0; i < LT::Classes; i++, X += S + Gap) Gift(C, i, (Mask & (1 << i)) != 0, X, Y, S);
	}

	// compass: the nearest classroom still to visit, then the Master Turtle, then the stairs
	{
		const float R = 30.f * U, CX = W * 0.5f, CY = 128.f * U;
		auto Visible = [WeakGM]()
		{
			float Rel = 0;
			bool bMaster = false;
			return WeakGM.IsValid() && WeakGM->GetCompass(Rel, bMaster) ? EVisibility::HitTestInvisible : EVisibility::Collapsed;
		};
		TSharedRef<SWidget> Ring = SNew(SBorder).BorderImage(Rounded(UIRgb(20, 30, 40, 170), R, UIGold(), 4.f * U));
		Ring->SetVisibility(TAttribute<EVisibility>::CreateLambda(Visible));
		Place(C, FVector2D(CX - R, CY - R), FVector2D(2.f * R, 2.f * R), Ring, true);
		TSharedRef<SWidget> Arrow = SNew(SLTShape)
			.Kind(ELTUIShape::Arrow)
			.Angle(TAttribute<float>::CreateLambda([WeakGM]()
			{
				float Rel = 0;
				bool bMaster = false;
				if (WeakGM.IsValid()) WeakGM->GetCompass(Rel, bMaster);
				return Rel;
			}))
			.Fill(TAttribute<FLinearColor>::CreateLambda([WeakGM]()
			{
				float Rel = 0;
				bool bMaster = false;
				if (WeakGM.IsValid()) WeakGM->GetCompass(Rel, bMaster);
				return bMaster ? UIRgb(200, 120, 255) : UIRgb(255, 200, 40);
			}))
			.Line(FLinearColor::White);
		Arrow->SetVisibility(TAttribute<EVisibility>::CreateLambda(Visible));
		Place(C, FVector2D(CX - R, CY - R), FVector2D(2.f * R, 2.f * R), Arrow, true);
	}

	// wisdom orb (Diablo style): classrooms finished on this floor
	{
		const float R = 62.f * U, CX = 22.f * U + R, CY = H - 22.f * U - R;
		const int32 Done = GM->GetGiftsDone();
		Place(C, FVector2D(CX - R, CY - R), FVector2D(2.f * R, 2.f * R),
			SNew(SLTShape).Kind(ELTUIShape::Orb).Amount(Done / (float)LT::Classes).Fill(UIRgb(240, 180, 40)).Line(UIGold()));
		Text(C, UIText(FString::Printf(TEXT("%d/10"), Done)), 30.f, CX, CY - 24.f * U, FLinearColor::White);
		Text(C, UIText(GM->T(TEXT("wisdom"))), 16.f, CX, CY + 16.f * U, UIRgb(255, 245, 200));
	}

	if (GM->GetScreen() == ELTScreen::Playing)
	{
		if (GM->CanTalk())
		{
			const FString Label = GM->GetTalkLabel();
			const float Half = FMath::Max(170.f * U, Label.Len() * 32.f * U * 0.55f * 0.5f + 30.f * U);
			Place(C, TAttribute<FVector2D>::CreateLambda([WeakGM, W, H, Half, Unit]()
				{
					const float Bounce = WeakGM.IsValid() ? FMath::Abs(FMath::Sin(WeakGM->GetTime() * 4.f)) * 8.f * Unit : 0.f;
					return FVector2D(W * 0.5f - Half, H - 120.f * Unit - Bounce);
				}), FVector2D(Half * 2.f, 64.f * U),
				Clickable(LTClick::Talk, Panel(UIRgb(40, 120, 60, 220), UIRgb(255, 215, 90, 230),
					SNew(SBox).HAlign(HAlign_Center).VAlign(VAlign_Center)
					[
						SNew(STextBlock).Text(UIText(Label)).Font(Font(32.f)).ColorAndOpacity(FLinearColor::White)
					])), true);
		}
		else if (GM->GetNearCaption() >= 0)
		{
			const FVector2D Pos(W * 0.5f - 440.f * U, H - 150.f * U), Size(880.f * U, 120.f * U);
			Place(C, Pos, Size, Panel(UIRgb(70, 30, 85, 225), UIRgb(240, 200, 90, 230),
				SNew(SBox).Padding(FMargin(20.f * U, 10.f * U))
				[
					TextBox(UIText(GM->GetCaptionText()), 24.f, Size.X - 40.f * U, FLinearColor::White)
				]));
		}
	}

	// timed message
	{
		const FVector2D Pos(W * 0.5f - 420.f * U, 170.f * U), Size(840.f * U, 80.f * U);
		TSharedRef<SWidget> Box = SNew(SBorder)
			.BorderImage(Rounded(FLinearColor::White, 18.f * U))
			.BorderBackgroundColor(TAttribute<FSlateColor>::CreateLambda([WeakGM]()
			{
				const float A = WeakGM.IsValid() ? WeakGM->GetMessageAlpha() : 0.f;
				return FSlateColor(UIAlpha(UIRgb(20, 20, 30), A * 150.f / 255.f));
			}))
			.Padding(FMargin(14.f * U, 6.f * U))
			[
				TextBox(TAttribute<FText>::CreateLambda([WeakGM]() { return WeakGM.IsValid() ? UIText(WeakGM->GetMessage()) : FText(); }), 28.f, Size.X - 28.f * U,
					TAttribute<FSlateColor>::CreateLambda([WeakGM]()
					{
						const float A = WeakGM.IsValid() ? WeakGM->GetMessageAlpha() : 0.f;
						return FSlateColor(UIAlpha(UIRgb(255, 245, 140), A));
					}))
			];
		Box->SetVisibility(TAttribute<EVisibility>::CreateLambda([WeakGM]()
		{
			return WeakGM.IsValid() && !WeakGM->GetMessage().IsEmpty() ? EVisibility::HitTestInvisible : EVisibility::Collapsed;
		}));
		Place(C, Pos, Size, Box, true);
	}

	// "+3 coins" popups floating up
	for (const FLTPopup& P : GM->GetPopups())
	{
		const int32 Id = P.Id;
		auto Find = [WeakGM, Id]() -> const FLTPopup*
		{
			return WeakGM.IsValid() ? WeakGM->GetPopups().FindByPredicate([Id](const FLTPopup& X) { return X.Id == Id; }) : nullptr;
		};
		const FLinearColor Color = P.Color;
		Place(C, TAttribute<FVector2D>::CreateLambda([Find, W, H, Unit]()
			{
				const FLTPopup* Q = Find();
				const float T = Q ? Q->Time : 1.5f, X = Q ? Q->X : 0.5f;
				return FVector2D(W * X - W * 0.5f, H * 0.4f - T * 80.f * Unit);
			}), FVector2D(W, 70.f * U),
			SNew(SBox).HAlign(HAlign_Center)
			[
				SNew(STextBlock)
				.Text(UIText(P.Text))
				.Font(Font(38.f))
				.ColorAndOpacity(TAttribute<FSlateColor>::CreateLambda([Find, Color]()
				{
					const FLTPopup* Q = Find();
					return FSlateColor(UIAlpha(Color, Q ? FMath::Max(0.f, 1.f - Q->Time / 1.5f) : 0.f));
				}))
			]);
	}
}

void SLTGameUI::BuildIntro(const TSharedRef<SCanvas>& C)
{
	const ALTGameMode* GM = GameMode.Get();
	const TWeakObjectPtr<ALTGameMode> WeakGM = GameMode;
	const float W = (float)View.X, Unit = U;
	const FLTFloorTheme& Theme = GM->GetTheme();
	const FLinearColor Goal = UIRgb(220, 255, 200);
	TSharedRef<SVerticalBox> Lines = SNew(SVerticalBox);
	auto Line = [this, &Lines](const FString& S, float Px, const FLinearColor& Color, float Top)
	{
		Lines->AddSlot().AutoHeight().HAlign(HAlign_Center).Padding(0, Top * U, 0, 0)
		[
			SNew(STextBlock).Text(UIText(S)).Font(Font(Px)).ColorAndOpacity(Color)
		];
	};
	Line(GM->T(TEXT("floor")) + FString::Printf(TEXT(" %d"), GM->GetFloorIndex() + 1), 38.f, UIYellow(), 14.f);
	Line(Theme.Name(GM->IsPolish()), 46.f, FLinearColor::White, 2.f);
	Line(GM->T(TEXT("goal1")), 24.f, Goal, 22.f);
	Line(GM->T(TEXT("goal2")), 24.f, Goal, 6.f);
	Line(GM->T(TEXT("goal3")), 24.f, Goal, 6.f);
	Line(GM->T(TEXT("press")), 20.f, UIRgb(200, 200, 200), 18.f);
	// the banner slides down from the top
	Place(C, TAttribute<FVector2D>::CreateLambda([WeakGM, W, Unit]()
		{
			const float T = WeakGM.IsValid() ? FMath::Min(1.f, WeakGM->GetStateTime() * 2.5f) : 1.f;
			return FVector2D(W * 0.5f - 440.f * Unit, 250.f * Unit - (1.f - T) * 400.f * Unit);
		}), FVector2D(880.f * U, 330.f * U),
		Panel(UIRgb(30, 60, 45, 225), UIAlpha(Theme.Accent, 0.9f), Lines));
}

// ------------------------------------------------------------------ quiz dialog

void SLTGameUI::BuildDialog(const TSharedRef<SCanvas>& C)
{
	const ALTGameMode* GM = GameMode.Get();
	const TWeakObjectPtr<ALTGameMode> WeakGM = GameMode;
	const FLTDialogState& D = GM->GetDialog();
	const float W = (float)View.X, H = (float)View.Y, Unit = U;
	const FLinearColor Accent = GM->GetSpeakerColor();
	const FLinearColor Paper = UIRgb(255, 252, 240);
	const bool bShowQuestion = D.bHasQuestion && (D.Phase == ELTDialogPhase::Ask || D.Phase == ELTDialogPhase::Wrong || D.Phase == ELTDialogPhase::Correct);

	Place(C, FVector2D(0, 0), View, Dim(110));

	// speech bubble
	const float BX = 40.f * U, BW = W - 80.f * U;
	const FVector2D BubblePos(BX, 40.f * U), BubbleSize(BW, H - 300.f * U);
	Place(C, BubblePos, BubbleSize, Panel(Paper, UIAlpha(Accent, 0.9f)));
	Text(C, UIText(GM->GetSpeaker()), 24.f, BX + 24.f * U, BubblePos.Y + 12.f * U, Accent, HAlign_Left);
	if (GM->IsSpeechAvailable())
	{
		Place(C, FVector2D(BX + BW - 210.f * U, BubblePos.Y + 12.f * U), FVector2D(190.f * U, 36.f * U),
			Clickable(LTClick::Read, Panel(UIRgb(60, 140, 220, 220), FLinearColor::White,
				SNew(SBox).HAlign(HAlign_Center).VAlign(VAlign_Center)
				[
					SNew(STextBlock).Text(UIText(GM->T(TEXT("read")))).Font(Font(20.f)).ColorAndOpacity(FLinearColor::White)
				])), true);
	}

	const FVector2D InnerPos(BX + 24.f * U, BubblePos.Y + 56.f * U), InnerSize(BW - 48.f * U, BubbleSize.Y - 70.f * U);
	if (bShowQuestion)
	{
		const FLinearColor TopColor = D.Phase == ELTDialogPhase::Correct ? UIRgb(40, 160, 60) : D.Phase == ELTDialogPhase::Wrong ? UIRgb(220, 80, 60) : UIRgb(120, 100, 80);
		Place(C, InnerPos, FVector2D(InnerSize.X, 70.f * U), TextBox(UIText(D.Line), 22.f, InnerSize.X, TopColor, false));
		Place(C, InnerPos + FVector2D(0, 74.f * U), FVector2D(InnerSize.X, InnerSize.Y - 80.f * U), TextBox(UIText(D.Q.Text), 34.f, InnerSize.X, UIDark(), false));
	}
	else if (D.Phase == ELTDialogPhase::Reward && D.Gift >= 0)
	{
		Place(C, InnerPos, FVector2D(InnerSize.X, InnerSize.Y * 0.4f), TextBox(UIText(D.Line), 34.f, InnerSize.X, UIDark(), false));
		// the gift pops up in a ring of rainbow rays
		const float CX = W * 0.5f, CY = InnerPos.Y + InnerSize.Y * 0.7f, Max = FMath::Min(180.f * U, (float)InnerSize.Y * 0.55f);
		auto Grow = [WeakGM]() { return WeakGM.IsValid() ? 0.4f + 0.6f * FMath::Min(1.f, WeakGM->GetDialog().Time * 2.f) : 1.f; };
		Place(C, FVector2D(CX - Max, CY - Max), FVector2D(2.f * Max, 2.f * Max),
			SNew(SLTShape).Kind(ELTUIShape::Rays).Angle(TAttribute<float>::CreateLambda([WeakGM]() { return WeakGM.IsValid() ? WeakGM->GetTime() * 0.7f : 0.f; })));
		const FLinearColor GiftFill = UIGiftColor(D.Gift);
		Place(C, TAttribute<FVector2D>::CreateLambda([Max, Grow, CX, CY]() { const float S = Max * Grow(); return FVector2D(CX - S * 0.5f, CY - S * 0.5f); }),
			TAttribute<FVector2D>::CreateLambda([Max, Grow]() { const float S = Max * Grow(); return FVector2D(S, S); }),
			SNew(SLTShape).Kind(ELTUIShape::GiftBox).Fill(GiftFill).Line(UIRgb(255, 225, 70)));
	}
	else
	{
		Place(C, InnerPos, InnerSize, TextBox(UIText(D.Line), 34.f, InnerSize.X, UIDark(), false));
	}

	// answers (2 x 2 grid)
	if (bShowQuestion)
	{
		static const FLinearColor Colors[4] = { UIRgb(230, 80, 80), UIRgb(60, 140, 230), UIRgb(240, 170, 40), UIRgb(70, 180, 90) };
		const int32 N = FMath::Min(D.Q.Answers.Num(), 4);
		const float Top = BubblePos.Y + BubbleSize.Y + 20.f * U, AW = (BW - 20.f * U) * 0.5f, AH = (H - Top - 70.f * U) * 0.5f - 8.f * U;
		for (int32 i = 0; i < N; i++)
		{
			const bool bAsk = D.Phase == ELTDialogPhase::Ask;
			const bool bSel = bAsk && i == D.Selected;
			FLinearColor Fill = UIRgb(255, 255, 255, 235);
			if (!bAsk)
			{
				if (i == D.Q.Correct) Fill = UIRgb(150, 230, 140, 240);
				else if (i == D.Chosen) Fill = UIRgb(250, 150, 140, 240);
				else Fill = UIRgb(220, 220, 220, 150);
			}
			FVector2D Pos(BX + (i % 2) * (AW + 20.f * U), Top + (i / 2) * (AH + 16.f * U));
			FVector2D Size(AW, AH);
			if (bSel)
			{
				Pos -= FVector2D(4.f * U, 4.f * U);
				Size += FVector2D(8.f * U, 8.f * U);
			}
			const bool bShake = D.Phase == ELTDialogPhase::Wrong && i == D.Chosen;
			const float Badge = FMath::Min(44.f * U, AH - 8.f * U);
			TSharedRef<SWidget> Content = SNew(SBorder)
				.BorderImage(Rounded(Fill, 22.f * U, bSel ? UIRgb(255, 200, 40) : Colors[i], (bSel ? 7.f : 5.f) * U))
				.Padding(FMargin(12.f * U, 4.f * U))
				[
					SNew(SHorizontalBox)
					+ SHorizontalBox::Slot().AutoWidth().VAlign(VAlign_Center)
					[
						SNew(SBox).WidthOverride(Badge).HeightOverride(Badge)
						[
							SNew(SBorder)
							.BorderImage(Rounded(Colors[i], Badge * 0.5f))
							.HAlign(HAlign_Center)
							.VAlign(VAlign_Center)
							.Padding(0)
							[
								SNew(STextBlock).Text(FText::AsNumber(i + 1)).Font(Font(26.f)).ColorAndOpacity(FLinearColor::White)
							]
						]
					]
					+ SHorizontalBox::Slot().FillWidth(1.f).VAlign(VAlign_Center).Padding(10.f * U, 0, 0, 0)
					[
						TextBox(UIText(D.Q.Answers[i]), 28.f, AW - 90.f * U, UIDark(), false)
					]
				];
			const FVector2D Base = Pos;
			TAttribute<FVector2D> PosAttr = Base;
			if (bShake)
				PosAttr = TAttribute<FVector2D>::CreateLambda([WeakGM, Base, Unit]()
				{
					const float T = WeakGM.IsValid() ? WeakGM->GetDialog().Time : 1.f;
					return Base + FVector2D(T < 0.5f ? FMath::Sin(T * 60.f) * 6.f * Unit : 0.f, 0.f);
				});
			if (bAsk) Place(C, PosAttr, Size, Clickable(LTClick::Answer0 + i, Content), true);
			else Place(C, PosAttr, Size, Content);
		}
	}

	// footer
	const FString Hint = (D.Phase == ELTDialogPhase::Ask && D.bHasQuestion ? GM->T(TEXT("answer_hint")) : GM->T(TEXT("continue")))
		+ TEXT("      ") + GM->T(TEXT("esc_close"));
	Text(C, UIText(Hint), 21.f, W * 0.5f, H - 46.f * U, FLinearColor::White);
}

// ------------------------------------------------------------------ pause and floor screens

void SLTGameUI::BuildPaused(const TSharedRef<SCanvas>& C)
{
	const ALTGameMode* GM = GameMode.Get();
	Place(C, FVector2D(0, 0), View, Dim(150));
	Text(C, UIText(GM->T(TEXT("paused"))), 70.f, (float)View.X * 0.5f, 120.f * U, FLinearColor::White);
	MenuList(C, GM->GetPauseItems(), 260.f * U);
}

void SLTGameUI::BuildFloorDone(const TSharedRef<SCanvas>& C)
{
	const ALTGameMode* GM = GameMode.Get();
	const TWeakObjectPtr<ALTGameMode> WeakGM = GameMode;
	const float W = (float)View.X, H = (float)View.Y, Unit = U;
	Place(C, FVector2D(0, 0), View, Dim(130));
	const FVector2D PanelPos(W * 0.5f - 420.f * U, H * 0.1f), PanelSize(840.f * U, 520.f * U);
	Place(C, PanelPos, PanelSize, Panel(UIRgb(30, 60, 45, 225), UIRgb(255, 215, 90, 230)));
	Text(C, UIText(GM->T(TEXT("done"))), 48.f, W * 0.5f, PanelPos.Y + 22.f * U, UIYellow());
	Text(C, UIText(GM->GetTheme().Name(GM->IsPolish())), 26.f, W * 0.5f, PanelPos.Y + 86.f * U, FLinearColor::White);

	// the stars pop in one after another
	const int32 Stars = GM->GetLastStars();
	for (int32 i = 0; i < 3; i++)
	{
		const float CX = W * 0.5f + (i - 1) * 130.f * U, CY = PanelPos.Y + 200.f * U, R = 50.f * U;
		auto Appear = [WeakGM, i]() { return WeakGM.IsValid() ? WeakGM->GetStateTime() * 2.5f - i * 0.6f : 10.f; };
		auto Filled = [Appear, i, Stars]() { return i < Stars && Appear() > 0.f; };
		auto Radius = [Appear, Filled, R]() { return Filled() ? R * (1.f + 0.4f * FMath::Max(0.f, 1.f - Appear())) : R; };
		Place(C, TAttribute<FVector2D>::CreateLambda([Radius, CX, CY]() { const float Rr = Radius(); return FVector2D(CX - Rr, CY - Rr); }),
			TAttribute<FVector2D>::CreateLambda([Radius]() { const float Rr = Radius(); return FVector2D(2.f * Rr, 2.f * Rr); }),
			SNew(SLTShape).Kind(ELTUIShape::Star)
			.Fill(TAttribute<FLinearColor>::CreateLambda([Filled]() { return Filled() ? UIRgb(255, 220, 40) : UIRgb(60, 60, 70, 140); }))
			.Line(TAttribute<FLinearColor>::CreateLambda([Filled]() { return Filled() ? UIRgb(200, 120, 0) : UIRgb(30, 30, 40, 180); })));
	}

	const float S = 52.f * U, Gap = 6.f * U;
	float X = W * 0.5f - (10.f * S + 9.f * Gap) * 0.5f;
	for (int32 i = 0; i < LT::Classes; i++, X += S + Gap) Gift(C, i, true, X, PanelPos.Y + 280.f * U, S);

	Text(C, UIText(FString::Printf(TEXT("%s: %d      %s: %d"), *GM->T(TEXT("mistakes")), GM->GetMistakes(), *GM->T(TEXT("coins")), GM->GetCoins())),
		28.f, W * 0.5f, PanelPos.Y + 360.f * U, FLinearColor::White);

	TSharedRef<SWidget> Next = SNew(STextBlock).Text(UIText(GM->T(TEXT("next")))).Font(Font(28.f)).ColorAndOpacity(UIRgb(220, 255, 200));
	Next->SetVisibility(TAttribute<EVisibility>::CreateLambda([WeakGM]()
	{
		const bool bShow = WeakGM.IsValid() && WeakGM->GetStateTime() > 1.f && FMath::FloorToInt(WeakGM->GetTime() * 2.f) % 2 == 0;
		return bShow ? EVisibility::HitTestInvisible : EVisibility::Hidden;
	}));
	Place(C, FVector2D(0, PanelPos.Y + 440.f * U), FVector2D(W, 50.f * U), SNew(SBox).HAlign(HAlign_Center)[Next]);
}

void SLTGameUI::BuildVictory(const TSharedRef<SCanvas>& C)
{
	const ALTGameMode* GM = GameMode.Get();
	const TWeakObjectPtr<ALTGameMode> WeakGM = GameMode;
	const float W = (float)View.X, Unit = U;
	Place(C, FVector2D(0, 0), View, Dim(100));
	Place(C, FVector2D(0, 0), View,
		SNew(SLTConfetti).Unit(U).Time(TAttribute<float>::CreateLambda([WeakGM]() { return WeakGM.IsValid() ? WeakGM->GetTime() : 0.f; })));

	// diploma
	const FVector2D PaperPos(W * 0.5f - 400.f * U, 70.f * U), PaperSize(800.f * U, 520.f * U);
	Place(C, PaperPos, PaperSize, SNew(SBorder).BorderImage(Rounded(UIRgb(250, 240, 215, 250), 6.f * U, UIRgb(200, 160, 60), 8.f * U)));
	const FLinearColor Brown = UIRgb(90, 60, 30);
	auto Line = [&](const FString& S, float Px, const FLinearColor& Color, float Top, float Height)
	{
		Place(C, FVector2D(PaperPos.X + 30.f * U, PaperPos.Y + Top * U), FVector2D(PaperSize.X - 60.f * U, Height * U),
			TextBox(UIText(S), Px, PaperSize.X - 60.f * U, Color, false));
	};
	Line(GM->T(TEXT("diploma")), 60.f, UIRgb(180, 120, 20), 24.f, 80.f);
	Line(GM->T(TEXT("school_name")), 26.f, Brown, 110.f, 40.f);
	Line(GM->T(TEXT("school_patron")), 18.f, Brown, 148.f, 30.f);
	Place(C, TAttribute<FVector2D>::CreateLambda([WeakGM, W, PaperPos, Unit]()
		{
			const float Bob = WeakGM.IsValid() ? FMath::Sin(WeakGM->GetTime() * 3.f) * 6.f * Unit : 0.f;
			return FVector2D(W * 0.5f - 60.f * Unit, PaperPos.Y + 186.f * Unit + Bob);
		}), FVector2D(120.f * U, 120.f * U), TurtleIcon(120.f * U));
	Line(GM->T(TEXT("victory")), 34.f, UIRgb(40, 140, 60), 310.f, 50.f);
	Line(GM->T(TEXT("diploma2")), 22.f, Brown, 362.f, 40.f);
	Line(GM->F(TEXT("victory2"), { FString::FromInt(GM->GetTotalStars()), FString::FromInt(LT::Floors * 3) }), 26.f, Brown, 410.f, 40.f);

	TSharedRef<SWidget> Press = SNew(STextBlock).Text(UIText(GM->T(TEXT("press")))).Font(Font(24.f)).ColorAndOpacity(FLinearColor::White);
	Press->SetVisibility(TAttribute<EVisibility>::CreateLambda([WeakGM]()
	{
		return WeakGM.IsValid() && WeakGM->GetStateTime() > 1.5f ? EVisibility::HitTestInvisible : EVisibility::Hidden;
	}));
	Place(C, FVector2D(0, PaperPos.Y + PaperSize.Y + 20.f * U), FVector2D(W, 50.f * U), SNew(SBox).HAlign(HAlign_Center)[Press]);
}
