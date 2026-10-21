using ClickPlay.Core.Calendar;
using ClickPlay.Core.Models;
using ClickPlay.Core.Services;
using Microsoft.Playwright;

namespace ClickPlay.Core.Browser;

/// <summary>
/// One Chromium page while a cascade step or search cycle is running. The GUI closes it after sports, cities, and locations.
/// The sport is chosen by clicking the site's sport bar (stored in the site session). The city listing and every
/// club schedule are then opened by URL (<c>/clubs/index.php?city=…</c>, <c>/club/?club=…&amp;date=…</c>),
/// one page load per club and day, instead of driving the datepicker, clock, and club-name popups.
/// </summary>
public sealed class ClickToPlayBrowserSession : IClickToPlaySession
{
    public const string HomeUrl = "https://clicktoplay.io/";
    public const string ClubsListingUrl = "https://clicktoplay.io/clubs/index.php";
    public const string ClubProfileUrl = "https://clicktoplay.io/club/";

    private const string SiteBusyMessage =
        "clicktoplay.io is busy (its database reports Too many connections). Wait a minute and try again.";

    private static readonly TimeSpan[] BusyRetryDelays =
        [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(12)];

    private readonly OverlayRecovery _overlayRecovery = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;
    private IPage? _page;
    private bool? _startedHeadless;
    private Sport? _currentSport;

    /// <inheritdoc />
    public async Task StartAsync(BrowserLaunchOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_page is not null && _startedHeadless == options.Headless)
            {
                return;
            }

            await DisposeBrowserCoreAsync().ConfigureAwait(false);

            _playwright = await Playwright.CreateAsync().ConfigureAwait(false);
            _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = options.Headless,
                SlowMo = 800
            }).ConfigureAwait(false);
            _context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                Locale = "bg-BG",
                ViewportSize = new ViewportSize { Width = 1440, Height = 900 }
            }).ConfigureAwait(false);
            _page = await _context.NewPageAsync().ConfigureAwait(false);
            _page.SetDefaultTimeout(10_000);
            _page.SetDefaultNavigationTimeout(30_000);
            _startedHeadless = options.Headless;
            _currentSport = null;

            await GotoHomeAndSettleAsync(_page, cancellationToken).ConfigureAwait(false);
            await TryRecoverSportsPanelAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Sport>> GetSportsAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var page = RequirePage();
            await EnsureHomeAsync(page, cancellationToken).ConfigureAwait(false);
            if (!await TryRecoverSportsPanelAsync(cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidOperationException("The sports panel is not clickable. A diagnostic screenshot was saved.");
            }

            var names = await page.EvaluateAsync<string[]>(
                """
                () => {
                  const pick = (sel) => [...document.querySelectorAll(sel)]
                    .filter(n => n.offsetWidth > 10 && n.offsetHeight > 10)
                    .map(n => (n.innerText || '').trim().split('\n')[0].trim())
                    .filter(t => t.length > 0 && t.length < 40);
                  const names = pick('.sport-container.index-sport-nav');
                  return names.length > 0 ? [...new Set(names)] : [...new Set(pick('.sport-container.nav'))];
                }
                """).ConfigureAwait(false);

            var sports = (names ?? [])
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => new Sport(n.Trim()))
                .ToArray();

            if (sports.Length == 0)
            {
                throw new InvalidOperationException("The site layout changed: no sports were found on the navigation panel.");
            }

            return sports;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<City>> SelectSportAsync(Sport sport, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sport);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var page = RequirePage();
            await ClickSportOnHomeAsync(page, sport, cancellationToken).ConfigureAwait(false);

            var reserve = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions
            {
                NameRegex = new System.Text.RegularExpressions.Regex(
                    "Резервирай корт|Reserve court",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            }).First;
            try
            {
                await reserve.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 }).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsPlaywrightFailure(ex))
            {
            }

            await ClickReserveAsync(page).ConfigureAwait(false);
            await WaitForClubsPageAsync(page).ConfigureAwait(false);

            var cities = await ReadCitiesAsync(page).ConfigureAwait(false);
            if (cities.Count == 0)
            {
                await CaptureDiagnosticsAsync("No city labels after opening clubs.").ConfigureAwait(false);
                throw new InvalidOperationException("The site layout changed: no cities were found after Резервирай.");
            }

            return cities;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Venue>> SelectCityAsync(Sport sport, City city, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sport);
        ArgumentNullException.ThrowIfNull(city);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var page = RequirePage();
            var needSport = !IsCurrentSport(sport)
                || !page.Url.Contains("/clubs", StringComparison.OrdinalIgnoreCase);
            if (needSport)
            {
                _gate.Release();
                try
                {
                    await SelectSportAsync(sport, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                page = RequirePage();
            }

            var cityId = await page.EvaluateAsync<string?>(
                """
                (name) => {
                  const norm = (s) => (s || '').trim().split(',')[0].trim();
                  const target = [...document.querySelectorAll('.city[data-city]')].find(n =>
                    norm(n.querySelector('.city-name-popup')?.textContent || n.textContent)
                      .localeCompare(name, undefined, { sensitivity: 'accent' }) === 0);
                  return target ? (target.getAttribute('data-city') || '') : '';
                }
                """,
                city.Name).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(cityId) || cityId == "cl")
            {
                throw new InvalidOperationException($"The site layout changed: city '{city.Name}' was not found.");
            }

            await GotoAndCheckBusyAsync(page, BuildListingUrl(cityId, DateTime.Now), cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await page.Locator("a.club-card h3.club-name").First.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Attached,
                    Timeout = 15_000
                }).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsPlaywrightFailure(ex))
            {
                await CaptureDiagnosticsAsync("No club cards on the city listing.").ConfigureAwait(false);
                throw new InvalidOperationException("The site layout changed: no locations were found for this city.");
            }

            await ScrollToLoadAllAsync(page).ConfigureAwait(false);

            var venues = await ReadVenuesAsync(page).ConfigureAwait(false);
            if (venues.Count == 0)
            {
                await CaptureDiagnosticsAsync("No club cards after city filter.").ConfigureAwait(false);
                throw new InvalidOperationException("The site layout changed: no locations were found for this city.");
            }

            return venues;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Opens <c>/club/?club={slug}&amp;date={yyyy-MM-dd}</c> for each selected location and day. A busy site,
    /// a timeout, or a missing schedule skips that club and day; only a closed browser or Stop ends the scan.
    /// </remarks>
    public async Task<IReadOnlyList<AvailabilitySlot>> SearchAvailabilityAsync(
        SearchRequest request,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var page = RequirePage();
            if (!IsCurrentSport(request.Sport))
            {
                progress?.Report($"Selecting {request.Sport.Name} on the sport bar…");
                await ClickSportOnHomeAsync(page, request.Sport, cancellationToken).ConfigureAwait(false);
            }

            var slots = new List<AvailabilitySlot>();
            var attempts = 0;
            var schedulesRead = 0;
            string? lastSkip = null;
            foreach (var day in SearchDatePlanner.Days(request.StartDate, request.DaysAhead))
            {
                var window = SearchDatePlanner.WindowFor(day, request.WeekdayWindow, request.WeekendWindow);
                foreach (var venue in request.Locations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(venue.ClubSlug))
                    {
                        progress?.Report($"{venue.Name} has no club link; reload the locations.");
                        continue;
                    }

                    attempts++;
                    progress?.Report($"Checking {venue.Name}, {day:yyyy-MM-dd}…");
                    try
                    {
                        slots.AddRange(await CollectVenueSlotsAsync(page, venue, day, window, cancellationToken)
                            .ConfigureAwait(false));
                        schedulesRead++;
                    }
                    catch (Exception ex) when (IsSkippableVenueFailure(page, ex))
                    {
                        lastSkip = $"Skipped {venue.Name}, {day:yyyy-MM-dd}: {FirstLine(ex.Message)}";
                        progress?.Report(lastSkip);
                    }
                }
            }

            if (attempts > 0 && schedulesRead == 0)
            {
                throw new InvalidOperationException(
                    $"No club schedule could be read in this scan. Last: {lastSkip}");
            }

            return slots;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task CloseBrowserAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisposeBrowserCoreAsync().ConfigureAwait(false);
            _playwright?.Dispose();
            _playwright = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await DisposeBrowserCoreAsync().ConfigureAwait(false);
            _playwright?.Dispose();
            _playwright = null;
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    /// <summary>
    /// Club listing URL for one city. After 17:00 it lists tomorrow at 10:00 (see <see cref="ListingTimePlanner"/>).
    /// </summary>
    /// <param name="cityId"><c>data-city</c> of the city, e.g. <c>42</c> for Варна.</param>
    /// <param name="now">Local time used for the evening rule.</param>
    /// <returns>Absolute <c>/clubs/index.php</c> URL.</returns>
    /// <example>
    /// <code>
    /// var url = ClickToPlayBrowserSession.BuildListingUrl("42", new DateTime(2026, 10, 21, 20, 0, 0));
    /// // https://clicktoplay.io/clubs/index.php?city=42&amp;date=2026-10-22&amp;hour=10:00
    /// </code>
    /// </example>
    public static string BuildListingUrl(string cityId, DateTime now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cityId);
        var url = $"{ClubsListingUrl}?city={Uri.EscapeDataString(cityId.Trim())}";
        if (ListingTimePlanner.ShouldUseTomorrowMorning(now))
        {
            url += $"&date={ListingTimePlanner.ListingDate(now):yyyy-MM-dd}&hour={ListingTimePlanner.MorningHour:HH:mm}";
        }

        return url;
    }

    /// <summary>
    /// Club profile URL for one day. No <c>hour</c> is sent: the site hides slots before that hour,
    /// and the GUI time window is applied after reading.
    /// </summary>
    /// <param name="clubSlug">The <c>club</c> query value from the club card link.</param>
    /// <param name="day">Schedule day.</param>
    /// <returns>Absolute <c>/club/</c> URL.</returns>
    /// <example>
    /// <code>
    /// var url = ClickToPlayBrowserSession.BuildClubUrl("supersport", new DateOnly(2026, 10, 23));
    /// // https://clicktoplay.io/club/?club=supersport&amp;date=2026-10-23
    /// </code>
    /// </example>
    public static string BuildClubUrl(string clubSlug, DateOnly day)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clubSlug);
        return $"{ClubProfileUrl}?club={Uri.EscapeDataString(clubSlug.Trim())}&date={day:yyyy-MM-dd}";
    }

    private IPage RequirePage()
    {
        if (_page is null)
        {
            throw new InvalidOperationException("The browser session has not been started.");
        }

        return _page;
    }

    private bool IsCurrentSport(Sport sport) =>
        _currentSport is not null && string.Equals(_currentSport.Name, sport.Name, StringComparison.Ordinal);

    /// <summary>
    /// Playwright reports timeouts as <see cref="TimeoutException"/>, which is not a <see cref="PlaywrightException"/>.
    /// </summary>
    private static bool IsPlaywrightFailure(Exception ex) => ex is PlaywrightException or TimeoutException;

    /// <summary>
    /// A club/day problem the scan can step over. A closed page (user closed Chromium, browser crash) is not.
    /// </summary>
    private static bool IsSkippableVenueFailure(IPage page, Exception ex) =>
        !page.IsClosed && (IsPlaywrightFailure(ex) || ex is SkipVenueException);

    private static string FirstLine(string message)
    {
        var line = message.Split('\n', 2)[0].Trim();
        return line.Length > 140 ? line[..137] + "…" : line;
    }

    private async Task EnsureHomeAsync(IPage page, CancellationToken cancellationToken)
    {
        if (page.Url.StartsWith(HomeUrl, StringComparison.OrdinalIgnoreCase)
            && !page.Url.Contains("/clubs", StringComparison.OrdinalIgnoreCase)
            && !page.Url.Contains("/club/", StringComparison.OrdinalIgnoreCase)
            && !page.Url.Contains("lang/", StringComparison.OrdinalIgnoreCase)
            && !await IsSiteDatabaseBusyAsync(page).ConfigureAwait(false))
        {
            return;
        }

        await GotoHomeAndSettleAsync(page, cancellationToken).ConfigureAwait(false);
        _currentSport = null;
    }

    /// <summary>
    /// On the home page, clicks the sport in the sport bar so the site stores it in the session
    /// (<c>ajax/set_sport.php</c>, cookie <c>selected_sport</c>). Club pages then show that sport's courts.
    /// </summary>
    private async Task ClickSportOnHomeAsync(IPage page, Sport sport, CancellationToken cancellationToken)
    {
        await EnsureHomeAsync(page, cancellationToken).ConfigureAwait(false);
        if (!await TryRecoverSportsPanelAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The sports panel is not clickable. A diagnostic screenshot was saved.");
        }

        // The site sets selected_sport=1 (Тенис) on first load, and Запази on the language prompt reloads the page,
        // so a click can land on the old document and be lost. Click, then confirm the cookie; retry if it did not change.
        string? stored = null;
        string? sportId = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var sportBox = await FindSportBoxAsync(page, sport).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"The site layout changed: sport '{sport.Name}' was not found.");
            sportId = await sportBox.GetAttributeAsync("data-id").ConfigureAwait(false);
            try
            {
                await page.RunAndWaitForResponseAsync(
                    () => sportBox.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }),
                    response => response.Url.Contains("set_sport", StringComparison.OrdinalIgnoreCase),
                    new PageRunAndWaitForResponseOptions { Timeout = 10_000 }).ConfigureAwait(false);
            }
            catch (Exception ex) when (IsPlaywrightFailure(ex) && !page.IsClosed)
            {
            }

            await WaitForSiteToRespondAsync(page, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(sportId))
            {
                _currentSport = sport;
                return;
            }

            stored = await ReadSelectedSportCookieAsync().ConfigureAwait(false);
            if (string.Equals(stored, sportId, StringComparison.Ordinal))
            {
                _currentSport = sport;
                return;
            }

            await EnsureHomeAsync(page, cancellationToken).ConfigureAwait(false);
            await TryRecoverSportsPanelAsync(cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"The site did not switch to '{sport.Name}' (selected_sport={stored ?? "none"}, expected {sportId}).");
    }

    /// <summary>
    /// Visible sport-bar box whose first text line is exactly the sport name
    /// (so "Тенис" does not match "Тенис на маса").
    /// </summary>
    private static async Task<ILocator?> FindSportBoxAsync(IPage page, Sport sport)
    {
        var id = await page.EvaluateAsync<string?>(
            """
            (name) => {
              const visible = (sel) => [...document.querySelectorAll(sel)].filter(n => n.offsetWidth > 10 && n.offsetHeight > 10);
              const title = (n) => (n.innerText || '').trim().split('\n')[0].trim();
              for (const sel of ['.sport-container.index-sport-nav', '.sport-container.nav', '.sport-container']) {
                const box = visible(sel).find(n => title(n) === name && n.getAttribute('data-id'));
                if (box) return box.getAttribute('data-id');
              }
              return '';
            }
            """,
            sport.Name).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var box = page.Locator($".sport-container[data-id='{id}']").Locator("visible=true").First;
        return await box.IsVisibleAsync().ConfigureAwait(false) ? box : null;
    }

    private async Task<string?> ReadSelectedSportCookieAsync()
    {
        if (_context is null)
        {
            return null;
        }

        var cookies = await _context.CookiesAsync([HomeUrl]).ConfigureAwait(false);
        return cookies.FirstOrDefault(c => c.Name == "selected_sport")?.Value;
    }

    /// <summary>
    /// Loads the home page, then waits 2 seconds so the language prompt can appear.
    /// Documented last-resort wait (not a generic sleep). If the site answers with its MySQL
    /// <c>Too many connections</c> page instead, reloads after 3, 6, and 12 seconds before giving up.
    /// </summary>
    /// <exception cref="InvalidOperationException">The site is still busy after the retries.</exception>
    private static async Task GotoHomeAndSettleAsync(IPage page, CancellationToken cancellationToken)
    {
        await page.GotoAsync(HomeUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded })
            .ConfigureAwait(false);
        foreach (var delay in BusyRetryDelays)
        {
            if (!await IsSiteDatabaseBusyAsync(page).ConfigureAwait(false))
            {
                break;
            }

            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded })
                .ConfigureAwait(false);
        }

        if (await IsSiteDatabaseBusyAsync(page).ConfigureAwait(false))
        {
            throw new InvalidOperationException(SiteBusyMessage);
        }

        await page.WaitForTimeoutAsync(2_000).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens a URL. If the visible page shows MySQL <c>Too many connections</c>, waits 3 seconds and reloads once.
    /// </summary>
    /// <exception cref="SkipVenueException">The page is still busy after the reload.</exception>
    private static async Task GotoAndCheckBusyAsync(IPage page, string url, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded })
            .ConfigureAwait(false);
        if (!await IsSiteDatabaseBusyAsync(page).ConfigureAwait(false))
        {
            return;
        }

        await Task.Delay(3_000, cancellationToken).ConfigureAwait(false);
        await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded })
            .ConfigureAwait(false);
        if (await IsSiteDatabaseBusyAsync(page).ConfigureAwait(false))
        {
            throw new SkipVenueException("clicktoplay.io is busy (Too many connections).");
        }
    }

    /// <summary>
    /// True when visible page text shows MySQL <c>max_connections</c>. Scripts are ignored.
    /// </summary>
    private static async Task<bool> IsSiteDatabaseBusyAsync(IPage page)
    {
        string? text = null;
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                text = await page.EvaluateAsync<string>(
                    "() => (document.body && document.body.innerText) || ''").ConfigureAwait(false);
                break;
            }
            catch (PlaywrightException) when (!page.IsClosed)
            {
                await page.WaitForTimeoutAsync(400).ConfigureAwait(false);
            }
        }

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return text.Contains("Too many connections", StringComparison.OrdinalIgnoreCase)
            || text.Contains("HY000/1040", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Waits 3 seconds and checks again. True only when the visible page is still busy.
    /// </summary>
    private static async Task<bool> StillBusyAfterRetryAsync(IPage page)
    {
        if (!await IsSiteDatabaseBusyAsync(page).ConfigureAwait(false))
        {
            return false;
        }

        await page.WaitForTimeoutAsync(3_000).ConfigureAwait(false);
        await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded })
            .ConfigureAwait(false);
        return await IsSiteDatabaseBusyAsync(page).ConfigureAwait(false);
    }

    /// <summary>
    /// Dismisses overlays and retries the sports bar. Returns false after a screenshot instead of throwing.
    /// </summary>
    private async Task<bool> TryRecoverSportsPanelAsync(CancellationToken cancellationToken)
    {
        var page = RequirePage();
        var target = new PlaywrightOverlayTarget(page);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await _overlayRecovery.RecoverAsync(target, cancellationToken).ConfigureAwait(false);
            if (await target.IsSportsPanelInteractableAsync(cancellationToken).ConfigureAwait(false))
            {
                return true;
            }
        }

        if (await IsSiteDatabaseBusyAsync(page).ConfigureAwait(false))
        {
            throw new InvalidOperationException(SiteBusyMessage);
        }

        await CaptureDiagnosticsAsync("Sports panel still unresponsive after overlay recovery.").ConfigureAwait(false);
        return false;
    }

    private static async Task ClickReserveAsync(IPage page)
    {
        var reserve = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions
        {
            NameRegex = new System.Text.RegularExpressions.Regex(
                "Резервирай корт|Reserve court",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        }).First;
        if (await reserve.IsVisibleAsync().ConfigureAwait(false))
        {
            try
            {
                await reserve.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
                await WaitForSiteToRespondAsync(page, CancellationToken.None).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (IsPlaywrightFailure(ex))
            {
            }
        }

        var hero = page.Locator("button.hero-btn.reservation").First;
        if (await hero.IsVisibleAsync().ConfigureAwait(false))
        {
            try
            {
                await hero.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
                await WaitForSiteToRespondAsync(page, CancellationToken.None).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (IsPlaywrightFailure(ex))
            {
            }
        }

        if (!page.Url.Contains("/clubs", StringComparison.OrdinalIgnoreCase))
        {
            await page.GotoAsync(ClubsListingUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            }).ConfigureAwait(false);
        }
    }

    private static async Task WaitForClubsPageAsync(IPage page)
    {
        try
        {
            await page.WaitForURLAsync("**/clubs/**", new PageWaitForURLOptions { Timeout = 15_000 })
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsPlaywrightFailure(ex))
        {
            if (!page.Url.Contains("/clubs", StringComparison.OrdinalIgnoreCase))
            {
                await page.GotoAsync(ClubsListingUrl, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded
                }).ConfigureAwait(false);
            }
        }

        if (await StillBusyAfterRetryAsync(page).ConfigureAwait(false))
        {
            throw new InvalidOperationException("clicktoplay.io is busy (Too many connections). Try again in a minute.");
        }

        await OpenCityFilterAsync(page).ConfigureAwait(false);
    }

    /// <summary>
    /// Opens the city popup by clicking <c>.search-by-location</c> (the Град input alone does not open it).
    /// </summary>
    private static async Task OpenCityFilterAsync(IPage page)
    {
        var place = page.Locator(".search-by-location").First;
        await place.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 15_000
        }).ConfigureAwait(false);
        await place.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);

        var popup = page.Locator(".dropdown-content-city-popup");
        try
        {
            await popup.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5_000
            }).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsPlaywrightFailure(ex))
        {
            await page.EvaluateAsync(
                """
                () => {
                  const container = document.querySelector('.search-by-location');
                  if (container) {
                    container.click();
                    container.classList.add('active');
                  }
                  const dropdown = document.querySelector('.dropdown-content-city-popup');
                  if (dropdown) dropdown.style.display = 'block';
                }
                """).ConfigureAwait(false);
            await popup.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 8_000
            }).ConfigureAwait(false);
        }

        await page.Locator(".city[data-city] .city-name-popup").First.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = 10_000
        }).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<City>> ReadCitiesAsync(IPage page)
    {
        var names = await page.EvaluateAsync<string[]>(
            """
            () => {
              const root = document.querySelector('.dropdown-content-city-popup') || document;
              return [...root.querySelectorAll('.city[data-city]')]
                .filter(n => (n.getAttribute('data-city') || '') !== 'cl')
                .map(n => (n.querySelector('.city-name-popup')?.textContent || n.textContent || '').trim())
                .filter(Boolean);
            }
            """).ConfigureAwait(false);

        return CityNameParser.Parse(names ?? []);
    }

    private static async Task ScrollToLoadAllAsync(IPage page)
    {
        for (var i = 0; i < 8; i++)
        {
            var previous = await page.EvaluateAsync<int>("() => document.body.scrollHeight").ConfigureAwait(false);
            await page.EvaluateAsync("() => window.scrollTo(0, document.body.scrollHeight)").ConfigureAwait(false);
            await page.WaitForTimeoutAsync(400).ConfigureAwait(false);
            var next = await page.EvaluateAsync<int>("() => document.body.scrollHeight").ConfigureAwait(false);
            if (next <= previous)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Reads club cards that link to a clicktoplay.io club profile, with the <c>club</c> slug from the link.
    /// Advert cards that link to other sites are skipped.
    /// </summary>
    private static async Task<IReadOnlyList<Venue>> ReadVenuesAsync(IPage page)
    {
        var rows = await page.EvaluateAsync<ScrapedVenue[]>(
            """
            () => {
              const seen = new Set();
              const results = [];
              for (const card of document.querySelectorAll('a.club-card')) {
                const name = (card.querySelector('h3.club-name')?.textContent || '').trim().split('\n')[0].trim();
                const address = (card.querySelector('.club-address')?.textContent || '').trim();
                let slug = '';
                try {
                  const url = new URL(card.href, location.href);
                  if (url.hostname.endsWith('clicktoplay.io') && url.pathname.startsWith('/club/')) {
                    slug = url.searchParams.get('club') || '';
                  }
                } catch (e) { }
                if (!name || !slug || name.length > 120 || seen.has(name)) continue;
                seen.add(name);
                results.push({ name, address, slug });
              }
              return results;
            }
            """).ConfigureAwait(false);

        return (rows ?? [])
            .Where(r => r is not null && !string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.Slug))
            .Select(r => new Venue(r.Name.Trim(), (r.Address ?? string.Empty).Trim(), r.Slug.Trim()))
            .ToArray();
    }

    /// <summary>
    /// Opens the club profile for one day and reads every free <c>.booking-slot</c> inside the GUI window.
    /// </summary>
    /// <exception cref="SkipVenueException">The site is busy or the page has no court schedule.</exception>
    private static async Task<IReadOnlyList<AvailabilitySlot>> CollectVenueSlotsAsync(
        IPage page,
        Venue venue,
        DateOnly day,
        TimeWindow window,
        CancellationToken cancellationToken)
    {
        await GotoAndCheckBusyAsync(page, BuildClubUrl(venue.ClubSlug!, day), cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await page.Locator(".sport-facility .facility").First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Attached,
                Timeout = 10_000
            }).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new SkipVenueException($"no court schedule on {page.Url}");
        }

        var rows = await page.EvaluateAsync<ScrapedSlot[]>(
            """
            () => {
              const widget = document.querySelector('#CLUBPROFILE .big-widget.booking.active, .big-widget.booking.active')
                || document.querySelector('.sport-facility');
              if (!widget) return [];
              const results = [];
              const facilities = [...widget.querySelectorAll('.facility.active')];
              for (const facility of facilities) {
                const court = (facility.querySelector('h3.facility-name, .facility-name')?.textContent || '')
                  .replace(/\s+/g, ' ').trim();
                const extras = (facility.querySelector('.facility-extras')?.textContent || '')
                  .replace(/\s+/g, ' ').trim();
                const slots = [...facility.querySelectorAll('.booking-slot')].filter(el =>
                  el.classList.contains('itsfree')
                  && !el.classList.contains('closed')
                  && el.hasAttribute('data-from-to'));
                const seen = new Set();
                for (const el of slots) {
                  const fromTo = (el.getAttribute('data-from-to') || '').trim();
                  if (!fromTo || seen.has(fromTo)) continue;
                  seen.add(fromTo);
                  results.push({
                    court,
                    extras,
                    fromTo,
                    dateDf: (el.getAttribute('data-df') || '').trim(),
                    price: (el.getAttribute('data-price') || '').trim(),
                    currency: (el.getAttribute('data-currency') || '').trim()
                  });
                }
              }
              return results;
            }
            """).ConfigureAwait(false);

        var list = new List<AvailabilitySlot>();
        foreach (var row in rows ?? [])
        {
            if (row is null)
            {
                continue;
            }

            var slot = BookingSlotParser.TryCreateSlot(
                venue.Name,
                day,
                window,
                row.Court,
                row.Extras,
                row.FromTo,
                row.Price,
                row.Currency,
                row.DateDf);
            if (slot is not null)
            {
                list.Add(slot);
            }
        }

        return list;
    }

    private async Task CaptureDiagnosticsAsync(string reason)
    {
        if (_page is null)
        {
            return;
        }

        var dir = AppPaths.DiagnosticsDirectory();
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        await _page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(dir, $"overlay-{stamp}.png")
        }).ConfigureAwait(false);
        var title = await _page.TitleAsync().ConfigureAwait(false);
        await File.WriteAllTextAsync(
            Path.Combine(dir, $"overlay-{stamp}.txt"),
            $"url={_page.Url}{Environment.NewLine}title={title}{Environment.NewLine}reason={reason}{Environment.NewLine}").ConfigureAwait(false);
    }

    /// <summary>
    /// After a click or form submit, wait for the document to load (not networkidle).
    /// </summary>
    private static async Task WaitForSiteToRespondAsync(IPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await page.WaitForLoadStateAsync(
                    LoadState.DOMContentLoaded,
                    new PageWaitForLoadStateOptions { Timeout = 15_000 })
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsPlaywrightFailure(ex))
        {
        }
    }

    private async Task DisposeBrowserCoreAsync()
    {
        if (_page is not null)
        {
            await _page.CloseAsync().ConfigureAwait(false);
            _page = null;
        }

        if (_context is not null)
        {
            await _context.CloseAsync().ConfigureAwait(false);
            _context = null;
        }

        if (_browser is not null)
        {
            await _browser.CloseAsync().ConfigureAwait(false);
            _browser = null;
        }

        _startedHeadless = null;
        _currentSport = null;
    }

    /// <summary>
    /// One club and day could not be read; the scan moves on.
    /// </summary>
    private sealed class SkipVenueException(string message) : Exception(message);

    private sealed class ScrapedVenue
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("slug")]
        public string Slug { get; set; } = string.Empty;
    }

    private sealed class ScrapedSlot
    {
        [System.Text.Json.Serialization.JsonPropertyName("court")]
        public string Court { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("extras")]
        public string Extras { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("fromTo")]
        public string FromTo { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("dateDf")]
        public string DateDf { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("price")]
        public string Price { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("currency")]
        public string Currency { get; set; } = string.Empty;
    }
}
