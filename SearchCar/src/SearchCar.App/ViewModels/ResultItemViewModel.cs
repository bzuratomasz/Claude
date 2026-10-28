using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using SearchCar.Core.Geo;
using SearchCar.Core.Listings;
using SearchCar.Core.Models;
using SearchCar.Core.Vin;

namespace SearchCar.App.ViewModels;

public sealed record CheckItemViewModel(string Name, string Detail, string Glyph, SolidColorBrush Brush);

public sealed record DetailItemViewModel(string Key, string Value);

public sealed class ResultItemViewModel : ObservableObject
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("pl-PL");
    private readonly GeoPoint _center;
    private SearchResult _result;
    private VinReport? _report;
    private bool _isLoadingDetails;
    private string _manualVin = "";

    public ResultItemViewModel(SearchResult result, GeoPoint center)
    {
        _result = result;
        _center = center;
    }

    public SearchResult Result => _result;
    public CarListing Listing => _result.Listing;
    public string Id => $"{Listing.Source}|{Listing.Id}";

    public string Title => Listing.Title;
    public string Headline => Listing.Headline ?? Listing.Title;

    public string Subtitle =>
        $"{Listing.MileageKm.ToString("N0", Culture)} km · {FuelTypeNames.Display(Listing.Fuel)} · {FuelTypeNames.Display(Listing.Gearbox)} · {Listing.Seller}";

    public string PlaceText =>
        string.Join(" · ", new[] { Listing.City, Listing.Source }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public string PriceText => Listing.Currency == "PLN" || Listing.PricePln is not decimal pln
        ? $"{Listing.Price.ToString("N0", Culture)} {Listing.Currency}"
        : $"{Listing.Price.ToString("N0", Culture)} {Listing.Currency} (≈ {pln.ToString("N0", Culture)} PLN)";

    public string DistanceText => _result.DistanceKm is double d && Listing.Location is { } p
        ? $"{d.ToString("N1", Culture)} km {GeoMath.CompassPoint(GeoMath.BearingDeg(_center, p))}"
        : Listing.City;

    public string VinText => string.IsNullOrWhiteSpace(Listing.Vin)
        ? Listing.VinHiddenOnSite ? "VIN: ukryty w serwisie" : "VIN: brak"
        : $"VIN: {Listing.Vin}";

    public bool HasUrl => Listing.Url is not null;

    public string ManualVin { get => _manualVin; set => SetProperty(ref _manualVin, value ?? ""); }

    public bool IsLoadingDetails
    {
        get => _isLoadingDetails;
        set
        {
            if (SetProperty(ref _isLoadingDetails, value))
                OnPropertyChanged(nameof(StatusText));
        }
    }

    /// <summary>Replaces the listing (e.g. after the offer page was read or a VIN was typed in).</summary>
    public void UpdateListing(CarListing listing)
    {
        _result = CarSearchService.ToResult(listing, _center);
        OnPropertyChanged(string.Empty); // everything derived from the listing may have changed
    }

    public VinReport? Report
    {
        get => _report;
        set
        {
            if (!SetProperty(ref _report, value))
                return;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBrush));
            OnPropertyChanged(nameof(StatusColorHex));
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(Checks));
            OnPropertyChanged(nameof(DecodedFields));
            OnPropertyChanged(nameof(HasDecodedFields));
        }
    }

    public string StatusText => Report is not null ? VinReport.StatusLabel(Report.Status)
        : IsLoadingDetails ? "Odczyt strony ogłoszenia…"
        : "Czeka na sprawdzenie VIN…";

    public SolidColorBrush StatusBrush => StatusStyle.Brush(StatusStyle.ColorOf(Report?.Status));
    public string StatusColorHex => StatusStyle.Hex(StatusStyle.ColorOf(Report?.Status));
    public string Summary => Report?.Summary ?? "Trwa weryfikacja…";

    public IReadOnlyList<CheckItemViewModel> Checks =>
        Report?.Checks.Select(c => new CheckItemViewModel(c.Name, c.Detail, StatusStyle.GlyphOf(c.Outcome),
            StatusStyle.Brush(StatusStyle.ColorOf(c.Outcome)))).ToList() ?? [];

    public IReadOnlyList<DetailItemViewModel> DecodedFields =>
        Report?.DecodedFields.Select(kv => new DetailItemViewModel(kv.Key, kv.Value)).ToList() ?? [];

    public bool HasDecodedFields => Report?.DecodedFields.Count > 0;
}
