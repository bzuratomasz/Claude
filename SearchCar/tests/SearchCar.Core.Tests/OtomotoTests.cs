using System.Text.Json;
using SearchCar.Core.Geo;
using SearchCar.Core.Listings;
using SearchCar.Core.Models;
using SearchCar.Core.Vin;

namespace SearchCar.Core.Tests;

public class OtomotoTests
{
    private static readonly VehicleCatalog Catalog = VehicleCatalog.LoadEmbedded();

    /// <summary>Wraps page props the way otomoto.pl (Next.js) embeds them.</summary>
    private static string Page(object pageProps) =>
        $"""<html><script id="__NEXT_DATA__" type="application/json">{JsonSerializer.Serialize(new { props = new { pageProps } })}</script></html>""";

    private static string SearchPage(string? city, params object[] nodes)
    {
        var data = new
        {
            advertSearch = new
            {
                totalCount = 70,
                pageInfo = new { pageSize = 32, currentOffset = 0 },
                appliedLocation = city is null ? null : new { city = new { name = city } },
                edges = nodes.Select(n => new { node = n }).ToArray(),
            },
        };
        return Page(new { urqlState = new Dictionary<string, object> { ["1"] = new { data = JsonSerializer.Serialize(data) } } });
    }

    private static object Node(string id, string make, string model, string year, string fuel = "diesel") => new
    {
        id,
        title = $"{make} {model} 2.0 TDI",
        url = $"https://www.otomoto.pl/osobowe/oferta/{id}.html",
        cepikVerified = true,
        location = new { city = new { name = "Piaseczno" }, region = new { name = "Mazowieckie" } },
        price = new { amount = new { units = 45900, currencyCode = "PLN" } },
        seller = new { __typename = "PrivateSeller" },
        parameters = new[]
        {
            new { key = "make", value = make.ToLowerInvariant(), displayValue = make },
            new { key = "model", value = model.ToLowerInvariant(), displayValue = model },
            new { key = "year", value = year, displayValue = year },
            new { key = "mileage", value = "187000", displayValue = "187 000 km" },
            new { key = "fuel_type", value = fuel, displayValue = "Diesel" },
            new { key = "gearbox", value = "manual", displayValue = "Manualna" },
        },
    };

    private static CarListing Listing() => new()
    {
        Id = "1", Make = "Skoda", Model = "Octavia", Year = 2023, MileageKm = 1, Price = 1,
        Url = new Uri("https://www.otomoto.pl/osobowe/oferta/1.html"), Source = "otomoto.pl",
    };

    private static string OfferPage(string description, string encryptedVin = "ReID+eO+vQw3uOeG.1.ypOTxBYp==") => Page(new
    {
        advert = new
        {
            description,
            details = new[] { new { key = "vin", value = encryptedVin } },
            parametersDict = new { has_vin = new { values = new[] { new { value = "1" } } } },
            seller = new
            {
                type = "PROFESSIONAL",
                name = "Auto Dealer",
                location = new { map = new { latitude = 52.1718, longitude = 21.1659 } },
            },
        },
    });

    [Fact]
    public void Embedded_catalog_has_all_otomoto_makes_and_models()
    {
        Assert.True(Catalog.Makes.Count > 150);
        Assert.True(Catalog.ModelCount > 2000);
        var skoda = Catalog.FindMake("Škoda");
        Assert.NotNull(skoda);
        Assert.Equal("octavia", VehicleCatalog.FindModel(skoda!, "Octavia")?.Id);
        Assert.Equal("mercedes-benz", Catalog.FindMake("Mercedes-Benz")?.Id);
    }

    [Theory]
    [InlineData("Góra Kalwaria", "gora-kalwaria")]
    [InlineData("Bielsko-Biała", "bielsko-biala")]
    [InlineData("Łódź", "lodz")]
    [InlineData("Ruda Śląska", "ruda-slaska")]
    public void Slug_matches_otomoto_city_paths(string name, string slug) => Assert.Equal(slug, VehicleCatalog.Slug(name));

    [Theory]
    [InlineData(1, 5)]
    [InlineData(25, 25)]
    [InlineData(60, 75)]
    [InlineData(450, 300)]
    public void SnapDistance_uses_otomoto_radius_values(double radius, int expected) =>
        Assert.Equal(expected, OtomotoListingProvider.SnapDistance(radius));

    [Fact]
    public void BuildSearchUrl_encodes_every_filter_and_page()
    {
        var provider = new OtomotoListingProvider(new HttpClient(), new PlaceCandidates(new NullGeocoder()), Catalog);
        var criteria = new SearchCriteria
        {
            Make = "Skoda", Model = "Octavia", YearFrom = 2015, MileageToKm = 200000, PriceTo = 80000,
            Fuel = FuelType.PetrolLpg, Gearbox = Gearbox.Manual, Center = new GeoPoint(52.07, 21.02),
        };

        var url = provider.BuildSearchUrl(criteria, "Piaseczno", 25, page: 2).ToString();

        Assert.StartsWith("https://www.otomoto.pl/osobowe/skoda/octavia/piaseczno?", url);
        Assert.Contains("search%5Bdist%5D=25", url);
        Assert.Contains("search%5Bfilter_float_year%3Afrom%5D=2015", url);
        Assert.Contains("search%5Bfilter_float_mileage%3Ato%5D=200000", url);
        Assert.Contains("search%5Bfilter_float_price%3Ato%5D=80000", url);
        Assert.Contains("search%5Bfilter_enum_fuel_type%5D=petrol-lpg", url);
        Assert.Contains("search%5Bfilter_enum_gearbox%5D=manual", url);
        Assert.Contains("page=2", url);
    }

    [Fact]
    public void ParseSearch_reads_offers_paging_and_applied_city()
    {
        var page = OtomotoPage.ParseSearch(SearchPage("Piaseczno", Node("6150822386", "Skoda", "Octavia", "2017")), "otomoto.pl");

        Assert.Equal("Piaseczno", page.AppliedCity);
        Assert.Equal(70, page.TotalCount);
        var l = Assert.Single(page.Listings);
        Assert.Equal(("Skoda", "Octavia", 2017, 187000, 45900m), (l.Make, l.Model, l.Year, l.MileageKm, l.Price));
        Assert.Equal((FuelType.Diesel, Gearbox.Manual), (l.Fuel, l.Gearbox));
        Assert.Equal("Osoba prywatna", l.Seller);
        Assert.True(l.CepikVerified);
        Assert.Equal("https://www.otomoto.pl/osobowe/oferta/6150822386.html", l.Url?.ToString());
        Assert.Null(l.Location);
    }

    [Fact]
    public void ParseSearch_reports_unknown_location_as_null_city() =>
        Assert.Null(OtomotoPage.ParseSearch(SearchPage(null), "otomoto.pl").AppliedCity);

    [Fact]
    public void ApplyDetails_takes_vin_from_description_and_location_from_map()
    {
        var detailed = OtomotoPage.ApplyDetails(Listing(), OfferPage("<p>Nr rej. WE8K310</p><p>Nr VIN TMBJR8NX2PY090651</p>"));

        Assert.Equal("TMBJR8NX2PY090651", detailed.Vin);
        Assert.Equal(VinSource.Description, detailed.VinSource);
        Assert.False(detailed.VinHiddenOnSite);
        Assert.Equal(new GeoPoint(52.1718, 21.1659), detailed.Location);
        Assert.Equal("Auto Dealer", detailed.Seller);
        Assert.True(detailed.DetailsLoaded);
    }

    [Fact]
    public void ApplyDetails_marks_captcha_protected_vin_as_hidden()
    {
        var detailed = OtomotoPage.ApplyDetails(Listing(), OfferPage("<p>Zapraszam, auto bez wypadku. Tel. 600700800</p>"));

        Assert.Null(detailed.Vin);
        Assert.True(detailed.VinHiddenOnSite);
    }

    [Fact]
    public async Task Hidden_vin_gets_its_own_report_status()
    {
        var report = await new VinVerifier().VerifyAsync(Listing() with { VinHiddenOnSite = true });
        Assert.Equal(VinStatus.HiddenOnSite, report.Status);
        Assert.Contains("Pokaż VIN", report.ToText());
    }

    [Fact]
    public void FindVinInText_ignores_numbers_and_prefers_real_vins()
    {
        Assert.Null(OtomotoPage.FindVinInText("Telefon 12345678901234567, przebieg 187000"));
        Assert.Equal("WVWZZZ1KZAW000001", OtomotoPage.FindVinInText("vin: wvwzzz1kzaw000001."));
    }

    [Fact]
    public void ParseOtomotoFilters_builds_catalog_from_filter_states()
    {
        var filters = new
        {
            filters = new
            {
                states = new object[]
                {
                    new { filterId = "filter_enum_make", conditions = Array.Empty<object>(),
                          values = new[] { new { values = new[] { new { id = "skoda", name = "Skoda" } } } } },
                    new { filterId = "filter_enum_model", conditions = new[] { new { filterId = "filter_enum_make", value = "skoda" } },
                          values = new[] { new { values = new[] { new { id = "octavia", name = "Octavia" }, new { id = "other", name = "Inny" } } } } },
                },
            },
        };
        var html = Page(new { urqlState = new Dictionary<string, object> { ["x"] = new { data = JsonSerializer.Serialize(filters) } } });

        var make = Assert.Single(VehicleCatalog.ParseOtomotoFilters(html));
        Assert.Equal("Skoda", make.Name);
        Assert.Equal(["Octavia"], make.Models.Select(m => m.Name));
    }

    private sealed class NullGeocoder : IGeocoder
    {
        public Task<GeocodeResult?> FindAsync(string query, CancellationToken cancellationToken = default) => Task.FromResult<GeocodeResult?>(null);
        public Task<ReverseGeocodeResult> ReverseAsync(GeoPoint point, CancellationToken cancellationToken = default) => Task.FromResult(ReverseGeocodeResult.Empty);
    }
}
