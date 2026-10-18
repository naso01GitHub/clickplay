using ClickPlay.Core.Browser;
using ClickPlay.Tests.Fakes;

namespace ClickPlay.Tests;

public sealed class OverlayRecoveryTests
{
    [Fact]
    public async Task SkipsSave_WhenSaveIsNotVisible()
    {
        var target = new FakeOverlayTarget(
            sportsInteractable: false,
            OverlayRecovery.Bulgarian);
        var recovery = new OverlayRecovery();

        await recovery.RecoverAsync(target, CancellationToken.None);

        Assert.Contains(OverlayRecovery.Bulgarian, target.Clicks);
        Assert.DoesNotContain(OverlayRecovery.Save, target.Clicks);
        Assert.DoesNotContain(OverlayRecovery.AcceptCookies, target.Clicks);
    }

    [Fact]
    public async Task NeverClicksAcceptCookies_EvenWhenVisible()
    {
        var target = new FakeOverlayTarget(
            sportsInteractable: false,
            OverlayRecovery.Bulgarian,
            OverlayRecovery.Save,
            OverlayRecovery.AcceptCookies);
        var recovery = new OverlayRecovery();

        await recovery.RecoverAsync(target, CancellationToken.None);

        Assert.Contains(OverlayRecovery.Bulgarian, target.Clicks);
        Assert.Contains(OverlayRecovery.Save, target.Clicks);
        Assert.DoesNotContain(OverlayRecovery.AcceptCookies, target.Clicks);
        Assert.False(OverlayRecovery.ShouldClickAcceptCookies());
    }

    [Fact]
    public async Task DismissesLanguagePrompt_EvenWhenSportsPanelLooksPresent()
    {
        var target = new FakeOverlayTarget(
            sportsInteractable: true,
            OverlayRecovery.Save,
            OverlayRecovery.Bulgarian);
        var recovery = new OverlayRecovery();

        await recovery.RecoverAsync(target, CancellationToken.None);

        Assert.Contains(OverlayRecovery.Bulgarian, target.Clicks);
        Assert.Contains(OverlayRecovery.Save, target.Clicks);
    }

    [Fact]
    public async Task DoesNothing_WhenLanguagePromptIsNotVisible()
    {
        var target = new FakeOverlayTarget(sportsInteractable: true);
        var recovery = new OverlayRecovery();

        await recovery.RecoverAsync(target, CancellationToken.None);

        Assert.Empty(target.Clicks);
    }
}
