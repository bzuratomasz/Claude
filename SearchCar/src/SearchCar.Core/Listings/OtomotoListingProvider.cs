using System.Collections.Concurrent;
using System.Globalization;
using SearchCar.Core.Geo;
using SearchCar.Core.Models;

namespace SearchCar.Core.Listings;

/// <summary>
/// Searches otomoto.pl by reading its public search pages (several pages per search) and, per offer, the offer page.
/// Requests are throttled; the VIN field that otomoto protects with reCAPTCHA is never requested - only a VIN the
/// seller wrote in the description is picked up, otherwise the listing is marked <see cref="CarListing.VinHiddenOnSite"/>.
/// </summary>
public sealed class OtomotoListingProvider(HttpClient http, PlaceCandidates places, VehicleCatalog catalog, TimeSpan? pageDelay = null)
    : ICarListingProvider
{
    public const string BaseUrl = "https://www.otomoto.pl";
    public const int MaxPages = 10;

    /// <summary>Radius values otomoto.pl accepts (km).</summary>
    public static IReadOnlyList<int> Distances { get; } = [0, 5, 10, 15, 25, 50, 75, 100, 150, 200, 250, 300];

    public static int MaxRadiusKm => Distances[^1];

    private readonly TimeSpan _pageDelay = pageDelay ?? TimeSpan.FromSeconds(1);
    private readonly PoliteHttp _http = new(http);
    private readonly ConcurrentDictionary<string, CarListing> _details = new();

    public string Name => "otomoto.pl";

    public async Task<ProviderSearchResult> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var notes = new List<string>();
        var candidates = await places.ForAsync(criteria, cancellationToken).ConfigureAwait(false);

        OtomotoSearchPage? first = null;
        Uri? firstUrl = null;
        PlaceCandidate used = new("", null, 0);
        foreach (var place in candidates.Take(4))
        {
            var dist = SnapDistance(criteria.RadiusKm + place.OffsetKm);
            var url = BuildSearchUrl(criteria, place.Name, dist, page: 1);
            var page = OtomotoPage.ParseSearch(await _http.GetAsync(url, cancellationToken).ConfigureAwait(false), Name);
            if (page.AppliedCity is null)
                continue; // otomoto did not know this place and silently searched all of Poland - try the next one

            (first, firstUrl, used) = (page, url, place);
            break;
        }

        if (first is null)
            throw new InvalidOperationException("otomoto.pl nie zna tej lokalizacji; wpisz pobliską miejscowość w wyszukiwarce miejsc.");

        var distance = SnapDistance(criteria.RadiusKm + used.OffsetKm);
        notes.Add(used.OffsetKm > 1
            ? $"otomoto.pl szuka według miejscowości: użyto {first.AppliedCity} ({used.OffsetKm:N0} km od wskazanego punktu), promień {distance} km."
            : $"otomoto.pl: szukanie w promieniu {distance} km od miejscowości {first.AppliedCity}.");
        if (criteria.RadiusKm > MaxRadiusKm)
            notes.Add($"otomoto.pl pozwala na maksymalnie {MaxRadiusKm} km.");

        var listings = new List<CarListing>(first.Listings);
        var pageSize = Math.Max(1, first.PageSize);
        var availablePages = (int)Math.Ceiling(first.TotalCount / (double)pageSize);
        var pagesToRead = Math.Clamp(criteria.MaxPages, 1, MaxPages);

        for (var pageNo = 2; pageNo <= Math.Min(pagesToRead, availablePages); pageNo++)
        {
            await Task.Delay(_pageDelay, cancellationToken).ConfigureAwait(false);
            var url = BuildSearchUrl(criteria, used.Name, distance, pageNo);
            listings.AddRange(OtomotoPage.ParseSearch(await _http.GetAsync(url, cancellationToken).ConfigureAwait(false), Name).Listings);
        }

        var read = Math.Min(pagesToRead, Math.Max(availablePages, 1));
        notes.Add($"otomoto.pl: pasujących ogłoszeń: {first.TotalCount:N0}; odczytano stron: {read} z {Math.Max(availablePages, 1)} (ogłoszeń: {listings.Count})." +
                  (availablePages > read ? " Zwiększ \"Liczba stron wyników\", aby odczytać więcej." : ""));

        return new ProviderSearchResult(listings, notes, [new SourceLink(Name, firstUrl!)]);
    }

    public async Task<CarListing> LoadDetailsAsync(CarListing listing, CancellationToken cancellationToken = default)
    {
        if (_details.TryGetValue(listing.Id, out var cached))
            return cached;
        if (listing.Url is null)
            return listing with { DetailsLoaded = true };

        var html = await _http.GetAsync(listing.Url, cancellationToken).ConfigureAwait(false);
        var detailed = OtomotoPage.ApplyDetails(listing, html);
        _details[listing.Id] = detailed;
        return detailed;
    }

    /// <summary>Builds e.g. https://www.otomoto.pl/osobowe/skoda/octavia/piaseczno?search[dist]=25&amp;page=2.</summary>
    public Uri BuildSearchUrl(SearchCriteria c, string placeName, int distanceKm, int page)
    {
        var path = new List<string> { "osobowe" };
        if (!string.IsNullOrWhiteSpace(c.Make))
        {
            var make = catalog.FindMake(c.Make);
            path.Add(make?.Id ?? VehicleCatalog.Slug(c.Make));
            if (!string.IsNullOrWhiteSpace(c.Model))
                path.Add((make is null ? null : VehicleCatalog.FindModel(make, c.Model))?.Id ?? VehicleCatalog.Slug(c.Model));
        }
        path.Add(VehicleCatalog.Slug(placeName));

        var query = new List<(string Key, string Value)> { ("search[dist]", distanceKm.ToString(CultureInfo.InvariantCulture)) };
        void Add(string key, object? value)
        {
            if (value is not null)
                query.Add((key, Convert.ToString(value, CultureInfo.InvariantCulture)!));
        }
        Add("search[filter_float_year:from]", c.YearFrom);
        Add("search[filter_float_year:to]", c.YearTo);
        Add("search[filter_float_mileage:from]", c.MileageFromKm);
        Add("search[filter_float_mileage:to]", c.MileageToKm);
        Add("search[filter_float_price:from]", c.PriceFrom);
        Add("search[filter_float_price:to]", c.PriceTo);
        Add("search[filter_enum_fuel_type]", FuelToId(c.Fuel));
        Add("search[filter_enum_gearbox]", c.Gearbox switch { Gearbox.Manual => "manual", Gearbox.Automatic => "automatic", _ => null });
        if (page > 1)
            Add("page", page);

        var qs = string.Join("&", query.Select(q => $"{Uri.EscapeDataString(q.Key)}={Uri.EscapeDataString(q.Value)}"));
        return new Uri($"{BaseUrl}/{string.Join("/", path.Select(Uri.EscapeDataString))}?{qs}");
    }

    public static int SnapDistance(double radiusKm) => Distances.FirstOrDefault(d => d >= radiusKm, MaxRadiusKm);

    internal static string? FuelToId(FuelType fuel) => fuel switch
    {
        FuelType.Petrol => "petrol",
        FuelType.PetrolLpg => "petrol-lpg",
        FuelType.PetrolCng => "petrol-cng",
        FuelType.Diesel => "diesel",
        FuelType.Hybrid => "hybrid",
        FuelType.PluginHybrid => "plugin-hybrid",
        FuelType.Electric => "electric",
        FuelType.Ethanol => "etanol",
        FuelType.Hydrogen => "hidrogen",
        _ => null,
    };

    internal static FuelType FuelFromId(string? id) => id switch
    {
        "petrol" => FuelType.Petrol,
        "petrol-lpg" => FuelType.PetrolLpg,
        "petrol-cng" => FuelType.PetrolCng,
        "diesel" => FuelType.Diesel,
        "hybrid" => FuelType.Hybrid,
        "plugin-hybrid" => FuelType.PluginHybrid,
        "electric" => FuelType.Electric,
        "etanol" => FuelType.Ethanol,
        "hidrogen" => FuelType.Hydrogen,
        _ => FuelType.Any,
    };


}
