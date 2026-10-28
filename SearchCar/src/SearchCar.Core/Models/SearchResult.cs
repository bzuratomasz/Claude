namespace SearchCar.Core.Models;

/// <summary>A listing with its distance from the search centre (null when the listing has no position yet).</summary>
public sealed record SearchResult(CarListing Listing, double? DistanceKm);

/// <summary>A search the user can open on a website, e.g. "otomoto.pl" → https://www.otomoto.pl/osobowe/...</summary>
public sealed record SourceLink(string Label, Uri Url);

/// <summary>What one site returned: matching offers plus human-readable notes.</summary>
public sealed record SearchOutcome(string Source, IReadOnlyList<SearchResult> Results, IReadOnlyList<string> Notes, IReadOnlyList<SourceLink> Links);
