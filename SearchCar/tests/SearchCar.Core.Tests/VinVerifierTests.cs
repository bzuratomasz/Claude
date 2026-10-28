using SearchCar.Core.Models;
using SearchCar.Core.Vin;

namespace SearchCar.Core.Tests;

public class VinVerifierTests
{
    private static CarListing Listing(string? vin, string make = "Volkswagen", string model = "Golf", int year = 2019) => new()
    {
        Id = "t-1",
        Make = make,
        Model = model,
        Year = year,
        MileageKm = 100_000,
        Price = 50_000,
        Vin = vin,
        Location = new GeoPoint(52.23, 21.01),
    };

    /// <summary>A well-formed VW VIN for the given model year with a correct check digit.</summary>
    private static string VwVin(int year)
    {
        var vin = $"WVWZZZ1K0{VinRules.YearCode(year)}W123456";
        return vin[..8] + VinRules.ComputeCheckDigit(vin) + vin[9..];
    }

    private readonly VinVerifier _verifier = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_vin_is_reported_as_not_present(string? vin)
    {
        var report = await _verifier.VerifyAsync(Listing(vin));
        Assert.Equal(VinStatus.NotPresent, report.Status);
        Assert.Contains("VIN:        brak", report.ToText());
    }

    [Fact]
    public async Task Consistent_vin_is_verified()
    {
        var report = await _verifier.VerifyAsync(Listing(VwVin(2019)));
        Assert.Equal(VinStatus.Verified, report.Status);
    }

    [Fact]
    public async Task Wrong_length_is_invalid()
    {
        var report = await _verifier.VerifyAsync(Listing(VwVin(2019)[..16]));
        Assert.Equal(VinStatus.Invalid, report.Status);
    }

    [Fact]
    public async Task Vin_of_another_brand_is_suspicious()
    {
        var report = await _verifier.VerifyAsync(Listing(VwVin(2019), make: "Toyota", model: "Corolla"));
        Assert.Equal(VinStatus.Suspicious, report.Status);
        Assert.Contains(report.Checks, c => c.Name == "Producent (WMI)" && c.Outcome == CheckOutcome.Fail);
    }

    [Fact]
    public async Task One_year_difference_is_only_a_warning()
    {
        var report = await _verifier.VerifyAsync(Listing(VwVin(2018), year: 2019));
        Assert.Equal(VinStatus.VerifiedWithWarnings, report.Status);
    }

    [Fact]
    public async Task Reused_vin_is_suspicious()
    {
        var report = await _verifier.VerifyAsync(Listing(VwVin(2019)), new VerificationContext(OtherListingsWithSameVin: 1));
        Assert.Equal(VinStatus.Suspicious, report.Status);
    }

    [Fact]
    public async Task Wrong_check_digit_is_invalid_for_us_vin()
    {
        var report = await _verifier.VerifyAsync(Listing("1M8GDM9A1KP042788", make: "Motor Coach", year: 2019));
        Assert.Equal(VinStatus.Invalid, report.Status);
    }

    [Fact]
    public async Task Decoder_mismatch_is_flagged_and_decoder_failure_is_tolerated()
    {
        var mismatch = new VinVerifier(new FakeDecoder(new VinDecodeResult { Make = "AUDI", ErrorCode = "0" }));
        Assert.Equal(VinStatus.Suspicious, (await mismatch.VerifyAsync(Listing(VwVin(2019)))).Status);

        var offline = new VinVerifier(new FakeDecoder(null));
        var report = await offline.VerifyAsync(Listing(VwVin(2019)));
        Assert.Equal(VinStatus.Verified, report.Status);
        Assert.Contains(report.Checks, c => c.Detail.StartsWith("Usługa niedostępna"));
    }

    [Theory]
    [InlineData("VW", "Volkswagen")]
    [InlineData("Škoda", "SKODA")]
    [InlineData("Mercedes", "Mercedes-Benz")]
    public void SameMake_handles_aliases_and_diacritics(string a, string b) => Assert.True(VinVerifier.SameMake(a, b));

    private sealed class FakeDecoder(VinDecodeResult? result) : IVinDecoder
    {
        public string Name => "Fake";

        public Task<VinDecodeResult> DecodeAsync(string vin, CancellationToken cancellationToken = default) =>
            result is null ? throw new HttpRequestException("offline") : Task.FromResult(result);
    }
}
