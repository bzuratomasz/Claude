#pragma once

#include "CoreMinimal.h"
#include "Brushes/SlateColorBrush.h"
#include "Styling/SlateBrush.h"
#include "Styling/SlateTypes.h"
#include "Widgets/SNullWidget.h"
#include "Widgets/DeclarativeSyntaxSupport.h"
#include "Widgets/SCompoundWidget.h"

class ALTGameMode;
class SBox;
class SCanvas;

/**
 * Everything drawn over the 3D view (port of the original Hud.cs, without the mini map): title and floor menus,
 * help, intro banner, the playing HUD (floor badge, gifts, coins, stars, compass, talk button, messages, captions),
 * quiz dialogs, pause menu, floor results and the diploma. Pure Slate, laid out on a canvas in the original's units
 * (u = min(H / 720, W / 1100)). It reads the game mode's state every frame: the widget tree is rebuilt only when the
 * state changes (screen, menu item, dialog line, ...); moving and fading parts use attributes. Clicks call
 * ALTGameMode::HandleClick.
 */
class LAZYTURTLE_API SLTGameUI : public SCompoundWidget
{
public:
	SLATE_BEGIN_ARGS(SLTGameUI) {}
		SLATE_ARGUMENT(TWeakObjectPtr<ALTGameMode>, GameMode)
	SLATE_END_ARGS()

	void Construct(const FArguments& InArgs);
	virtual void Tick(const FGeometry& AllottedGeometry, const double InCurrentTime, const float InDeltaTime) override;
	virtual bool SupportsKeyboardFocus() const override { return false; }

private:
	TWeakObjectPtr<ALTGameMode> GameMode;
	TSharedPtr<SBox> Root;
	FString Signature;
	FVector2D View = FVector2D(1280, 720);
	float U = 1.f;

	FSlateColorBrush WhiteBrush = FSlateColorBrush(FLinearColor::White);
	FSlateNoResource NoBrush;
	/** Rounded panel brushes by colour / radius / outline (widgets keep raw pointers to them). */
	TMap<FString, TSharedPtr<FSlateBrush>> Brushes;

	FString MakeSignature(const ALTGameMode& GM) const;
	void Rebuild();

	// ---- screens
	void BuildTitle(const TSharedRef<SCanvas>& C);
	void BuildHelp(const TSharedRef<SCanvas>& C);
	void BuildFloors(const TSharedRef<SCanvas>& C);
	void BuildPlayHud(const TSharedRef<SCanvas>& C);
	void BuildIntro(const TSharedRef<SCanvas>& C);
	void BuildDialog(const TSharedRef<SCanvas>& C);
	void BuildPaused(const TSharedRef<SCanvas>& C);
	void BuildFloorDone(const TSharedRef<SCanvas>& C);
	void BuildVictory(const TSharedRef<SCanvas>& C);
	void MenuList(const TSharedRef<SCanvas>& C, const TArray<FString>& Items, float Top);

	// ---- building blocks
	const FSlateBrush* Rounded(const FLinearColor& Fill, float Radius, const FLinearColor& Outline = FLinearColor::Transparent, float OutlineWidth = 0.f);
	FSlateFontInfo Font(float Px, bool bOutline = true) const;
	/** Adds a widget to the canvas; it is made hit-test invisible unless bKeepVisibility (clickable or self-hiding widgets). */
	void Place(const TSharedRef<SCanvas>& C, const TAttribute<FVector2D>& Pos, const TAttribute<FVector2D>& Size, const TSharedRef<SWidget>& Widget,
		bool bKeepVisibility = false) const;
	/** Outlined text with its top at Y, aligned to X (left / centre / right). */
	void Text(const TSharedRef<SCanvas>& C, const TAttribute<FText>& Str, float Px, float X, float Y, const TAttribute<FSlateColor>& Color,
		EHorizontalAlignment Align = HAlign_Center, bool bOutline = true);
	/** Wrapped text inside a box, shrunk to fit. */
	TSharedRef<SWidget> TextBox(const TAttribute<FText>& Str, float Px, float BoxWidth, const TAttribute<FSlateColor>& Color,
		bool bOutline = true, EHorizontalAlignment Align = HAlign_Center) const;
	TSharedRef<SWidget> Panel(const FLinearColor& Fill, const FLinearColor& Border, const TSharedRef<SWidget>& Content = SNullWidget::NullWidget);
	TSharedRef<SWidget> Clickable(int32 Action, const TSharedRef<SWidget>& Content, bool bHandCursor = true);
	TSharedRef<SWidget> Dim(int32 Alpha);
	TSharedRef<SWidget> CoinIcon(float Size);
	TSharedRef<SWidget> TurtleIcon(float Size);
	void Gift(const TSharedRef<SCanvas>& C, int32 Index, bool bHave, float X, float Y, float Size);
};
