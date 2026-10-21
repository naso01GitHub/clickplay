# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Required reading

- [AGENTS.md](AGENTS.md) — binding project rules (cross-platform constraints, MVVM, Playwright usage, testing requirements, product behavior to preserve). Follow it for every change.
- [target_web.md](target_web.md) — source of truth for clicktoplay.io: locators, click/wait discipline, MySQL "Too many connections" handling, calendar/datepicker handling, overlay recovery. Read it before touching anything under `src/ClickPlay.Core/Browser/`.
- [Request.txt](Request.txt) — the original functional spec of the startup → sport → city → search-loop flow.

Never let tests or ad-hoc scripts hit the live site; never log in, book, or click `.booking-slot`.

## Commands

Solution file is `ClickPlay.slnx` (net10.0).

```powershell
dotnet build ClickPlay.slnx
dotnet test ClickPlay.slnx
dotnet test --filter "FullyQualifiedName~MainViewModelCascadeTests"          # one class
dotnet test --filter "FullyQualifiedName~SearchInputValidatorTests.MethodName" # one test
dotnet run --project src/ClickPlay.App
```

One-time Chromium install after the first build (Windows PowerShell or Mac `pwsh`):

```powershell
pwsh src/ClickPlay.Core/bin/Debug/net10.0/playwright.ps1 install chromium
```

No linter is configured. Mac setup (tools, Chromium, standalone `osx-arm64`/`osx-x64` publish): [INSTALL-MAC.md](INSTALL-MAC.md).

## Architecture

Three projects:

- **ClickPlay.Core** — UI-free, cross-platform. Models, validation, calendar logic, and all Playwright code.
- **ClickPlay.App** — Avalonia 12 + CommunityToolkit.Mvvm. Essentially one screen: `MainWindow` bound to `MainViewModel`.
- **ClickPlay.Tests** — xUnit; references both projects and tests the ViewModel directly with fakes.

### Layering of the site access

`MainViewModel` → `IClickToPlayCatalog` → `PlaywrightClickToPlayCatalog` (thin adapter) → `IClickToPlaySession` → `ClickToPlayBrowserSession` (the Playwright implementation of target_web.md). The sport is chosen by a real click (site session), then the city listing and each club/day schedule are opened by URL (`BuildListingUrl`, `BuildClubUrl`); venues carry their `ClubSlug` from the card link. Note that Playwright timeouts are `System.TimeoutException`, not `PlaywrightException`. `App.axaml.cs` wires this up and disposes the session on exit. `PlaceholderClickToPlayCatalog` is a static-data stand-in.

The browser is **not** long-lived: the ViewModel calls `StartAsync` → one operation → `CloseBrowserAsync` for each cascade step (sports on startup, cities on sport change, venues on city change). Search runs a repeat loop in `MainViewModel.Search`: open browser, scan every selected venue across `DaysAhead` days, close, report only slots not seen in the previous scan, wait `Repeat (min)`, repeat until Stop cancels the token. Location checkboxes are re-read at the start of every cycle.

### Core helpers worth knowing

- `Browser/OverlayRecovery` + `IOverlayTarget` / `PlaywrightOverlayTarget` — language-prompt dismissal, abstracted so it is unit-testable with `Fakes/FakeOverlayTarget`.
- `Browser/BookingSlotParser` — filters `.booking-slot.itsfree` data by time window and `data-df` day.
- `Browser/HumanPointer` — stepped mouse movement for "click like a person".
- `Calendar/BulgarianHolidayCalendar`, `SearchDatePlanner`, `ListingTimePlanner` — weekday vs weekend/holiday window selection, the days-ahead date list, and the "after 17:00 use tomorrow 10:00" rule.
- `Validation/SearchInputValidator` (days ahead, repeat minutes, time windows) and `EmailRecipientParser` (one address per line).
- `Browser/AppPaths` — diagnostics screenshots go to `%AppData%/ClickPlay/diagnostics` (cross-platform `ApplicationData`); the results log is `results-log.txt` next to the executable.

### Mail

`Core/Mail`: `IMailSender` → `MailKitMailSender`; `ResultsMailFormatter` builds subject/body; `SmtpSettingsValidator` checks the GUI fields; `MailSettingsStore` saves non-secret fields to `%AppData%/ClickPlay/smtp.settings.json` and the password via `ISecretStore` → `ProtectedSecretStore` (MSAL extensions storage: DPAPI file on Windows, Keychain on Mac). `MainViewModel.Search` validates and snapshots SMTP at start, then calls `MailNewResultsAsync` after each scan that adds new Results; To/Subject are read at send time. Tests use `Fakes/FakeMailSender` and `Fakes/FakeSecretStore`.

### Testing pattern

Tests construct `MainViewModel` with a fake catalog/session (`tests/ClickPlay.Tests/Fakes/`) and drive properties/commands. `MainViewModel` exposes injectable seams such as `ScanClock` and `ResultsLogPath` for deterministic tests — use them rather than real time or the real log file.

## Conventions

- Doxygen-style XML docs are the house style (see [.cursor/skills/doxygen-comments/SKILL.md](.cursor/skills/doxygen-comments/SKILL.md)): `<summary>`, params, `<returns>`, and an `<example><code>` block. Match this on new public members.
- `.cursor/hooks` save Cursor conversations to the gitignored `conversations/` folder; ignore that folder.
