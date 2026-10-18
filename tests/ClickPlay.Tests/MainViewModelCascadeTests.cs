using ClickPlay.App.ViewModels;
using ClickPlay.Core.Services;
using ClickPlay.Tests.Fakes;

namespace ClickPlay.Tests;

public sealed class MainViewModelCascadeTests
{
    [Fact]
    public async Task LoadSports_StartsSessionThenFillsSports()
    {
        var session = new FakeClickToPlaySession();
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session));

        await vm.LoadSportsCommand.ExecuteAsync(null);

        Assert.NotNull(session.LastStartOptions);
        Assert.False(session.LastStartOptions!.Headless);
        Assert.Equal(2, vm.Sports.Count);
        Assert.False(vm.CanSearch);
    }

    [Fact]
    public async Task SelectingSport_LoadsCitiesAndClearsLocations()
    {
        var session = new FakeClickToPlaySession();
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session));
        await vm.LoadSportsCommand.ExecuteAsync(null);

        vm.SelectedSport = vm.Sports[0];
        await WaitUntil(() => vm.Cities.Count > 0);

        Assert.Equal("Падел", session.LastSport?.Name);
        Assert.Equal(2, vm.Cities.Count);
        Assert.Empty(vm.Locations);
        Assert.False(vm.CanSearch);
    }

    [Fact]
    public async Task SelectingCity_LoadsLocations_SearchDisabledUntilChecked()
    {
        var session = new FakeClickToPlaySession();
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session));
        await vm.LoadSportsCommand.ExecuteAsync(null);
        vm.SelectedSport = vm.Sports[0];
        await WaitUntil(() => vm.Cities.Count > 0);

        vm.SelectedCity = vm.Cities[0];
        await WaitUntil(() => vm.Locations.Count > 0);

        Assert.Equal("Sofia", session.LastCity?.Name);
        Assert.Equal(2, vm.Locations.Count);
        Assert.False(vm.CanSearch);

        vm.Locations[0].IsSelected = true;
        Assert.True(vm.CanSearch);
    }

    [Fact]
    public async Task SelectAllLocations_EnablesSearch()
    {
        var session = new FakeClickToPlaySession();
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session));
        await vm.LoadSportsCommand.ExecuteAsync(null);
        vm.SelectedSport = vm.Sports[0];
        await WaitUntil(() => vm.Cities.Count > 0);
        vm.SelectedCity = vm.Cities[0];
        await WaitUntil(() => vm.Locations.Count > 0);

        vm.SelectAllLocationsCommand.Execute(null);

        Assert.True(vm.Locations.All(l => l.IsSelected));
        Assert.True(vm.CanSearch);
    }

    [Fact]
    public void RepeatText_KeepsDigitsAndCapsAtSixty()
    {
        var session = new FakeClickToPlaySession();
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session));

        vm.RepeatText = "12a";
        Assert.Equal("12", vm.RepeatText);

        vm.RepeatText = "61";
        Assert.Equal("60", vm.RepeatText);
    }

    [Fact]
    public void SelectedDate_DefaultsToToday()
    {
        var session = new FakeClickToPlaySession();
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session));

        Assert.Equal(DateTime.Today, vm.SelectedDate?.Date);
        Assert.Equal(DateTime.Today, vm.CalendarDisplayDate.Date);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 50; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.Fail("Timed out waiting for catalog cascade.");
    }
}
