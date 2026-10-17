namespace ClickPlay.Core.Models;

/// <summary>
/// Playwright browser launch flags. Headless true runs without a window; false shows Chromium.
/// </summary>
/// <param name="Headless">When true, Chromium runs headless. When false, the browser window is shown.</param>
/// <example>
/// <code>
/// var options = new BrowserLaunchOptions(Headless: false);
/// </code>
/// </example>
public sealed record BrowserLaunchOptions(bool Headless = true);
