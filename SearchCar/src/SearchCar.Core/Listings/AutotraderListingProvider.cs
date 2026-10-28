using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using SearchCar.Core.Geo;
using SearchCar.Core.Models;

namespace SearchCar.Core.Listings;

/// <summary>
/// Searches autotrader.pl. The site filters by voivodeship (/szukaj/osobowe/marka/model/wojewodztwo/str-N) but not by radius,
/// so each offer's town is geocoded and offers outside the radius are dropped here.
/// </summary>
public sealed partial class AutotraderListingProvider(HttpClient http, PlaceCandidates places, TownLocator towns, TimeSpan? pageDelay = null)
    : ICarListingProvider
{
    public const string BaseUrl = "https://www.autotrader.pl";
    private const int PageSize = 30;

    private readonly PoliteHttp _http = new(http);
    private readonly TimeSpan _pageDelay = pageDelay ?? TimeSpan.FromSeconds(1);
    private readonly ConcurrentDictionary<string, CarListing> _details = new();

    public string Name => "autotrader.pl";

    public async Task<ProviderSearchResult> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var region = (await places.ForAsync(criteria, cancellationToken).ConfigureAwait(false)).FirstOrDefault(p => p.Region is not null)?.Region
                     ?? throw new InvalidOperationException("nie udało się ustalić województwa.");

        var listings = new List<CarListing>();
        Uri? firstUrl = null;
        int total = 0;
        for (var page = 1; page <= Math.Clamp(criteria.MaxPages, 1, OtomotoListingProvider.MaxPages); page++)
        {
            if (page > 1)
                await Task.Delay(_pageDelay, cancellationToken).ConfigureAwait(false);
            var url = BuildSearchUrl(criteria, region, page);
            firstUrl ??= url;
            var html = await _http.GetOrNullAsync(url, cancellationToken).ConfigureAwait(false);
            if (html is null)
                break; // no such page (or make/model unknown to the site)
            var found = AutotraderPage.ParseSearch(html, Name);
            if (page == 1)
                total = AutotraderPage.TotalCount(html) ?? found.Count;
            listings.AddRange(found);
            if (found.Count < PageSize)
                break;
        }

        // The site only knows the town: place each offer at its town and keep those within the radius.
        var located = await Task.WhenAll(listings.Select(async l =>
            l with { Location = await towns.LocateAsync(l.City, l.Region, cancellationToken).ConfigureAwait(false) })).ConfigureAwait(false);
        var near = located.Where(l => l.Location is { } p && GeoMath.DistanceKm(criteria.Center, p) <= criteria.RadiusKm).ToList();

        var notes = new List<string>
        {
            $"autotrader.pl: województwo {region}; ofert w województwie: {total:N0}, odczytano: {listings.Count}, w promieniu {criteria.RadiusKm:N0} km: {near.Count}.",
        };
        return new ProviderSearchResult(near, notes, [new SourceLink(Name, firstUrl!)]);
    }

    public async Task<CarListing> LoadDetailsAsync(CarListing listing, CancellationToken cancellationToken = default)
    {
        if (_details.TryGetValue(listing.Id, out var cached))
            return cached;
        var result = listing with { DetailsLoaded = true };
        if (listing.Url is not null &&
            OtomotoPage.FindVinInText(AutotraderPage.MainText(await _http.GetAsync(listing.Url, cancellationToken).ConfigureAwait(false))) is { } vin)
            result = result with { Vin = vin, VinSource = VinSource.Description };
        _details[listing.Id] = result;
        return result;
    }

    public static Uri BuildSearchUrl(SearchCriteria c, string region, int page)
    {
        var path = new List<string> { "szukaj", "osobowe" };
        if (!string.IsNullOrWhiteSpace(c.Make))
        {
            path.Add(VehicleCatalog.Slug(c.Make));
            if (!string.IsNullOrWhiteSpace(c.Model))
                path.Add(VehicleCatalog.Slug(c.Model));
        }
        path.Add(VehicleCatalog.Slug(region));
        if (page > 1)
            path.Add($"str-{page}");
        return new Uri($"{BaseUrl}/{string.Join("/", path.Select(Uri.EscapeDataString))}");
    }
}

internal static partial class AutotraderPage
{
    [GeneratedRegex("""<div class="offer-card offer-card--[^"]*">(.*?)(?=<div class="offer-card offer-card--|</main>|$)""", RegexOptions.Singleline)]
    private static partial Regex CardRegex();

    [GeneratedRegex("""href="(/oferta/[^"#]+)" """)]
    private static partial Regex LinkRegex();

    [GeneratedRegex("""<h2>\s*(.*?)\s*</h2>""", RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    // Plain <p> right after the title holds the version; partner cards (e.g. AAA Auto) have a logo block instead.
    [GeneratedRegex("""</h2>\s*<p>\s*([^<]*?)\s*</p>""")]
    private static partial Regex VersionRegex();

    [GeneratedRegex("""offer-card__price">\s*<strong>\s*([^<]+)</strong>""")]
    private static partial Regex PriceRegex();

    [GeneratedRegex("""offer-detail--city'>([^<]*)</span>\s*<span[^>]*>\(([^)]*)\)</span>""")]
    private static partial Regex PlaceRegex();

    [GeneratedRegex("""<span title='[^']*' class='offer-detail'>([^<]*)</span>""")]
    private static partial Regex DetailRegex();

    [GeneratedRegex("""-(\d+)$""")]
    private static partial Regex IdRegex();

    [GeneratedRegex("""Wszystkie \((\d+)\)""")]
    private static partial Regex TotalRegex();

    [GeneratedRegex("<script.*?</script>|<footer.*?</footer>", RegexOptions.Singleline)]
    private static partial Regex NoiseRegex();

    public static IReadOnlyList<CarListing> ParseSearch(string html, string sourceName)
    {
        var list = new List<CarListing>();
        foreach (Match card in CardRegex().Matches(html))
        {
            var c = card.Groups[1].Value;
            var link = LinkRegex().Match(c);
            var title = TitleRegex().Match(c);
            if (!link.Success || !title.Success)
                continue;

            var makeModel = WebUtility.HtmlDecode(title.Groups[1].Value).Trim();
            var place = PlaceRegex().Match(c);
            var details = DetailRegex().Matches(c).Select(m => WebUtility.HtmlDecode(m.Groups[1].Value).Trim()).ToList();

            // Details are "(region)", "2020", "71 427 km", "diesel" in this order.
            var year = details.Select(d => int.TryParse(d, out var y) && y is > 1900 and < 2100 ? y : 0).FirstOrDefault(y => y > 0);
            var kmText = details.FirstOrDefault(d => d.EndsWith("km", StringComparison.Ordinal));
            var fuel = details.Select(FuelParsing.FromText).FirstOrDefault(f => f != FuelType.Any);
            var digits = (string s) => new string(s.Where(char.IsDigit).ToArray());

            var make = KnownMake(makeModel);
            list.Add(new CarListing
            {
                Id = IdRegex().Match(link.Groups[1].Value) is { Success: true } id ? id.Groups[1].Value : link.Groups[1].Value,
                Make = make,
                Model = makeModel.Length > make.Length ? makeModel[make.Length..].Trim() : makeModel,
                Year = year,
                MileageKm = kmText is not null && int.TryParse(digits(kmText), out var km) ? km : -1,
                Price = decimal.TryParse(digits(PriceRegex().Match(c).Groups[1].Value), out var price) ? price : 0,
                Fuel = fuel,
                Headline = $"{makeModel} {WebUtility.HtmlDecode(VersionRegex().Match(c).Groups[1].Value).Trim()}".Trim(),
                City = place.Success ? CultureInfo.GetCultureInfo("pl-PL").TextInfo.ToTitleCase(WebUtility.HtmlDecode(place.Groups[1].Value)) : "",
                Region = place.Success ? place.Groups[2].Value.ToLowerInvariant() : "",
                Source = sourceName,
                Url = new Uri(new Uri(AutotraderListingProvider.BaseUrl), link.Groups[1].Value),
            });
        }
        return list;
    }

    public static int? TotalCount(string html) =>
        TotalRegex().Match(html) is { Success: true } m ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;

    /// <summary>Visible page text without scripts and footer (the footer mentions "VIN" in its links).</summary>
    public static string MainText(string html) => NoiseRegex().Replace(html, " ");

    /// <summary>"Mercedes-Benz Klasa C" → "Mercedes-Benz"; multi-word makes are matched against a few known ones.</summary>
    private static string KnownMake(string makeModel)
    {
        string[] multiWord = ["Alfa Romeo", "Aston Martin", "Land Rover", "Rolls-Royce", "Mercedes-Benz", "DS Automobiles"];
        return multiWord.FirstOrDefault(m => makeModel.StartsWith(m, StringComparison.OrdinalIgnoreCase))
               ?? makeModel.Split(' ', 2)[0];
    }
}
