using ClickPlay.App.ViewModels;
using ClickPlay.Core.Services;
using ClickPlay.Tests.Fakes;

namespace ClickPlay.Tests;

public sealed class StatusClassifierTests
{
    [Theory]
    [InlineData("Search failed (Checking Supersport, 23.10.2026): TimeoutException: Timeout", StatusKind.Error)]
    [InlineData("Could not load sports: clicktoplay.io is busy (its database reports Too many connections).", StatusKind.Error)]
    [InlineData("Mail failed: AuthenticationException: 535", StatusKind.Error)]
    [InlineData("Email: SMTP port must be a number from 1 to 65535 (usually 465 or 587).", StatusKind.Error)]
    [InlineData("Days ahead must be a positive integer.", StatusKind.Error)]
    [InlineData("Skipped Supersport, 23.10.2026: no court schedule", StatusKind.Warning)]
    [InlineData("Mail skipped: no To address.", StatusKind.Warning)]
    [InlineData("Search stopped.", StatusKind.Warning)]
    [InlineData("Loading cancelled.", StatusKind.Warning)]
    [InlineData("Select a sport, a city, and at least one location.", StatusKind.Warning)]
    [InlineData("Found 3 new free hour(s). Closing browser…", StatusKind.Success)]
    [InlineData("Mailed 3 new free hour(s) to 2 address(es).", StatusKind.Success)]
    [InlineData("12 sports loaded. Select a sport.", StatusKind.Success)]
    [InlineData("Checking Supersport, 23.10.2026…", StatusKind.Working)]
    [InlineData("Waiting 5 minute(s) before the next search…", StatusKind.Working)]
    [InlineData("Select a city.", StatusKind.Info)]
    [InlineData("No new free hours. Closing browser…", StatusKind.Working)]
    [InlineData("", StatusKind.Info)]
    public void Classify_PicksBannerStyle(string message, StatusKind expected)
    {
        Assert.Equal(expected, StatusClassifier.Classify(message));
    }

    [Fact]
    public void ViewModel_StatusChange_UpdatesBannerProperties()
    {
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(new FakeClickToPlaySession()));
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.StatusMessage = "Search failed (x): TimeoutException";

        Assert.True(vm.StatusIsError);
        Assert.Equal("✖", vm.StatusIcon);
        Assert.Contains(nameof(MainViewModel.StatusIsError), changed);

        vm.StatusMessage = "Mailed 1 new free hour(s) to 1 address(es).";
        Assert.False(vm.StatusIsError);
        Assert.True(vm.StatusIsSuccess);
        Assert.Equal("✔", vm.StatusIcon);
    }
}
