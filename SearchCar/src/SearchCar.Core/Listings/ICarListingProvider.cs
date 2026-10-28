using SearchCar.Core.Models;

namespace SearchCar.Core.Listings;

/// <param name="Listings">Offers found (may include some outside the criteria; the search service filters again).</param>
/// <param name="Notes">Messages worth showing the user, e.g. which town the site searched around.</param>
/// <param name="Links">The same search on the provider's website, for opening in a browser.</param>
public sealed record ProviderSearchResult(IReadOnlyList<CarListing> Listings, IReadOnlyList<string> Notes, IReadOnlyList<SourceLink> Links)
{
    public static ProviderSearchResult Of(IReadOnlyList<CarListing> listings) => new(listings, [], []);
}

/// <summary>A source of car offers (a marketplace, a dealer feed...).</summary>
public interface ICarListingProvider
{
    string Name { get; }

    Task<ProviderSearchResult> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the offer page for data the results page does not carry (VIN, exact location).
    /// Providers that return complete listings keep the default.
    /// </summary>
    Task<CarListing> LoadDetailsAsync(CarListing listing, CancellationToken cancellationToken = default) =>
        Task.FromResult(listing with { DetailsLoaded = true });
}
