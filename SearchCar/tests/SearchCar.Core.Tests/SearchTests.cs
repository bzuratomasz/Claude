using SearchCar.Core.Geo;
using SearchCar.Core.Listings;
using SearchCar.Core.Models;

namespace SearchCar.Core.Tests;

public class SearchTests
{
    private static readonly GeoPoint Warsaw = new(52.2297, 21.0122);
    private static readonly GeoPoint Krakow = new(50.0647, 19.9450);

    [Fact]
    public void Distance_Warsaw_Krakow_is_about_252_km() =>
        Assert.InRange(GeoMath.DistanceKm(Warsaw, Krakow), 250, 254);

    [Fact]
    public void Destination_lands_at_requested_distance()
    {
        var p = GeoMath.Destination(Warsaw, 135, 40);
        Assert.InRange(GeoMath.DistanceKm(Warsaw, p), 39.99, 40.01);
        Assert.Equal("pd.-wsch.", GeoMath.CompassPoint(GeoMath.BearingDeg(Warsaw, p)));
    }

    private static CarListing Car(string id, string make, string model, int year, int km, decimal price, GeoPoint at) => new()
    {
        Id = id, Make = make, Model = model, Year = year, MileageKm = km, Price = price, Location = at, Source = "test",
    };

    private sealed class FakeProvider(params CarListing[] listings) : ICarListingProvider
    {
        public string Name => "test";
        public Task<ProviderSearchResult> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProviderSearchResult(listings, ["note"], []));
    }

    private sealed class FailingProvider : ICarListingProvider
    {
        public string Name => "broken";
        public Task<ProviderSearchResult> SearchAsync(SearchCriteria criteria, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("x", null, System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Search_respects_every_criterion_and_sorts_by_distance()
    {
        var near = GeoMath.Destination(Krakow, 0, 10);
        var mid = GeoMath.Destination(Krakow, 90, 40);
        var provider = new FakeProvider(
            Car("ok-far", "Skoda", "Octavia", 2018, 120_000, 50_000, mid),
            Car("ok-near", "Škoda", "Octavia", 2016, 180_000, 40_000, near),
            Car("too-old", "Skoda", "Octavia", 2009, 120_000, 20_000, near),
            Car("too-far", "Skoda", "Octavia", 2018, 120_000, 50_000, GeoMath.Destination(Krakow, 0, 120)),
            Car("too-expensive", "Skoda", "Octavia", 2020, 50_000, 150_000, near),
            Car("other-model", "Skoda", "Fabia", 2018, 50_000, 30_000, near));
        var criteria = new SearchCriteria
        {
            Make = "Skoda", Model = "Octavia", YearFrom = 2012, YearTo = 2022, MileageToKm = 250_000, PriceTo = 120_000,
            Center = Krakow, RadiusKm = 80,
        };

        var results = (await new CarSearchService([provider]).SearchAsync(criteria)).Results;

        Assert.Equal(["ok-near", "ok-far"], results.Select(r => r.Listing.Id));
    }

    [Fact]
    public async Task A_failing_site_reports_a_polish_note_and_does_not_break_the_others()
    {
        var outcome = await new CarSearchService([new FailingProvider(), new FakeProvider(Car("a", "Skoda", "Octavia", 2018, 1, 1, Krakow))])
            .SearchAsync(new SearchCriteria { Center = Krakow });

        Assert.Single(outcome.Results);
        Assert.Contains(outcome.Notes, n => n == "broken: wyszukiwanie nie powiodło się - serwis odpowiedział błędem HTTP 403.");
    }
}
