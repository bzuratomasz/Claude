using SearchCar.Core.Vin;

namespace SearchCar.Core.Tests;

public class VinRulesTests
{
    [Theory]
    [InlineData("1M8GDM9AXKP042788", 'X')] // textbook example from 49 CFR 565
    [InlineData("11111111111111111", '1')]
    public void ComputeCheckDigit_matches_known_vins(string vin, char expected) =>
        Assert.Equal(expected, VinRules.ComputeCheckDigit(vin));

    [Fact]
    public void IsCheckDigitValid_detects_typo()
    {
        Assert.True(VinRules.IsCheckDigitValid("1M8GDM9AXKP042788"));
        Assert.False(VinRules.IsCheckDigitValid("1M8GDM9A1KP042788"));
    }

    [Theory]
    [InlineData("WVWZZZ1KZAW000001", true)]
    [InlineData("WVWZZZ1KZAW00000", false)]  // 16 chars
    [InlineData("WVWZZZ1KZOW000001", false)] // O is not allowed
    public void IsWellFormed(string vin, bool expected) => Assert.Equal(expected, VinRules.IsWellFormed(vin));

    [Fact]
    public void Normalize_strips_spaces_dashes_and_uppercases() =>
        Assert.Equal("WVWZZZ1KZAW000001", VinRules.Normalize(" wvw-zzz 1kz aw000001 "));

    [Fact]
    public void ModelYearCandidates_repeat_every_30_years()
    {
        Assert.Equal([1989, 2019], VinRules.ModelYearCandidates("1M8GDM9AXKP042788", 2026));
        Assert.Equal([2001], VinRules.ModelYearCandidates("WVWZZZ1KZ1W000001", 2026));
    }

    [Theory]
    [InlineData(2019, 'K')]
    [InlineData(2010, 'A')]
    [InlineData(2001, '1')]
    [InlineData(2026, 'T')]
    public void YearCode_roundtrips(int year, char code) => Assert.Equal(code, VinRules.YearCode(year));

    [Theory]
    [InlineData("1M8GDM9AXKP042788", true)]
    [InlineData("LRW3E7EK0NC000001", true)]
    [InlineData("WVWZZZ1KZAW000001", false)]
    public void CheckDigit_is_mandatory_only_for_north_america_and_china(string vin, bool expected) =>
        Assert.Equal(expected, VinRules.IsCheckDigitMandatory(vin));
}
