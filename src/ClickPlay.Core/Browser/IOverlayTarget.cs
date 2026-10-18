namespace ClickPlay.Core.Browser;

/// <summary>
/// Overlay actions used by recovery. Tests can fake this; Playwright implements it.
/// </summary>
public interface IOverlayTarget
{
    Task<bool> IsSportsPanelInteractableAsync(CancellationToken cancellationToken);

    Task<bool> IsVisibleAsync(string name, CancellationToken cancellationToken);

    Task ClickAsync(string name, CancellationToken cancellationToken);
}
