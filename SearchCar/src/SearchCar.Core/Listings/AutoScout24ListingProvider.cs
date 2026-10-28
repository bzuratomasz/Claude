using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using SearchCar.Core.Models;

namespace SearchCar.Core.Listings;

/// <summary>
/// Searches AutoScout24 (mostly German, Austrian and other EU dealers) by coordinates and radius, so it finds cars
/// across the border - useful for imports. Prices are in EUR and converted to PLN with the NBP rate.
/// Only the public list (/lst/...) and offer (/oferta/...) pages are read; both are allowed by the site's robots.txt.
/// </summary>
public sealed class AutoScout24ListingProvider(HttpClient http, IExchangeRates rates, TimeSpan? pageDelay = null) : ICarListingProvider
{
    public const string BaseUrl = "https://www.autoscout24.pl";
    private const int PageSize = 20;
    private const int MaxRadiusKm = 500;

    private readonly PoliteHttp _http = new(http);
    private readonly TimeSpan _pageDelay = pageDelay ?? TimeSpan.FromSeconds(1);
    private readonly ConcurrentDictionary<string, CarListing> _details = new();

    public string Name => "AutoScout24";

    public async Task<ProviderSearchResult> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var eur = await rates.ToPlnAsync("EUR", cancellationToken).ConfigureAwait(false);
        var notes = new List<string>();
        var listings = new List<CarListing>();
        Uri? firstUrl = null;
        int total = 0, pages = 1;

        for (var page = 1; page <= Math.Clamp(criteria.MaxPages, 1, OtomotoListingProvider.MaxPages) && page <= pages; page++)
        {
            if (page > 1)
                await Task.Delay(_pageDelay, cancellationToken).ConfigureAwait(false);

            var url = BuildSearchUrl(criteria, eur, page);
            firstUrl ??= url;
            var result = AutoScout24Page.ParseSearch(await _http.GetAsync(url, cancellationToken).ConfigureAwait(false), Name, eur, criteria.RadiusKm);
            (total, pages) = (result.Total, Math.Max(1, result.Pages));
            listings.AddRange(result.Listings);
            if (result.FartherDropped)
                break; // the site widened the search because nothing was within the radius - further pages are farther still
        }

        notes.Add(listings.Count == 0
            ? $"AutoScout24: brak ofert w promieniu {criteria.RadiusKm:N0} km (serwis ma głównie auta z zagranicy - zwiększ promień, np. przy granicy)."
            : $"AutoScout24: ofert w promieniu: {listings.Count} (głównie zagraniczne; ceny w EUR przeliczone po kursie NBP {eur:N4} zł).");
        return new ProviderSearchResult(listings, notes, [new SourceLink(Name, firstUrl!)]);
    }

    public async Task<CarListing> LoadDetailsAsync(CarListing listing, CancellationToken cancellationToken = default)
    {
        if (_details.TryGetValue(listing.Id, out var cached))
            return cached;
        if (listing.Url is null)
            return listing with { DetailsLoaded = true };

        var detailed = AutoScout24Page.ApplyDetails(listing, await _http.GetAsync(listing.Url, cancellationToken).ConfigureAwait(false));
        _details[listing.Id] = detailed;
        return detailed;
    }

    /// <summary>e.g. https://www.autoscout24.pl/lst/skoda/octavia?atype=C&amp;lat=52.23&amp;lon=21.01&amp;zipr=50&amp;fregfrom=2015&amp;page=2</summary>
    public static Uri BuildSearchUrl(SearchCriteria c, decimal eurRate, int page)
    {
        var path = "/lst";
        if (!string.IsNullOrWhiteSpace(c.Make))
        {
            path += "/" + Uri.EscapeDataString(VehicleCatalog.Slug(c.Make));
            if (!string.IsNullOrWhiteSpace(c.Model))
                path += "/" + Uri.EscapeDataString(VehicleCatalog.Slug(c.Model));
        }

        var q = new List<string>
        {
            "atype=C", "sort=standard", "desc=0", "ustate=N%2CU",
            string.Create(CultureInfo.InvariantCulture, $"lat={c.Center.Latitude:F5}"),
            string.Create(CultureInfo.InvariantCulture, $"lon={c.Center.Longitude:F5}"),
            $"zipr={(int)Math.Min(Math.Ceiling(c.RadiusKm), MaxRadiusKm)}",
        };
        void Add(string key, decimal? value)
        {
            if (value is decimal v)
                q.Add(string.Create(CultureInfo.InvariantCulture, $"{key}={v:0}"));
        }
        Add("fregfrom", c.YearFrom);
        Add("fregto", c.YearTo);
        Add("kmfrom", c.MileageFromKm);
        Add("kmto", c.MileageToKm);
        Add("pricefrom", c.PriceFrom is decimal pf ? Math.Floor(pf / eurRate) : null);
        Add("priceto", c.PriceTo is decimal pt ? Math.Ceiling(pt / eurRate) : null);
        if (c.Gearbox != Gearbox.Any)
            q.Add(c.Gearbox == Gearbox.Manual ? "gear=M" : "gear=A");
        if (page > 1)
            q.Add($"page={page}");

        return new Uri($"{BaseUrl}{path}?{string.Join("&", q)}");
    }
}

/// <param name="FartherDropped">Some offers were farther than the radius (AutoScout24 widens an empty search on its own).</param>
internal sealed record AutoScout24SearchPage(IReadOnlyList<CarListing> Listings, int Total, int Pages, bool FartherDropped);

internal static class AutoScout24Page
{
    public static AutoScout24SearchPage ParseSearch(string html, string sourceName, decimal eurRate, double maxDistanceKm = double.MaxValue)
    {
        var props = OtomotoPage.NextData(html).GetProperty("props").GetProperty("pageProps");
        var listings = new List<CarListing>();
        var dropped = false;
        if (props.TryGetProperty("listings", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("location", out var loc) && Int(loc, "distanceToSearchLocationInKm") is int km && km > maxDistanceKm)
                {
                    dropped = true;
                    continue;
                }
                if (ParseItem(item, sourceName, eurRate) is { } listing)
                    listings.Add(listing);
            }
        }

        return new AutoScout24SearchPage(listings, Int(props, "numberOfResults") ?? listings.Count, Int(props, "numberOfPages") ?? 1, dropped);
    }

    private static CarListing? ParseItem(JsonElement item, string sourceName, decimal eurRate)
    {
        if (!item.TryGetProperty("vehicle", out var vehicle) || !item.TryGetProperty("tracking", out var tracking))
            return null;

        var make = Str(vehicle, "make");
        var model = Str(vehicle, "model");
        if (make is null || model is null)
            return null;

        // "10-2019" -> 2019
        var firstReg = Str(tracking, "firstRegistration") ?? "";
        var year = int.TryParse(firstReg.Split('-').LastOrDefault(), out var y) ? y : 0;
        var mileage = int.TryParse(Str(tracking, "mileage"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var km) ? km : -1;

        decimal price = 0;
        if (item.TryGetProperty("price", out var p) && p.TryGetProperty("priceRaw", out var raw) && raw.ValueKind == JsonValueKind.Number)
            price = raw.GetDecimal();
        else if (decimal.TryParse(Str(tracking, "price"), NumberStyles.Number, CultureInfo.InvariantCulture, out var tp))
            price = tp;

        string city = "", seller = "";
        if (item.TryGetProperty("location", out var loc))
        {
            var country = Str(loc, "countryCode");
            city = $"{Str(loc, "city")}{(country is null or "PL" ? "" : $" ({country})")}".Trim();
        }
        if (item.TryGetProperty("seller", out var s))
            seller = Str(s, "companyName") ?? (Str(s, "type") == "Private" ? "Osoba prywatna" : "Firma");

        var url = Str(item, "url");
        return new CarListing
        {
            Id = Str(item, "id") ?? Guid.NewGuid().ToString("N"),
            Make = make,
            Model = model,
            Year = year,
            MileageKm = mileage,
            Price = price,
            Currency = "EUR",
            PricePln = Math.Round(price * eurRate, 0),
            Fuel = FuelParsing.FromText(Str(vehicle, "fuel")),
            Gearbox = FuelParsing.GearboxFromText(Str(vehicle, "transmission")),
            Headline = $"{make} {model} {Str(vehicle, "modelVersionInput")}".Trim(),
            City = city,
            Seller = seller,
            Source = sourceName,
            Url = url is null ? null : new Uri(new Uri(AutoScout24ListingProvider.BaseUrl), url),
        };
    }

    /// <summary>Exact position and a VIN from the description (AutoScout24 has no VIN field on its pages).</summary>
    public static CarListing ApplyDetails(CarListing listing, string html)
    {
        var result = listing with { DetailsLoaded = true };
        var props = OtomotoPage.NextData(html).GetProperty("props").GetProperty("pageProps");
        if (!props.TryGetProperty("listingDetails", out var details))
            return result;

        if (details.TryGetProperty("location", out var loc) &&
            loc.TryGetProperty("latitude", out var lat) && lat.ValueKind == JsonValueKind.Number &&
            loc.TryGetProperty("longitude", out var lon) && lon.ValueKind == JsonValueKind.Number)
            result = result with { Location = new GeoPoint(lat.GetDouble(), lon.GetDouble()) };

        if (OtomotoPage.FindVinInText(Str(details, "description")) is { } vin)
            result = result with { Vin = vin, VinSource = VinSource.Description };

        return result;
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
}
