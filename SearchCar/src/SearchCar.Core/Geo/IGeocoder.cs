using SearchCar.Core.Models;

namespace SearchCar.Core.Geo;

public sealed record GeocodeResult(GeoPoint Point, string DisplayName);

/// <param name="Names">Names of the place, most specific first (village/town/city, then municipality).</param>
/// <param name="Region">Polish voivodeship without the word "województwo", e.g. "mazowieckie"; null when unknown.</param>
public sealed record ReverseGeocodeResult(IReadOnlyList<string> Names, string? Region)
{
    public static ReverseGeocodeResult Empty { get; } = new([], null);
}

public interface IGeocoder
{
    Task<GeocodeResult?> FindAsync(string query, CancellationToken cancellationToken = default);

    Task<ReverseGeocodeResult> ReverseAsync(GeoPoint point, CancellationToken cancellationToken = default);
}
