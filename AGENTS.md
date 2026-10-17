# ClickPlay agent rules

This repository is a **Windows and Mac** desktop app in **C#**, **Avalonia**, and **Playwright**. Follow these rules for all changes.

## Cross-platform

- Targets: **Windows** and **Mac** only (not Android).
- Stack: **C#**, **Avalonia**, **Playwright** (`Microsoft.Playwright`).
- Prefer cross-platform libraries and APIs (`Path.Combine`, `Environment.GetFolderPath`, `HttpClient`, MailKit, Playwright).
- Do not introduce Windows-only dependencies into shared application code (`ClickPlay.Core`) without explicit justification. No `System.Windows.*`, `Microsoft.Win32`, WinForms, WPF, or Win32 P/Invoke in shared code.

## Coding standards

- .NET 8 or later (this machine uses net10.0), nullable reference types on, file-scoped namespaces.
- MVVM: Avalonia views bind to ViewModels. No Playwright or SMTP in code-behind.
- Async all the way (`async`/`await`, `CancellationToken` on scrape and mail).
- UI updates on the Avalonia dispatcher. Run Playwright off the UI thread.
- Prefer `ILogger` or user-visible status text over `Console.WriteLine`.
- Names: `PascalCase` types/methods, `_camelCase` private fields.
- XML docs on public scraper and mail APIs.

## Configuration, secrets, and site access

- Keep environment-specific settings outside source code (user app-data JSON or environment variables). Gitignore local settings.
- Do not commit credentials, private keys, tokens, or personal session data.
- Do not hardcode user-specific paths or machine-specific configuration.
- Validate external data before using it (HTML/JSON from clicktoplay.io, SMTP input, days-ahead integer, time windows).
- Follow [clicktoplay.io](https://clicktoplay.io/) applicable terms and access requirements. Do not log in, book, or submit payment.

## Browser automation (Playwright)

- Use **Microsoft.Playwright** only. Do not add Selenium or Windows-only WebView2 into shared code.
- One browser/context lifecycle per search session; dispose with `await using`.
- Default **headless**; optional “show browser” for debugging.
- Wait for load state / locators (`WaitForSelectorAsync`, `Locator`). No fixed `Thread.Sleep` except a documented last resort.
- Prefer role/text locators that match Bulgarian UI (`Резервирай`, `Град`) with English fallbacks.
- Timeouts: navigation ~30s, element ~10s; cancel via `CancellationToken`.
- Parse only visible club/city data. Fail with a clear message if the site layout changed.
- Do not store ClickToPlay session cookies in git.
- Install browsers with Playwright’s Chromium installer (documented for Win and Mac).

### Overlay recovery (sports panel)

Call a dedicated helper (e.g. `DismissBlockingOverlaysAsync`) before interacting with the sports panel.

Check **visibility** before every click (`Locator.IsVisibleAsync`). Do not click a button that is not visible.

1. Check whether a dialog or consent overlay is blocking interaction.
2. If the **Запази** button is visible, select **Български**, then click **Запази**.
3. If the **Приемам** button is then visible, click it.
4. Wait until the dialogs or overlays disappear (locator hidden or detached).
5. Retry interacting with the sports panel **once**.
6. If the panel remains unresponsive, capture a diagnostic screenshot and log the current page state (URL, title, visible overlay text). Save screenshots under the user app-data diagnostics folder — not a hardcoded machine path.

Sequence when those controls are present: **Български** → **Запази** → **Приемам** → sports panel.

- Do not assume every sports-panel failure is caused by these dialogs.
- Do not repeatedly click buttons without verifying the result (one attempt per visible button, then re-check visibility).

## Component testing requirements

- Add or update tests when changing ViewModels, validators, holiday/date logic, catalog interfaces, or mail formatting.
- Use a cross-platform test stack (`xUnit` + `net10.0`). Do not add MSTest Windows-only runners or WinAppDriver.
- Component tests must not open [clicktoplay.io](https://clicktoplay.io/) and must not send real email. Mock `IClickToPlayCatalog` and SMTP.
- Do not put credentials, tokens, or machine-specific paths in test fixtures.
- Cover at least:
  - Days-ahead parsing (valid integer vs empty/zero/non-numeric)
  - Time-window parsing (`from` before `to`; weekday vs weekend/holiday selection)
  - UI cascade: sport selected → cities load; city selected → locations load; Search disabled until at least one location is selected
  - Multi-select locations (select all / clear)
  - Overlay recovery helper: click **Запази** / **Приемам** only when the locator is visible; skip when hidden
- Prefer testing ViewModels and Core types over clicking Avalonia controls. If a view test is required, use Avalonia headless, not a live desktop session.
- Tests must pass on Windows and Mac with `dotnet test`.

## Product behavior agents must preserve

- Sport dropdown from the site navigation.
- City dropdown appears after a sport is selected.
- Locations (all clubs/venues for that sport + city) appear after sport **and** city are selected. **Multiple locations can be selected**.
- Days ahead: a **text box** (positive integer). No per-day time rows.
- Two time windows only: **weekdays**, and **weekends and holidays** (Saturday, Sunday, and Bulgarian public holidays).
- Search selected locations for free hours in those windows; email results via user SMTP settings.
