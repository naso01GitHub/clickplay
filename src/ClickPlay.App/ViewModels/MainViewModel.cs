using System.Collections.ObjectModel;
using ClickPlay.Core.Browser;
using ClickPlay.Core.Models;
using ClickPlay.Core.Services;
using ClickPlay.Core.Validation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickPlay.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IClickToPlayCatalog _catalog;
    private readonly HashSet<string> _seenResultLines = new(StringComparer.Ordinal);
    private CancellationTokenSource? _loadCts;
    private bool _suppressCityReload;
    private bool _suppressVenueReload;

    public MainViewModel(IClickToPlayCatalog catalog)
    {
        _catalog = catalog;
        Sports.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsSportsVisible));
        Cities.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsCityVisible));
        Locations.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsLocationsVisible));
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
    private string _repeatText = "1";

    /// <summary>
    /// Tests can shorten the wait between cycles.
    /// </summary>
    public TimeSpan? SearchLoopDelayOverride { get; set; }

    /// <summary>
    /// Clock for Results scan stamps. Tests can replace it.
    /// </summary>
    public Func<DateTime> ScanClock { get; set; } = () => DateTime.Now;

    /// <summary>
    /// Results log on disk. Defaults to app-data <c>results-log.txt</c>. Tests can replace it.
    /// </summary>
    public string ResultsLogPath { get; set; } = AppPaths.ResultsLogFile();

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

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isSearching;

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
    /// When true, Chromium is shown. When false (default), Playwright runs headless.
    /// </summary>
    [ObservableProperty]
    private bool _debug;

    public IReadOnlyList<bool> DebugOptions { get; } = [false, true];

    public BrowserLaunchOptions BrowserLaunchOptions => new(Headless: !Debug);

    public bool IsSportsVisible => Sports.Count > 0;

    public bool IsCityVisible => Cities.Count > 0;

    public bool IsLocationsVisible => Locations.Count > 0;

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

    [RelayCommand]
    private void Stop()
    {
        _loadCts?.Cancel();
        StatusMessage = "Stopping search…";
    }

    [RelayCommand]
    private async Task Search()
    {
        if (!SearchInputValidator.TryParseDaysAhead(DaysAheadText, out var daysAhead, out var daysError))
        {
            StatusMessage = daysError;
            return;
        }

        if (!SearchInputValidator.TryParseRepeat(RepeatText, out var repeatMinutes, out var repeatError))
        {
            StatusMessage = repeatError;
            return;
        }

        if (!TryReadWindows(out var weekday, out var weekend, out var windowError))
        {
            StatusMessage = windowError;
            return;
        }

        var selected = Locations.Where(l => l.IsSelected).Select(l => l.Venue).ToList();
        if (SelectedSport is null || SelectedCity is null || selected.Count == 0)
        {
            StatusMessage = "Select a sport, a city, and at least one location.";
            return;
        }

        var sport = SelectedSport;
        var city = SelectedCity;
        var cancellationToken = ResetLoadToken();
        var progress = new Progress<string>(msg => StatusMessage = msg);

        IsSearching = true;
        IsBusy = true;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var cycleLocations = Locations.Where(l => l.IsSelected).Select(l => l.Venue).ToList();
                    if (cycleLocations.Count == 0)
                    {
                        StatusMessage = "Select at least one location.";
                        await WaitBeforeNextSearchAsync(ReadWaitDuration(repeatMinutes), cancellationToken)
                            .ConfigureAwait(true);
                        continue;
                    }

                    if (!TryReadSearchCycle(sport, city, cycleLocations, out var request, out var cycleError))
                    {
                        StatusMessage = cycleError;
                        await WaitBeforeNextSearchAsync(ReadWaitDuration(repeatMinutes), cancellationToken)
                            .ConfigureAwait(true);
                        continue;
                    }

                    StatusMessage = "Opening clicktoplay.io…";
                    await _catalog.StartAsync(BrowserLaunchOptions, cancellationToken).ConfigureAwait(true);
                    var slots = await _catalog.SearchAvailabilityAsync(request, progress, cancellationToken)
                        .ConfigureAwait(true);

                    var newCount = PublishNewResults(slots);
                    StatusMessage = newCount == 0
                        ? (slots.Count == 0
                            ? "No free hours found. Closing browser…"
                            : "No new free hours. Closing browser…")
                        : $"Found {newCount} new free hour(s). Closing browser…";
                    await _catalog.CloseBrowserAsync(cancellationToken).ConfigureAwait(true);

                    await WaitBeforeNextSearchAsync(ReadWaitDuration(repeatMinutes), cancellationToken)
                        .ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    ResetResultsLog();
                    StatusMessage = FormatSearchFailure(StatusMessage, ex);
                    await CloseCatalogQuietlyAsync().ConfigureAwait(true);
                    await WaitBeforeNextSearchAsync(ReadWaitDuration(repeatMinutes), cancellationToken)
                        .ConfigureAwait(true);
                }
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Search stopped.";
        }
        finally
        {
            await CloseCatalogQuietlyAsync().ConfigureAwait(true);
            IsSearching = false;
            IsBusy = false;
            OnPropertyChanged(nameof(CanSearch));
            OnPropertyChanged(nameof(CanStop));
        }
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

    private bool TryReadSearchCycle(
        Sport sport,
        City city,
        IReadOnlyList<Venue> selected,
        out SearchRequest request,
        out string error)
    {
        request = null!;
        if (!SearchInputValidator.TryParseDaysAhead(DaysAheadText, out var daysAhead, out error))
        {
            return false;
        }

        if (!TryReadWindows(out var weekday, out var weekend, out error))
        {
            return false;
        }

        var start = DateOnly.FromDateTime((SelectedDate ?? DateTime.Today).Date);
        request = new SearchRequest(sport, city, selected, start, daysAhead, weekday, weekend);
        error = string.Empty;
        return true;
    }

    private TimeSpan ReadWaitDuration(int fallbackMinutes)
    {
        if (SearchLoopDelayOverride is { } overrideWait)
        {
            return overrideWait;
        }

        if (SearchInputValidator.TryParseRepeat(RepeatText, out var minutes, out _))
        {
            return TimeSpan.FromMinutes(minutes);
        }

        return TimeSpan.FromMinutes(fallbackMinutes);
    }

    private async Task CloseCatalogQuietlyAsync()
    {
        try
        {
            await _catalog.CloseBrowserAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch
        {
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

    private bool TryReadWindows(out TimeWindow weekday, out TimeWindow weekend, out string error)
    {
        weekday = new TimeWindow(default, default);
        weekend = weekday;

        var wf = SearchInputValidator.ToClockTime(WeekdayFromHour, WeekdayFromMinute);
        var wt = SearchInputValidator.ToClockTime(WeekdayToHour, WeekdayToMinute);
        weekday = new TimeWindow(wf, wt);
        if (!weekday.IsValid)
        {
            error = "Weekday from time must be before to time.";
            return false;
        }

        var xf = SearchInputValidator.ToClockTime(WeekendFromHour, WeekendFromMinute);
        var xt = SearchInputValidator.ToClockTime(WeekendToHour, WeekendToMinute);
        weekend = new TimeWindow(xf, xt);
        if (!weekend.IsValid)
        {
            error = "Weekend/holiday from time must be before to time.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Status while waiting between cycles: minutes while over 1 minute, else seconds.
    /// </summary>
    public static string FormatWaitStatus(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return "Starting next search…";
        }

        if (remaining >= TimeSpan.FromMinutes(1))
        {
            var minutes = Math.Max(1, (int)Math.Floor(remaining.TotalMinutes));
            return $"Waiting {minutes} minute(s) before the next search…";
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
        return $"Waiting {seconds} second(s) before the next search…";
    }

    /// <summary>
    /// Short failure line: last search step plus exception type and message.
    /// </summary>
    public static string FormatSearchFailure(string? lastStep, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        var step = string.IsNullOrWhiteSpace(lastStep)
            ? "search"
            : lastStep.Trim().TrimEnd('.', '…');
        var detail = $"{ex.GetType().Name}: {ex.Message}";
        if (ex.InnerException is { } inner && !string.IsNullOrWhiteSpace(inner.Message))
        {
            detail += $" ({inner.GetType().Name}: {inner.Message})";
        }

        if (detail.Length > 180)
        {
            detail = detail[..177] + "…";
        }

        return $"Search failed ({step}): {detail}";
    }

    /// <summary>
    /// Results header for one scan, e.g. <c>20.10.2026 23:06:01</c>.
    /// </summary>
    public static string FormatScanStamp(DateTime when) =>
        when.ToString("dd.MM.yyyy HH:mm:ss");

    private int PublishNewResults(IReadOnlyList<AvailabilitySlot> slots)
    {
        var block = new List<string> { FormatScanStamp(ScanClock()) };
        Results.Add(block[0]);
        var newCount = 0;
        foreach (var slot in slots)
        {
            var line = slot.ToLogLine();
            if (_seenResultLines.Add(line))
            {
                Results.Add(line);
                block.Add(line);
                newCount++;
            }
        }

        Results.Add(string.Empty);
        block.Add(string.Empty);
        AppendResultsLog(block);
        return newCount;
    }

    private void ResetResultsLog()
    {
        _seenResultLines.Clear();
        Results.Clear();
        try
        {
            var dir = Path.GetDirectoryName(ResultsLogPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(ResultsLogPath, string.Empty);
        }
        catch
        {
        }
    }

    private void AppendResultsLog(IReadOnlyList<string> lines)
    {
        try
        {
            var dir = Path.GetDirectoryName(ResultsLogPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.AppendAllLines(ResultsLogPath, lines);
        }
        catch
        {
        }
    }

    private async Task WaitBeforeNextSearchAsync(TimeSpan wait, CancellationToken cancellationToken)
    {
        var remaining = wait;
        while (remaining > TimeSpan.Zero)
        {
            StatusMessage = FormatWaitStatus(remaining);
            var slice = remaining > TimeSpan.FromMinutes(1)
                ? TimeSpan.FromMinutes(1)
                : TimeSpan.FromSeconds(1);
            if (slice > remaining)
            {
                slice = remaining;
            }

            await Task.Delay(slice, cancellationToken).ConfigureAwait(true);
            remaining -= slice;
        }
    }

    private CancellationToken ResetLoadToken()
    {
        var previous = _loadCts;
        _loadCts = new CancellationTokenSource();
        previous?.Cancel();
        return _loadCts.Token;
    }
}
