using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SearchCar.Core.Geo;
using SearchCar.Core.Models;
using SearchCar.Core.Vin;

namespace SearchCar.Core.Listings;

/// <summary>
/// Searches autoplac.pl. The results page embeds the site's own API response (Angular "ng-state"), which already
/// carries coordinates; the offer page publishes the VIN in clear text when the seller entered one.
/// Uses the path-based search (/oferty/samochody-osobowe/marka/model/wojewodztwo/miasto?range=&amp;p=) allowed by robots.txt.
/// </summary>
public sealed partial class AutoplacListingProvider(HttpClient http, PlaceCandidates places, TimeSpan? pageDelay = null) : ICarListingProvider
{
    public const string BaseUrl = "https://autoplac.pl";
    private const int PageSize = 24;

    private readonly PoliteHttp _http = new(http);
    private readonly TimeSpan _pageDelay = pageDelay ?? TimeSpan.FromSeconds(1);
    private readonly ConcurrentDictionary<string, CarListing> _details = new();

    public string Name => "autoplac.pl";

    public async Task<ProviderSearchResult> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        AutoplacSearchPage? first = null;
        Uri? firstUrl = null;
        PlaceCandidate? used = null;
        foreach (var place in (await places.ForAsync(criteria, cancellationToken).ConfigureAwait(false)).Take(4))
        {
            if (place.Region is null)
                continue;
            var url = BuildSearchUrl(criteria, place, page: 1);
            var html = await _http.GetOrNullAsync(url, cancellationToken).ConfigureAwait(false);
            if (html is null || AutoplacPage.ParseSearch(html, Name) is not { AppliedCity: not null } page)
                continue; // the site does not know this town - try the next one
            (first, firstUrl, used) = (page, url, place);
            break;
        }

        if (first is null || used is null)
            throw new InvalidOperationException("autoplac.pl nie zna tej lokalizacji.");

        var listings = new List<CarListing>(first.Listings);
        var available = (int)Math.Ceiling(first.Total / (double)PageSize);
        var toRead = Math.Min(Math.Clamp(criteria.MaxPages, 1, OtomotoListingProvider.MaxPages), available);
        for (var p = 2; p <= toRead; p++)
        {
            await Task.Delay(_pageDelay, cancellationToken).ConfigureAwait(false);
            var html = await _http.GetAsync(BuildSearchUrl(criteria, used, p), cancellationToken).ConfigureAwait(false);
            listings.AddRange(AutoplacPage.ParseSearch(html, Name)?.Listings ?? []);
        }

        var notes = new List<string>
        {
            $"autoplac.pl: szukanie w promieniu {RangeFor(criteria, used)} km od miejscowości {first.AppliedCity}; ofert: {first.Total:N0}, odczytano stron: {Math.Max(toRead, 1)} z {Math.Max(available, 1)}.",
        };
        return new ProviderSearchResult(listings, notes, [new SourceLink(Name, firstUrl!)]);
    }

    public async Task<CarListing> LoadDetailsAsync(CarListing listing, CancellationToken cancellationToken = default)
    {
        if (_details.TryGetValue(listing.Id, out var cached))
            return cached;
        if (listing.Url is null)
            return listing with { DetailsLoaded = true };

        var detailed = AutoplacPage.ApplyDetails(listing, await _http.GetAsync(listing.Url, cancellationToken).ConfigureAwait(false));
        _details[listing.Id] = detailed;
        return detailed;
    }

    public static Uri BuildSearchUrl(SearchCriteria c, PlaceCandidate place, int page)
    {
        var path = new List<string> { "oferty", "samochody-osobowe" };
        if (!string.IsNullOrWhiteSpace(c.Make))
        {
            path.Add(VehicleCatalog.Slug(c.Make));
            if (!string.IsNullOrWhiteSpace(c.Model))
                path.Add(VehicleCatalog.Slug(c.Model));
        }
        path.Add(VehicleCatalog.Slug(place.Region!));
        path.Add(VehicleCatalog.Slug(place.Name));

        var query = $"range={RangeFor(c, place)}" + (page > 1 ? $"&p={page}" : "");
        return new Uri($"{BaseUrl}/{string.Join("/", path.Select(Uri.EscapeDataString))}?{query}");
    }

    private static int RangeFor(SearchCriteria c, PlaceCandidate place) =>
        (int)Math.Clamp(Math.Ceiling(c.RadiusKm + place.OffsetKm), 5, 500);
}

internal sealed record AutoplacSearchPage(IReadOnlyList<CarListing> Listings, int Total, string? AppliedCity);

internal static partial class AutoplacPage
{
    [GeneratedRegex("""<script id="ng-state"[^>]*>(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex StateRegex();

    public static JsonElement State(string html)
    {
        var m = StateRegex().Match(html);
        if (!m.Success)
            throw new InvalidDataException("Strona autoplac.pl nie zawiera danych ng-state - układ strony mógł się zmienić.");
        using var doc = JsonDocument.Parse(m.Groups[1].Value);
        return doc.RootElement.Clone();
    }

    public static AutoplacSearchPage? ParseSearch(string html, string sourceName)
    {
        foreach (var entry in State(html).EnumerateObject())
        {
            if (!entry.Name.Contains("/offers/search", StringComparison.Ordinal) || !entry.Value.TryGetProperty("body", out var body))
                continue;

            var listings = new List<CarListing>();
            if (body.TryGetProperty("offerList", out var list) && list.ValueKind == JsonValueKind.Array)
                foreach (var item in list.EnumerateArray())
                    if (ParseItem(item, sourceName) is { } listing)
                        listings.Add(listing);

            string? city = null;
            if (body.TryGetProperty("filters", out var filters))
                city = Str(filters, "city");
            var total = body.TryGetProperty("offerCount", out var count) && count.ValueKind == JsonValueKind.Number ? count.GetInt32() : listings.Count;
            return new AutoplacSearchPage(listings, total, city);
        }
        return null;
    }

    private static CarListing? ParseItem(JsonElement item, string sourceName)
    {
        if (!item.TryGetProperty("offer", out var o))
            return null;
        var make = Str(o, "brand");
        var model = Str(o, "model");
        if (make is null || model is null)
            return null;

        decimal price = 0;
        var currency = "PLN";
        if (o.TryGetProperty("priceInfo", out var pi))
        {
            if (pi.TryGetProperty("primary", out var primary) && primary.TryGetProperty("price", out var pr) && pr.ValueKind == JsonValueKind.Number)
                price = pr.GetDecimal();
            currency = Str(pi, "currency") ?? currency;
        }

        GeoPoint? location = null;
        var city = Str(o, "city") ?? "";
        if (o.TryGetProperty("locationInfo", out var li))
        {
            city = Str(li, "city") ?? city;
            if (li.TryGetProperty("latitude", out var lat) && lat.ValueKind == JsonValueKind.Number &&
                li.TryGetProperty("longitude", out var lon) && lon.ValueKind == JsonValueKind.Number)
                location = new GeoPoint(lat.GetDouble(), lon.GetDouble());
        }

        var seller = "Osoba prywatna";
        if (item.TryGetProperty("dealer", out var dealer) && dealer.ValueKind == JsonValueKind.Object)
            seller = Str(dealer, "description") ?? Str(dealer, "companyName") ?? "Firma";

        var webUrl = Str(o, "webUrl");
        return new CarListing
        {
            Id = o.TryGetProperty("id", out var id) ? id.ToString() : Guid.NewGuid().ToString("N"),
            Make = make,
            Model = model,
            Year = o.TryGetProperty("productionYear", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetInt32() : 0,
            MileageKm = o.TryGetProperty("mileage", out var km) && km.ValueKind == JsonValueKind.Number ? km.GetInt32() : -1,
            Price = price,
            Currency = currency,
            Fuel = FuelParsing.FromText(Str(o, "fuelType") ?? Str(o, "fuelTypeText")),
            Gearbox = FuelParsing.GearboxFromText(Str(o, "transmissionTypeText") ?? Str(o, "transmissionType")),
            Headline = Str(o, "title"),
            Location = location,
            City = city,
            Region = Str(o, "voivodeshipDisplay") ?? "",
            Seller = seller,
            Source = sourceName,
            VinHiddenOnSite = false,
            Url = webUrl is null ? null : new Uri(new Uri(AutoplacListingProvider.BaseUrl), webUrl),
        };
    }

    /// <summary>The offer page's state holds "vin" in clear text when the seller entered it, plus the description.</summary>
    public static CarListing ApplyDetails(CarListing listing, string html)
    {
        var result = listing with { DetailsLoaded = true };
        var state = State(html);

        string? vin = null, description = null;
        foreach (var entry in state.EnumerateObject())
        {
            if (!entry.Name.Contains("/offer/", StringComparison.Ordinal) || !entry.Value.TryGetProperty("body", out var body))
                continue;
            vin ??= FindString(body, "vin");
            description ??= FindString(body, "description");
        }

        var clear = VinRules.Normalize(vin);
        if (VinRules.IsWellFormed(clear))
            return result with { Vin = clear, VinSource = VinSource.Listing };
        if (OtomotoPage.FindVinInText(description) is { } fromText)
            return result with { Vin = fromText, VinSource = VinSource.Description };
        return result;
    }

    /// <summary>First string property with this name anywhere in the tree.</summary>
    private static string? FindString(JsonElement e, string name)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in e.EnumerateObject())
                {
                    if (p.Name == name && p.Value.ValueKind == JsonValueKind.String)
                        return p.Value.GetString();
                    if (FindString(p.Value, name) is { } found)
                        return found;
                }
                break;
            case JsonValueKind.Array:
                foreach (var x in e.EnumerateArray())
                    if (FindString(x, name) is { } found)
                        return found;
                break;
        }
        return null;
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
