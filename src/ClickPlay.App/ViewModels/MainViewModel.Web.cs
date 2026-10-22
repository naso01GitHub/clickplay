using System.Collections.ObjectModel;
using ClickPlay.App.Platform;
using ClickPlay.Core.Browser;
using ClickPlay.Core.Logging;
using ClickPlay.Core.Mail;
using ClickPlay.Core.Models;
using ClickPlay.Core.Services;
using ClickPlay.Core.Validation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClickPlay.App.ViewModels;

/// <summary>
/// Main window ViewModel — web search part: the Search/Stop loop against clicktoplay.io, browser options,
/// Results publishing, the errors log, and the wait between cycles.
/// </summary>
public partial class MainViewModel
{
    private readonly IKeepAwake? _keepAwake;

    /// <summary>
    /// Records of the previous successful scan (Results line → its location/day). A record is "new", shown and
    /// mailed, when it is not in here. Replaced after every successful scan; a failed cycle leaves it as it is.
    /// </summary>
    private Dictionary<string, VenueDay> _previousScan = new(StringComparer.Ordinal);

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
    /// Errors-only log on disk. Defaults to <c>errors-log.txt</c> next to the exe; deleted and restarted weekly.
    /// Tests can replace it.
    /// </summary>
    public string ErrorsLogPath { get; set; } = AppPaths.ErrorsLogFile();

    /// <summary>
    /// When true, Chromium is shown. When false (default), Playwright runs headless.
    /// </summary>
    [ObservableProperty]
    private bool _debug;

    public IReadOnlyList<bool> DebugOptions { get; } = [false, true];

    public BrowserLaunchOptions BrowserLaunchOptions => new(Headless: !Debug);

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
        _keepAwake?.Begin();
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
                    var scan = await _catalog.SearchAvailabilityAsync(request, progress, cancellationToken)
                        .ConfigureAwait(true);

                    var newSlots = PublishNewResults(scan);
                    StatusMessage = newSlots.Count == 0
                        ? (scan.Slots.Count == 0
                            ? "No free hours found. Closing browser…"
                            : "No new free hours. Closing browser…")
                        : $"Found {newSlots.Count} new free hour(s). Closing browser…";
                    await _catalog.CloseBrowserAsync(cancellationToken).ConfigureAwait(true);
                    await MailNewResultsAsync(smtp, sport, newSlots, cancellationToken).ConfigureAwait(true);

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
            _keepAwake?.End();
            IsSearching = false;
            IsBusy = false;
            OnPropertyChanged(nameof(CanSearch));
            OnPropertyChanged(nameof(CanStop));
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
    /// Adds a scan block to the Results window: the scan stamp, every record that was not in the previous
    /// successful scan, and an empty line. A record that disappeared (booked) and came back (cancelled) is new again.
    /// For location/days the scan skipped, the previous records are kept, so they are not re-sent next time.
    /// Results are not written to disk.
    /// </summary>
    /// <param name="scan">This scan's free slots and skipped location/days.</param>
    /// <returns>Only the slots that are new in this scan (used for the mail).</returns>
    /// <example>
    /// <code>
    /// var newSlots = PublishNewResults(scan);
    /// await MailNewResultsAsync(smtp, sport, newSlots, cancellationToken);
    /// </code>
    /// </example>
    private List<AvailabilitySlot> PublishNewResults(AvailabilityScan scan)
    {
        Results.Add(FormatScanStamp(ScanClock()));
        var current = new Dictionary<string, VenueDay>(StringComparer.Ordinal);
        var newSlots = new List<AvailabilitySlot>();
        foreach (var slot in scan.Slots)
        {
            var line = slot.ToLogLine();
            if (!current.TryAdd(line, VenueDay.Of(slot)))
            {
                continue;
            }

            if (!_previousScan.ContainsKey(line))
            {
                Results.Add(line);
                newSlots.Add(slot);
            }
        }

        var skipped = scan.Skipped.ToHashSet();
        foreach (var (line, venueDay) in _previousScan)
        {
            if (skipped.Contains(venueDay))
            {
                current.TryAdd(line, venueDay);
            }
        }

        _previousScan = current;
        Results.Add(string.Empty);
        return newSlots;
    }

    /// <summary>
    /// Adds a failed search cycle to the Results window and writes it to the errors log.
    /// The previous scan's records are kept, so the next successful scan does not re-send them.
    /// </summary>
    /// <param name="failure">Line from <see cref="FormatSearchFailure"/>.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// PublishSearchFailure(FormatSearchFailure(StatusMessage, ex));
    /// </code>
    /// </example>
    private void PublishSearchFailure(string failure)
    {
        Results.Add(FormatScanStamp(ScanClock()));
        Results.Add(failure);
        Results.Add(string.Empty);
        LogError(failure);
    }

    /// <summary>
    /// Writes one error to <see cref="ErrorsLogPath"/> (errors only; the file restarts weekly). Never throws.
    /// </summary>
    /// <param name="message">Error text, e.g. "Search failed (…)" or "Mail failed: …".</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// LogError($"Could not load sports: {ex.Message}");
    /// </code>
    /// </example>
    private void LogError(string message) => new ErrorLog(ErrorsLogPath, ScanClock).Append(message);

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
}
