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
- Sports: visible `.sport-container.index-sport-nav` on home (not the zero-size a11y list items). After a sport click, URL stays on home; then click **Резервирай корт** and wait for `/clubs/index.php`.
- Cities: on `/clubs` click `.search-by-location` (the `input.find-city` Град box alone does not open the list). Read `.city[data-city]` (skip `data-city=cl`). Select a city by typing its name then clicking `.city[data-city]` (Варна is `42`).
- After sport **and** city are chosen, locations are `a.club-card` for that pair. Multiple locations can be selected. Before reading club cards, if local time is after 17:00, set the site calendar to **tomorrow** and the clock to **10:00**, then collect locations.
- Browser lifecycle: collect sports then close; GUI sport selected → open, navigate to that sport, collect cities, close; GUI city selected → open, navigate to that sport and city, collect locations, close; GUI Search → open and run the repeat loop (close after Results, wait, reopen).
- Search selected locations for free hours in the weekday or weekend/holiday windows.
- If the HTML contains `Too many connections` / `HY000/1040` / `mysqli_connect`, stop the cycle. That is clicktoplay.io’s MySQL `max_connections`, not a ClickPlay connection pool. One Playwright tab is sequential; the site still opens a PHP mysqli on every page/AJAX hit and can leak or share a global cap with all visitors.
- After every click (sport, Резервирай, city, clock, club card, overlay), wait for `DOMContentLoaded` and the MySQL busy check before the next action. Do **not** wait for `networkidle` — it stalls the jQuery datepicker. After calendar `onSelect`, wait for `a.club-card` instead. Visible controls are clicked like a person: move the mouse onto the box in steps, pause, then mouse-down/up. Do not JS-click club cards. Do not click `.booking-slot` (no booking). Calendar day still uses the datepicker API.
- During availability inspect, wait **5 seconds** before club/hour clicks. For each days-ahead date, set the listing day on the hidden jQuery UI widget `#date.setsearchdate.hasDatepicker` via `datepicker('setDate')` and the widget `onSelect` (also copy ISO into `#setsearchdate`). Do not rely on hover/color. If the API is missing, open `.date-filter-container.desktop-only .single-date-filter-container` and click `#ui-datepicker-div td[data-handler=selectDay] a.ui-state-default`. `#show_datepicker` is duplicated; do not use it as the only opener. Skip days beyond `data-max` (usually 14). Then click `div.secondary-filter-button.time.toclear_filters` and choose the **earliest** `.hour-option` in the GUI weekday or weekend/holiday window. Ignore clock rows `0.00` / `00:00` and `Изчисти`. If the clock has no hour inside the GUI window, skip that date and continue with the next day.
- In the repeat search, open a location with **Име на клуб** (`input.find-club`), click `.club-search[data-teren]` in `.dropdown-content-club-popup`, then click the matching `a.club-card` so free slots load. Club profile: `.sport-facility` → `.facility.active` → `.facility-schedule`. Collect every `.booking-slot` that has class `itsfree` (and usually `pr showtd`), `data-from-to`, and `data-df`. Skip `.closed`. Keep a slot when `data-from-to` start is inside the GUI time window **and** `data-df` is the search day (`dd.MM.yyyy`). Each Results line includes location name, time range, and `data-df`.

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


