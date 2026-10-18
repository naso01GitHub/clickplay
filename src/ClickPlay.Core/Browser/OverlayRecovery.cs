namespace ClickPlay.Core.Browser;

/// <summary>
/// Language overlay recovery. Never accepts cookies.
/// </summary>
public sealed class OverlayRecovery
{
    public const string Bulgarian = "Български";
    public const string BulgarianEn = "Bulgarian";
    public const string Save = "Запази";
    public const string AcceptCookies = "Приемам";

    /// <summary>
    /// If a language prompt is visible, selects Bulgarian and saves. Does not click Приемам.
    /// Sports-panel clickability does not skip this check — the prompt can still cover Резервирай.
    /// </summary>
    /// <param name="target">Page overlay actions.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <example>
    /// <code>
    /// await new OverlayRecovery().RecoverAsync(target, CancellationToken.None);
    /// </code>
    /// </example>
    public async Task RecoverAsync(IOverlayTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        var saveVisible = await target.IsVisibleAsync(Save, cancellationToken).ConfigureAwait(false);
        var bulgarianVisible = await target.IsVisibleAsync(Bulgarian, cancellationToken).ConfigureAwait(false)
            || await target.IsVisibleAsync(BulgarianEn, cancellationToken).ConfigureAwait(false);

        if (!saveVisible && !bulgarianVisible)
        {
            return;
        }

        if (await target.IsVisibleAsync(Bulgarian, cancellationToken).ConfigureAwait(false))
        {
            await target.ClickAsync(Bulgarian, cancellationToken).ConfigureAwait(false);
        }
        else if (await target.IsVisibleAsync(BulgarianEn, cancellationToken).ConfigureAwait(false))
        {
            await target.ClickAsync(BulgarianEn, cancellationToken).ConfigureAwait(false);
        }

        if (await target.IsVisibleAsync(Save, cancellationToken).ConfigureAwait(false))
        {
            await target.ClickAsync(Save, cancellationToken).ConfigureAwait(false);
        }

        await WaitUntilLanguagePromptGoneAsync(target, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Cookie-accept must never run.
    /// </summary>
    /// <returns>Always false.</returns>
    /// <example>
    /// <code>
    /// var click = OverlayRecovery.ShouldClickAcceptCookies();
    /// </code>
    /// </example>
    public static bool ShouldClickAcceptCookies() => false;

    private static async Task WaitUntilLanguagePromptGoneAsync(IOverlayTarget target, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 20; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var save = await target.IsVisibleAsync(Save, cancellationToken).ConfigureAwait(false);
            var bg = await target.IsVisibleAsync(Bulgarian, cancellationToken).ConfigureAwait(false)
                || await target.IsVisibleAsync(BulgarianEn, cancellationToken).ConfigureAwait(false);
            if (!save && !bg)
            {
                return;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }
}
