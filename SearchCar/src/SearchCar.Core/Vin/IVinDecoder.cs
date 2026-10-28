namespace SearchCar.Core.Vin;

public sealed record VinDecodeResult
{
    public string? Make { get; init; }
    public string? Model { get; init; }
    public int? ModelYear { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorText { get; init; }

    /// <summary>All non-empty fields returned by the decoder, for display.</summary>
    public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>();

    public bool IsClean => ErrorCode is "0";
}

public interface IVinDecoder
{
    string Name { get; }
    Task<VinDecodeResult> DecodeAsync(string vin, CancellationToken cancellationToken = default);
}
