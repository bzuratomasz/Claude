using System.Globalization;
using System.Text;
using SearchCar.Core.Models;

namespace SearchCar.Core.Vin;

/// <summary>Extra facts about a listing that only the search as a whole can know.</summary>
/// <param name="OtherListingsWithSameVin">Other offers with this VIN that look like a different car or repeat on the same site.</param>
/// <param name="SameCarOnOtherSites">Sites where the same car (same VIN, make and year) is also offered - normal for dealers.</param>
public sealed record VerificationContext(int OtherListingsWithSameVin = 0, IReadOnlyList<string>? SameCarOnOtherSites = null);

/// <summary>Builds a <see cref="VinReport"/> for a listing: offline structural checks plus an optional online decode.</summary>
public sealed class VinVerifier(IVinDecoder? decoder = null, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<VinReport> VerifyAsync(CarListing listing, VerificationContext? context = null, CancellationToken cancellationToken = default)
    {
        context ??= new VerificationContext();
        var checks = new List<ReportCheck>();
        var vin = VinRules.Normalize(listing.Vin);

        if (vin.Length == 0)
        {
            if (listing.VinHiddenOnSite)
            {
                checks.Add(new("Obecność VIN", CheckOutcome.Warning,
                    $"Sprzedający podał VIN w serwisie {listing.Source}, ale serwis pokazuje go dopiero po weryfikacji reCAPTCHA. " +
                    "Otwórz ogłoszenie, kliknij \"Pokaż VIN\" i wklej numer w pole poniżej, aby go sprawdzić."));
                AddSiteChecks(listing, checks);
                return Build(listing, null, VinStatus.HiddenOnSite, checks, new Dictionary<string, string>());
            }

            checks.Add(new("Obecność VIN", CheckOutcome.Fail,
                "Sprzedający nie podał numeru VIN. Poproś o niego przed oględzinami - bez VIN nie da się sprawdzić historii pojazdu, " +
                "przebiegu ani tego, czy auto nie jest kradzione."));
            AddSiteChecks(listing, checks);
            return Build(listing, null, VinStatus.NotPresent, checks, new Dictionary<string, string>());
        }

        checks.Add(new("Obecność VIN", CheckOutcome.Pass, listing.VinSource switch
        {
            VinSource.Description => $"{vin} (znaleziony w opisie sprzedającego)",
            VinSource.Manual => $"{vin} (wpisany przez Ciebie)",
            _ => vin,
        }));
        AddSiteChecks(listing, checks);

        // --- Structure -------------------------------------------------------------------------------
        var lengthOk = vin.Length == VinRules.Length;
        checks.Add(lengthOk
            ? new("Długość", CheckOutcome.Pass, "17 znaków")
            : new("Długość", CheckOutcome.Fail, $"{vin.Length} znaków - VIN musi mieć dokładnie 17"));

        var badChars = VinRules.InvalidCharacters(vin);
        checks.Add(badChars.Count == 0
            ? new("Znaki", CheckOutcome.Pass, "Tylko dozwolone znaki (bez I, O, Q)")
            : new("Znaki", CheckOutcome.Fail, $"Niedozwolone znaki: {string.Join(", ", badChars)}"));

        if (!lengthOk || badChars.Count > 0)
            return Build(listing, vin, VinStatus.Invalid, checks, new Dictionary<string, string>());

        var fields = new Dictionary<string, string>
        {
            ["WMI"] = vin[..3],
            ["VDS"] = vin[3..9],
            ["VIS"] = vin[9..],
            ["Region"] = WmiDirectory.Region(vin),
        };

        // --- Check digit -----------------------------------------------------------------------------
        var expected = VinRules.ComputeCheckDigit(vin);
        var mandatory = VinRules.IsCheckDigitMandatory(vin);
        if (VinRules.IsCheckDigitValid(vin))
            checks.Add(new("Cyfra kontrolna", CheckOutcome.Pass, $"Pozycja 9 '{vin[8]}' jest poprawna"));
        else if (mandatory)
            checks.Add(new("Cyfra kontrolna", CheckOutcome.Fail,
                $"Na pozycji 9 jest '{vin[8]}', oczekiwano '{expected}'. W tym regionie cyfra kontrolna jest obowiązkowa - VIN jest błędnie wpisany lub podrobiony."));
        else
            checks.Add(new("Cyfra kontrolna", CheckOutcome.Info,
                $"Na pozycji 9 jest '{vin[8]}' (wzór daje '{expected}'). W pojazdach europejskich nie jest obowiązkowa, więc samo to nie jest błędem."));

        var invalid = checks.Any(c => c.Outcome == CheckOutcome.Fail);

        // --- Manufacturer ----------------------------------------------------------------------------
        var wmiMake = WmiDirectory.Manufacturer(vin);
        if (wmiMake is null)
            checks.Add(new("Producent (WMI)", CheckOutcome.Info, $"'{vin[..3]}' nie ma w lokalnym spisie; region: {fields["Region"]}"));
        else if (SameMake(wmiMake, listing.Make))
            checks.Add(new("Producent (WMI)", CheckOutcome.Pass, $"'{vin[..3]}' = {wmiMake}, zgodny z ogłoszeniem"));
        else
            checks.Add(new("Producent (WMI)", CheckOutcome.Fail, $"'{vin[..3]}' należy do marki {wmiMake}, a w ogłoszeniu jest {listing.Make}"));
        if (wmiMake is not null)
            fields["Producent (WMI)"] = wmiMake;

        // --- Model year (position 10) ----------------------------------------------------------------
        var thisYear = _clock.GetLocalNow().Year;
        var candidates = VinRules.ModelYearCandidates(vin, thisYear + 1);
        checks.Add(YearCheck("Rok modelowy (pozycja 10)", listing.Year, candidates, strict: mandatory));

        // --- Duplicates ------------------------------------------------------------------------------
        if (context.OtherListingsWithSameVin > 0)
            checks.Add(new("Unikalność w wynikach", CheckOutcome.Fail,
                $"Ten sam VIN występuje w innych ogłoszeniach ({context.OtherListingsWithSameVin}) - możliwy sklonowany VIN, duplikat lub oszustwo."));
        else
            checks.Add(new("Unikalność w wynikach", CheckOutcome.Pass, "Żadne inne ogłoszenie nie ma tego VIN"));
        if (context.SameCarOnOtherSites is { Count: > 0 } sites)
            checks.Add(new("Inne serwisy", CheckOutcome.Info,
                $"Ten sam samochód (ten sam VIN, marka i rok) jest wystawiony także w: {string.Join(", ", sites)}. Porównaj ceny i opisy."));

        // --- Online decode ---------------------------------------------------------------------------
        if (decoder is not null)
            await AddOnlineChecksAsync(listing, vin, checks, fields, cancellationToken).ConfigureAwait(false);

        checks.Add(new("Historia pojazdu", CheckOutcome.Info,
            "Sprawdzenie budowy numeru nie potwierdza historii auta. Dla auta zarejestrowanego w Polsce sprawdź historiapojazdu.gov.pl " +
            "(potrzebny numer rejestracyjny i data pierwszej rejestracji); dla aut sprowadzonych użyj płatnego raportu (np. carVertical, AutoDNA)."));

        var status = invalid ? VinStatus.Invalid
            : checks.Any(c => c.Outcome == CheckOutcome.Fail) ? VinStatus.Suspicious
            : checks.Any(c => c.Outcome == CheckOutcome.Warning) ? VinStatus.VerifiedWithWarnings
            : VinStatus.Verified;

        return Build(listing, vin, status, checks, fields);
    }

    private async Task AddOnlineChecksAsync(CarListing listing, string vin, List<ReportCheck> checks,
        Dictionary<string, string> fields, CancellationToken cancellationToken)
    {
        VinDecodeResult decoded;
        try
        {
            decoded = await decoder!.DecodeAsync(vin, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            checks.Add(new($"Dekodowanie online ({decoder!.Name})", CheckOutcome.Info, $"Usługa niedostępna: {ex.Message}"));
            return;
        }

        foreach (var (key, value) in decoded.Fields)
            fields[key] = value;

        checks.Add(decoded.IsClean
            ? new($"Dekodowanie online ({decoder.Name})", CheckOutcome.Pass, "Zdekodowano bez błędów")
            : new($"Dekodowanie online ({decoder.Name})", CheckOutcome.Info,
                $"{decoded.ErrorText ?? "Częściowe dekodowanie"} (baza NHTSA w pełni obejmuje auta z rynku USA; numery europejskie często dekoduje tylko częściowo)"));

        if (decoded.Make is { } make)
            checks.Add(SameMake(make, listing.Make)
                ? new("Marka z dekodera", CheckOutcome.Pass, $"{make}, zgodna z ogłoszeniem")
                : new("Marka z dekodera", CheckOutcome.Fail, $"Dekoder podaje {make}, ogłoszenie {listing.Make}"));

        if (decoded.Model is { } model)
            checks.Add(Normalize(model).Contains(Normalize(listing.Model)) || Normalize(listing.Model).Contains(Normalize(model))
                ? new("Model z dekodera", CheckOutcome.Pass, $"{model}, zgodny z ogłoszeniem")
                : new("Model z dekodera", CheckOutcome.Warning, $"Dekoder podaje {model}, ogłoszenie {listing.Model}"));

        if (decoded.ModelYear is int year)
            checks.Add(YearCheck("Rok modelowy z dekodera", listing.Year, [year], strict: VinRules.IsCheckDigitMandatory(vin)));
    }

    private static void AddSiteChecks(CarListing listing, List<ReportCheck> checks)
    {
        if (listing.CepikVerified == true)
            checks.Add(new("CEPIK (weryfikacja serwisu)", CheckOutcome.Pass,
                $"{listing.Source} podaje, że dane z ogłoszenia potwierdzono w Centralnej Ewidencji Pojazdów (CEPIK)."));
    }

    private static ReportCheck YearCheck(string name, int listingYear, IReadOnlyList<int> candidates, bool strict)
    {
        if (candidates.Count == 0)
            return new(name, CheckOutcome.Info, "Rok modelowy nie jest zakodowany");
        if (listingYear <= 0)
            return new(name, CheckOutcome.Info, $"VIN wskazuje {string.Join(" lub ", candidates)}; rok w ogłoszeniu nieznany");

        var described = string.Join(" lub ", candidates);
        if (candidates.Contains(listingYear))
            return new(name, CheckOutcome.Pass, $"{described}, zgodny z rokiem w ogłoszeniu ({listingYear})");

        if (candidates.Any(y => Math.Abs(y - listingYear) == 1))
            return new(name, CheckOutcome.Warning,
                $"VIN wskazuje {described}, ogłoszenie {listingYear}. Różnica 1 roku jest częsta (rok modelowy a rok produkcji/rejestracji).");

        return new(name, strict ? CheckOutcome.Fail : CheckOutcome.Warning,
            $"VIN wskazuje {described}, ogłoszenie {listingYear}." +
            (strict ? "" : " Niektórzy europejscy producenci nie kodują tu roku - poproś sprzedającego o dowód rejestracyjny."));
    }

    private VinReport Build(CarListing listing, string? vin, VinStatus status, List<ReportCheck> checks, Dictionary<string, string> fields)
    {
        var fails = checks.Count(c => c.Outcome == CheckOutcome.Fail);
        var warnings = checks.Count(c => c.Outcome == CheckOutcome.Warning);
        var summary = status switch
        {
            VinStatus.NotPresent => "Brak numeru VIN w ogłoszeniu. Auta nie da się zweryfikować - podejdź do oferty ostrożnie i poproś sprzedającego o VIN.",
            VinStatus.HiddenOnSite => "Ogłoszenie ma VIN, ale serwis ukrywa go za reCAPTCHA. Odkryj go na stronie ogłoszenia i wklej tutaj, aby uruchomić sprawdzenie.",
            VinStatus.Invalid => "VIN jest nieprawidłowy - sprzedający pomylił się przy wpisywaniu albo to nie jest prawdziwy VIN.",
            VinStatus.Suspicious => $"VIN ma poprawną budowę, ale liczba testów sprzecznych z ogłoszeniem: {fails}. Sprawdź dokumenty, zanim pójdziesz dalej.",
            VinStatus.VerifiedWithWarnings => $"VIN jest zgodny z ogłoszeniem; drobne ostrzeżenia: {warnings}.",
            _ => "VIN ma poprawną budowę i jest zgodny z ogłoszeniem.",
        };

        return new VinReport
        {
            ListingId = listing.Id,
            ListingTitle = listing.Title,
            ListingUrl = listing.Url,
            Vin = vin,
            Status = status,
            Summary = summary,
            Checks = checks,
            DecodedFields = fields,
            GeneratedAt = _clock.GetLocalNow(),
        };
    }

    public static bool SameMake(string a, string b)
    {
        static string Canonical(string make) => Normalize(make) switch
        {
            "vw" => "volkswagen",
            "mercedes" or "mercedesbenz" or "mb" => "mercedesbenz",
            "chevy" => "chevrolet",
            var other => other,
        };
        return Canonical(a) == Canonical(b);
    }

    /// <summary>Lower-case letters and digits only, diacritics removed ("Škoda" -> "skoda").</summary>
    internal static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
            if (char.IsLetterOrDigit(c) && CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }
}
