using System.Net.Http.Json;
using System.Text.Json;

namespace SearchCar.Core.Listings;

public interface IExchangeRates
{
    /// <summary>How many PLN one unit of <paramref name="currency"/> is worth.</summary>
    Task<decimal> ToPlnAsync(string currency, CancellationToken cancellationToken = default);
}

/// <summary>Average exchange rates (table A) from the National Bank of Poland API, cached for the session.</summary>
public sealed class NbpExchangeRates(HttpClient http) : IExchangeRates
{
    // Used only when the NBP API cannot be reached; good enough to compare prices roughly.
    private static readonly Dictionary<string, decimal> Fallback = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EUR"] = 4.30m, ["CHF"] = 4.60m, ["CZK"] = 0.17m, ["USD"] = 3.90m,
    };

    private readonly Dictionary<string, decimal> _cache = new(StringComparer.OrdinalIgnoreCase) { ["PLN"] = 1m };

    public async Task<decimal> ToPlnAsync(string currency, CancellationToken cancellationToken = default)
    {
        lock (_cache)
        {
            if (_cache.TryGetValue(currency, out var cached))
                return cached;
        }

        decimal rate;
        try
        {
            using var doc = await http.GetFromJsonAsync<JsonDocument>(
                $"https://api.nbp.pl/api/exchangerates/rates/a/{Uri.EscapeDataString(currency.ToLowerInvariant())}/?format=json",
                cancellationToken).ConfigureAwait(false);
            rate = doc!.RootElement.GetProperty("rates")[0].GetProperty("mid").GetDecimal();
        }
        catch (Exception ex) when (ex is not OperationCanceledException && Fallback.ContainsKey(currency))
        {
            rate = Fallback[currency];
        }

        lock (_cache)
            _cache[currency] = rate;
        return rate;
    }
}
