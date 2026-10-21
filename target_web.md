# Target website: clicktoplay.io

This is the source of truth for [https://clicktoplay.io/](https://clicktoplay.io/). App-wide rules stay in [AGENTS.md](AGENTS.md).

## Access and terms

- Follow [clicktoplay.io](https://clicktoplay.io/) applicable terms and access requirements.
- Do not log in, book, or submit payment.
- Do not store ClickToPlay session cookies in git.
- Validate external data before using it (HTML/JSON from clicktoplay.io).
- Component tests must not open [clicktoplay.io](https://clicktoplay.io/). Mock `IClickToPlayCatalog`.

## Site UI and locators

- Prefer role/text locators that match Bulgarian UI (`Резервирай`, `Град`) with English fallbacks.
- Parse only visible club/city data. Fail with a clear message if the site layout changed.
- Sports: visible `.sport-container.index-sport-nav` on home (not the zero-size a11y list items). After a sport click, URL stays on home; then click **Резервирай** and wait for `/clubs/index.php`.
- Sport is stored in the site session: the sport-bar click posts `ajax/set_sport.php` and sets cookie `selected_sport` (= the box's `data-id`). Club pages then show that sport's courts. Select the sport by clicking, not by forging the cookie.
- Cities: on `/clubs` click `.search-by-location` (the `input.find-city` Град box alone does not open the list). Read `.city[data-city]` (skip `data-city=cl`); the value is the city id (Варна is `42`).
- Locations: open `https://clicktoplay.io/clubs/index.php?city={id}` (the site's own GET search form). If local time is after 17:00 add `&date={tomorrow yyyy-MM-dd}&hour=10:00`. Read `a.club-card` whose link is `/club/?club={slug}…` and keep the slug with the venue; skip advert cards linking elsewhere. Multiple locations can be selected.
- Browser lifecycle: collect sports then close; GUI sport selected → open, collect cities into the dropdown, close; GUI city selected → reopen, collect locations, close; GUI Search → open and run the repeat loop (close after Results, wait, reopen).
- Search selected locations for free hours in the weekday or weekend/holiday windows.
- If the HTML contains `Too many connections` or `HY000/1040` in **visible** page text, wait 3 seconds, reload, and check once more. If it is still there, skip that club or day and continue the scan. Do not end the search, and do not match `mysqli_connect` inside scripts. That is clicktoplay.io’s MySQL `max_connections`, not a ClickPlay connection pool. One Playwright tab is sequential; the site still opens a PHP mysqli on every page/AJAX hit and can leak or share a global cap with all visitors.
- After every click (sport, Резервирай, city popup, overlay) and every `GotoAsync`, wait for `DOMContentLoaded` and run the MySQL busy check before the next action. Do **not** wait for `networkidle`. Do not add a fixed 5s/10s pause; wait on locators/URL instead. Chromium `SlowMo` is 800ms, which also spaces out requests to the site.
- Playwright .NET throws `System.TimeoutException` on timeouts — it is **not** a `PlaywrightException`. Any optional wait must catch both, or one slow page aborts the whole scan.
- Repeat search: after the sport click, open each selected location's schedule directly, one page load per location and day: `https://clicktoplay.io/club/?club={slug}&date={yyyy-MM-dd}`. Do **not** pass `hour` — the site hides slots before that hour; apply the GUI window in code. No datepicker, clock dropdown, **Име на клуб** popup, or `GoBack` is needed. Skip days beyond the site's booking range (usually 14).
- Club profile: `.sport-facility` → `.facility.active` → `.facility-schedule`. Collect every `.booking-slot` that has class `itsfree` (and usually `pr showtd`), `data-from-to`, and `data-df`. Skip `.closed`. Do not click `.booking-slot` (no booking). Keep a slot when `data-from-to` start is inside the GUI time window **and** `data-df` is the search day (`dd.MM.yyyy`). If the page is busy, times out, or has no `.sport-facility .facility`, skip that location and day and continue. Fail the scan only when no schedule at all could be read, or the browser was closed. Each Results scan starts with a date-time stamp; slot lines include location, court, date, and time — not price. Re-read location checkboxes before every scan.

## Overlay recovery (sports panel)

Call a dedicated helper (e.g. `DismissBlockingOverlaysAsync`) before interacting with the sports panel.

Check **visibility** before every click (`Locator.IsVisibleAsync`). Do not click a button that is not visible.

1. Load the page, then wait **2 seconds** so the language prompt can appear.
2. Check whether a language prompt is visible. If it is, select **Български** then click **Запази**. The sports bar can look present while this prompt still covers **Резервирай**.
3. Do not accept cookies.
4. Wait until the dialogs or overlays disappear (locator hidden or detached).
5. Continue to the sports panel; retry interacting with it **once**.
6. If the panel remains unresponsive, capture a diagnostic screenshot and log the current page state (URL, title, visible overlay text). Save screenshots under the user app-data diagnostics folder — not a hardcoded machine path.



- Do not assume every sports-panel failure is caused by these dialogs.
- Do not repeatedly click buttons without verifying the result (one attempt per visible button, then re-check visibility).

## Tests that cover the target site helper


