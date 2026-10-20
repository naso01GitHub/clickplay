using Microsoft.Playwright;

namespace ClickPlay.Core.Browser;

/// <summary>
/// Moves the real mouse onto a control, pauses, then clicks so the site hover/click handlers can run visibly.
/// </summary>
internal static class HumanPointer
{
    /// <summary>
    /// Scrolls the target into view, moves the cursor there in steps, waits, then mouse-down/up with a hold delay.
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

        var box = await locator.BoundingBoxAsync().ConfigureAwait(false);
        if (box is { Width: > 1, Height: > 1 })
        {
            var x = box.X + (box.Width / 2);
            var y = box.Y + (box.Height / 2);
            await page.Mouse.MoveAsync(x, y, new MouseMoveOptions { Steps = 28 }).ConfigureAwait(false);
        }
        else
        {
            await locator.HoverAsync(new LocatorHoverOptions { Timeout = timeoutMs }).ConfigureAwait(false);
        }

        await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken).ConfigureAwait(false);

        await locator.ClickAsync(new LocatorClickOptions
        {
            Timeout = timeoutMs,
            Delay = 220
        }).ConfigureAwait(false);

        await Task.Delay(TimeSpan.FromMilliseconds(1_200), cancellationToken).ConfigureAwait(false);
    }
}
