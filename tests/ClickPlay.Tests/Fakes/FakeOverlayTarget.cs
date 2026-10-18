using ClickPlay.Core.Browser;

namespace ClickPlay.Tests.Fakes;

public sealed class FakeOverlayTarget : IOverlayTarget
{
    private readonly HashSet<string> _visible;
    private readonly bool _sportsInteractable;

    public FakeOverlayTarget(bool sportsInteractable, params string[] visible)
    {
        _sportsInteractable = sportsInteractable;
        _visible = new HashSet<string>(visible, StringComparer.Ordinal);
    }

    public List<string> Clicks { get; } = [];

    public Task<bool> IsSportsPanelInteractableAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(_sportsInteractable);
    }

    public Task<bool> IsVisibleAsync(string name, CancellationToken cancellationToken)
    {
        return Task.FromResult(_visible.Contains(name));
    }

    public Task ClickAsync(string name, CancellationToken cancellationToken)
    {
        Clicks.Add(name);
        _visible.Remove(name);
        if (name == OverlayRecovery.Save)
        {
            _visible.Remove(OverlayRecovery.Bulgarian);
            _visible.Remove(OverlayRecovery.BulgarianEn);
        }

        return Task.CompletedTask;
    }
}
