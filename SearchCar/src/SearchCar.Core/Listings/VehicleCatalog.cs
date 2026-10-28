using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace SearchCar.Core.Listings;

public sealed record CatalogItem(string Id, string Name);

public sealed record CatalogMakeEntry(string Id, string Name, IReadOnlyList<CatalogItem> Models);

/// <summary>
/// All makes and models known to otomoto.pl. A snapshot ships with the app (Data/otomoto-catalog.json)
/// and can be refreshed from the live site with <see cref="FetchFromOtomotoAsync"/>.
/// </summary>
public sealed class VehicleCatalog
{
    private const string ResourceName = "SearchCar.Core.Data.otomoto-catalog.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public VehicleCatalog(IReadOnlyList<CatalogMakeEntry> makes, string fetched)
    {
        Makes = makes;
        Fetched = fetched;
    }

    public IReadOnlyList<CatalogMakeEntry> Makes { get; }

    /// <summary>When the data was taken from otomoto.pl (yyyy-MM-dd).</summary>
    public string Fetched { get; }

    public int ModelCount => Makes.Sum(m => m.Models.Count);

    public CatalogMakeEntry? FindMake(string? nameOrId) =>
        string.IsNullOrWhiteSpace(nameOrId) ? null
        : Makes.FirstOrDefault(m => Key(m.Name) == Key(nameOrId) || m.Id == nameOrId.Trim().ToLowerInvariant());

    public static CatalogItem? FindModel(CatalogMakeEntry make, string? nameOrId) =>
        string.IsNullOrWhiteSpace(nameOrId) ? null
        : make.Models.FirstOrDefault(m => Key(m.Name) == Key(nameOrId) || m.Id == nameOrId.Trim().ToLowerInvariant());

    /// <summary>The snapshot embedded in the assembly.</summary>
    public static VehicleCatalog LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"Missing resource {ResourceName}");
        return Load(stream);
    }

    public static VehicleCatalog Load(Stream json)
    {
        var file = JsonSerializer.Deserialize<CatalogFile>(json, JsonOptions) ?? throw new InvalidDataException("Empty catalog");
        return new VehicleCatalog(file.Makes, file.Fetched);
    }

    /// <summary>Loads <paramref name="path"/> when it exists and is readable, otherwise the embedded snapshot.</summary>
    public static VehicleCatalog LoadFileOrEmbedded(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                var catalog = Load(stream);
                if (catalog.Makes.Count > 0)
                    return catalog;
            }
        }
        catch (Exception)
        {
            // A damaged cache falls back to the snapshot.
        }

        return LoadEmbedded();
    }

    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, new CatalogFile(Fetched, Makes.ToList()), cancellationToken: cancellationToken);
    }

    /// <summary>Downloads the current make/model lists from otomoto.pl's search page filters.</summary>
    public static async Task<VehicleCatalog> FetchFromOtomotoAsync(HttpClient http, TimeProvider? clock = null, CancellationToken cancellationToken = default)
    {
        var html = await http.GetStringAsync(OtomotoListingProvider.BaseUrl + "/osobowe", cancellationToken).ConfigureAwait(false);
        var makes = ParseOtomotoFilters(html);
        if (makes.Count == 0)
            throw new InvalidDataException("otomoto.pl nie zwróciło żadnych marek - układ strony mógł się zmienić.");
        var today = (clock ?? TimeProvider.System).GetLocalNow().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new VehicleCatalog(makes, today);
    }

    internal static IReadOnlyList<CatalogMakeEntry> ParseOtomotoFilters(string html)
    {
        List<CatalogItem>? makes = null;
        var models = new Dictionary<string, List<CatalogItem>>();

        foreach (var data in OtomotoPage.UrqlData(html))
        {
            if (!data.TryGetProperty("filters", out var filters) || !filters.TryGetProperty("states", out var states))
                continue;

            foreach (var state in states.EnumerateArray())
            {
                var filterId = state.GetProperty("filterId").GetString();
                var conditions = state.TryGetProperty("conditions", out var c) && c.ValueKind == JsonValueKind.Array ? c : default;
                var conditionCount = conditions.ValueKind == JsonValueKind.Array ? conditions.GetArrayLength() : 0;

                if (filterId == "filter_enum_make" && conditionCount == 0)
                    makes = Values(state);
                else if (filterId == "filter_enum_model" && conditionCount == 1 &&
                         conditions[0].GetProperty("filterId").GetString() == "filter_enum_make")
                    models[conditions[0].GetProperty("value").GetString()!] = Values(state);
            }
        }

        return (makes ?? [])
            .Select(m => new CatalogMakeEntry(m.Id, m.Name,
                (models.GetValueOrDefault(m.Id) ?? []).Where(x => x.Id != "other").OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        static List<CatalogItem> Values(JsonElement state) =>
            state.TryGetProperty("values", out var groups) && groups.ValueKind == JsonValueKind.Array
                ? groups.EnumerateArray()
                    .Where(g => g.TryGetProperty("values", out var v) && v.ValueKind == JsonValueKind.Array)
                    .SelectMany(g => g.GetProperty("values").EnumerateArray())
                    .Select(v => new CatalogItem(v.GetProperty("id").GetString()!, v.GetProperty("name").GetString()!))
                    .ToList()
                : [];
    }

    /// <summary>Comparison key: lower-case letters and digits only, Polish diacritics folded ("Škoda" == "skoda").</summary>
    public static string Key(string text) => Slug(text).Replace("-", "");

    /// <summary>URL slug as otomoto.pl uses it ("Góra Kalwaria" -> "gora-kalwaria").</summary>
    public static string Slug(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text.Trim().ToLowerInvariant().Replace('ł', 'l').Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
                sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }
        return sb.ToString().Trim('-');
    }

    private sealed record CatalogFile(string Fetched, List<CatalogMakeEntry> Makes);
}
