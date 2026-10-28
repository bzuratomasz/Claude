using Microsoft.UI.Xaml;

namespace SearchCar.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        // The UI is in Polish: numbers like "183 100 km" and "12,5 km" regardless of the Windows region settings.
        var polish = System.Globalization.CultureInfo.GetCultureInfo("pl-PL");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = polish;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = polish;
        System.Globalization.CultureInfo.CurrentCulture = polish;
        System.Globalization.CultureInfo.CurrentUICulture = polish;

        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
