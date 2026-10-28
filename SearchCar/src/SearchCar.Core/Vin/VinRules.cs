namespace SearchCar.Core.Vin;

/// <summary>Structural rules for 17-character VINs (ISO 3779 / 49 CFR 565).</summary>
public static class VinRules
{
    public const int Length = 17;

    // Position 10 model-year codes, starting at 1980 and repeating every 30 years.
    private const string YearCodes = "ABCDEFGHJKLMNPRSTVWXY123456789";
    private static readonly int[] Weights = [8, 7, 6, 5, 4, 3, 2, 10, 0, 9, 8, 7, 6, 5, 4, 3, 2];

    public static string Normalize(string? vin) =>
        new string((vin ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();

    public static bool IsAllowedCharacter(char c) =>
        c is >= '0' and <= '9' || (c is >= 'A' and <= 'Z' && c is not ('I' or 'O' or 'Q'));

    public static IReadOnlyList<char> InvalidCharacters(string vin) =>
        vin.Where(c => !IsAllowedCharacter(c)).Distinct().ToList();

    public static bool IsWellFormed(string vin) => vin.Length == Length && vin.All(IsAllowedCharacter);

    public static int? Transliterate(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        'A' or 'J' => 1,
        'B' or 'K' or 'S' => 2,
        'C' or 'L' or 'T' => 3,
        'D' or 'M' or 'U' => 4,
        'E' or 'N' or 'V' => 5,
        'F' or 'W' => 6,
        'G' or 'P' or 'X' => 7,
        'H' or 'Y' => 8,
        'R' or 'Z' => 9,
        _ => null,
    };

    /// <summary>Check digit (position 9) the VIN should carry, or null if the VIN is not well-formed.</summary>
    public static char? ComputeCheckDigit(string vin)
    {
        if (vin.Length != Length)
            return null;

        var sum = 0;
        for (var i = 0; i < Length; i++)
        {
            if (i == 8)
                continue;
            if (Transliterate(vin[i]) is not int value)
                return null;
            sum += value * Weights[i];
        }

        var remainder = sum % 11;
        return remainder == 10 ? 'X' : (char)('0' + remainder);
    }

    public static bool IsCheckDigitValid(string vin) => ComputeCheckDigit(vin) is char expected && vin[8] == expected;

    /// <summary>
    /// The check digit is mandatory for vehicles built for North America (WMI starting 1-5) and China (L).
    /// European and most other manufacturers are not required to use it.
    /// </summary>
    public static bool IsCheckDigitMandatory(string vin) => vin.Length > 0 && vin[0] is >= '1' and <= '5' or 'L';

    /// <summary>Model years the position-10 code can stand for, up to <paramref name="maxYear"/>.</summary>
    public static IReadOnlyList<int> ModelYearCandidates(string vin, int maxYear)
    {
        if (vin.Length < 10)
            return [];
        var index = YearCodes.IndexOf(vin[9]);
        if (index < 0)
            return [];

        var years = new List<int>();
        for (var year = 1980 + index; year <= maxYear; year += 30)
            years.Add(year);
        return years;
    }

    public static char YearCode(int year) => YearCodes[((year - 1980) % 30 + 30) % 30];
}
