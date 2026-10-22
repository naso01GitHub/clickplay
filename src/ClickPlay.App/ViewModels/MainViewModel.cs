using System.Collections.ObjectModel;
using ClickPlay.App.Platform;
using ClickPlay.Core.Browser;
using ClickPlay.Core.Mail;
using ClickPlay.Core.Models;
using ClickPlay.Core.Services;
using ClickPlay.Core.Validation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickPlay.App.ViewModels;

/// <summary>
/// Main window ViewModel — GUI part: constructor, Sport → City → Locations cascade, search inputs,
/// status banner, and panel visibility. Web search loop: <c>MainViewModel.Web.cs</c>; mail: <c>MainViewModel.Mail.cs</c>.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IClickToPlayCatalog _catalog;
    private CancellationTokenSource? _loadCts;
    private bool _suppressCityReload;
    private bool _suppressVenueReload;

    /// <summary>
    /// Creates the main window ViewModel.
    /// </summary>
    /// <param name="catalog">Site access.</param>
    /// <param name="mailSender">Sends results mail; null disables mail.</param>
    /// <param name="mailSettingsStore">Loads and saves the Email section; null keeps it in memory only.</param>
    /// <param name="keepAwake">Blocks system sleep while Search runs; null lets the PC sleep.</param>
    /// <example>
    /// <code>
    /// var vm = new MainViewModel(catalog, new MailKitMailSender(), new MailSettingsStore(new ProtectedSecretStore()), new SystemKeepAwake());
    /// </code>
    /// </example>
    public MainViewModel(
        IClickToPlayCatalog catalog,
        IMailSender? mailSender = null,
        MailSettingsStore? mailSettingsStore = null,
        IKeepAwake? keepAwake = null)
    {
        _catalog = catalog;
        _mailSender = mailSender;
        _mailSettingsStore = mailSettingsStore;
        _keepAwake = keepAwake;
        Sports.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsSportsVisible));
        Cities.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsCityVisible));
        Locations.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(IsLocationsVisible));
            OnPropertyChanged(nameof(IsLocationsPanelVisible));
        };
        LoadMailSettings();
        SyncSmtpPicks();
    }

    public ObservableCollection<Sport> Sports { get; } = [];

    public ObservableCollection<City> Cities { get; } = [];

    public ObservableCollection<LocationItemViewModel> Locations { get; } = [];

    public ObservableCollection<string> Results { get; } = [];

    [ObservableProperty]
    private Sport? _selectedSport;

    [ObservableProperty]
    private City? _selectedCity;

    /// <summary>
    /// Search start date. Defaults to today.
    /// </summary>
    [ObservableProperty]
    private DateTime? _selectedDate = DateTime.Today;

    public DateTime CalendarDisplayDate => (SelectedDate ?? DateTime.Today).Date;

    partial void OnSelectedDateChanged(DateTime? value) =>
        OnPropertyChanged(nameof(CalendarDisplayDate));

    [ObservableProperty]
    private string _daysAheadText = "7";

    /// <summary>
    /// Minutes to wait after closing the browser before the next search cycle. Digits only, 1–60.
    /// </summary>
    [ObservableProperty]
    private string _repeatText = "2";

    private bool _suppressRepeatSanitize;

    private decimal _weekdayFromHour = 18;
    private decimal _weekdayFromMinute;
    private decimal _weekdayToHour = 21;
    private decimal _weekdayToMinute;
    private decimal _weekendFromHour = 10;
    private decimal _weekendFromMinute;
    private decimal _weekendToHour = 20;
    private decimal _weekendToMinute;

    public decimal WeekdayFromHour
    {
        get => _weekdayFromHour;
        set => SetClock(ref _weekdayFromHour, value, 23);
    }

    public decimal WeekdayFromMinute
    {
        get => _weekdayFromMinute;
        set => SetClock(ref _weekdayFromMinute, value, 59);
    }

    public decimal WeekdayToHour
    {
        get => _weekdayToHour;
        set => SetClock(ref _weekdayToHour, value, 23);
    }

    public decimal WeekdayToMinute
    {
        get => _weekdayToMinute;
        set => SetClock(ref _weekdayToMinute, value, 59);
    }

    public decimal WeekendFromHour
    {
        get => _weekendFromHour;
        set => SetClock(ref _weekendFromHour, value, 23);
    }

    public decimal WeekendFromMinute
    {
        get => _weekendFromMinute;
        set => SetClock(ref _weekendFromMinute, value, 59);
    }

    public decimal WeekendToHour
    {
        get => _weekendToHour;
        set => SetClock(ref _weekendToHour, value, 23);
    }

    public decimal WeekendToMinute
    {
        get => _weekendToMinute;
        set => SetClock(ref _weekendToMinute, value, 59);
    }

    [ObservableProperty]
    private string _statusMessage = "Load sports to begin.";

    /// <summary>Banner style of <see cref="StatusMessage"/>.</summary>
    public StatusKind StatusKind => StatusClassifier.Classify(StatusMessage);

    /// <summary>Banner icon for <see cref="StatusMessage"/>.</summary>
    public string StatusIcon => StatusClassifier.Icon(StatusKind);

    /// <summary>True when the banner shows an error (red).</summary>
    public bool StatusIsError => StatusKind == StatusKind.Error;

    /// <summary>True when the banner shows a warning (amber).</summary>
    public bool StatusIsWarning => StatusKind == StatusKind.Warning;

    /// <summary>True when the banner shows a success (green).</summary>
    public bool StatusIsSuccess => StatusKind == StatusKind.Success;

    /// <summary>True when the banner shows work in progress (blue-grey).</summary>
    public bool StatusIsWorking => StatusKind == StatusKind.Working;

    /// <summary>
    /// Called by CommunityToolkit when the status text changes; refreshes the banner style properties.
    /// </summary>
    /// <param name="value">New status text.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// viewModel.StatusMessage = "Search failed (…)"; // StatusIsError becomes true
    /// </code>
    /// </example>
    partial void OnStatusMessageChanged(string value)
    {
        OnPropertyChanged(nameof(StatusKind));
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusIsError));
        OnPropertyChanged(nameof(StatusIsWarning));
        OnPropertyChanged(nameof(StatusIsSuccess));
        OnPropertyChanged(nameof(StatusIsWorking));
    }

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isSearching;

    public bool IsSportsVisible => Sports.Count > 0;

    public bool IsCityVisible => Cities.Count > 0;

    public bool IsLocationsVisible => Locations.Count > 0;

    /// <summary>
    /// True while the Email (SMTP) expander is open.
    /// </summary>
    [ObservableProperty]
    private bool _isMailExpanded;

    /// <summary>
    /// Locations list is shown when there are locations and the Email section is closed (they overlap otherwise).
    /// </summary>
    public bool IsLocationsPanelVisible => IsLocationsVisible && !IsMailExpanded;

    /// <summary>
    /// Called by CommunityToolkit when <see cref="IsMailExpanded"/> changes; refreshes <see cref="IsLocationsPanelVisible"/>.
    /// </summary>
    /// <param name="value">New expander state.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// viewModel.IsMailExpanded = true; // Locations panel hides
    /// </code>
    /// </example>
    partial void OnIsMailExpandedChanged(bool value) => OnPropertyChanged(nameof(IsLocationsPanelVisible));

    public bool CanSearch =>
        !IsBusy
        && SelectedSport is not null
        && SelectedCity is not null
        && Locations.Any(l => l.IsSelected);

    public bool CanStop => IsSearching;

    partial void OnSelectedSportChanged(Sport? value)
    {
        OnPropertyChanged(nameof(CanSearch));
        if (_suppressCityReload)
        {
            return;
        }

        _ = LoadCitiesAsync();
    }

    partial void OnSelectedCityChanged(City? value)
    {
        OnPropertyChanged(nameof(CanSearch));
        if (_suppressVenueReload)
        {
            return;
        }

        _ = LoadVenuesAsync();
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(CanStop));
    }

    partial void OnIsSearchingChanged(bool value) => OnPropertyChanged(nameof(CanStop));

    partial void OnRepeatTextChanged(string value)
    {
        if (_suppressRepeatSanitize)
        {
            return;
        }

        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length > 0 && int.TryParse(digits, out var n) && n > 60)
        {
            digits = "60";
        }

        if (digits == value)
        {
            return;
        }

        _suppressRepeatSanitize = true;
        RepeatText = digits;
        _suppressRepeatSanitize = false;
    }

    private void SetClock(ref decimal field, decimal value, int max, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        var clamped = SearchInputValidator.ClampClockNumber(value, max);
        if (!SetProperty(ref field, clamped, propertyName) && clamped != value)
        {
            OnPropertyChanged(propertyName);
        }
    }

    [RelayCommand]
    private async Task LoadSportsAsync()
    {
        var cancellationToken = ResetLoadToken();
        IsBusy = true;
        StatusMessage = "Loading sports…";
        try
        {
            await _catalog.StartAsync(BrowserLaunchOptions, cancellationToken).ConfigureAwait(true);
            var sports = await _catalog.GetSportsAsync(cancellationToken).ConfigureAwait(true);
            Sports.Clear();
            foreach (var sport in sports)
            {
                Sports.Add(sport);
            }

            _suppressCityReload = true;
            SelectedSport = null;
            _suppressCityReload = false;
            ClearCities();
            StatusMessage = sports.Count == 0
                ? "No sports found."
                : $"{sports.Count} sports loaded. Select a sport.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Loading cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load sports: {ex.Message}";
        }
        finally
        {
            await CloseCatalogQuietlyAsync().ConfigureAwait(true);
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAllLocations()
    {
        foreach (var location in Locations)
        {
            location.IsSelected = true;
        }

        OnPropertyChanged(nameof(CanSearch));
    }

    [RelayCommand]
    private void ClearLocations()
    {
        foreach (var location in Locations)
        {
            location.IsSelected = false;
        }

        OnPropertyChanged(nameof(CanSearch));
    }

    private async Task LoadCitiesAsync()
    {
        ClearCities();
        if (SelectedSport is null)
        {
            return;
        }

        var cancellationToken = ResetLoadToken();
        IsBusy = true;
        StatusMessage = "Opening clicktoplay.io for cities…";
        try
        {
            var sport = SelectedSport;
            await _catalog.StartAsync(BrowserLaunchOptions, cancellationToken).ConfigureAwait(true);
            var cities = await _catalog.GetCitiesAsync(sport, cancellationToken).ConfigureAwait(true);
            foreach (var city in cities)
            {
                Cities.Add(city);
            }

            StatusMessage = cities.Count == 0
                ? "No cities for this sport."
                : "Select a city.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Loading cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load cities: {ex.Message}";
        }
        finally
        {
            await CloseCatalogQuietlyAsync().ConfigureAwait(true);
            IsBusy = false;
        }
    }

    private async Task LoadVenuesAsync()
    {
        ClearVenues();
        if (SelectedSport is null || SelectedCity is null)
        {
            return;
        }

        var cancellationToken = ResetLoadToken();
        IsBusy = true;
        StatusMessage = "Opening clicktoplay.io for locations…";
        try
        {
            await _catalog.StartAsync(BrowserLaunchOptions, cancellationToken).ConfigureAwait(true);
            var venues = await _catalog.GetVenuesAsync(SelectedSport, SelectedCity, cancellationToken).ConfigureAwait(true);
            foreach (var venue in venues)
            {
                var item = new LocationItemViewModel(venue);
                item.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(LocationItemViewModel.IsSelected))
                    {
                        OnPropertyChanged(nameof(CanSearch));
                    }
                };
                Locations.Add(item);
            }

            StatusMessage = venues.Count == 0
                ? "No locations for this city."
                : "Select one or more locations.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Loading cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load locations: {ex.Message}";
        }
        finally
        {
            await CloseCatalogQuietlyAsync().ConfigureAwait(true);
            IsBusy = false;
            OnPropertyChanged(nameof(CanSearch));
        }
    }

    private void ClearCities()
    {
        _suppressVenueReload = true;
        SelectedCity = null;
        _suppressVenueReload = false;
        Cities.Clear();
        ClearVenues();
        OnPropertyChanged(nameof(CanSearch));
    }

    private void ClearVenues()
    {
        Locations.Clear();
        OnPropertyChanged(nameof(CanSearch));
    }

    private CancellationToken ResetLoadToken()
    {
        var previous = _loadCts;
        _loadCts = new CancellationTokenSource();
        previous?.Cancel();
        return _loadCts.Token;
    }
}
