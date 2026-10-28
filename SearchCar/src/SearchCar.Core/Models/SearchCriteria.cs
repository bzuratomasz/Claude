namespace SearchCar.Core.Models;

public sealed record SearchCriteria
{
    public string? Make { get; init; }
    public string? Model { get; init; }
    public int? YearFrom { get; init; }
    public int? YearTo { get; init; }
    public int? MileageFromKm { get; init; }
    public int? MileageToKm { get; init; }
    public decimal? PriceFrom { get; init; }
    public decimal? PriceTo { get; init; }
    public FuelType Fuel { get; init; } = FuelType.Any;
    public Gearbox Gearbox { get; init; } = Gearbox.Any;
    public required GeoPoint Center { get; init; }

    /// <summary>Name of the place at <see cref="Center"/>, when known (helps sites that search by city).</summary>
    public string? CenterName { get; init; }

    public double RadiusKm { get; init; } = 50;

    /// <summary>How many result pages to read from each site.</summary>
    public int MaxPages { get; init; } = 2;
}
