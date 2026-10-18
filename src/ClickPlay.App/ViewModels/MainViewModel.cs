using System.Collections.ObjectModel;
using ClickPlay.Core.Models;
using ClickPlay.Core.Services;
using ClickPlay.Core.Validation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickPlay.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IClickToPlayCatalog _catalog;
    private CancellationTokenSource? _loadCts;
    private bool _suppressCityReload;
    private bool _suppressVenueReload;

    public MainViewModel(IClickToPlayCatalog catalog)
    {
        _catalog = catalog;
    }

    public ObservableCollection<Sport> Sports { get; } = [];

    public ObservableCollection<City> Cities { get; } = [];

    public ObservableCollection<LocationItemViewModel> Locations { get; } = [];

    public ObservableCollection<string> Results { get; } = [];

    [ObservableProperty]
    private Sport? _selectedSport;

    [ObservableProperty]
    private City? _selectedCity;

    [ObservableProperty]
    private string _daysAheadText = "7";

    [ObservableProperty]
    private string _weekdayFromText = "18:00";

    [ObservableProperty]
    private string _weekdayToText = "21:00";

    [ObservableProperty]
    private string _weekendFromText = "10:00";

    [ObservableProperty]
    private string _weekendToText = "20:00";

    [ObservableProperty]
    private string _statusMessage = "Load sports to begin.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _smtpHost = string.Empty;

    [ObservableProperty]
    private string _smtpPort = "587";

    [ObservableProperty]
    private string _smtpUser = string.Empty;

    [ObservableProperty]
    private string _smtpPassword = string.Empty;

    [ObservableProperty]
    private string _mailFrom = string.Empty;

    /// <summary>
    /// Recipient addresses, one per line (commas or semicolons also accepted).
    /// </summary>
    [ObservableProperty]
    private string _mailTo = string.Empty;

    /// <summary>
    /// Parsed To recipients in order, one address per entry.
    /// </summary>
    public IReadOnlyList<string> MailRecipients => EmailRecipientParser.Parse(MailTo);

    /// <summary>
    /// When true, Playwright starts Chromium with Headless = true. When false, Headless = false (visible browser).
    /// </summary>
    [ObservableProperty]
    private bool _headless;

    public IReadOnlyList<bool> HeadlessOptions { get; } = [false, true];

    public BrowserLaunchOptions BrowserLaunchOptions => new(Headless);

    public bool IsCityVisible => SelectedSport is not null;

    public bool IsLocationsVisible => SelectedSport is not null && SelectedCity is not null;

    public bool CanSearch =>
        !IsBusy
        && SelectedSport is not null
        && SelectedCity is not null
        && Locations.Any(l => l.IsSelected);

    partial void OnSelectedSportChanged(Sport? value)
    {
        OnPropertyChanged(nameof(IsCityVisible));
        OnPropertyChanged(nameof(IsLocationsVisible));
        OnPropertyChanged(nameof(CanSearch));
        if (_suppressCityReload)
        {
            return;
        }

        _ = LoadCitiesAsync();
    }

    partial void OnSelectedCityChanged(City? value)
    {
        OnPropertyChanged(nameof(IsLocationsVisible));
        OnPropertyChanged(nameof(CanSearch));
        if (_suppressVenueReload)
        {
            return;
        }

        _ = LoadVenuesAsync();
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CanSearch));

    partial void OnMailToChanged(string value) => OnPropertyChanged(nameof(MailRecipients));

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

    [RelayCommand]
    private void Search()
    {
        Results.Clear();
        if (!SearchInputValidator.TryParseDaysAhead(DaysAheadText, out var daysAhead, out var daysError))
        {
            StatusMessage = daysError;
            return;
        }

        if (!TryReadWindows(out var weekday, out var weekend, out var windowError))
        {
            StatusMessage = windowError;
            return;
        }

        var selected = Locations.Where(l => l.IsSelected).ToList();
        if (SelectedSport is null || SelectedCity is null || selected.Count == 0)
        {
            StatusMessage = "Select a sport, a city, and at least one location.";
            return;
        }

        Results.Add($"Sport: {SelectedSport.Name}");
        Results.Add($"City: {SelectedCity.Name}");
        Results.Add($"Headless: {Headless}");
        Results.Add("To:");
        foreach (var recipient in MailRecipients)
        {
            Results.Add($"  • {recipient}");
        }
        Results.Add($"Days ahead: {daysAhead}");
        Results.Add($"Weekdays: {weekday.From:HH:mm}–{weekday.To:HH:mm}");
        Results.Add($"Weekends and holidays: {weekend.From:HH:mm}–{weekend.To:HH:mm}");
        Results.Add("Locations:");
        foreach (var location in selected)
        {
            Results.Add($"  • {location.DisplayName}");
        }

        StatusMessage = "Search criteria ready. Availability scraping and email will run when Playwright is connected.";
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
        StatusMessage = "Loading cities…";
        try
        {
            var sport = SelectedSport;
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
        StatusMessage = "Loading locations…";
        try
        {
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
        OnPropertyChanged(nameof(IsLocationsVisible));
        OnPropertyChanged(nameof(CanSearch));
    }

    private void ClearVenues()
    {
        Locations.Clear();
        OnPropertyChanged(nameof(CanSearch));
    }

    private bool TryReadWindows(out TimeWindow weekday, out TimeWindow weekend, out string error)
    {
        weekday = new TimeWindow(default, default);
        weekend = weekday;

        if (!SearchInputValidator.TryParseTime(WeekdayFromText, out var wf, out error)
            || !SearchInputValidator.TryParseTime(WeekdayToText, out var wt, out error))
        {
            return false;
        }

        weekday = new TimeWindow(wf, wt);
        if (!weekday.IsValid)
        {
            error = "Weekday from time must be before to time.";
            return false;
        }

        if (!SearchInputValidator.TryParseTime(WeekendFromText, out var xf, out error)
            || !SearchInputValidator.TryParseTime(WeekendToText, out var xt, out error))
        {
            return false;
        }

        weekend = new TimeWindow(xf, xt);
        if (!weekend.IsValid)
        {
            error = "Weekend/holiday from time must be before to time.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private CancellationToken ResetLoadToken()
    {
        var previous = _loadCts;
        _loadCts = new CancellationTokenSource();
        previous?.Cancel();
        return _loadCts.Token;
    }
}
