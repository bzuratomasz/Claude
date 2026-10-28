using System.Runtime.CompilerServices;
using System.Threading.Channels;
using SearchCar.Core.Geo;
using SearchCar.Core.Models;

namespace SearchCar.Core.Listings;

/// <summary>Queries all providers in parallel and filters their offers by the criteria (including distance).</summary>
public sealed class CarSearchService(IEnumerable<ICarListingProvider> providers)
{
    private readonly IReadOnlyList<ICarListingProvider> _providers = providers.ToList();

    /// <summary>Yields each site's results as soon as that site is done, so fast sites show up first.</summary>
    public async IAsyncEnumerable<SearchOutcome> SearchStreamAsync(SearchCriteria criteria, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateUnbounded<SearchOutcome>();
        var tasks = _providers.Select(async p =>
        {
            var outcome = await SearchOneAsync(p, criteria, cancellationToken).ConfigureAwait(false);
            await channel.Writer.WriteAsync(outcome, cancellationToken).ConfigureAwait(false);
        }).ToList();
        _ = Task.WhenAll(tasks).ContinueWith(t => channel.Writer.TryComplete(t.Exception?.GetBaseException()), TaskScheduler.Default);

        await foreach (var outcome in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            yield return outcome;
    }

    /// <summary>All sites together, sorted by distance (used by tests and simple callers).</summary>
    public async Task<SearchOutcome> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var all = new List<SearchOutcome>();
        await foreach (var outcome in SearchStreamAsync(criteria, cancellationToken).ConfigureAwait(false))
            all.Add(outcome);

        return new SearchOutcome("",
            all.SelectMany(o => o.Results).OrderBy(r => r.DistanceKm ?? double.MaxValue).ThenBy(r => r.Listing.ComparablePricePln).ToList(),
            all.SelectMany(o => o.Notes).ToList(),
            all.SelectMany(o => o.Links).ToList());
    }

    public async Task<CarListing> LoadDetailsAsync(CarListing listing, CancellationToken cancellationToken = default)
    {
        var provider = _providers.FirstOrDefault(p => p.Name == listing.Source);
        return provider is null ? listing with { DetailsLoaded = true } : await provider.LoadDetailsAsync(listing, cancellationToken).ConfigureAwait(false);
    }

    public static SearchResult ToResult(CarListing listing, GeoPoint center) =>
        new(listing, listing.Location is { } p ? GeoMath.DistanceKm(center, p) : null);

    /// <summary>Unknown values (year 0, mileage below 0) pass; they are checked again once the offer page was read.</summary>
    public static bool Matches(SearchCriteria c, SearchResult result)
    {
        var l = result.Listing;
        var yearKnown = l.Year > 0;
        var mileageKnown = l.MileageKm >= 0;
        var price = l.ComparablePricePln;
        return (result.DistanceKm is not double d || d <= c.RadiusKm)
            && (string.IsNullOrWhiteSpace(c.Make) || VehicleCatalog.Key(l.Make) == VehicleCatalog.Key(c.Make))
            && (string.IsNullOrWhiteSpace(c.Model) || VehicleCatalog.Key(l.Model).Contains(VehicleCatalog.Key(c.Model)))
            && (!yearKnown || c.YearFrom is not int yf || l.Year >= yf)
            && (!yearKnown || c.YearTo is not int yt || l.Year <= yt)
            && (!mileageKnown || c.MileageFromKm is not int mf || l.MileageKm >= mf)
            && (!mileageKnown || c.MileageToKm is not int mt || l.MileageKm <= mt)
            && (c.PriceFrom is not decimal pf || price >= pf)
            && (c.PriceTo is not decimal pt || price <= pt)
            && (c.Fuel == FuelType.Any || l.Fuel == FuelType.Any || l.Fuel == c.Fuel)
            && (c.Gearbox == Gearbox.Any || l.Gearbox == Gearbox.Any || l.Gearbox == c.Gearbox);
    }

    private static async Task<SearchOutcome> SearchOneAsync(ICarListingProvider provider, SearchCriteria criteria, CancellationToken ct)
    {
        try
        {
            var found = await provider.SearchAsync(criteria, ct).ConfigureAwait(false);
            var results = found.Listings
                .DistinctBy(l => l.Id)
                .Select(l => ToResult(l, criteria.Center))
                .Where(r => Matches(criteria, r))
                .ToList();
            return new SearchOutcome(provider.Name, results, found.Notes, found.Links);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // One site failing must not hide the results of the others.
            var reason = ex switch
            {
                TaskCanceledException or TimeoutException => "serwis nie odpowiedział w wyznaczonym czasie",
                HttpRequestException { StatusCode: { } code } => $"serwis odpowiedział błędem HTTP {(int)code}",
                HttpRequestException => "brak połączenia z serwisem",
                _ => ex.Message,
            };
            return new SearchOutcome(provider.Name, [], [$"{provider.Name}: wyszukiwanie nie powiodło się - {reason}."], []);
        }
    }
}
