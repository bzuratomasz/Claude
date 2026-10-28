using SearchCar.Core.Listings;
using SearchCar.Core.Models;

namespace SearchCar.Core.Geo;

/// <param name="Name">Town name, e.g. "Piaseczno".</param>
/// <param name="Region">Voivodeship, e.g. "mazowieckie" (null when unknown).</param>
/// <param name="OffsetKm">How far the town is from the user's point (0 when it is the place itself).</param>
public sealed record PlaceCandidate(string Name, string? Region, double OffsetKm);

/// <summary>
/// Most Polish sites search around a town rather than a point. This lists towns to try, best first:
/// the place the user typed, the place reverse-geocoded at the point, then the nearest larger towns.
/// </summary>
public sealed class PlaceCandidates(IGeocoder geocoder)
{
    private readonly Dictionary<GeoPoint, ReverseGeocodeResult> _cache = [];

    public async Task<IReadOnlyList<PlaceCandidate>> ForAsync(SearchCriteria c, CancellationToken ct)
    {
        var reverse = await ReverseAsync(c.Center, ct).ConfigureAwait(false);
        var nearest = PolishCities.NearestTo(c.Center).Take(3).ToList();
        var region = reverse.Region ?? nearest[0].City.Region;

        var list = new List<PlaceCandidate>();
        if (!string.IsNullOrWhiteSpace(c.CenterName))
            list.Add(new(c.CenterName!, region, 0));
        list.AddRange(reverse.Names.Select(n => new PlaceCandidate(n, region, 0)));
        list.AddRange(nearest.Select(n => new PlaceCandidate(n.City.Name, n.City.Region, n.DistanceKm)));
        return list.DistinctBy(p => VehicleCatalog.Slug(p.Name)).ToList();
    }

    private async Task<ReverseGeocodeResult> ReverseAsync(GeoPoint point, CancellationToken ct)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(point, out var hit))
                return hit;
        }

        ReverseGeocodeResult result;
        try
        {
            result = await geocoder.ReverseAsync(point, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            result = ReverseGeocodeResult.Empty; // the built-in town list still works
        }

        lock (_cache)
            _cache[point] = result;
        return result;
    }
}
