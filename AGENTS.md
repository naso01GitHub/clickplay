# ClickPlay agent rules

This repository is a **Windows and Mac** desktop app in **C#**, **Avalonia**, and **Playwright**. Follow these rules for all changes.

Target website (clicktoplay.io) rules — terms, locators, overlay recovery, and live-site test limits — are in [target_web.md](target_web.md). Read that file before changing scraping, locators, overlay recovery, or site access.

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

## Configuration and secrets

- Keep environment-specific settings outside source code (user app-data JSON or environment variables). Gitignore local settings.
- Do not commit credentials, private keys, tokens, or personal session data.
- Do not hardcode user-specific paths or machine-specific configuration.
- Validate external data before using it (SMTP input, days-ahead integer, time windows). For HTML/JSON from the target site, see [target_web.md](target_web.md).

## Browser automation (Playwright)

- Use **Microsoft.Playwright** only. Do not add Selenium or Windows-only WebView2 into shared code.
- One browser/context lifecycle per search session; dispose with `await using`.
- Default **headless**; optional **Debug** in the UI (`true` shows Chromium, `false` is headless).
- Wait for load state / locators (`WaitForSelectorAsync`, `Locator`). No fixed `Thread.Sleep` except a documented last resort.
- Timeouts: navigation ~30s, element ~10s; cancel via `CancellationToken`.
- Install Chromium once per machine (Windows PowerShell or Mac `pwsh`):
  `pwsh src/ClickPlay.Core/bin/Debug/net10.0/playwright.ps1 install chromium`
- Site-specific locators, cookies, and overlay recovery: [target_web.md](target_web.md).

## Component testing requirements

- Add or update tests when changing ViewModels, validators, holiday/date logic, catalog interfaces, or mail formatting.
- Use a cross-platform test stack (`xUnit` + `net10.0`). Do not add MSTest Windows-only runners or WinAppDriver.
- Component tests must not send real email. Mock `IClickToPlayCatalog` and SMTP. Do not hit the live site; see [target_web.md](target_web.md).
- Do not put credentials, tokens, or machine-specific paths in test fixtures.
- Cover at least:
  - Days-ahead parsing (valid integer vs empty/zero/non-numeric)
  - Time-window parsing (`from` before `to`; weekday vs weekend/holiday selection)
  - UI cascade: sport selected → cities load; city selected → locations load; Search disabled until at least one location is selected
  - Multi-select locations (select all / clear)
- Prefer testing ViewModels and Core types over clicking Avalonia controls. If a view test is required, use Avalonia headless, not a live desktop session.
- Tests must pass on Windows and Mac with `dotnet test`.
- Overlay-recovery tests: [target_web.md](target_web.md).

## Product behavior agents must preserve

- City dropdown appears after a sport is selected.
- Locations appear after sport **and** city are selected. **Multiple locations can be selected**.
- Days ahead: a **text box** (positive integer). No per-day time rows. Each scan covers the GUI calendar day plus the following days, `Days ahead` days in total. Before every search cycle, a calendar date in the past is moved to today (a future date is kept).
- Repeat (minutes between cycles): default **2**, range 1–60.
- Two time windows only: **weekdays**, and **weekends and holidays** (Saturday, Sunday, and Bulgarian public holidays).
- Email results via user SMTP settings (To accepts many addresses, one per line). After each scan that adds **new** Results records, send **one** mail with all of them to every To address. Subject = Subject field + location + time slot of the first new record (`(+N more)` when several).
- While Search runs, Host, Port, User, Password, and From show only `*` and are read-only. To and Subject stay visible and editable; the mail uses their values at send time. A mail failure never stops Search.
- Email settings persist in `%AppData%/ClickPlay/smtp.settings.json` (no password); the password is stored only in the OS secure store (`ProtectedSecretStore`: DPAPI on Windows, Keychain on Mac). A **Remember login** checkbox (default on) controls whether User and Password are saved; turning it off erases the saved login immediately, and they start empty at the next launch.
- Sport/city/location data and free-hour search against the live site: [target_web.md](target_web.md).
