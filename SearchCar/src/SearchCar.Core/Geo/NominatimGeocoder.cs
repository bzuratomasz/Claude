using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using SearchCar.Core.Models;

namespace SearchCar.Core.Geo;

/// <summary>Place search using the public OpenStreetMap Nominatim service (max 1 request/second, identifying User-Agent required).</summary>
public sealed class NominatimGeocoder(HttpClient http) : IGeocoder
{
    private const string BaseUrl = "https://nominatim.openstreetmap.org";

    // Nominatim's usage policy: at most one request per second.
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static DateTime _lastRequest = DateTime.MinValue;

    private async Task<T?> GetThrottledAsync<T>(string url, CancellationToken ct)
    {
        await Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var wait = _lastRequest.AddSeconds(1.1) - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct).ConfigureAwait(false);
            return await http.GetFromJsonAsync<T>(url, ct).ConfigureAwait(false);
        }
        finally
        {
            _lastRequest = DateTime.UtcNow;
            Gate.Release();
        }
    }

    public async Task<GeocodeResult?> FindAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        var url = $"{BaseUrl}/search?format=json&limit=1&accept-language=pl&q={Uri.EscapeDataString(query.Trim())}";
        var places = await GetThrottledAsync<Place[]>(url, cancellationToken).ConfigureAwait(false);
        var place = places?.FirstOrDefault();
        if (place is null)
            return null;

        return new GeocodeResult(
            new GeoPoint(double.Parse(place.Lat, CultureInfo.InvariantCulture), double.Parse(place.Lon, CultureInfo.InvariantCulture)),
            place.DisplayName);
    }

    public async Task<ReverseGeocodeResult> ReverseAsync(GeoPoint point, CancellationToken cancellationToken = default)
    {
        var url = string.Create(CultureInfo.InvariantCulture,
            $"{BaseUrl}/reverse?format=json&zoom=14&accept-language=pl&lat={point.Latitude}&lon={point.Longitude}");
        var place = await GetThrottledAsync<ReversePlace>(url, cancellationToken).ConfigureAwait(false);
        var a = place?.Address;
        if (a is null)
            return ReverseGeocodeResult.Empty;

        string?[] names = [a.City, a.Town, a.Village, a.Municipality?.Replace("gmina ", "", StringComparison.OrdinalIgnoreCase)];
        var region = a.State?.Replace("województwo", "", StringComparison.OrdinalIgnoreCase).Trim();
        return new ReverseGeocodeResult(
            names.Where(n => !string.IsNullOrWhiteSpace(n)).Select(n => n!.Trim()).Distinct().ToList(),
            string.IsNullOrEmpty(region) ? null : region);
    }

    private sealed record Place(
        [property: JsonPropertyName("lat")] string Lat,
        [property: JsonPropertyName("lon")] string Lon,
        [property: JsonPropertyName("display_name")] string DisplayName);

    private sealed record ReversePlace([property: JsonPropertyName("address")] Address? Address);

    private sealed record Address(
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("town")] string? Town,
        [property: JsonPropertyName("village")] string? Village,
        [property: JsonPropertyName("municipality")] string? Municipality,
        [property: JsonPropertyName("state")] string? State);
}
