using Microsoft.Playwright;

namespace ClickPlay.Core.Browser;

/// <summary>
/// Overlay recovery against a Playwright page.
/// </summary>
public sealed class PlaywrightOverlayTarget : IOverlayTarget
{
    private readonly IPage _page;

    /// <summary>
    /// Wraps a Playwright page for <see cref="OverlayRecovery"/>.
    /// </summary>
    /// <param name="page">The session page.</param>
    /// <example>
    /// <code>
    /// await new OverlayRecovery().RecoverAsync(new PlaywrightOverlayTarget(page), CancellationToken.None);
    /// </code>
    /// </example>
    public PlaywrightOverlayTarget(IPage page)
    {
        _page = page;
    }

    /// <summary>
    /// True when a sport-bar box is visible with a real size (not the zero-size accessibility items).
    /// </summary>
    /// <param name="cancellationToken">Checked before reading the page.</param>
    /// <returns>True when the sports bar can be clicked.</returns>
    /// <example>
    /// <code>
    /// var ready = await target.IsSportsPanelInteractableAsync(CancellationToken.None);
    /// </code>
    /// </example>
    public async Task<bool> IsSportsPanelInteractableAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _page.EvaluateAsync<bool>(
            """
            () => {
              const el = [...document.querySelectorAll('.sport-container, .index-sport-nav')]
                .find(e => e.offsetWidth > 20 && e.offsetHeight > 10);
              return !!el;
            }
            """).ConfigureAwait(false);
    }

    /// <summary>
    /// True when an element with this text is visible, e.g. Български or Запази on the language prompt.
    /// </summary>
    /// <param name="name">Visible text to look for.</param>
    /// <param name="cancellationToken">Checked before reading the page.</param>
    /// <returns>True when visible; false when hidden, missing, or the page is navigating.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    /// <example>
    /// <code>
    /// if (await target.IsVisibleAsync("Запази", CancellationToken.None)) { }
    /// </code>
    /// </example>
    public async Task<bool> IsVisibleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();
        var locator = _page.GetByText(name).First;
        try
        {
            return await locator.IsVisibleAsync(new LocatorIsVisibleOptions()).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Clicks the element with exactly this text if it is visible, then waits for the page to load.
    /// Does nothing when the element is not visible.
    /// </summary>
    /// <param name="name">Exact visible text, e.g. Български.</param>
    /// <param name="cancellationToken">Checked before clicking.</param>
    /// <returns>A task that completes after the click and the load wait (timeout ignored).</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    /// <example>
    /// <code>
    /// await target.ClickAsync("Български", CancellationToken.None);
    /// </code>
    /// </example>
    public async Task ClickAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        cancellationToken.ThrowIfCancellationRequested();
        var locator = _page.GetByText(name, new PageGetByTextOptions { Exact = true }).First;
        if (!await locator.IsVisibleAsync().ConfigureAwait(false))
        {
            return;
        }

        await locator.ClickAsync().ConfigureAwait(false);
        try
        {
            await _page.WaitForLoadStateAsync(
                    LoadState.DOMContentLoaded,
                    new PageWaitForLoadStateOptions { Timeout = 15_000 })
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is PlaywrightException or TimeoutException)
        {
        }
    }
}
