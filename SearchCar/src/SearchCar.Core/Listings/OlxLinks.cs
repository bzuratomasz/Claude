using System.Globalization;
using SearchCar.Core.Geo;
using SearchCar.Core.Models;

namespace SearchCar.Core.Listings;

/// <summary>
/// OLX.pl refuses automated requests (HTTP 403), so the app does not read it. Instead it builds the same search
/// as a link the user can open in a browser.
/// </summary>
public static class OlxLinks
{
    public static SourceLink Build(SearchCriteria c)
    {
        var town = !string.IsNullOrWhiteSpace(c.CenterName) ? c.CenterName! : PolishCities.NearestTo(c.Center).First().City.Name;

        var path = new List<string> { "motoryzacja", "samochody" };
        if (!string.IsNullOrWhiteSpace(c.Make))
        {
            path.Add(VehicleCatalog.Slug(c.Make));
            if (!string.IsNullOrWhiteSpace(c.Model))
                path.Add(VehicleCatalog.Slug(c.Model));
        }
        path.Add(VehicleCatalog.Slug(town));

        var q = new List<string> { $"search%5Bdist%5D={SnapDistance(c.RadiusKm)}" };
        void Add(string key, decimal? v)
        {
            if (v is decimal value)
                q.Add($"search%5B{Uri.EscapeDataString(key)}%5D={value.ToString("0", CultureInfo.InvariantCulture)}");
        }
        Add("filter_float_year:from", c.YearFrom);
        Add("filter_float_year:to", c.YearTo);
        Add("filter_float_milage:from", c.MileageFromKm);
        Add("filter_float_milage:to", c.MileageToKm);
        Add("filter_float_price:from", c.PriceFrom);
        Add("filter_float_price:to", c.PriceTo);

        return new SourceLink("OLX.pl (tylko w przeglądarce)",
            new Uri($"https://www.olx.pl/{string.Join("/", path.Select(Uri.EscapeDataString))}/?{string.Join("&", q)}"));
    }

    private static int SnapDistance(double km) => new[] { 0, 2, 5, 10, 15, 30, 50, 75, 100 }.FirstOrDefault(d => d >= km, 100);
}
