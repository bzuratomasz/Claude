using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using SearchCar.Core.Geo;
using SearchCar.Core.Models;
using SearchCar.Core.Vin;

namespace SearchCar.Core.Listings;

/// <summary>
/// Searches sprzedajemy.pl (local classifieds). The list page only has title, price and town, so every offer page
/// is read during the search (schema.org Vehicle data + map coordinates). The VIN is behind a "pokaż VIN" click,
/// so such offers are marked <see cref="CarListing.VinHiddenOnSite"/> unless the VIN is in the description.
/// </summary>
public sealed partial class SprzedajemyListingProvider(HttpClient http, PlaceCandidates places, TimeSpan? pageDelay = null) : ICarListingProvider
{
    public const string BaseUrl = "https://sprzedajemy.pl";
    private const int PageSize = 30;

    /// <summary>Radius values the site accepts (km).</summary>
    public static IReadOnlyList<int> Distances { get; } = [0, 2, 5, 10, 15, 30, 50, 75, 100, 150];

    private readonly PoliteHttp _http = new(http);
    private readonly TimeSpan _pageDelay = pageDelay ?? TimeSpan.FromSeconds(1);

    public string Name => "sprzedajemy.pl";

    public async Task<ProviderSearchResult> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        string? firstHtml = null;
        Uri? firstUrl = null;
        PlaceCandidate? used = null;
        foreach (var place in (await places.ForAsync(criteria, cancellationToken).ConfigureAwait(false)).Take(4))
        {
            var url = BuildSearchUrl(criteria, place, page: 1);
            if (await _http.GetOrNullAsync(url, cancellationToken).ConfigureAwait(false) is not { } html)
                continue; // 404 = unknown town (or make/model) - try the next town
            (firstHtml, firstUrl, used) = (html, url, place);
            break;
        }

        if (firstHtml is null || used is null)
            throw new InvalidOperationException("sprzedajemy.pl nie zna tej lokalizacji lub marki/modelu.");

        var items = new List<CarListing>(SprzedajemyPage.ParseSearch(firstHtml, Name));
        var total = SprzedajemyPage.TotalCount(firstHtml) ?? items.Count;
        var available = (int)Math.Ceiling(total / (double)PageSize);
        var toRead = Math.Min(Math.Clamp(criteria.MaxPages, 1, OtomotoListingProvider.MaxPages), available);
        for (var p = 2; p <= toRead; p++)
        {
            await Task.Delay(_pageDelay, cancellationToken).ConfigureAwait(false);
            items.AddRange(SprzedajemyPage.ParseSearch(await _http.GetAsync(BuildSearchUrl(criteria, used, p), cancellationToken).ConfigureAwait(false), Name));
        }

        // Read offer pages only for offers whose price (and year, when the title shows one) already fits.
        var candidates = items
            .DistinctBy(l => l.Id)
            .Where(l => (criteria.PriceFrom is not decimal pf || l.Price >= pf) && (criteria.PriceTo is not decimal pt || l.Price <= pt))
            .Where(l => SprzedajemyPage.YearInTitle(l.Headline) is not int y ||
                        ((criteria.YearFrom is not int yf || y >= yf) && (criteria.YearTo is not int yt || y <= yt)))
            .ToList();
        var detailed = await Task.WhenAll(candidates.Select(l => LoadOfferAsync(l, cancellationToken))).ConfigureAwait(false);

        var distance = SnapDistance(criteria.RadiusKm + used.OffsetKm);
        var notes = new List<string>
        {
            $"sprzedajemy.pl: szukanie w promieniu {distance} km od miejscowości {used.Name}; ogłoszeń: {total:N0}, odczytano stron: {Math.Max(toRead, 1)} z {Math.Max(available, 1)}.",
        };
        if (criteria.RadiusKm > Distances[^1])
            notes.Add($"sprzedajemy.pl pozwala na maksymalnie {Distances[^1]} km.");

        return new ProviderSearchResult(detailed, notes, [new SourceLink(Name, firstUrl!)]);
    }

    private async Task<CarListing> LoadOfferAsync(CarListing listing, CancellationToken ct)
    {
        try
        {
            return SprzedajemyPage.ApplyDetails(listing, await _http.GetAsync(listing.Url!, ct).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return listing with { DetailsLoaded = true }; // keep the offer with what the list page had
        }
    }

    public static Uri BuildSearchUrl(SearchCriteria c, PlaceCandidate place, int page)
    {
        var path = new List<string> { VehicleCatalog.Slug(place.Name), "motoryzacja", "samochody-osobowe" };
        if (!string.IsNullOrWhiteSpace(c.Make))
        {
            path.Add(VehicleCatalog.Slug(c.Make));
            if (!string.IsNullOrWhiteSpace(c.Model))
                path.Add(VehicleCatalog.Slug(c.Model));
        }
        var query = $"inp_distance={SnapDistance(c.RadiusKm + place.OffsetKm)}" + (page > 1 ? $"&offset={(page - 1) * PageSize}" : "");
        return new Uri($"{BaseUrl}/{string.Join("/", path.Select(Uri.EscapeDataString))}?{query}");
    }

    public static int SnapDistance(double radiusKm) => Distances.FirstOrDefault(d => d >= radiusKm, Distances[^1]);
}

internal static partial class SprzedajemyPage
{
    [GeneratedRegex("""<article class="element[^"]*">(.*?)</article>""", RegexOptions.Singleline)]
    private static partial Regex ArticleRegex();

    [GeneratedRegex("""<h2 class="title[^"]*">\s*<a href="([^"]+)"[^>]*>\s*(.*?)\s*</a>""", RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex("""<span class="price[^"]*">\s*([^<]+?)\s*</span>""", RegexOptions.Singleline)]
    private static partial Regex PriceRegex();

    [GeneratedRegex("""<strong class="city">\s*([^<]+?)\s*</strong>""", RegexOptions.Singleline)]
    private static partial Regex CityRegex();

    [GeneratedRegex("""seller-type-info--(\w+)""")]
    private static partial Regex SellerTypeRegex();

    [GeneratedRegex("""nr(\d+)$""")]
    private static partial Regex IdRegex();

    [GeneratedRegex("""offset=(\d+)""")]
    private static partial Regex OffsetRegex();

    [GeneratedRegex("""<script[^>]*application/ld\+json[^>]*>(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex LdJsonRegex();

    [GeneratedRegex("""data-coordinates="\{'lat':\s*([-\d.]+),\s*'lng':\s*([-\d.]+)""")]
    private static partial Regex CoordinatesRegex();

    [GeneratedRegex("""Skrzynia bieg[óo]w\s+(\S+)""", RegexOptions.IgnoreCase)]
    private static partial Regex GearboxRegex();

    [GeneratedRegex("""\bVIN\s+(pokaż VIN|[A-HJ-NPR-Z0-9]{17})""", RegexOptions.IgnoreCase)]
    private static partial Regex VinRowRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"(?<!\d)(19[5-9]\d|20[0-4]\d)(?!\d)")]
    private static partial Regex YearRegex();

    /// <summary>A production year written in the title ("... 2007r Nowy rozrząd"), if any.</summary>
    public static int? YearInTitle(string? title) =>
        title is not null && YearRegex().Match(title) is { Success: true } m ? int.Parse(m.Value, CultureInfo.InvariantCulture) : null;

    public static IReadOnlyList<CarListing> ParseSearch(string html, string sourceName)
    {
        var list = new List<CarListing>();
        foreach (Match article in ArticleRegex().Matches(html))
        {
            var a = article.Groups[1].Value;
            var title = TitleRegex().Match(a);
            if (!title.Success)
                continue;

            var href = WebUtility.HtmlDecode(title.Groups[1].Value);
            var priceText = PriceRegex().Match(a).Groups[1].Value;
            var digits = new string(priceText.Where(char.IsDigit).ToArray());
            list.Add(new CarListing
            {
                Id = IdRegex().Match(href) is { Success: true } id ? id.Groups[1].Value : href,
                Make = "", Model = "", Year = 0, MileageKm = -1,
                Price = decimal.TryParse(digits, out var price) ? price : 0,
                Headline = WebUtility.HtmlDecode(title.Groups[2].Value).Trim(),
                City = WebUtility.HtmlDecode(CityRegex().Match(a).Groups[1].Value),
                Seller = SellerTypeRegex().Match(a).Groups[1].Value == "private" ? "Osoba prywatna" : "Firma",
                Source = sourceName,
                Url = new Uri(new Uri(SprzedajemyListingProvider.BaseUrl), href),
            });
        }
        return list;
    }

    /// <summary>Total offers, derived from the last pagination link (offset of the last page + up to 30).</summary>
    public static int? TotalCount(string html)
    {
        var offsets = OffsetRegex().Matches(html).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
        return offsets.Count == 0 ? null : offsets.Max() + 30;
    }

    public static CarListing ApplyDetails(CarListing listing, string html)
    {
        var result = listing with { DetailsLoaded = true };

        foreach (Match m in LdJsonRegex().Matches(html))
        {
            JsonElement v;
            try
            {
                using var doc = JsonDocument.Parse(m.Groups[1].Value);
                v = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue;
            }
            if (v.ValueKind != JsonValueKind.Object || Str(v, "@type") != "Vehicle")
                continue;

            var make = v.TryGetProperty("brand", out var brand) ? Str(brand, "name") : null;
            var mileage = v.TryGetProperty("mileageFromOdometer", out var odo) ? Str(odo, "value") : null;
            var year = Str(v, "productionDate") ?? Str(v, "dateVehicleFirstRegistered");
            result = result with
            {
                Make = make ?? result.Make,
                Model = Str(v, "model") ?? result.Model,
                Year = int.TryParse(year?[..Math.Min(4, year.Length)], out var y) ? y : result.Year,
                MileageKm = decimal.TryParse(mileage, NumberStyles.Number, CultureInfo.InvariantCulture, out var km) ? (int)km : result.MileageKm,
                Fuel = FuelParsing.FromText(Str(v, "fuelType")),
            };

            if (OtomotoPage.FindVinInText(Str(v, "description")) is { } fromDescription)
                result = result with { Vin = fromDescription, VinSource = VinSource.Description };
        }

        var text = WebUtility.HtmlDecode(TagRegex().Replace(html, " "));
        text = Regex.Replace(text, @"\s+", " ");
        if (GearboxRegex().Match(text) is { Success: true } g)
            result = result with { Gearbox = FuelParsing.GearboxFromText(g.Groups[1].Value) };

        if (VinRowRegex().Match(text) is { Success: true } vinRow && string.IsNullOrEmpty(result.Vin))
        {
            var value = VinRules.Normalize(vinRow.Groups[1].Value);
            result = VinRules.IsWellFormed(value)
                ? result with { Vin = value, VinSource = VinSource.Listing }
                : result with { VinHiddenOnSite = true };
        }

        if (CoordinatesRegex().Match(html) is { Success: true } c &&
            double.TryParse(c.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
            double.TryParse(c.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
            result = result with { Location = new GeoPoint(lat, lon) };

        return result;
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
