using SearchCar.Core.Listings;
using SearchCar.Core.Models;

namespace SearchCar.Core.Geo;

/// <summary>
/// Turns "town, voivodeship" from a listing into coordinates, for sites that publish only the town.
/// Larger towns come from the built-in list; the rest are geocoded once per session and cached.
/// </summary>
public sealed class TownLocator(IGeocoder geocoder)
{
    private readonly Dictionary<string, Task<GeoPoint?>> _cache = [];

    public Task<GeoPoint?> LocateAsync(string town, string? region, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(town))
            return Task.FromResult<GeoPoint?>(null);

        var key = $"{VehicleCatalog.Key(town)}|{VehicleCatalog.Key(region ?? "")}";
        lock (_cache)
        {
            if (!_cache.TryGetValue(key, out var task))
                _cache[key] = task = LookUpAsync(town.Trim(), region?.Trim(), CancellationToken.None); // shared by later searches
            return task.WaitAsync(ct);
        }
    }

    private async Task<GeoPoint?> LookUpAsync(string town, string? region, CancellationToken ct)
    {
        var known = PolishCities.All.FirstOrDefault(c => VehicleCatalog.Key(c.Name) == VehicleCatalog.Key(town));
        if (known is not null)
            return known.Point;

        try
        {
            var query = string.IsNullOrEmpty(region) ? $"{town}, Polska" : $"{town}, województwo {region}, Polska";
            return (await geocoder.FindAsync(query, ct).ConfigureAwait(false))?.Point;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}
