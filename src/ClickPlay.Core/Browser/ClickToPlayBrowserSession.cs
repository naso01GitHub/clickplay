using ClickPlay.Core.Models;
using ClickPlay.Core.Services;
using Microsoft.Playwright;

namespace ClickPlay.Core.Browser;

/// <summary>
/// One Chromium page for the app lifetime. Does not close after filling dropdowns.
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
                  const nodes = [...document.querySelectorAll('.sport-container')];
                  const names = nodes.map(n => (n.innerText || '').trim().split('\n')[0].trim())
                    .filter(t => t.length > 0 && t.length < 40);
                  return [...new Set(names)];
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

            var clicked = await page.EvaluateAsync<bool>(
                """
                (name) => {
                  const els = [...document.querySelectorAll('.sport-container')];
                  const target = els.find(el => (el.innerText || '').trim().split('\n')[0].trim() === name && el.offsetWidth > 0)
                    || els.find(el => (el.innerText || '').trim().split('\n')[0].trim() === name);
                  if (!target) return false;
                  target.click();
                  return true;
                }
                """,
                sport.Name).ConfigureAwait(false);

            if (!clicked)
            {
                throw new InvalidOperationException($"The site layout changed: sport '{sport.Name}' was not found.");
            }

            await page.Locator("button.hero-btn.reservation").First
                .WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 })
                .ConfigureAwait(false);

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
    public async Task<IReadOnlyList<Venue>> SelectCityAsync(City city, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(city);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var page = RequirePage();
            if (_currentSport is null)
            {
                throw new InvalidOperationException("Select a sport before loading locations.");
            }

            if (!page.Url.Contains("/clubs", StringComparison.OrdinalIgnoreCase))
            {
                _gate.Release();
                try
                {
                    await SelectSportAsync(_currentSport, cancellationToken).ConfigureAwait(false);
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

            try
            {
                await filtered.ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                await page.WaitForTimeoutAsync(2_000).ConfigureAwait(false);
            }

            await page.Locator("a.club-card h3.club-name").First.WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Attached,
                Timeout = 15_000
            }).ConfigureAwait(false);

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
        await page.WaitForTimeoutAsync(2_000).ConfigureAwait(false);
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
        var hero = page.Locator("button.hero-btn.reservation").First;
        if (await hero.IsVisibleAsync().ConfigureAwait(false))
        {
            try
            {
                await hero.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
                return;
            }
            catch (PlaywrightException)
            {
                // Cookie overlay may intercept; clubs URL is used as a fallback below.
            }
        }

        var reserve = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions
        {
            NameRegex = new System.Text.RegularExpressions.Regex("Резервирай корт|Reserve court", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        }).First;

        if (await reserve.IsVisibleAsync().ConfigureAwait(false))
        {
            try
            {
                await reserve.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
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
    /// Opens the clubs city list by clicking Град. The site reveals cities on that click/focus.
    /// </summary>
    private static async Task OpenCityFilterAsync(IPage page)
    {
        var filters = page.GetByText("Филтри", new PageGetByTextOptions { Exact = true }).First;
        if (await filters.IsVisibleAsync().ConfigureAwait(false))
        {
            var cityAlreadyVisible = await page.Locator("input.find-city").First.IsVisibleAsync().ConfigureAwait(false);
            if (!cityAlreadyVisible)
            {
                try
                {
                    await filters.ClickAsync(new LocatorClickOptions { Timeout = 5_000 }).ConfigureAwait(false);
                }
                catch (PlaywrightException)
                {
                }
            }
        }

        var grad = page.Locator("input.find-city")
            .Or(page.GetByPlaceholder("Град"))
            .Or(page.GetByPlaceholder("City"))
            .First;

        await grad.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Attached,
            Timeout = 15_000
        }).ConfigureAwait(false);

        if (await grad.IsVisibleAsync().ConfigureAwait(false))
        {
            try
            {
                await grad.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
            }
        }

        await page.EvaluateAsync(
            """
            () => {
              const input = document.querySelector('input.find-city');
              if (input) {
                input.focus();
                input.dispatchEvent(new Event('focus', { bubbles: true }));
                input.click();
              }
              const container = document.querySelector('.search-by-location');
              if (container) container.classList.add('active');
              const dropdown = document.querySelector('.dropdown-content-city-popup');
              if (dropdown) dropdown.style.display = 'block';
            }
            """).ConfigureAwait(false);

        await page.Locator(".city-name-popup").First.WaitForAsync(new LocatorWaitForOptions
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
              return [...root.querySelectorAll('.city-name-popup')]
                .map(n => (n.textContent || '').trim())
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
}
