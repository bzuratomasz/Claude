using CommunityToolkit.Mvvm.ComponentModel;
using SearchCar.Core.Listings;

namespace SearchCar.App.ViewModels;

/// <summary>One website the user can include in the search.</summary>
public sealed class SourceOptionViewModel(ICarListingProvider provider, string label, string hint, bool isEnabled) : ObservableObject
{
    private bool _isEnabled = isEnabled;

    public ICarListingProvider Provider { get; } = provider;
    public string Label { get; } = label;
    public string Hint { get; } = hint;

    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }
}
