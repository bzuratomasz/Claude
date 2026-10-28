using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;

namespace SearchCar.Core.Vin;

/// <summary>Decodes VINs with the free US NHTSA vPIC API (https://vpic.nhtsa.dot.gov/api/).</summary>
public sealed class NhtsaVinDecoder(HttpClient http) : IVinDecoder
{
    private static readonly (string Key, string Label)[] DisplayFields =
    [
        ("Make", "Marka"), ("Model", "Model"), ("ModelYear", "Rok modelowy"), ("Trim", "Wersja"),
        ("Manufacturer", "Producent"), ("VehicleType", "Typ pojazdu"), ("BodyClass", "Nadwozie"),
        ("FuelTypePrimary", "Paliwo"), ("DisplacementL", "Silnik (l)"), ("EngineCylinders", "Cylindry"),
        ("EngineHP", "Moc (KM)"), ("TransmissionStyle", "Skrzynia"), ("DriveType", "Napęd"),
        ("PlantCountry", "Kraj fabryki"), ("PlantCity", "Miasto fabryki"),
    ];

    private readonly ConcurrentDictionary<string, VinDecodeResult> _cache = new();

    public string Name => "NHTSA vPIC";

    public async Task<VinDecodeResult> DecodeAsync(string vin, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(vin, out var cached))
            return cached;

        var url = $"https://vpic.nhtsa.dot.gov/api/vehicles/DecodeVinValues/{Uri.EscapeDataString(vin)}?format=json";
        using var doc = await http.GetFromJsonAsync<JsonDocument>(url, cancellationToken).ConfigureAwait(false)
                        ?? throw new InvalidOperationException("Empty response from NHTSA.");

        var row = doc.RootElement.GetProperty("Results")[0];
        string? Get(string name) =>
            row.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.GetString())
                ? p.GetString()!.Trim()
                : null;

        var fields = new Dictionary<string, string>();
        foreach (var (key, label) in DisplayFields)
            if (Get(key) is { } value)
                fields[label] = value;

        var result = new VinDecodeResult
        {
            Make = Get("Make"),
            Model = Get("Model"),
            ModelYear = int.TryParse(Get("ModelYear"), out var year) ? year : null,
            ErrorCode = Get("ErrorCode"),
            ErrorText = Get("ErrorText"),
            Fields = fields,
        };
        _cache[vin] = result;
        return result;
    }
}
