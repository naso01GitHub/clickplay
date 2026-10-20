using Microsoft.Playwright;

namespace ClickPlay.Core.Browser;

/// <summary>
/// Overlay recovery against a Playwright page.
/// </summary>
public sealed class PlaywrightOverlayTarget : IOverlayTarget
{
    private readonly IPage _page;

    public PlaywrightOverlayTarget(IPage page)
    {
        _page = page;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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
        catch (PlaywrightException)
        {
        }
    }
}
