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
- Sport list comes from the site navigation panel.
- After a sport is chosen, cities come from the booking city list (`Град`).
- After sport **and** city are chosen, locations are all clubs/venues for that pair. Multiple locations can be selected.
- Search selected locations for free hours in the weekday or weekend/holiday windows.

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


