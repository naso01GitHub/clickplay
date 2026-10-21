using Microsoft.Playwright;

namespace ClickPlay.Core.Browser;

/// <summary>
/// Clicks a locator after scrolling it into view, then pauses so the site can catch up.
/// </summary>
internal static class HumanPointer
{
    /// <summary>
    /// Scrolls the target into view, clicks it, and waits 800ms.
    /// </summary>
    /// <example>
    /// <code>
    /// await HumanPointer.ClickAsync(page, page.Locator("a.club-card").First, CancellationToken.None);
    /// </code>
    /// </example>
    public static async Task ClickAsync(
        IPage page,
        ILocator locator,
        CancellationToken cancellationToken,
        int timeoutMs = 10_000)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(locator);
        cancellationToken.ThrowIfCancellationRequested();

        await locator.ScrollIntoViewIfNeededAsync(new LocatorScrollIntoViewIfNeededOptions { Timeout = timeoutMs })
            .ConfigureAwait(false);
        await locator.ClickAsync(new LocatorClickOptions { Timeout = timeoutMs }).ConfigureAwait(false);
        await page.WaitForTimeoutAsync(800).ConfigureAwait(false);
    }
}
