using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using SearchCar.Core.Models;
using SearchCar.Core.Vin;

namespace SearchCar.Core.Listings;

/// <summary>One page of otomoto.pl search results.</summary>
internal sealed record OtomotoSearchPage(IReadOnlyList<CarListing> Listings, int TotalCount, int PageSize, int Offset, string? AppliedCity);

/// <summary>Reads the JSON that otomoto.pl (a Next.js site) embeds in its pages as <c>__NEXT_DATA__</c>.</summary>
internal static partial class OtomotoPage
{
    [GeneratedRegex("""<script id="__NEXT_DATA__"[^>]*>(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex NextDataRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    // 17 characters without I, O, Q, not glued to other letters/digits.
    [GeneratedRegex(@"(?<![A-Za-z0-9])[A-HJ-NPR-Za-hj-npr-z0-9]{17}(?![A-Za-z0-9])")]
    private static partial Regex VinCandidateRegex();

    public static JsonElement NextData(string html)
    {
        var match = NextDataRegex().Match(html);
        if (!match.Success)
            throw new InvalidDataException("Strona nie zawiera danych __NEXT_DATA__ - otomoto.pl mogło zmienić układ strony lub zablokować zapytanie.");
        using var doc = JsonDocument.Parse(match.Groups[1].Value);
        return doc.RootElement.Clone();
    }

    /// <summary>The GraphQL results cached in <c>pageProps.urqlState</c>, each already parsed.</summary>
    public static IEnumerable<JsonElement> UrqlData(string html)
    {
        var pageProps = NextData(html).GetProperty("props").GetProperty("pageProps");
        if (!pageProps.TryGetProperty("urqlState", out var urql) || urql.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var entry in urql.EnumerateObject())
        {
            if (entry.Value.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String)
            {
                using var doc = JsonDocument.Parse(data.GetString()!);
                yield return doc.RootElement.Clone();
            }
        }
    }

    public static OtomotoSearchPage ParseSearch(string html, string sourceName)
    {
        foreach (var data in UrqlData(html))
        {
            if (!data.TryGetProperty("advertSearch", out var search))
                continue;

            var listings = new List<CarListing>();
            foreach (var edge in search.GetProperty("edges").EnumerateArray())
                if (edge.TryGetProperty("node", out var node) && ParseNode(node, sourceName) is { } listing)
                    listings.Add(listing);

            var pager = search.GetProperty("pageInfo");
            string? city = null;
            if (search.TryGetProperty("appliedLocation", out var loc) && loc.ValueKind == JsonValueKind.Object &&
                loc.TryGetProperty("city", out var c) && c.ValueKind == JsonValueKind.Object)
                city = Str(c, "name");

            return new OtomotoSearchPage(listings, search.GetProperty("totalCount").GetInt32(),
                pager.GetProperty("pageSize").GetInt32(), pager.GetProperty("currentOffset").GetInt32(), city);
        }

        throw new InvalidDataException("Brak wyników na stronie - otomoto.pl mogło zmienić układ strony.");
    }

    private static CarListing? ParseNode(JsonElement node, string sourceName)
    {
        var p = new Dictionary<string, (string Value, string Display)>();
        foreach (var param in node.GetProperty("parameters").EnumerateArray())
            p[Str(param, "key") ?? ""] = (Str(param, "value") ?? "", Str(param, "displayValue") ?? "");

        if (!p.TryGetValue("make", out var make) || !p.TryGetValue("model", out var model) ||
            !int.TryParse(p.GetValueOrDefault("year").Value, out var year))
            return null;

        int.TryParse(p.GetValueOrDefault("mileage").Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mileage);

        decimal price = 0;
        var currency = "PLN";
        if (node.TryGetProperty("price", out var priceEl) && priceEl.TryGetProperty("amount", out var amount))
        {
            if (amount.TryGetProperty("units", out var units) && units.ValueKind == JsonValueKind.Number)
                price = units.GetDecimal();
            currency = Str(amount, "currencyCode") ?? currency;
        }

        var seller = "";
        if (node.TryGetProperty("sellerLink", out var link) && link.ValueKind == JsonValueKind.Object)
            seller = Str(link, "name") ?? "";
        if (seller.Length == 0 && node.TryGetProperty("seller", out var s) && s.ValueKind == JsonValueKind.Object)
            seller = Str(s, "__typename") == "PrivateSeller" ? "Osoba prywatna" : "Firma";

        string city = "", region = "";
        if (node.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.Object)
        {
            if (loc.TryGetProperty("city", out var c) && c.ValueKind == JsonValueKind.Object) city = Str(c, "name") ?? "";
            if (loc.TryGetProperty("region", out var r) && r.ValueKind == JsonValueKind.Object) region = Str(r, "name") ?? "";
        }

        var url = Str(node, "url");
        return new CarListing
        {
            Id = Str(node, "id") ?? Guid.NewGuid().ToString("N"),
            Make = make.Display,
            Model = model.Display,
            Year = year,
            MileageKm = mileage,
            Price = price,
            Currency = currency,
            Fuel = OtomotoListingProvider.FuelFromId(p.GetValueOrDefault("fuel_type").Value),
            Gearbox = p.GetValueOrDefault("gearbox").Value switch
            {
                "manual" => Gearbox.Manual,
                "automatic" => Gearbox.Automatic,
                _ => Gearbox.Any,
            },
            Headline = Str(node, "title"),
            CepikVerified = node.TryGetProperty("cepikVerified", out var cepik) && cepik.ValueKind is JsonValueKind.True or JsonValueKind.False
                ? cepik.GetBoolean()
                : null,
            City = city,
            Region = region,
            Seller = seller,
            Source = sourceName,
            Url = Uri.TryCreate(url, UriKind.Absolute, out var u) ? u : null,
        };
    }

    /// <summary>Adds what only the offer page has: VIN (when published in clear), location, seller.</summary>
    public static CarListing ApplyDetails(CarListing listing, string html)
    {
        var advert = NextData(html).GetProperty("props").GetProperty("pageProps").GetProperty("advert");
        var result = listing with { DetailsLoaded = true };

        // The structured VIN field is encrypted and only revealed after reCAPTCHA; we only note that it exists.
        var vinFieldExists = false;
        if (advert.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
        {
            foreach (var d in details.EnumerateArray())
            {
                if (Str(d, "key") != "vin")
                    continue;
                vinFieldExists = true;
                var clear = VinRules.Normalize(Str(d, "value"));
                if (VinRules.IsWellFormed(clear))
                    result = result with { Vin = clear, VinSource = VinSource.Listing };
            }
        }
        if (advert.TryGetProperty("parametersDict", out var dict) && dict.ValueKind == JsonValueKind.Object && dict.TryGetProperty("has_vin", out _))
            vinFieldExists = true;

        if (string.IsNullOrEmpty(result.Vin) && FindVinInText(Str(advert, "description")) is { } fromText)
            result = result with { Vin = fromText, VinSource = VinSource.Description };

        result = result with { VinHiddenOnSite = string.IsNullOrEmpty(result.Vin) && vinFieldExists };

        if (advert.TryGetProperty("seller", out var seller) && seller.ValueKind == JsonValueKind.Object)
        {
            if (Str(seller, "name") is { Length: > 0 } name && Str(seller, "type") != "PRIVATE")
                result = result with { Seller = name };

            if (seller.TryGetProperty("location", out var loc) && loc.ValueKind == JsonValueKind.Object &&
                loc.TryGetProperty("map", out var map) && map.ValueKind == JsonValueKind.Object &&
                map.TryGetProperty("latitude", out var lat) && lat.ValueKind == JsonValueKind.Number &&
                map.TryGetProperty("longitude", out var lon) && lon.ValueKind == JsonValueKind.Number &&
                (lat.GetDouble() != 0 || lon.GetDouble() != 0))
            {
                result = result with { Location = new GeoPoint(lat.GetDouble(), lon.GetDouble()) };
            }
        }

        return result;
    }

    /// <summary>A VIN written by the seller in free text, e.g. "Nr VIN TMBJR8NX2PY090651".</summary>
    public static string? FindVinInText(string? htmlOrText)
    {
        if (string.IsNullOrWhiteSpace(htmlOrText))
            return null;

        var text = WebUtility.HtmlDecode(TagRegex().Replace(htmlOrText, " "));
        var candidates = VinCandidateRegex().Matches(text)
            .Select(m => m.Value.ToUpperInvariant())
            .Where(v => v.Any(char.IsDigit) && v.Any(char.IsLetter))
            .Distinct()
            .ToList();

        // Prefer something that really looks like a VIN: known manufacturer or a valid check digit.
        return candidates.FirstOrDefault(v => WmiDirectory.Manufacturer(v) is not null || VinRules.IsCheckDigitValid(v))
               ?? candidates.FirstOrDefault();
    }

    private static string? Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
