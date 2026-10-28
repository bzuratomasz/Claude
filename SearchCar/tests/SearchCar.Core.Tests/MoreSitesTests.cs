using System.Text.Json;
using SearchCar.Core.Geo;
using SearchCar.Core.Listings;
using SearchCar.Core.Models;
using SearchCar.Core.Vin;

namespace SearchCar.Core.Tests;

public class MoreSitesTests
{
    private static string NextData(object pageProps) =>
        $"""<html><script id="__NEXT_DATA__" type="application/json">{JsonSerializer.Serialize(new { props = new { pageProps } })}</script></html>""";

    private static string NgState(Dictionary<string, object> state) =>
        $"""<html><script id="ng-state" type="application/json">{JsonSerializer.Serialize(state)}</script></html>""";

    private static readonly SearchCriteria Warsaw = new() { Center = new GeoPoint(52.23, 21.01), RadiusKm = 50 };

    // ------------------------------------------------------------------ AutoScout24

    private static object As24Item(string id, int distance) => new
    {
        id,
        url = $"/oferta/skoda-octavia-{id}",
        price = new { priceRaw = 13850 },
        vehicle = new { make = "Skoda", model = "Octavia", modelVersionInput = "Combi 1.5 TSI", fuel = "Benzyna", transmission = "Manualna" },
        tracking = new { firstRegistration = "10-2019", mileage = "96008", price = "13850" },
        location = new { countryCode = "DE", city = "Zeesen", distanceToSearchLocationInKm = distance },
        seller = new { type = "Dealer", companyName = "AMZ Autohaus" },
    };

    [Fact]
    public void AutoScout24_parses_offers_converts_eur_and_drops_offers_beyond_radius()
    {
        var html = NextData(new { numberOfResults = 2, numberOfPages = 1, listings = new[] { As24Item("a", 64), As24Item("b", 801) } });

        var page = AutoScout24Page.ParseSearch(html, "AutoScout24", eurRate: 4.3775m, maxDistanceKm: 100);

        var l = Assert.Single(page.Listings);
        Assert.True(page.FartherDropped);
        Assert.Equal((2019, 96008, 13850m, "EUR", 60628m), (l.Year, l.MileageKm, l.Price, l.Currency, l.PricePln));
        Assert.Equal((FuelType.Petrol, Gearbox.Manual), (l.Fuel, l.Gearbox));
        Assert.Equal("Zeesen (DE)", l.City);
        Assert.Equal("https://www.autoscout24.pl/oferta/skoda-octavia-a", l.Url?.ToString());
        Assert.Equal(60628m, l.ComparablePricePln);
    }

    [Fact]
    public void AutoScout24_details_give_coordinates_and_vin_from_description()
    {
        var html = NextData(new
        {
            listingDetails = new
            {
                location = new { latitude = 52.27196, longitude = 13.63305 },
                description = "<ul><li>FIN: TMBJR8NX2PY090651</li></ul>",
            },
        });

        var d = AutoScout24Page.ApplyDetails(new CarListing { Id = "a", Make = "Skoda", Model = "Octavia", Year = 2019, MileageKm = 1, Price = 1 }, html);

        Assert.Equal(new GeoPoint(52.27196, 13.63305), d.Location);
        Assert.Equal(("TMBJR8NX2PY090651", VinSource.Description), (d.Vin, d.VinSource));
    }

    [Fact]
    public void AutoScout24_url_converts_pln_price_limits_to_eur()
    {
        var url = AutoScout24ListingProvider.BuildSearchUrl(Warsaw with { Make = "Skoda", Model = "Octavia", PriceTo = 43775, Gearbox = Gearbox.Automatic, YearFrom = 2015 }, 4.3775m, 2).ToString();

        Assert.StartsWith("https://www.autoscout24.pl/lst/skoda/octavia?", url);
        Assert.Contains("priceto=10000", url);
        Assert.Contains("gear=A", url);
        Assert.Contains("fregfrom=2015", url);
        Assert.Contains("zipr=50", url);
        Assert.Contains("page=2", url);
    }

    // ------------------------------------------------------------------ autoplac.pl

    [Fact]
    public void Autoplac_parses_embedded_api_response_with_coordinates()
    {
        var html = NgState(new()
        {
            ["https://api.autoplac.pl/offers/search?seoCategories=x"] = new
            {
                body = new
                {
                    offerCount = 456,
                    filters = new { city = "Piaseczno" },
                    offerList = new[]
                    {
                        new
                        {
                            dealer = new { description = "AUTO ROGOWSKI" },
                            offer = new
                            {
                                id = 4708978, brand = "Skoda", model = "Octavia", productionYear = 2023, mileage = 158000,
                                fuelType = "DIESEL", transmissionTypeText = "Automat", title = "Skoda Octavia IV 2.0 TDI",
                                webUrl = "/oferta/skoda/octavia/iv-2-0-AR",
                                priceInfo = new { primary = new { price = 72500 }, currency = "PLN" },
                                locationInfo = new { city = "Mińsk Mazowiecki", latitude = 52.184852, longitude = 21.550894 },
                            },
                        },
                    },
                },
            },
        });

        var page = AutoplacPage.ParseSearch(html, "autoplac.pl")!;

        Assert.Equal(("Piaseczno", 456), (page.AppliedCity, page.Total));
        var l = Assert.Single(page.Listings);
        Assert.Equal(("Skoda", 2023, 158000, 72500m), (l.Make, l.Year, l.MileageKm, l.Price));
        Assert.Equal((FuelType.Diesel, Gearbox.Automatic), (l.Fuel, l.Gearbox));
        Assert.Equal(new GeoPoint(52.184852, 21.550894), l.Location);
        Assert.Equal("AUTO ROGOWSKI", l.Seller);
        Assert.Equal("https://autoplac.pl/oferta/skoda/octavia/iv-2-0-AR", l.Url?.ToString());
    }

    [Fact]
    public void Autoplac_details_take_clear_text_vin()
    {
        var html = NgState(new()
        {
            ["https://api.autoplac.pl/offer/iv-2-0-AR"] = new { body = new { offer = new { vin = "TMBJJ8NX4PY144362", description = "Polecam" } } },
            ["https://api.autoplac.pl/offer/form?vehicleType=PASSENGER"] = new { body = new { details = new { vin = new { active = true } } } },
        });

        var d = AutoplacPage.ApplyDetails(new CarListing { Id = "1", Make = "Skoda", Model = "Octavia", Year = 2023, MileageKm = 1, Price = 1 }, html);

        Assert.Equal(("TMBJJ8NX4PY144362", VinSource.Listing), (d.Vin, d.VinSource));
    }

    // ------------------------------------------------------------------ sprzedajemy.pl

    private const string SprzedajemyList = """
        <article class="element "><ul><li class="details">
          <h2 class="title "> <a href="/skoda-octavia-1-9-tdi-2007r-lodz-4-6bea9f-nr73921879" class="offerLink" title="x"> Skoda Octavia 1,9 TDI 2007r Nowy rozrząd! </a> </h2>
          <span class="price "> 6 700 zł </span>
          <div class="seller-type-info seller-type-info--private"></div>
          <strong class="city"> Łódź </strong>
        </li></ul></article>
        <a href="/motoryzacja/samochody-osobowe/skoda/octavia?offset=60">3</a>
        """;

    [Fact]
    public void Sprzedajemy_parses_list_items_and_total()
    {
        var l = Assert.Single(SprzedajemyPage.ParseSearch(SprzedajemyList, "sprzedajemy.pl"));

        Assert.Equal(("73921879", 6700m, "Łódź", "Osoba prywatna"), (l.Id, l.Price, l.City, l.Seller));
        Assert.Equal(2007, SprzedajemyPage.YearInTitle(l.Headline));
        Assert.Equal(90, SprzedajemyPage.TotalCount(SprzedajemyList));
        Assert.False(l.DetailsLoaded);
    }

    [Fact]
    public void Sprzedajemy_details_read_vehicle_data_hidden_vin_and_coordinates()
    {
        var ld = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["@type"] = "Vehicle",
            ["brand"] = new Dictionary<string, string> { ["name"] = "Škoda" },
            ["model"] = "Octavia",
            ["productionDate"] = "2007",
            ["fuelType"] = "diesel",
            ["mileageFromOdometer"] = new { value = "321000" },
            ["description"] = "Przebieg 321 tyś km",
        });
        var html = $$"""
            <script type="application/ld+json">{{ld}}</script>
            <li class="location " data-coordinates="{'lat': 51.7592485, 'lng': 19.4559833, 'zoom': 12}">
            <li><span>Skrzynia biegów</span> <strong>manualna</strong></li>
            <li><span>VIN</span> <a>pokaż VIN &raquo;</a></li>
            """;

        var d = SprzedajemyPage.ApplyDetails(new CarListing { Id = "1", Make = "", Model = "", Year = 0, MileageKm = -1, Price = 6700 }, html);

        Assert.Equal(("Škoda", "Octavia", 2007, 321000), (d.Make, d.Model, d.Year, d.MileageKm));
        Assert.Equal((FuelType.Diesel, Gearbox.Manual), (d.Fuel, d.Gearbox));
        Assert.True(d.VinHiddenOnSite);
        Assert.Equal(new GeoPoint(51.7592485, 19.4559833), d.Location);
    }

    [Theory]
    [InlineData(29, 30)]
    [InlineData(40, 50)]
    [InlineData(400, 150)]
    public void Sprzedajemy_snaps_radius(double km, int expected) => Assert.Equal(expected, SprzedajemyListingProvider.SnapDistance(km));

    // ------------------------------------------------------------------ shared

    [Theory]
    [InlineData("Benzyna+LPG", FuelType.PetrolLpg)]
    [InlineData("PETROL", FuelType.Petrol)]
    [InlineData("Hybryda (benzyna/elektryczny)", FuelType.Hybrid)]
    [InlineData("Hybryda Plug-in", FuelType.PluginHybrid)]
    [InlineData("Elektryczny", FuelType.Electric)]
    [InlineData("DIESEL", FuelType.Diesel)]
    public void FuelParsing_recognises_site_texts(string text, FuelType expected) => Assert.Equal(expected, FuelParsing.FromText(text));

    [Theory]
    [InlineData("Manualna", Gearbox.Manual)]
    [InlineData("Automat", Gearbox.Automatic)]
    [InlineData("Półautomatyczna", Gearbox.Automatic)]
    public void GearboxFromText(string text, Gearbox expected) => Assert.Equal(expected, FuelParsing.GearboxFromText(text));

    [Fact]
    public void Olx_link_has_town_radius_and_filters()
    {
        var link = OlxLinks.Build(Warsaw with { Make = "Skoda", Model = "Octavia", CenterName = "Piaseczno", YearFrom = 2015, RadiusKm = 40 });

        Assert.Equal("https://www.olx.pl/motoryzacja/samochody/skoda/octavia/piaseczno/?search%5Bdist%5D=50&search%5Bfilter_float_year%3Afrom%5D=2015",
            link.Url.ToString());
    }

    [Fact]
    public async Task Same_car_on_another_site_is_noted_not_flagged()
    {
        var vin = "TMBJJ8NX4PY144362";
        var listing = new CarListing { Id = "1", Make = "Skoda", Model = "Octavia", Year = 2023, MileageKm = 1, Price = 1, Vin = vin };

        var report = await new VinVerifier().VerifyAsync(listing, new VerificationContext(0, ["otomoto.pl"]));

        Assert.NotEqual(VinStatus.Suspicious, report.Status);
        Assert.Contains(report.Checks, c => c.Name == "Inne serwisy" && c.Detail.Contains("otomoto.pl"));
    }

    [Fact]
    public void Unknown_year_and_mileage_do_not_exclude_an_offer_before_its_page_is_read()
    {
        var listing = new CarListing { Id = "1", Make = "Skoda", Model = "Octavia", Year = 0, MileageKm = -1, Price = 20000 };
        var criteria = Warsaw with { YearFrom = 2015, MileageToKm = 100000 };

        Assert.True(CarSearchService.Matches(criteria, new SearchResult(listing, null)));
        Assert.False(CarSearchService.Matches(criteria, new SearchResult(listing with { Year = 2010 }, null)));
    }
}
