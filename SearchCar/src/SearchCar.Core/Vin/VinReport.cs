using System.Globalization;
using System.Text;

namespace SearchCar.Core.Vin;

public enum CheckOutcome { Pass, Info, Warning, Fail }

public enum VinStatus
{
    /// <summary>The seller did not publish a VIN.</summary>
    NotPresent,
    /// <summary>The site has a VIN but only reveals it to a person (e.g. after reCAPTCHA).</summary>
    HiddenOnSite,
    /// <summary>The VIN is malformed (wrong length, illegal characters, or a mandatory check digit is wrong).</summary>
    Invalid,
    /// <summary>The VIN is well-formed but contradicts the listing (other brand, other year, reused VIN...).</summary>
    Suspicious,
    VerifiedWithWarnings,
    Verified,
}

public sealed record ReportCheck(string Name, CheckOutcome Outcome, string Detail);

public sealed record VinReport
{
    public required string ListingId { get; init; }
    public required string ListingTitle { get; init; }
    public Uri? ListingUrl { get; init; }
    public string? Vin { get; init; }
    public required VinStatus Status { get; init; }
    public required string Summary { get; init; }
    public required IReadOnlyList<ReportCheck> Checks { get; init; }
    public IReadOnlyDictionary<string, string> DecodedFields { get; init; } = new Dictionary<string, string>();
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;

    public static string StatusLabel(VinStatus status) => status switch
    {
        VinStatus.NotPresent => "Brak VIN",
        VinStatus.HiddenOnSite => "VIN ukryty w serwisie",
        VinStatus.Invalid => "VIN nieprawidłowy",
        VinStatus.Suspicious => "VIN podejrzany",
        VinStatus.VerifiedWithWarnings => "VIN OK z uwagami",
        VinStatus.Verified => "VIN zweryfikowany",
        _ => status.ToString(),
    };

    public static string OutcomeLabel(CheckOutcome outcome) => outcome switch
    {
        CheckOutcome.Pass => "OK",
        CheckOutcome.Warning => "UWAGA",
        CheckOutcome.Fail => "BŁĄD",
        _ => "INFO",
    };

    public string ToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("RAPORT WERYFIKACJI VIN");
        sb.AppendLine(new string('=', 60));
        sb.AppendLine($"Ogłoszenie: {ListingTitle} ({ListingId})");
        if (ListingUrl is not null)
            sb.AppendLine($"Link:       {ListingUrl}");
        sb.AppendLine($"VIN:        {(string.IsNullOrEmpty(Vin) ? "brak" : Vin)}");
        sb.AppendLine($"Status:     {StatusLabel(Status)}");
        sb.AppendLine($"Utworzono:  {GeneratedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}");
        sb.AppendLine();
        sb.AppendLine(Summary);
        sb.AppendLine();
        sb.AppendLine("Sprawdzenia");
        sb.AppendLine(new string('-', 60));
        foreach (var check in Checks)
            sb.AppendLine($"[{OutcomeLabel(check.Outcome),-5}] {check.Name}: {check.Detail}");

        if (DecodedFields.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Dane z dekodera");
            sb.AppendLine(new string('-', 60));
            foreach (var (key, value) in DecodedFields)
                sb.AppendLine($"{key,-16} {value}");
        }

        return sb.ToString();
    }
}
