using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SearchCar.Core.Geo;
using SearchCar.Core.Listings;
using SearchCar.Core.Models;
using SearchCar.Core.Vin;

namespace SearchCar.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    /// <summary>Offer pages are read at most this many at a time (otomoto.pl is asked politely).</summary>
    private const int MaxParallelVerifications = 2;

    private readonly VinVerifier _verifier;
    private readonly IGeocoder _geocoder;
    private readonly PlaceCandidates _places;
    private readonly Func<CancellationToken, Task<VehicleCatalog>> _refreshCatalog;
    private CancellationTokenSource? _searchCts;
    private SearchCriteria? _lastCriteria;

    private VehicleCatalog _catalog;
    private string _make = "";
    private string _model = "";
    private double _yearFrom = double.NaN;
    private double _yearTo = double.NaN;
    private double _mileageFrom = double.NaN;
    private double _mileageTo = double.NaN;
    private double _priceFrom = double.NaN;
    private double _priceTo = double.NaN;
    private int _fuelIndex;
    private int _gearboxIndex;
    private double _latitude = 52.2297;   // Warsaw
    private double _longitude = 21.0122;
    private string _locationName = "Warszawa";
    private bool _locationIsNamedPlace = true;
    private double _radiusKm = 50;
    private double _maxPages = 2;
    private string _placeQuery = "";
    private bool _isBusy;
    private string _statusMessage = "Ustaw filtry i lokalizację, a potem kliknij Szukaj.";
    private string _searchNotes = "";
    private ResultItemViewModel? _selectedResult;

    public MainViewModel(IEnumerable<SourceOptionViewModel> sources, VinVerifier verifier, IGeocoder geocoder, PlaceCandidates places,
        VehicleCatalog catalog, Func<CancellationToken, Task<VehicleCatalog>> refreshCatalog)
    {
        Sources = [.. sources];
        _verifier = verifier;
        _geocoder = geocoder;
        _places = places;
        _catalog = catalog;
        _refreshCatalog = refreshCatalog;
        FuelOptions = Enum.GetValues<FuelType>().Select(FuelTypeNames.Display).ToList();
        LoadMakeOptions();
    }

    /// <summary>Raised after <see cref="Results"/> is replaced by a new search.</summary>
    public event EventHandler? ResultsReplaced;

    /// <summary>Raised on the UI thread whenever a result's data or VIN report changes.</summary>
    public event EventHandler<ResultItemViewModel>? ResultUpdated;

    /// <summary>Raised when a result is dropped (it turned out to be outside the radius).</summary>
    public event EventHandler<ResultItemViewModel>? ResultRemoved;

    public ObservableCollection<string> MakeOptions { get; } = [];
    public ObservableCollection<string> ModelOptions { get; } = [];
    public IReadOnlyList<string> FuelOptions { get; }
    public IReadOnlyList<string> GearboxOptions { get; } = Enum.GetValues<Gearbox>().Select(FuelTypeNames.Display).ToList();
    public ObservableCollection<ResultItemViewModel> Results { get; } = [];

    /// <summary>Websites to search; each can be switched on or off.</summary>
    public ObservableCollection<SourceOptionViewModel> Sources { get; }

    /// <summary>The current search on each website (plus OLX, which can only be opened in a browser).</summary>
    public ObservableCollection<SourceLink> SearchLinks { get; } = [];

    public string CatalogInfo =>
        $"Marek: {_catalog.Makes.Count}, modeli: {_catalog.ModelCount:N0} z otomoto.pl ({_catalog.Fetched})";

    public string Make
    {
        get => _make;
        set
        {
            if (!SetProperty(ref _make, value ?? ""))
                return;
            ModelOptions.Clear();
            foreach (var model in _catalog.FindMake(_make)?.Models ?? [])
                ModelOptions.Add(model.Name);
        }
    }

    public string Model { get => _model; set => SetProperty(ref _model, value ?? ""); }
    public double YearFrom { get => _yearFrom; set => SetProperty(ref _yearFrom, value); }
    public double YearTo { get => _yearTo; set => SetProperty(ref _yearTo, value); }
    public double MileageFrom { get => _mileageFrom; set => SetProperty(ref _mileageFrom, value); }
    public double MileageTo { get => _mileageTo; set => SetProperty(ref _mileageTo, value); }
    public double PriceFrom { get => _priceFrom; set => SetProperty(ref _priceFrom, value); }
    public double PriceTo { get => _priceTo; set => SetProperty(ref _priceTo, value); }
    public int FuelIndex { get => _fuelIndex; set => SetProperty(ref _fuelIndex, value); }
    public int GearboxIndex { get => _gearboxIndex; set => SetProperty(ref _gearboxIndex, value); }
    public string PlaceQuery { get => _placeQuery; set => SetProperty(ref _placeQuery, value ?? ""); }

    public double MaxPages
    {
        get => _maxPages;
        set => SetProperty(ref _maxPages, double.IsNaN(value) ? 2 : Math.Clamp(Math.Round(value), 1, OtomotoListingProvider.MaxPages));
    }

    public double MaxRadiusKm => OtomotoListingProvider.MaxRadiusKm;

    public double RadiusKm
    {
        get => _radiusKm;
        set
        {
            if (SetProperty(ref _radiusKm, Math.Clamp(Math.Round(value), 1, MaxRadiusKm)))
                OnPropertyChanged(nameof(RadiusText));
        }
    }

    public string RadiusText => $"W promieniu {RadiusKm:N0} km";

    public GeoPoint Center => new(_latitude, _longitude);

    public string LocationText =>
        $"{_locationName} · {_latitude.ToString("F4", CultureInfo.InvariantCulture)}, {_longitude.ToString("F4", CultureInfo.InvariantCulture)}";

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    public string SearchNotes
    {
        get => _searchNotes;
        private set
        {
            if (SetProperty(ref _searchNotes, value))
                OnPropertyChanged(nameof(HasSearchNotes));
        }
    }

    public bool HasSearchNotes => SearchNotes.Length > 0;

    public bool HasSearchLinks => SearchLinks.Count > 0;

    public string ResultsHeader => Results.Count == 0 ? "Wyniki" : $"Wyniki ({Results.Count})";

    public ResultItemViewModel? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (!SetProperty(ref _selectedResult, value))
                return;
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(HasNoSelection));
        }
    }

    public bool HasSelection => SelectedResult is not null;
    public bool HasNoSelection => SelectedResult is null;

    public void SetCenter(GeoPoint point, string? placeName = null)
    {
        _latitude = point.Latitude;
        _longitude = point.Longitude;
        _locationIsNamedPlace = placeName is not null;
        _locationName = placeName ?? "Wskazane na mapie";
        OnPropertyChanged(nameof(Center));
        OnPropertyChanged(nameof(LocationText));
    }

    public async Task<bool> FindPlaceAsync()
    {
        if (string.IsNullOrWhiteSpace(PlaceQuery))
            return false;

        try
        {
            IsBusy = true;
            StatusMessage = $"Szukam \"{PlaceQuery}\"…";
            var place = await _geocoder.FindAsync(PlaceQuery);
            if (place is null)
            {
                StatusMessage = $"Nie znaleziono miejsca \"{PlaceQuery}\".";
                return false;
            }

            SetCenter(place.Point, place.DisplayName.Split(',')[0].Trim());
            StatusMessage = $"Ustawiono lokalizację: {place.DisplayName}.";
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Wyszukiwanie miejsca nie powiodło się: {ex.Message}";
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ResetFilters()
    {
        Make = "";
        Model = "";
        YearFrom = YearTo = MileageFrom = MileageTo = PriceFrom = PriceTo = double.NaN;
        FuelIndex = 0;
        GearboxIndex = 0;
        RadiusKm = 50;
        MaxPages = 2;
    }

    public async Task RefreshCatalogAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Pobieram marki i modele z otomoto.pl…";
            _catalog = await _refreshCatalog(CancellationToken.None);
            var (make, model) = (Make, Model);
            LoadMakeOptions();
            Make = make;
            Model = model;
            OnPropertyChanged(nameof(CatalogInfo));
            StatusMessage = $"Zaktualizowano: {CatalogInfo}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Nie udało się zaktualizować marek i modeli: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SearchAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        var providers = Sources.Where(s => s.IsEnabled).Select(s => s.Provider).ToList();
        if (providers.Count == 0)
        {
            StatusMessage = "Wybierz co najmniej jedno źródło.";
            return;
        }

        var criteria = BuildCriteria();
        var search = new CarSearchService(providers);
        _lastCriteria = criteria;

        IsBusy = true;
        StatusMessage = $"Przeszukuję serwisy ({string.Join(", ", providers.Select(p => p.Name))}), stron: {criteria.MaxPages}…";
        SearchNotes = "";
        SearchLinks.Clear();
        OnPropertyChanged(nameof(HasSearchLinks));
        SelectedResult = null;
        Results.Clear();
        OnPropertyChanged(nameof(ResultsHeader));
        ResultsReplaced?.Invoke(this, EventArgs.Empty);

        using var gate = new SemaphoreSlim(MaxParallelVerifications);
        var work = new List<Task>();
        var dropped = 0;
        var sitesDone = new List<string>();

        try
        {
            await foreach (var outcome in search.SearchStreamAsync(criteria, ct))
            {
                // Each site shows up as soon as it answers; its offers are verified while other sites are still searched.
                sitesDone.Add($"{outcome.Source}: {outcome.Results.Count}");
                SearchNotes = string.Join(Environment.NewLine, new[] { SearchNotes }.Where(n => n.Length > 0).Concat(outcome.Notes));
                foreach (var link in outcome.Links)
                    SearchLinks.Add(link);
                OnPropertyChanged(nameof(HasSearchLinks));

                var items = outcome.Results.Select(r => new ResultItemViewModel(r, criteria.Center)).ToList();
                foreach (var item in items)
                {
                    Results.Add(item);
                    ResultUpdated?.Invoke(this, item);
                }
                OnPropertyChanged(nameof(ResultsHeader));
                StatusMessage = $"Wyniki z: {string.Join(", ", sitesDone)}. Odczytuję ogłoszenia i sprawdzam VIN…";

                work.AddRange(items.Select(item => ProcessOneAsync(item, search, gate, () => dropped++, ct)));
            }

            await AddOlxLinkAsync(criteria, ct);
            await Task.WhenAll(work);

            // A VIN shared by several offers can only be seen once every offer page was read; re-check those.
            foreach (var item in Results.Where(i => i.Report is not null && HasSharedVin(i)).ToList())
            {
                item.Report = await _verifier.VerifyAsync(item.Listing, ContextFor(item, Results), ct);
                ResultUpdated?.Invoke(this, item);
            }
        }
        catch (OperationCanceledException)
        {
            return; // a newer search took over
        }

        var reports = Results.Select(i => i.Report).OfType<VinReport>().ToList();
        StatusMessage = Results.Count == 0
            ? "Brak aut spełniających kryteria. Zwiększ promień lub usuń część filtrów."
            : $"Aut: {Results.Count}. Zweryfikowane: {reports.Count(r => r.Status == VinStatus.Verified)}, " +
              $"z uwagami: {reports.Count(r => r.Status == VinStatus.VerifiedWithWarnings)}, " +
              $"podejrzane/nieprawidłowe: {reports.Count(r => r.Status is VinStatus.Suspicious or VinStatus.Invalid)}, " +
              $"VIN ukryty w serwisie: {reports.Count(r => r.Status == VinStatus.HiddenOnSite)}, " +
              $"bez VIN: {reports.Count(r => r.Status == VinStatus.NotPresent)}." +
              (dropped > 0 ? $" Pominięto {dropped} (po odczycie ogłoszenia nie spełniały kryteriów lub były dalej niż {criteria.RadiusKm:N0} km)." : "");
        IsBusy = false;
    }

    /// <summary>Checks a VIN the user typed in (e.g. revealed on the offer page).</summary>
    public async Task VerifyManualVinAsync(ResultItemViewModel item)
    {
        var vin = VinRules.Normalize(item.ManualVin);
        if (vin.Length == 0)
            return;

        item.UpdateListing(item.Listing with { Vin = vin, VinSource = VinSource.Manual, VinHiddenOnSite = false });
        await ReverifyAsync(item);
    }

    public async Task ReverifyAsync(ResultItemViewModel item)
    {
        item.Report = null;
        ResultUpdated?.Invoke(this, item);
        item.Report = await _verifier.VerifyAsync(item.Listing, ContextFor(item, Results));
        ResultUpdated?.Invoke(this, item);
    }

    private async Task ProcessOneAsync(ResultItemViewModel item, CarSearchService search, SemaphoreSlim gate, Action onDropped, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (!item.Listing.DetailsLoaded)
            {
                item.IsLoadingDetails = true;
                try
                {
                    item.UpdateListing(await search.LoadDetailsAsync(item.Listing, ct));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Without the offer page the VIN check still runs on what the results page gave us.
                }
                finally
                {
                    item.IsLoadingDetails = false;
                }
            }

            // Sites round the radius up and measure from a town centre, and some data is only known from the
            // offer page - drop what does not really match.
            if (_lastCriteria is { } criteria && !CarSearchService.Matches(criteria, item.Result))
            {
                Results.Remove(item);
                if (SelectedResult == item)
                    SelectedResult = null;
                OnPropertyChanged(nameof(ResultsHeader));
                ResultRemoved?.Invoke(this, item);
                onDropped();
                return;
            }

            item.Report = await _verifier.VerifyAsync(item.Listing, ContextFor(item, Results), ct);
            ResultUpdated?.Invoke(this, item);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task AddOlxLinkAsync(SearchCriteria criteria, CancellationToken ct)
    {
        try
        {
            var town = (await _places.ForAsync(criteria, ct)).FirstOrDefault();
            SearchLinks.Add(OlxLinks.Build(criteria with { CenterName = town?.Name ?? criteria.CenterName }));
            OnPropertyChanged(nameof(HasSearchLinks));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The OLX link is a convenience.
        }
    }

    private bool HasSharedVin(ResultItemViewModel item)
    {
        var context = ContextFor(item, Results);
        return context.OtherListingsWithSameVin > 0 || context.SameCarOnOtherSites is { Count: > 0 };
    }

    /// <summary>
    /// The same VIN on another site with the same make and year is the same car advertised twice (normal for dealers);
    /// on the same site, or with a different make/year, it is a warning sign.
    /// </summary>
    private static VerificationContext ContextFor(ResultItemViewModel item, IEnumerable<ResultItemViewModel> all)
    {
        var vin = VinRules.Normalize(item.Listing.Vin);
        if (vin.Length == 0)
            return new VerificationContext();

        var me = item.Listing;
        var others = all.Where(o => o != item && VinRules.Normalize(o.Listing.Vin) == vin).Select(o => o.Listing).ToList();
        var sameCarElsewhere = others
            .Where(o => o.Source != me.Source && VinVerifier.SameMake(o.Make, me.Make) &&
                        (o.Year <= 0 || me.Year <= 0 || Math.Abs(o.Year - me.Year) <= 1))
            .ToList();
        return new VerificationContext(
            others.Count - sameCarElsewhere.Count,
            sameCarElsewhere.Select(o => o.Source).Distinct().ToList());
    }

    private void LoadMakeOptions()
    {
        MakeOptions.Clear();
        foreach (var make in _catalog.Makes)
            MakeOptions.Add(make.Name);
    }

    private SearchCriteria BuildCriteria()
    {
        static int? AsInt(double v) => double.IsNaN(v) ? null : (int)v;
        static decimal? AsDecimal(double v) => double.IsNaN(v) ? null : (decimal)v;

        return new SearchCriteria
        {
            Make = Make,
            Model = Model,
            YearFrom = AsInt(YearFrom),
            YearTo = AsInt(YearTo),
            MileageFromKm = AsInt(MileageFrom),
            MileageToKm = AsInt(MileageTo),
            PriceFrom = AsDecimal(PriceFrom),
            PriceTo = AsDecimal(PriceTo),
            Fuel = (FuelType)Math.Max(0, FuelIndex),
            Gearbox = (Gearbox)Math.Max(0, GearboxIndex),
            Center = Center,
            CenterName = _locationIsNamedPlace ? _locationName : null,
            RadiusKm = RadiusKm,
            MaxPages = (int)MaxPages,
        };
    }
}
