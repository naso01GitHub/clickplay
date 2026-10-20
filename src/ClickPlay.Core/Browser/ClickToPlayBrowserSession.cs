using ClickPlay.Core.Calendar;
using ClickPlay.Core.Models;
using ClickPlay.Core.Services;
using Microsoft.Playwright;

namespace ClickPlay.Core.Browser;

/// <summary>
/// One Chromium page while a cascade step or search cycle is running. The GUI closes it after sports, cities, and locations.
/// </summary>
public sealed class ClickToPlayBrowserSession : IClickToPlaySession
{
    public const string HomeUrl = "https://clicktoplay.io/";

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
                SlowMo = options.Headless ? 0 : 250
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

            await GotoHomeAndSettleAsync(_page).ConfigureAwait(false);
            await RecoverSportsPanelAsync(cancellationToken).ConfigureAwait(false);
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
            await EnsureHomeAsync(page).ConfigureAwait(false);
            await RecoverSportsPanelAsync(cancellationToken).ConfigureAwait(false);

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
            await EnsureHomeAsync(page).ConfigureAwait(false);
            await RecoverSportsPanelAsync(cancellationToken).ConfigureAwait(false);

            var sportBox = page.Locator(".sport-container.index-sport-nav")
                .Filter(new LocatorFilterOptions { HasText = sport.Name })
                .First;
            if (!await sportBox.IsVisibleAsync().ConfigureAwait(false))
            {
                sportBox = page.Locator(".sport-container.nav")
                    .Filter(new LocatorFilterOptions { HasText = sport.Name })
                    .First;
            }

            if (!await sportBox.IsVisibleAsync().ConfigureAwait(false))
            {
                throw new InvalidOperationException($"The site layout changed: sport '{sport.Name}' was not found.");
            }

            await sportBox.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
            await WaitForSiteToRespondAsync(page, cancellationToken).ConfigureAwait(false);

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
            catch (PlaywrightException)
            {
            }

            await ClickReserveAsync(page).ConfigureAwait(false);
            await WaitForClubsPageAsync(page).ConfigureAwait(false);
            _currentSport = sport;

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
            var needSport = _currentSport is null
                || !string.Equals(_currentSport.Name, sport.Name, StringComparison.Ordinal)
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

            await OpenCityFilterAsync(page).ConfigureAwait(false);

            var cityId = await page.EvaluateAsync<string?>(
                """
                (name) => {
                  const norm = (s) => (s || '').trim().split(',')[0].trim();
                  const target = [...document.querySelectorAll('.city[data-city]')].find(n =>
                    norm(n.textContent).localeCompare(name, undefined, { sensitivity: 'accent' }) === 0);
                  return target ? (target.getAttribute('data-city') || '') : '';
                }
                """,
                city.Name).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(cityId))
            {
                throw new InvalidOperationException($"The site layout changed: city '{city.Name}' was not found.");
            }

            var filtered = page.WaitForResponseAsync(
                response => response.Url.Contains("/clubs", StringComparison.OrdinalIgnoreCase),
                new PageWaitForResponseOptions { Timeout = 30_000 });

            var typed = await TrySelectCityOptionAsync(page, city.Name, cityId, cancellationToken)
                .ConfigureAwait(false);
            if (!typed)
            {
                await page.EvaluateAsync(
                    """
                    (id) => {
                      const hidden = document.querySelector('#search_city');
                      if (hidden) hidden.value = id;
                      const form = document.getElementById('search_form');
                      if (form) form.submit();
                    }
                    """,
                    cityId).ConfigureAwait(false);
            }

            try
            {
                await filtered.ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                await page.WaitForTimeoutAsync(2_000).ConfigureAwait(false);
            }

            await WaitForSiteToRespondAsync(page, cancellationToken).ConfigureAwait(false);

            await page.Locator("a.club-card h3.club-name").First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Attached,
                Timeout = 15_000
            }).ConfigureAwait(false);

            var now = DateTime.Now;
            if (ListingTimePlanner.ShouldUseTomorrowMorning(now))
            {
                var tomorrow = ListingTimePlanner.ListingDate(now);
                await SelectDayOnCalendarAsync(page, tomorrow, cancellationToken).ConfigureAwait(false);
                await TrySelectExactHourAsync(page, ListingTimePlanner.MorningHour, cancellationToken)
                    .ConfigureAwait(false);
                try
                {
                    await page.Locator("a.club-card h3.club-name").First.WaitForAsync(new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Attached,
                        Timeout = 15_000
                    }).ConfigureAwait(false);
                }
                catch (PlaywrightException)
                {
                }
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
            if (!page.Url.Contains("/clubs", StringComparison.OrdinalIgnoreCase))
            {
                _gate.Release();
                try
                {
                    await SelectSportAsync(request.Sport, cancellationToken).ConfigureAwait(false);
                    await SelectCityAsync(request.Sport, request.City, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                }

                page = RequirePage();
            }

            var slots = new List<AvailabilitySlot>();
            foreach (var day in SearchDatePlanner.Days(request.StartDate, request.DaysAhead))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var window = SearchDatePlanner.WindowFor(day, request.WeekdayWindow, request.WeekendWindow);
                progress?.Report($"Selecting {day:yyyy-MM-dd} on the site calendar…");
                if (!await SelectDayOnCalendarAsync(page, day, cancellationToken).ConfigureAwait(false))
                {
                    progress?.Report($"Site calendar cannot select {day:yyyy-MM-dd}; next day.");
                    continue;
                }

                progress?.Report($"Selecting hour for {day:yyyy-MM-dd}…");
                var hourReady = await TrySelectHourFromTimeButtonAsync(page, window, cancellationToken)
                    .ConfigureAwait(false);
                if (!hourReady)
                {
                    progress?.Report($"Clock has no usable hour (only 0.00 / Изчисти or none in window); next day.");
                    continue;
                }

                foreach (var venue in request.Locations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progress?.Report($"Checking {venue.Name}, {day:yyyy-MM-dd}…");
                    await PauseBeforeInspectClickAsync(cancellationToken).ConfigureAwait(false);
                    var found = await CollectVenueSlotsAsync(page, venue, day, window, cancellationToken)
                        .ConfigureAwait(false);
                    slots.AddRange(found);
                }
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

    private IPage RequirePage()
    {
        if (_page is null)
        {
            throw new InvalidOperationException("The browser session has not been started.");
        }

        return _page;
    }

    private async Task EnsureHomeAsync(IPage page)
    {
        if (page.Url.StartsWith(HomeUrl, StringComparison.OrdinalIgnoreCase)
            && !page.Url.Contains("/clubs", StringComparison.OrdinalIgnoreCase)
            && !page.Url.Contains("lang/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await GotoHomeAndSettleAsync(page).ConfigureAwait(false);
        _currentSport = null;
    }

    /// <summary>
    /// Loads the home page, then waits 2 seconds so the language prompt can appear.
    /// Documented last-resort wait (not a generic sleep).
    /// </summary>
    private static async Task GotoHomeAndSettleAsync(IPage page)
    {
        await page.GotoAsync(HomeUrl, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded })
            .ConfigureAwait(false);
        await ThrowIfSiteDatabaseBusyAsync(page).ConfigureAwait(false);
        await page.WaitForTimeoutAsync(2_000).ConfigureAwait(false);
    }

    /// <summary>
    /// clicktoplay.io PHP uses mysqli; when MySQL hits max_connections the HTML shows HY000/1040.
    /// </summary>
    private static async Task ThrowIfSiteDatabaseBusyAsync(IPage page)
    {
        var html = await page.ContentAsync().ConfigureAwait(false);
        if (html.Contains("Too many connections", StringComparison.OrdinalIgnoreCase)
            || html.Contains("HY000/1040", StringComparison.OrdinalIgnoreCase)
            || html.Contains("mysqli_connect", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "clicktoplay.io’s database is full (mysqli Too many connections). Stop searching and wait; this is their MySQL limit, not a ClickPlay bug.");
        }
    }

    private async Task RecoverSportsPanelAsync(CancellationToken cancellationToken)
    {
        var page = RequirePage();
        var target = new PlaywrightOverlayTarget(page);
        await _overlayRecovery.RecoverAsync(target, cancellationToken).ConfigureAwait(false);

        if (await target.IsSportsPanelInteractableAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await _overlayRecovery.RecoverAsync(target, cancellationToken).ConfigureAwait(false);
        if (await target.IsSportsPanelInteractableAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        await CaptureDiagnosticsAsync("Sports panel still unresponsive after overlay recovery.").ConfigureAwait(false);
        throw new InvalidOperationException("The sports panel is not clickable. A diagnostic screenshot was saved.");
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
            catch (PlaywrightException)
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
            catch (PlaywrightException)
            {
            }
        }

        if (!page.Url.Contains("/clubs", StringComparison.OrdinalIgnoreCase))
        {
            await page.GotoAsync("https://clicktoplay.io/clubs/index.php", new PageGotoOptions
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
        catch (PlaywrightException)
        {
            if (!page.Url.Contains("/clubs", StringComparison.OrdinalIgnoreCase))
            {
                await page.GotoAsync("https://clicktoplay.io/clubs/index.php", new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded
                }).ConfigureAwait(false);
            }
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
        catch (PlaywrightException)
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

    /// <summary>
    /// Types the city name so the popup filters, then clicks <c>.city[data-city]</c>.
    /// </summary>
    private static async Task<bool> TrySelectCityOptionAsync(
        IPage page,
        string cityName,
        string cityId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var input = page.Locator("input.find-city").First;
        try
        {
            await input.FillAsync(cityName, new LocatorFillOptions { Timeout = 5_000 }).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
        }

        var option = page.Locator($".city[data-city='{cityId}']").First;
        try
        {
            await option.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 8_000
            }).ConfigureAwait(false);
            await option.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
            return true;
        }
        catch (PlaywrightException)
        {
            return false;
        }
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
                if (!name || name.length > 120 || seen.has(name)) continue;
                seen.add(name);
                results.push({ name, address });
              }
              return results;
            }
            """).ConfigureAwait(false);

        return (rows ?? [])
            .Where(r => r is not null && !string.IsNullOrWhiteSpace(r.Name))
            .Select(r => new Venue(r.Name.Trim(), (r.Address ?? string.Empty).Trim()))
            .ToArray();
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
    /// Pause before each inspect click so the listing and hour dropdown can settle.
    /// </summary>
    private static async Task PauseBeforeInspectClickAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// After a click or form submit, wait for the document to load (not networkidle — that stalls the datepicker).
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
        catch (PlaywrightException)
        {
        }

        await ThrowIfSiteDatabaseBusyAsync(page).ConfigureAwait(false);
    }

    /// <summary>
    /// After the datepicker <c>onSelect</c>, wait for club cards — do not use networkidle (it blocks the widget).
    /// </summary>
    private static async Task WaitForListingAfterCalendarAsync(IPage page, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await page.Locator("a.club-card").First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Attached,
                Timeout = 15_000
            }).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
        }

        await ThrowIfSiteDatabaseBusyAsync(page).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the listing day through jQuery UI datepicker on hidden <c>#date</c> (onSelect), not hover/color clicks.
    /// </summary>
    /// <returns>False when the day is beyond <c>data-max</c> or the widget did not apply it.</returns>
    private static async Task<bool> SelectDayOnCalendarAsync(
        IPage page,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var expected = day.ToString("yyyy-MM-dd");
        var applied = await TryApplyDatepickerApiAsync(page, day).ConfigureAwait(false);
        if (applied.Error == "beyond-max")
        {
            return false;
        }

        if (!applied.Ok)
        {
            await TryClickDatepickerDayAsync(page, day, cancellationToken).ConfigureAwait(false);
            applied = await TryApplyDatepickerApiAsync(page, day).ConfigureAwait(false);
            if (applied.Error == "beyond-max")
            {
                return false;
            }
        }

        if (!applied.Ok)
        {
            throw new InvalidOperationException(
                $"The site calendar did not select {expected} (value is '{applied.Iso}').");
        }

        await WaitForListingAfterCalendarAsync(page, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Calls <c>datepicker('setDate')</c> and the widget <c>onSelect</c> on <c>#date</c>.
    /// </summary>
    private static async Task<DateApplyResult> TryApplyDatepickerApiAsync(IPage page, DateOnly day)
    {
        var result = await page.EvaluateAsync<DateApplyResult>(
            """
            (args) => {
              const input = document.getElementById('date') || document.querySelector('.setsearchdate');
              if (!input || !window.jQuery || !window.jQuery.fn || !window.jQuery.fn.datepicker) {
                return { ok: false, iso: '', error: 'no-datepicker' };
              }
              const $ = window.jQuery;
              const max = parseInt(input.getAttribute('data-max') || '14', 10);
              const today = new Date();
              today.setHours(0, 0, 0, 0);
              const d = new Date(args.y, args.m - 1, args.d);
              d.setHours(0, 0, 0, 0);
              const diff = Math.round((d - today) / 86400000);
              if (diff > max) {
                return { ok: false, iso: '', error: 'beyond-max' };
              }
              $(input).datepicker('setDate', d);
              const inst = $.datepicker._getInst(input);
              const fmt = $.datepicker._get(inst, 'dateFormat');
              const text = $.datepicker.formatDate(fmt, d, $.datepicker._getFormatConfig(inst));
              const onSelect = $.datepicker._get(inst, 'onSelect');
              if (typeof onSelect === 'function') {
                onSelect.apply(input, [text, inst]);
              } else {
                $(input).trigger('change');
              }
              const setsearch = document.getElementById('setsearchdate');
              if (setsearch) setsearch.value = args.iso;
              const got = $(input).datepicker('getDate');
              if (!got) return { ok: false, iso: '', error: 'no-getdate' };
              const iso = got.getFullYear() + '-'
                + String(got.getMonth() + 1).padStart(2, '0') + '-'
                + String(got.getDate()).padStart(2, '0');
              return { ok: iso === args.iso, iso, error: '' };
            }
            """,
            new { iso = day.ToString("yyyy-MM-dd"), y = day.Year, m = day.Month, d = day.Day })
            .ConfigureAwait(false);

        return result ?? new DateApplyResult { Ok = false, Error = "no-datepicker" };
    }

    /// <summary>
    /// Opens the visible date control and clicks the day link inside jQuery UI's popup.
    /// </summary>
    private static async Task TryClickDatepickerDayAsync(
        IPage page,
        DateOnly day,
        CancellationToken cancellationToken)
    {
        var opener = page.Locator(".date-filter-container.desktop-only .single-date-filter-container").First;
        if (!await opener.IsVisibleAsync().ConfigureAwait(false))
        {
            opener = page.Locator(".single-date-filter-container").First;
        }

        if (!await opener.IsVisibleAsync().ConfigureAwait(false))
        {
            return;
        }

        await PauseBeforeInspectClickAsync(cancellationToken).ConfigureAwait(false);
        await HumanPointer.ClickAsync(page, opener, cancellationToken).ConfigureAwait(false);

        var picker = page.Locator("#ui-datepicker-div");
        try
        {
            await picker.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 8_000
            }).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            return;
        }

        var month = day.Month - 1;
        var dayLink = picker.Locator(
            $"td[data-handler='selectDay'][data-month='{month}'][data-year='{day.Year}'] a.ui-state-default");
        for (var step = 0; step < 16 && !await dayLink.First.IsVisibleAsync().ConfigureAwait(false); step++)
        {
            var next = picker.Locator(".ui-datepicker-next");
            if (!await next.IsVisibleAsync().ConfigureAwait(false))
            {
                break;
            }

            await PauseBeforeInspectClickAsync(cancellationToken).ConfigureAwait(false);
            await HumanPointer.ClickAsync(page, next, cancellationToken, timeoutMs: 5_000).ConfigureAwait(false);
        }

        if (await dayLink.First.IsVisibleAsync().ConfigureAwait(false))
        {
            await PauseBeforeInspectClickAsync(cancellationToken).ConfigureAwait(false);
            await HumanPointer.ClickAsync(page, dayLink.First, cancellationToken).ConfigureAwait(false);
            try
            {
                await picker.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Hidden,
                    Timeout = 5_000
                }).ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
            }
        }
    }

    /// <summary>
    /// Opens <c>input.find-club</c> (Име на клуб), picks the row, then clicks the matching <c>a.club-card</c> so the schedule loads.
    /// </summary>
    private static async Task<bool> TryOpenVenueFromClubNameAsync(
        IPage page,
        string venueName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var clubBox = page.Locator("input.find-club").First;
        try
        {
            await clubBox.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10_000
            }).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            return false;
        }

        await clubBox.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);

        var popup = page.Locator(".dropdown-content-club-popup");
        try
        {
            await popup.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 5_000
            }).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            await page.EvaluateAsync(
                """
                () => {
                  const input = document.querySelector('input.find-club');
                  if (input) {
                    input.focus();
                    input.click();
                  }
                  const drop = document.querySelector('.dropdown-content-club-popup');
                  if (drop) drop.style.display = 'block';
                }
                """).ConfigureAwait(false);
        }

        try
        {
            await clubBox.FillAsync(venueName, new LocatorFillOptions { Timeout = 5_000 }).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
        }

        var option = popup.Locator(".club-search[data-teren]")
            .Filter(new LocatorFilterOptions { HasText = venueName.Trim() })
            .First;
        if (!await option.IsVisibleAsync().ConfigureAwait(false))
        {
            option = page.Locator(".club-search[data-teren]")
                .Filter(new LocatorFilterOptions { HasText = venueName.Trim() })
                .First;
        }

        if (!await option.IsVisibleAsync().ConfigureAwait(false))
        {
            return false;
        }

        await option.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
        await WaitForSiteToRespondAsync(page, cancellationToken).ConfigureAwait(false);

        var card = page.Locator("a.club-card")
            .Filter(new LocatorFilterOptions { HasText = venueName.Trim() })
            .First;
        try
        {
            await card.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 15_000
            }).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            return false;
        }

        await card.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Opens <c>div.secondary-filter-button.time.toclear_filters</c> and clicks the earliest hour in the GUI window.
    /// </summary>
    /// <returns>False when that dropdown has no hour inside the window.</returns>
    private static async Task<bool> TrySelectHourFromTimeButtonAsync(
        IPage page,
        TimeWindow window,
        CancellationToken cancellationToken)
    {
        var timeButton = page.Locator("div.secondary-filter-button.time.toclear_filters").First;
        if (!await timeButton.IsVisibleAsync().ConfigureAwait(false))
        {
            timeButton = page.Locator("div.secondary-filter-button.time").First;
        }

        if (!await timeButton.IsVisibleAsync().ConfigureAwait(false))
        {
            return false;
        }

        await PauseBeforeInspectClickAsync(cancellationToken).ConfigureAwait(false);
        await HumanPointer.ClickAsync(page, timeButton, cancellationToken).ConfigureAwait(false);

        var optionRoot = page.Locator("div.dropdown-options.time.toclear_filters, #hours_to_move");
        try
        {
            await optionRoot.Locator(".hour-option").First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 8_000
            }).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            return false;
        }

        var rawHours = await page.EvaluateAsync<string[]>(
            """
            () => {
              const roots = document.querySelectorAll('div.dropdown-options.time.toclear_filters, #hours_to_move, #hours_move_mobile');
              const source = roots.length > 0 ? roots : [document];
              const Options = [];
              for (const root of source) {
                for (const el of root.querySelectorAll('.hour-option, .option-drop, a, button, div')) {
                  const text = (el.getAttribute('data') || el.textContent || '').replace(/\s+/g, ' ').trim();
                  if (text) Options.push(text);
                }
              }
              return Options;
            }
            """).ConfigureAwait(false);

        var parsed = BookingSlotParser.ParseSelectableHours(rawHours ?? []);
        if (parsed.Count == 0)
        {
            return false;
        }

        var chosen = BookingSlotParser.EarliestHourInWindow(window, parsed);
        if (chosen is null)
        {
            return false;
        }

        var label = chosen.Value.ToString("HH:mm");
        var option = optionRoot.Locator(".hour-option").Filter(new LocatorFilterOptions { HasText = label }).First;
        if (!await option.IsVisibleAsync().ConfigureAwait(false))
        {
            option = page.Locator(".hour-option").Filter(new LocatorFilterOptions { HasText = label }).First;
        }

        if (!await option.IsVisibleAsync().ConfigureAwait(false))
        {
            return false;
        }

        await PauseBeforeInspectClickAsync(cancellationToken).ConfigureAwait(false);
        await HumanPointer.ClickAsync(page, option, cancellationToken).ConfigureAwait(false);
        await WaitForListingAfterCalendarAsync(page, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Opens the clock dropdown and clicks a specific hour (ignores 0.00 / Изчисти).
    /// </summary>
    private static async Task<bool> TrySelectExactHourAsync(
        IPage page,
        TimeOnly hour,
        CancellationToken cancellationToken)
    {
        var window = new TimeWindow(hour, hour);
        return await TrySelectHourFromTimeButtonAsync(page, window, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<AvailabilitySlot>> CollectVenueSlotsAsync(
        IPage page,
        Venue venue,
        DateOnly day,
        TimeWindow window,
        CancellationToken cancellationToken)
    {
        var opened = await TryOpenVenueFromClubNameAsync(page, venue.Name, cancellationToken)
            .ConfigureAwait(false);
        if (!opened)
        {
            return [];
        }

        await WaitForSiteToRespondAsync(page, CancellationToken.None).ConfigureAwait(false);

        try
        {
            await page.WaitForURLAsync("**/club/**", new PageWaitForURLOptions { Timeout = 20_000 })
                .ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            if (!page.Url.Contains("/club", StringComparison.OrdinalIgnoreCase))
            {
                await ThrowIfSiteDatabaseBusyAsync(page).ConfigureAwait(false);
                return [];
            }
        }

        await ThrowIfSiteDatabaseBusyAsync(page).ConfigureAwait(false);

        try
        {
            await page.Locator(".sport-facility .facility.active .facility-schedule .booking-slot").First
                .WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Attached,
                    Timeout = 15_000
                }).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            throw new InvalidOperationException(
                "The site layout changed: facility-schedule booking slots were not found.");
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

        try
        {
            await page.GoBackAsync(new PageGoBackOptions { WaitUntil = WaitUntilState.DOMContentLoaded })
                .ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
            await page.GotoAsync("https://clicktoplay.io/clubs/index.php", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded
            }).ConfigureAwait(false);
        }

        await WaitForSiteToRespondAsync(page, CancellationToken.None).ConfigureAwait(false);

        try
        {
            await page.Locator("a.club-card").First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Attached,
                Timeout = 10_000
            }).ConfigureAwait(false);
        }
        catch (PlaywrightException)
        {
        }

        return list;
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

    private sealed class ScrapedVenue
    {
        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;
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

    private sealed class DateApplyResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("iso")]
        public string Iso { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;
    }
}
