using ClickPlay.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ClickPlay.App.ViewModels;

public partial class LocationItemViewModel : ViewModelBase
{
    public LocationItemViewModel(Venue venue)
    {
        Venue = venue;
    }

    public Venue Venue { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(Venue.Address)
        ? Venue.Name
        : $"{Venue.Name} — {Venue.Address}";

    [ObservableProperty]
    private bool _isSelected;
}
