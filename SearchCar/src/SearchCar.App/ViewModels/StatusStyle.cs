using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using SearchCar.Core.Vin;
using Windows.UI;

namespace SearchCar.App.ViewModels;

/// <summary>Colours and glyphs shared by the result list, the report panel and the map markers.</summary>
public static class StatusStyle
{
    private static readonly Color Green = Color.FromArgb(255, 0x4C, 0xAF, 0x50);
    private static readonly Color Amber = Color.FromArgb(255, 0xF0, 0xA0, 0x20);
    private static readonly Color Red = Color.FromArgb(255, 0xEF, 0x53, 0x50);
    private static readonly Color DarkRed = Color.FromArgb(255, 0xE9, 0x1E, 0x63);
    private static readonly Color Grey = Color.FromArgb(255, 0x9E, 0x9E, 0x9E);
    private static readonly Color Blue = Color.FromArgb(255, 0x42, 0xA5, 0xF5);
    private static readonly Color Purple = Color.FromArgb(255, 0xAB, 0x47, 0xBC);

    public static Color ColorOf(VinStatus? status) => status switch
    {
        VinStatus.Verified => Green,
        VinStatus.VerifiedWithWarnings => Amber,
        VinStatus.Suspicious => Red,
        VinStatus.Invalid => DarkRed,
        VinStatus.NotPresent => Grey,
        VinStatus.HiddenOnSite => Purple,
        _ => Blue, // still verifying
    };

    public static Color ColorOf(CheckOutcome outcome) => outcome switch
    {
        CheckOutcome.Pass => Green,
        CheckOutcome.Warning => Amber,
        CheckOutcome.Fail => Red,
        _ => Blue,
    };

    public static string GlyphOf(CheckOutcome outcome) => outcome switch
    {
        CheckOutcome.Pass => "",    // CheckMark
        CheckOutcome.Warning => "", // Warning
        CheckOutcome.Fail => "",    // Cancel
        _ => "",                    // Info
    };

    public static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static SolidColorBrush Brush(Color c) => new(c);

    public static SolidColorBrush White { get; } = new(Colors.White);
}
