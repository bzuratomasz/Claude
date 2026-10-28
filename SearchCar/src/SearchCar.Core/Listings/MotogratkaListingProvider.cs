using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using SearchCar.Core.Geo;
using SearchCar.Core.Models;

namespace SearchCar.Core.Listings;

/// <summary>
/// Searches motogratka.pl. Its robots.txt forbids automated paging (page=), radius (promien=) and location filters,
/// so only the first results page for the town in the path (/motoryzacja/osobowe/marka/model/miasto) is read.
/// </summary>
public sealed partial class MotogratkaListingProvider(HttpClient http, PlaceCandidates places, TownLocator towns) : ICarListingProvider
{
    public const string BaseUrl = "https://motogratka.pl";

    private readonly PoliteHttp _http = new(http);
    private readonly ConcurrentDictionary<string, CarListing> _details = new();

    public string Name => "motogratka.pl";

    public async Task<ProviderSearchResult> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        foreach (var place in (await places.ForAsync(criteria, cancellationToken).ConfigureAwait(false)).Take(4))
        {
            var url = BuildSearchUrl(criteria, place.Name);
            var html = await _http.GetOrNullAsync(url, cancellationToken).ConfigureAwait(false);
            if (html is null || MotogratkaPage.ParseSearch(html, Name) is not { Count: > 0 } found)
                continue;

            var located = await Task.WhenAll(found.Select(async l =>
                l with { Location = await towns.LocateAsync(l.City, l.Region, cancellationToken).ConfigureAwait(false) })).ConfigureAwait(false);
            var notes = new List<string>
            {
                $"motogratka.pl: pierwsza strona ogłoszeń z miejscowości {place.Name} ({found.Count}); zasady serwisu nie pozwalają na automatyczne przeglądanie kolejnych stron i promienia.",
            };
            return new ProviderSearchResult(located, notes, [new SourceLink(Name, url)]);
        }

        return new ProviderSearchResult([], ["motogratka.pl: brak ogłoszeń dla tej miejscowości."], []);
    }

    public async Task<CarListing> LoadDetailsAsync(CarListing listing, CancellationToken cancellationToken = default)
    {
        if (_details.TryGetValue(listing.Id, out var cached))
            return cached;
        var result = listing with { DetailsLoaded = true };
        if (listing.Url is not null)
        {
            var html = await _http.GetAsync(listing.Url, cancellationToken).ConfigureAwait(false);
            if (OtomotoPage.FindVinInText(MotogratkaPage.MainText(html)) is { } vin)
                result = result with { Vin = vin, VinSource = VinSource.Description };
        }
        _details[listing.Id] = result;
        return result;
    }

    public static Uri BuildSearchUrl(SearchCriteria c, string town)
    {
        var path = new List<string> { "motoryzacja", "osobowe" };
        if (!string.IsNullOrWhiteSpace(c.Make))
        {
            path.Add(VehicleCatalog.Slug(c.Make));
            if (!string.IsNullOrWhiteSpace(c.Model))
                path.Add(VehicleCatalog.Slug(c.Model));
        }
        path.Add(VehicleCatalog.Slug(town));
        return new Uri($"{BaseUrl}/{string.Join("/", path.Select(Uri.EscapeDataString))}");
    }
}

internal static partial class MotogratkaPage
{
    [GeneratedRegex("""<script[^>]*application/ld\+json[^>]*>(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex LdJsonRegex();

    [GeneratedRegex("""id="item-(\d+)".*?teaserUnified__location"[^>]*>\s*([^<]+?)\s*</span>""", RegexOptions.Singleline)]
    private static partial Regex LocationRegex();

    [GeneratedRegex("""/ob/(\d+)""")]
    private static partial Regex IdRegex();

    [GeneratedRegex("<script.*?</script>|<footer.*?</footer>|<header.*?</header>", RegexOptions.Singleline)]
    private static partial Regex NoiseRegex();

    public static IReadOnlyList<CarListing> ParseSearch(string html, string sourceName)
    {
        // "item-49240331" → "Warszawa, mazowieckie"
        var places = LocationRegex().Matches(html)
            .GroupBy(m => m.Groups[1].Value)
            .ToDictionary(g => g.Key, g => WebUtility.HtmlDecode(g.First().Groups[2].Value));

        var list = new List<CarListing>();
        foreach (Match m in LdJsonRegex().Matches(html))
        {
            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(m.Groups[1].Value);
                root = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                continue;
            }
            if (root.ValueKind != JsonValueKind.Object || Str(root, "@type") != "Product" ||
                !root.TryGetProperty("offers", out var agg) || !agg.TryGetProperty("offers", out var offers))
                continue;

            foreach (var offer in offers.EnumerateArray())
            {
                if (!offer.TryGetProperty("itemOffered", out var car) || !car.TryGetProperty("offers", out var inner))
                    continue;
                var url = Str(inner, "url");
                var id = url is not null && IdRegex().Match(url) is { Success: true } idm ? idm.Groups[1].Value : null;
                if (id is null)
                    continue;

                var place = places.GetValueOrDefault(id, "").Split(',', 2);
                var mileage = car.TryGetProperty("mileageFromOdometer", out var odo) ? Str(odo, "value") : null;
                list.Add(new CarListing
                {
                    Id = id,
                    Make = Str(car, "brand") ?? "",
                    Model = Str(car, "model") ?? "",
                    Year = car.TryGetProperty("vehicleModelDate", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetInt32() : 0,
                    MileageKm = int.TryParse(new string((mileage ?? "").Where(char.IsDigit).ToArray()), out var km) ? km : -1,
                    Price = inner.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : 0,
                    Currency = Str(inner, "priceCurrency") ?? "PLN",
                    Fuel = FuelParsing.FromText(Str(car, "fuelType")),
                    Headline = WebUtility.HtmlDecode(Str(car, "name") ?? ""),
                    City = place[0].Trim(),
                    Region = place.Length > 1 ? place[1].Trim() : "",
                    Source = sourceName,
                    Url = new Uri(url!.Replace("http://", "https://")),
                });
            }
        }
        return list;
    }

    public static string MainText(string html) => NoiseRegex().Replace(html, " ");

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
