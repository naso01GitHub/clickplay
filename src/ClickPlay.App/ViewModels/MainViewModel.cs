using System.Collections.ObjectModel;
using ClickPlay.Core.Browser;
using ClickPlay.Core.Mail;
using ClickPlay.Core.Models;
using ClickPlay.Core.Services;
using ClickPlay.Core.Validation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickPlay.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IClickToPlayCatalog _catalog;
    private readonly IMailSender? _mailSender;
    private readonly MailSettingsStore? _mailSettingsStore;
    private readonly HashSet<string> _seenResultLines = new(StringComparer.Ordinal);
    private CancellationTokenSource? _loadCts;
    private bool _suppressCityReload;
    private bool _suppressVenueReload;

    /// <summary>
    /// Creates the main window ViewModel.
    /// </summary>
    /// <param name="catalog">Site access.</param>
    /// <param name="mailSender">Sends results mail; null disables mail.</param>
    /// <param name="mailSettingsStore">Loads and saves the Email section; null keeps it in memory only.</param>
    /// <example>
    /// <code>
    /// var vm = new MainViewModel(catalog, new MailKitMailSender(), new MailSettingsStore(new ProtectedSecretStore()));
    /// </code>
    /// </example>
    public MainViewModel(
        IClickToPlayCatalog catalog,
        IMailSender? mailSender = null,
        MailSettingsStore? mailSettingsStore = null)
    {
        _catalog = catalog;
        _mailSender = mailSender;
        _mailSettingsStore = mailSettingsStore;
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

    /// <summary>
    /// Tests can shorten the wait between cycles.
    /// </summary>
    public TimeSpan? SearchLoopDelayOverride { get; set; }

    /// <summary>
    /// Clock for Results scan stamps. Tests can replace it.
    /// </summary>
    public Func<DateTime> ScanClock { get; set; } = () => DateTime.Now;

    /// <summary>
    /// Clock for "today" when the calendar is moved forward before a search cycle. Tests can replace it.
    /// </summary>
    public Func<DateTime> CalendarClock { get; set; } = () => DateTime.Now;

    /// <summary>
    /// Results log on disk. Defaults to <c>results-log.txt</c> next to the exe. Tests can replace it.
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

    [ObservableProperty]
    private string _smtpHost = string.Empty;

    [ObservableProperty]
    private string _smtpPort = "587";

    /// <summary>Known mail providers for the Host drop-down (Gmail, Yahoo, …).</summary>
    public IReadOnlyList<SmtpPreset> SmtpHostPresets => SmtpPresets.All;

    /// <summary>
    /// Provider picked in the Host drop-down. Picking one fills Host and its usual Port.
    /// Follows the Host text: a typed known host selects its provider, any other host clears it.
    /// </summary>
    [ObservableProperty]
    private SmtpPreset? _selectedSmtpPreset;

    /// <summary>Port choices for the Port drop-down: the provider's ports, or common ports for other hosts.</summary>
    public IReadOnlyList<SmtpPortOption> SmtpPortOptions => SmtpPresets.PortsFor(SmtpHost);

    /// <summary>
    /// Port picked in the Port drop-down. Picking one fills Port. Follows the Port text.
    /// </summary>
    [ObservableProperty]
    private SmtpPortOption? _selectedSmtpPortOption;

    private bool _syncingSmtpPicks;

    [ObservableProperty]
    private string _smtpUser = string.Empty;

    [ObservableProperty]
    private string _smtpPassword = string.Empty;

    /// <summary>
    /// "Remember login": when true, User and Password are saved and filled in at the next start.
    /// Turning it off erases the saved login at once; the fields keep their values for this session.
    /// </summary>
    [ObservableProperty]
    private bool _rememberLogin = true;

    private bool _loadingMailSettings;

    [ObservableProperty]
    private string _mailFrom = string.Empty;

    /// <summary>
    /// Recipient addresses, one per line (commas or semicolons also accepted).
    /// </summary>
    [ObservableProperty]
    private string _mailTo = string.Empty;

    /// <summary>
    /// Mail subject prefix. The sent subject is this text + location + time slot of the first new result.
    /// Editable while Search runs; read when the mail is sent.
    /// </summary>
    [ObservableProperty]
    private string _mailSubject = string.Empty;

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

    /// <summary>
    /// Called by CommunityToolkit when the Host text changes: refreshes the port choices and
    /// selects the matching provider (or none) without overwriting what was typed.
    /// </summary>
    /// <param name="value">New Host text.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// viewModel.SmtpHost = "smtp.gmail.com"; // SelectedSmtpPreset becomes Gmail
    /// </code>
    /// </example>
    partial void OnSmtpHostChanged(string value)
    {
        OnPropertyChanged(nameof(SmtpPortOptions));
        SyncSmtpPicks();
    }

    /// <summary>
    /// Called by CommunityToolkit when the Port text changes: selects the matching port choice (or none).
    /// </summary>
    /// <param name="value">New Port text.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// viewModel.SmtpPort = "465"; // SelectedSmtpPortOption becomes 465 (SSL)
    /// </code>
    /// </example>
    partial void OnSmtpPortChanged(string value) => SyncSmtpPicks();

    /// <summary>
    /// Called by CommunityToolkit when "Remember login" changes. Turning it off saves at once, which erases
    /// the stored User and Password; the fields keep their values so the current session still works.
    /// </summary>
    /// <param name="value">New checkbox state.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// viewModel.RememberLogin = false; // saved login erased now; fields empty at the next start
    /// </code>
    /// </example>
    partial void OnRememberLoginChanged(bool value)
    {
        if (_loadingMailSettings || value)
        {
            return;
        }

        SaveMailSettings();
        StatusMessage = "Saved login erased. User and Password will be empty at the next start.";
    }

    /// <summary>
    /// Called by CommunityToolkit when a provider is picked: fills Host and the provider's usual Port.
    /// </summary>
    /// <param name="value">Picked provider; null (cleared) changes nothing.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// viewModel.SelectedSmtpPreset = SmtpPresets.FindByHost("smtp.mail.yahoo.com"); // Host + Port 465
    /// </code>
    /// </example>
    partial void OnSelectedSmtpPresetChanged(SmtpPreset? value)
    {
        if (_syncingSmtpPicks || value is null)
        {
            return;
        }

        SmtpHost = value.Host;
        SmtpPort = value.DefaultPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Called by CommunityToolkit when a port is picked: fills Port.
    /// </summary>
    /// <param name="value">Picked port; null (cleared) changes nothing.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// viewModel.SelectedSmtpPortOption = viewModel.SmtpPortOptions[1]; // Port = 587
    /// </code>
    /// </example>
    partial void OnSelectedSmtpPortOptionChanged(SmtpPortOption? value)
    {
        if (_syncingSmtpPicks || value is null)
        {
            return;
        }

        SmtpPort = value.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Sets both drop-downs to match the Host and Port text, without writing back to the text boxes.
    /// </summary>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// SyncSmtpPicks(); // after loading saved settings
    /// </code>
    /// </example>
    private void SyncSmtpPicks()
    {
        _syncingSmtpPicks = true;
        try
        {
            SelectedSmtpPreset = SmtpPresets.FindByHost(SmtpHost);
            var port = int.TryParse(SmtpPort?.Trim(), out var p) ? p : -1;
            SelectedSmtpPortOption = SmtpPortOptions.FirstOrDefault(o => o.Port == port);
        }
        finally
        {
            _syncingSmtpPicks = false;
        }
    }

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

        SmtpSettings? smtp = null;
        if (SmtpSettingsValidator.IsConfigured(SmtpHost)
            && !SmtpSettingsValidator.TryCreate(SmtpHost, SmtpPort, SmtpUser, SmtpPassword, MailFrom, out smtp, out var smtpError))
        {
            StatusMessage = $"Email: {smtpError}";
            return;
        }

        SaveMailSettings();
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

                    AdvanceCalendarToToday();
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

                    var newSlots = PublishNewResults(slots);
                    StatusMessage = newSlots.Count == 0
                        ? (slots.Count == 0
                            ? "No free hours found. Closing browser…"
                            : "No new free hours. Closing browser…")
                        : $"Found {newSlots.Count} new free hour(s). Closing browser…";
                    await _catalog.CloseBrowserAsync(cancellationToken).ConfigureAwait(true);
                    await MailNewResultsAsync(smtp, newSlots, cancellationToken).ConfigureAwait(true);

                    await WaitBeforeNextSearchAsync(ReadWaitDuration(repeatMinutes), cancellationToken)
                        .ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var failure = FormatSearchFailure(StatusMessage, ex);
                    StatusMessage = failure;
                    PublishSearchFailure(failure);
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

    /// <summary>
    /// Before each search cycle: if the GUI calendar date is before today (e.g. Search ran past midnight),
    /// moves the calendar to today, so the scan never starts on a past day. A future date is kept.
    /// </summary>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// AdvanceCalendarToToday();
    /// TryReadSearchCycle(sport, city, locations, out var request, out var error); // starts today or later
    /// </code>
    /// </example>
    private void AdvanceCalendarToToday()
    {
        var today = CalendarClock().Date;
        if (SelectedDate is null || SelectedDate.Value.Date < today)
        {
            SelectedDate = today;
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

    /// <summary>
    /// Adds a scan block to Results and results-log: the scan stamp, every slot not shown in an earlier scan, and an empty line.
    /// </summary>
    /// <param name="slots">All free slots found in this scan.</param>
    /// <returns>Only the slots that are new in this scan (used for the mail).</returns>
    /// <example>
    /// <code>
    /// var newSlots = PublishNewResults(slots);
    /// await MailNewResultsAsync(smtp, newSlots, cancellationToken);
    /// </code>
    /// </example>
    private List<AvailabilitySlot> PublishNewResults(IReadOnlyList<AvailabilitySlot> slots)
    {
        var block = new List<string> { FormatScanStamp(ScanClock()) };
        Results.Add(block[0]);
        var newSlots = new List<AvailabilitySlot>();
        foreach (var slot in slots)
        {
            var line = slot.ToLogLine();
            if (_seenResultLines.Add(line))
            {
                Results.Add(line);
                block.Add(line);
                newSlots.Add(slot);
            }
        }

        Results.Add(string.Empty);
        block.Add(string.Empty);
        AppendResultsLog(block);
        return newSlots;
    }

    /// <summary>
    /// After a scan with new Results records, sends one mail with all of them to every To address.
    /// To and Subject are read now, so edits made during Search apply. A mail failure never stops Search.
    /// Adds a "Mailed …", "Mail skipped …", or "Mail failed …" line to Results and results-log.
    /// </summary>
    /// <param name="smtp">SMTP settings snapshotted when Search started; null means mail is off.</param>
    /// <param name="newSlots">New slots of this scan; nothing is sent when empty.</param>
    /// <param name="cancellationToken">Stop button; cancels sending.</param>
    /// <returns>A task that completes when the mail was sent, skipped, or failed.</returns>
    /// <exception cref="OperationCanceledException">Stop was pressed while sending.</exception>
    /// <example>
    /// <code>
    /// await MailNewResultsAsync(smtp, newSlots, cancellationToken);
    /// </code>
    /// </example>
    private async Task MailNewResultsAsync(
        SmtpSettings? smtp,
        IReadOnlyList<AvailabilitySlot> newSlots,
        CancellationToken cancellationToken)
    {
        if (smtp is null || _mailSender is null || newSlots.Count == 0)
        {
            return;
        }

        var recipients = MailRecipients;
        string note;
        if (recipients.Count == 0)
        {
            note = "Mail skipped: no To address.";
        }
        else
        {
            StatusMessage = $"Mailing {newSlots.Count} new free hour(s)…";
            try
            {
                await _mailSender.SendAsync(
                        smtp,
                        recipients,
                        ResultsMailFormatter.Subject(MailSubject, newSlots),
                        ResultsMailFormatter.Body(FormatScanStamp(ScanClock()), newSlots),
                        cancellationToken)
                    .ConfigureAwait(true);
                note = $"Mailed {newSlots.Count} new free hour(s) to {recipients.Count} address(es).";
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                note = $"Mail failed: {ex.GetType().Name}: {ex.Message}";
            }
        }

        StatusMessage = note;
        var line = $"{FormatScanStamp(ScanClock())} {note}";
        if (Results.Count > 0 && Results[^1].Length == 0)
        {
            Results.Insert(Results.Count - 1, line);
        }
        else
        {
            Results.Add(line);
        }

        AppendResultsLog([line, string.Empty]);
    }

    /// <summary>
    /// Fills the Email fields (Host, Port, User, Password, From, To, Subject) from the saved settings.
    /// Does nothing when no settings store was given.
    /// </summary>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// // Called once from the constructor.
    /// LoadMailSettings();
    /// </code>
    /// </example>
    private void LoadMailSettings()
    {
        if (_mailSettingsStore is null)
        {
            return;
        }

        var saved = _mailSettingsStore.Load();
        _loadingMailSettings = true;
        try
        {
            SmtpHost = saved.Host;
            SmtpPort = saved.Port;
            SmtpUser = saved.User;
            SmtpPassword = saved.Password;
            MailFrom = saved.From;
            MailTo = saved.To;
            MailSubject = saved.Subject;
            RememberLogin = saved.RememberLogin;
        }
        finally
        {
            _loadingMailSettings = false;
        }
    }

    /// <summary>
    /// Saves the Email section: the password to the OS secure store, the rest to <c>smtp.settings.json</c>.
    /// Called when Search starts and when the app exits.
    /// </summary>
    /// <example>
    /// <code>
    /// viewModel.SaveMailSettings();
    /// </code>
    /// </example>
    public void SaveMailSettings()
    {
        if (_mailSettingsStore is null)
        {
            return;
        }

        try
        {
            _mailSettingsStore.Save(new MailSettings(
                SmtpHost, SmtpPort, SmtpUser, SmtpPassword, MailFrom, MailTo, MailSubject, RememberLogin));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save email settings: {ex.Message}";
        }
    }

    private void PublishSearchFailure(string failure)
    {
        _seenResultLines.Clear();
        Results.Clear();
        var block = new List<string>
        {
            FormatScanStamp(ScanClock()),
            failure,
            string.Empty
        };
        foreach (var line in block)
        {
            Results.Add(line);
        }

        AppendResultsLog(block);
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
