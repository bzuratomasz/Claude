namespace SearchCar.Core.Listings;

/// <summary>Per-site HTTP access: at most two requests at a time, so a search never hammers a website.</summary>
public sealed class PoliteHttp(HttpClient http, int maxParallel = 2)
{
    private readonly SemaphoreSlim _gate = new(maxParallel, maxParallel);

    /// <summary>The page body, or null for 404 (e.g. a town the site does not know).</summary>
    public async Task<string?> GetOrNullAsync(Uri url, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> GetAsync(Uri url, CancellationToken ct) =>
        await GetOrNullAsync(url, ct).ConfigureAwait(false)
        ?? throw new HttpRequestException($"Strona nie istnieje (404): {url}");
}
