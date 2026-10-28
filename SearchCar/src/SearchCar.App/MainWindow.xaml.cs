using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using SearchCar.App.ViewModels;
using SearchCar.Core.Geo;
using SearchCar.Core.Listings;
using SearchCar.Core.Models;
using SearchCar.Core.Vin;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace SearchCar.App;

public sealed partial class MainWindow : Window
{
    private const string MapHost = "searchcar.local";

    private static readonly HttpClient Http = CreateHttpClient();

    private static readonly string AppDataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SearchCar");

    private static readonly string CatalogCachePath = Path.Combine(AppDataFolder, "otomoto-catalog.json");

    private bool _mapReady;

    public MainWindow()
    {
        var geocoder = new NominatimGeocoder(Http);
        var places = new PlaceCandidates(geocoder);
        var towns = new TownLocator(geocoder);
        var catalog = VehicleCatalog.LoadFileOrEmbedded(CatalogCachePath);
        SourceOptionViewModel[] sources =
        [
            new(new OtomotoListingProvider(Http, places, catalog), "otomoto.pl", "największy serwis w Polsce", true),
            new(new AutoplacListingProvider(Http, places), "autoplac.pl", "komisy i dealerzy, VIN zwykle jawny", true),
            new(new SprzedajemyListingProvider(Http, places), "sprzedajemy.pl", "lokalne ogłoszenia; wolniejsze (czyta każde ogłoszenie)", true),
            new(new AutotraderListingProvider(Http, places, towns), "autotrader.pl", "ogłoszenia z województwa, odległość wg miejscowości", true),
            new(new MotogratkaListingProvider(Http, places, towns), "motogratka.pl", "tylko 1. strona wyników dla miejscowości (zasady serwisu)", true),
            new(new AutoScout24ListingProvider(Http, new NbpExchangeRates(Http)), "AutoScout24", "auta z zagranicy (DE, AT...), ceny w EUR", false),
        ];
        ViewModel = new MainViewModel(
            sources,
            new VinVerifier(new NhtsaVinDecoder(Http)),
            geocoder,
            places,
            catalog,
            async ct =>
            {
                var fresh = await VehicleCatalog.FetchFromOtomotoAsync(Http, cancellationToken: ct);
                await fresh.SaveAsync(CatalogCachePath, ct);
                return fresh;
            });

        InitializeComponent();
        AppWindow.Resize(new SizeInt32(1600, 950));
        ApplyDarkTitleBar();
        MapView.DefaultBackgroundColor = Windows.UI.Color.FromArgb(255, 0x20, 0x20, 0x20); // no white flash before the map loads

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ViewModel.ResultsReplaced += (_, _) => _ = PushListingsToMapAsync();
        ViewModel.ResultUpdated += (_, item) => _ = RunMapScriptAsync($"upsertListing({MapItemJson(item)})");
        ViewModel.ResultRemoved += (_, item) => _ = RunMapScriptAsync($"removeListing({Js(item.Id)})");

        _ = InitializeMapAsync();
    }

    public MainViewModel ViewModel { get; }

    // ---------------------------------------------------------------- map

    private async Task InitializeMapAsync()
    {
        try
        {
            // An unpackaged app cannot write next to its exe when installed under Program Files; keep the browser profile in LocalAppData.
            var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SearchCar", "WebView2");
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userData);

            await MapView.EnsureCoreWebView2Async();
            MapView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                MapHost, Path.Combine(AppContext.BaseDirectory, "Web"), CoreWebView2HostResourceAccessKind.Allow);
            MapView.CoreWebView2.WebMessageReceived += MapView_WebMessageReceived;
            MapView.Source = new Uri($"https://{MapHost}/map.html");
        }
        catch (Exception ex)
        {
            ShowMapError($"Mapa niedostępna (czy zainstalowano WebView2 Runtime?): {ex.Message}");
        }
    }

    private void MapView_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        using var doc = JsonDocument.Parse(args.WebMessageAsJson);
        var root = doc.RootElement;
        switch (root.GetProperty("type").GetString())
        {
            case "ready":
                _mapReady = true;
                _ = SyncMapCenterAsync(fit: true);
                _ = PushListingsToMapAsync();
                break;

            case "center":
                ViewModel.SetCenter(new GeoPoint(root.GetProperty("lat").GetDouble(), root.GetProperty("lon").GetDouble()));
                break;

            case "select":
                var id = root.GetProperty("id").GetString();
                var item = ViewModel.Results.FirstOrDefault(r => r.Id == id);
                if (item is not null)
                {
                    ResultsList.SelectedItem = item;
                    ResultsList.ScrollIntoView(item);
                }
                break;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.RadiusKm):
                _ = RunMapScriptAsync($"setRadius({Num(ViewModel.RadiusKm)})");
                break;
        }
    }

    private Task SyncMapCenterAsync(bool fit)
    {
        var c = ViewModel.Center;
        return RunMapScriptAsync($"setCenter({Num(c.Latitude)}, {Num(c.Longitude)}, {Num(ViewModel.RadiusKm)}, {(fit ? "true" : "false")})");
    }

    private Task PushListingsToMapAsync()
    {
        var items = ViewModel.Results.Where(r => r.Listing.Location is not null).Select(MapItem);
        return RunMapScriptAsync($"setListings({JsonSerializer.Serialize(items)})");
    }

    /// <summary>Marker data for the map; offers without a known position yet are sent with null coordinates and skipped.</summary>
    private static object MapItem(ResultItemViewModel r) => new
    {
        id = r.Id,
        lat = r.Listing.Location?.Latitude,
        lon = r.Listing.Location?.Longitude,
        title = $"{r.Title} · {r.PriceText}",
        color = r.StatusColorHex,
    };

    private static string MapItemJson(ResultItemViewModel r) => JsonSerializer.Serialize(MapItem(r));

    private async Task RunMapScriptAsync(string script)
    {
        if (!_mapReady)
            return;
        try
        {
            // Functions are only defined when Leaflet loaded; guard so an offline map does not throw.
            await MapView.CoreWebView2.ExecuteScriptAsync($"if (window.setCenter) {{ {script}; }}");
        }
        catch (Exception)
        {
            // The map is a convenience; never let it break the search.
        }
    }

    private static string Num(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Js(string value) => JsonSerializer.Serialize(value);

    // ---------------------------------------------------------------- search panel

    private async void Search_Click(object sender, RoutedEventArgs e)
    {
        await SyncMapCenterAsync(fit: true);
        await ViewModel.SearchAsync();
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ViewModel.ResetFilters();

    private async void FindPlace_Click(object sender, RoutedEventArgs e) => await FindPlaceAsync();

    private async void PlaceQuery_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            await FindPlaceAsync();
        }
    }

    private async Task FindPlaceAsync()
    {
        if (await ViewModel.FindPlaceAsync())
            await SyncMapCenterAsync(fit: true);
    }

    private void ShowMapError(string message) =>
        DispatcherQueue.TryEnqueue(() => ReportActionMessage.Text = message);

    // ---------------------------------------------------------------- results + report

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = ResultsList.SelectedItem as ResultItemViewModel;
        ViewModel.SelectedResult = item;
        ReportActionMessage.Text = "";
        if (item is not null)
            _ = RunMapScriptAsync($"selectListing({Js(item.Id)})");
    }

    private void CopyReport_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedResult?.Report is not { } report)
            return;

        var package = new DataPackage();
        package.SetText(report.ToText());
        Clipboard.SetContent(package);
        ReportActionMessage.Text = "Raport skopiowano do schowka.";
    }

    private async void SaveReport_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedResult?.Report is not { } report)
            return;

        var fileName = $"raport-VIN-{(string.IsNullOrEmpty(report.Vin) ? report.ListingId : report.Vin)}";
        try
        {
            var picker = new FileSavePicker { SuggestedFileName = fileName };
            picker.FileTypeChoices.Add("Plik tekstowy", [".txt"]);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(this));

            var file = await picker.PickSaveFileAsync();
            if (file is null)
                return;
            await FileIO.WriteTextAsync(file, report.ToText());
            ReportActionMessage.Text = $"Zapisano: {file.Path}";
        }
        catch (Exception)
        {
            // The picker is not available when the app runs elevated; fall back to Documents.
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SearchCar");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, fileName + ".txt");
            await File.WriteAllTextAsync(path, report.ToText());
            ReportActionMessage.Text = $"Zapisano: {path}";
        }
    }

    private async void OpenOffer_Click(object sender, RoutedEventArgs e) => await OpenOfferAsync(ViewModel.SelectedResult);

    private async void ResultsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) =>
        await OpenOfferAsync(ViewModel.SelectedResult);

    private async Task OpenOfferAsync(ResultItemViewModel? item)
    {
        if (item?.Listing.Url is { } url)
            await Launcher.LaunchUriAsync(url);
    }

    private async void VerifyManualVin_Click(object sender, RoutedEventArgs e) => await VerifyManualVinAsync();

    private async void ManualVin_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            await VerifyManualVinAsync();
        }
    }

    private async Task VerifyManualVinAsync()
    {
        if (ViewModel.SelectedResult is { } item)
        {
            ReportActionMessage.Text = "";
            await ViewModel.VerifyManualVinAsync(item);
        }
    }

    private async void RefreshCatalog_Click(object sender, RoutedEventArgs e) => await ViewModel.RefreshCatalogAsync();

    private async void Reverify_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedResult is { } item)
        {
            ReportActionMessage.Text = "";
            await ViewModel.ReverifyAsync(item);
        }
    }

    /// <summary>The app uses the dark theme; the system title bar follows the Windows setting unless coloured explicitly.</summary>
    private void ApplyDarkTitleBar()
    {
        var bar = AppWindow.TitleBar;
        var background = Windows.UI.Color.FromArgb(255, 0x20, 0x20, 0x20);
        var hover = Windows.UI.Color.FromArgb(255, 0x3A, 0x3A, 0x3A);
        var white = Windows.UI.Color.FromArgb(255, 0xFF, 0xFF, 0xFF);
        var grey = Windows.UI.Color.FromArgb(255, 0x9E, 0x9E, 0x9E);
        bar.BackgroundColor = bar.ButtonBackgroundColor = background;
        bar.InactiveBackgroundColor = bar.ButtonInactiveBackgroundColor = background;
        bar.ForegroundColor = bar.ButtonForegroundColor = white;
        bar.InactiveForegroundColor = bar.ButtonInactiveForegroundColor = grey;
        bar.ButtonHoverBackgroundColor = hover;
        bar.ButtonHoverForegroundColor = white;
        bar.ButtonPressedBackgroundColor = hover;
    }

    private static HttpClient CreateHttpClient()
    {
        // Some classified sites need 10-30 s for a results page.
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        // Nominatim's usage policy requires an identifying User-Agent.
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SearchCar/1.0 (WinUI desktop app)");
        return http;
    }
}
