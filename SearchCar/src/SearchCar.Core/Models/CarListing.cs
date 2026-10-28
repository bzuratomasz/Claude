namespace SearchCar.Core.Models;

/// <summary>A single car offer returned by a listing provider.</summary>
public sealed record CarListing
{
    public required string Id { get; init; }
    public required string Make { get; init; }
    public required string Model { get; init; }
    public required int Year { get; init; }
    public required int MileageKm { get; init; }
    public required decimal Price { get; init; }
    public string Currency { get; init; } = "PLN";

    /// <summary>Price converted to PLN for foreign-currency offers (null when the offer is already in PLN).</summary>
    public decimal? PricePln { get; init; }

    /// <summary>Price in PLN for comparing and filtering, whatever the offer's currency.</summary>
    public decimal ComparablePricePln => Currency == "PLN" ? Price : PricePln ?? Price;
    public FuelType Fuel { get; init; }
    public Gearbox Gearbox { get; init; }

    /// <summary>The seller's own headline, e.g. "Skoda Octavia 1.5 TSI ACT Ambition".</summary>
    public string? Headline { get; init; }

    /// <summary>VIN as published by the seller; null or empty when it is not known.</summary>
    public string? Vin { get; init; }

    public VinSource VinSource { get; init; }

    /// <summary>The site holds a VIN for this offer but only reveals it to a person (e.g. behind reCAPTCHA).</summary>
    public bool VinHiddenOnSite { get; init; }

    /// <summary>The site states the offer data was checked against the CEPIK vehicle register.</summary>
    public bool? CepikVerified { get; init; }

    /// <summary>Exact or approximate position; null until known (some sites only give a city on the results page).</summary>
    public GeoPoint? Location { get; init; }

    public string City { get; init; } = "";
    public string Region { get; init; } = "";
    public string Seller { get; init; } = "";
    public string Source { get; init; } = "";
    public Uri? Url { get; init; }

    /// <summary>True once the offer page has been read (for providers that fetch details lazily).</summary>
    public bool DetailsLoaded { get; init; }

    public string Title => string.IsNullOrWhiteSpace(Make)
        ? Headline ?? Id
        : Year > 0 ? $"{Year} {Make} {Model}" : $"{Make} {Model}";
}
