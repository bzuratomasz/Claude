using System.Text.Json;
using SearchCar.Core.Geo;
using SearchCar.Core.Listings;
using SearchCar.Core.Models;

namespace SearchCar.Core.Tests;

public class LocalSitesTests
{
    private const string AutotraderList = """
        <a class="filter filter--active" rel="nofollow"> Wszystkie (127) </a>
        <div class="offer-card offer-card--premium"> <div class="offer-card__image "> <a href="/oferta/skoda-octavia-czernica-1002113280" title="x">
          <div class="offer-card__basic offer-card__basic--partner-program"> <h2> Skoda Octavia </h2> <p> IV Kombi 2.0 tdi 150KM DSG Style </p> </div>
          <div class="offer-card__price"> <strong>89 000 PLN</strong> <small>brutto</small>
          <div class="offer-card__details"> <div> <span title='czernica' class='offer-detail offer-detail--city'>czernica</span> <span title='dolnośląskie' class='offer-detail'>(dolnośląskie)</span> </div>
            <span title='2020' class='offer-detail'>2020</span> <span title='71427 km' class='offer-detail'>71&#160;427 km</span> <span title='diesel' class='offer-detail'>diesel</span> </div>
        </div>
        <div class="offer-card offer-card--standard"> <a href="/oferta/mercedes-benz-klasa-c-piaseczno-1002124912" title="x">
          <div class="offer-card__basic"> <h2> Mercedes-Benz Klasa C </h2> <div class="offer-car__basic--flex"> <p class="logo"><img alt="AAA Auto"></p> </div> </div>
          <div class="offer-card__price"> <strong>67 999 PLN</strong>
          <span title='Piaseczno' class='offer-detail offer-detail--city'>Piaseczno</span> <span title='mazowieckie' class='offer-detail'>(mazowieckie)</span>
          <span title='2023' class='offer-detail'>2023</span> <span title='132053 km' class='offer-detail'>132&#160;053 km</span> <span title='1498 cm3' class='offer-detail'>1498 cm3</span>
        </div>
        </main>
        """;

    [Fact]
    public void Autotrader_parses_regular_and_partner_cards()
    {
        var list = AutotraderPage.ParseSearch(AutotraderList, "autotrader.pl");

        Assert.Equal(2, list.Count);
        var a = list[0];
        Assert.Equal(("1002113280", "Skoda", "Octavia", 2020, 71427, 89000m), (a.Id, a.Make, a.Model, a.Year, a.MileageKm, a.Price));
        Assert.Equal((FuelType.Diesel, "Czernica", "dolnośląskie"), (a.Fuel, a.City, a.Region));
        Assert.Equal("Skoda Octavia IV Kombi 2.0 tdi 150KM DSG Style", a.Headline);
        Assert.Equal("https://www.autotrader.pl/oferta/skoda-octavia-czernica-1002113280", a.Url?.ToString());

        var b = list[1];
        Assert.Equal(("Mercedes-Benz", "Klasa C", 2023, 132053, FuelType.Any), (b.Make, b.Model, b.Year, b.MileageKm, b.Fuel));
        Assert.Equal(127, AutotraderPage.TotalCount(AutotraderList));
    }

    [Fact]
    public void Autotrader_url_uses_voivodeship_and_page_path()
    {
        var url = AutotraderListingProvider.BuildSearchUrl(new SearchCriteria { Make = "Skoda", Model = "Octavia", Center = default }, "małopolskie", 3);
        Assert.Equal("https://www.autotrader.pl/szukaj/osobowe/skoda/octavia/malopolskie/str-3", url.ToString());
    }

    [Fact]
    public void Motogratka_parses_structured_offers_and_card_locations()
    {
        var ld = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["@type"] = "Product",
            ["offers"] = new
            {
                offers = new[]
                {
                    new
                    {
                        itemOffered = new Dictionary<string, object>
                        {
                            ["@type"] = "Car",
                            ["name"] = "Skoda Octavia III 1.6TDI 2019 r.",
                            ["brand"] = "Skoda",
                            ["model"] = "Octavia",
                            ["fuelType"] = "diesel",
                            ["vehicleModelDate"] = 2019,
                            ["mileageFromOdometer"] = new { value = "145400 km" },
                            ["offers"] = new { url = "http://motogratka.pl/motoryzacja/skoda-octavia-iii/ob/49240331", price = 45900, priceCurrency = "PLN" },
                        },
                    },
                },
            },
        });
        var html = $"""
            <script type="application/ld+json">{ld}</script>
            <article class="teaserUnified" id="item-49240331"><span class="teaserUnified__location" data-cy="teaserLocation"> Warszawa, mazowieckie </span></article>
            """;

        var l = Assert.Single(MotogratkaPage.ParseSearch(html, "motogratka.pl"));

        Assert.Equal(("49240331", "Skoda", "Octavia", 2019, 145400, 45900m), (l.Id, l.Make, l.Model, l.Year, l.MileageKm, l.Price));
        Assert.Equal((FuelType.Diesel, "Warszawa", "mazowieckie"), (l.Fuel, l.City, l.Region));
        Assert.Equal("https://motogratka.pl/motoryzacja/skoda-octavia-iii/ob/49240331", l.Url?.ToString());
    }

    [Fact]
    public async Task TownLocator_uses_built_in_towns_without_network()
    {
        var locator = new TownLocator(new ThrowingGeocoder());
        Assert.Equal(PolishCities.All[0].Point, await locator.LocateAsync("warszawa", "mazowieckie", CancellationToken.None));
        Assert.Null(await locator.LocateAsync("Nieznanowo", null, CancellationToken.None)); // geocoder failure -> unknown, no exception
    }

    private sealed class ThrowingGeocoder : IGeocoder
    {
        public Task<GeocodeResult?> FindAsync(string query, CancellationToken cancellationToken = default) => throw new HttpRequestException("offline");
        public Task<ReverseGeocodeResult> ReverseAsync(GeoPoint point, CancellationToken cancellationToken = default) => throw new HttpRequestException("offline");
    }
}
