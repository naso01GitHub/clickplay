using ClickPlay.App.ViewModels;
using ClickPlay.Core.Browser;
using ClickPlay.Core.Calendar;
using ClickPlay.Core.Models;
using ClickPlay.Core.Services;
using ClickPlay.Tests.Fakes;

namespace ClickPlay.Tests;

public sealed class AvailabilityLoopTests
{
    [Fact]
    public void OrthodoxEaster_2025_IsApril20()
    {
        Assert.Equal(new DateOnly(2025, 4, 20), BulgarianHolidayCalendar.OrthodoxEaster(2025));
        Assert.True(BulgarianHolidayCalendar.IsPublicHoliday(new DateOnly(2025, 4, 20)));
        Assert.True(BulgarianHolidayCalendar.IsPublicHoliday(new DateOnly(2025, 4, 18)));
        Assert.True(BulgarianHolidayCalendar.IsPublicHoliday(new DateOnly(2025, 4, 21)));
    }

    [Fact]
    public void NewYearAndLiberation_AreHolidays()
    {
        Assert.True(BulgarianHolidayCalendar.IsPublicHoliday(new DateOnly(2026, 1, 1)));
        Assert.True(BulgarianHolidayCalendar.IsPublicHoliday(new DateOnly(2026, 3, 3)));
        Assert.True(BulgarianHolidayCalendar.UsesWeekendWindow(new DateOnly(2026, 3, 3)));
    }

    [Fact]
    public void SaturdayUsesWeekendWindow_TuesdayDoesNotUnlessHoliday()
    {
        Assert.True(BulgarianHolidayCalendar.UsesWeekendWindow(new DateOnly(2026, 10, 17)));
        Assert.False(BulgarianHolidayCalendar.UsesWeekendWindow(new DateOnly(2026, 10, 20)));
    }

    [Fact]
    public void Planner_PicksWeekendWindowOnHoliday()
    {
        var weekday = new TimeWindow(new TimeOnly(18, 0), new TimeOnly(21, 0));
        var weekend = new TimeWindow(new TimeOnly(10, 0), new TimeOnly(20, 0));
        var holiday = new DateOnly(2026, 3, 3);
        var window = SearchDatePlanner.WindowFor(holiday, weekday, weekend);
        Assert.Equal(weekend, window);
        Assert.Equal(3, SearchDatePlanner.Days(new DateOnly(2026, 10, 18), 3).Count);
        Assert.True(SearchDatePlanner.IsInWindow(weekday, new TimeOnly(18, 0)));
        Assert.False(SearchDatePlanner.IsInWindow(weekday, new TimeOnly(21, 30)));
    }

    [Fact]
    public void Slot_LogLineContainsLocationAndTime()
    {
        var slot = new AvailabilitySlot(
            "Amik Sport",
            new DateOnly(2026, 10, 19),
            new TimeOnly(18, 0),
            new TimeOnly(19, 0),
            "Корт 1",
            "data-id=12 | indoor");
        Assert.Equal("Amik Sport | Корт 1 | 19.10.2026 18:00–19:00 | data-id=12 | indoor", slot.ToLogLine());
    }

    [Fact]
    public void FacilitySchedule_CollectsItsfreeFromTo_SkipsClosed_FiltersWindow()
    {
        var day = new DateOnly(2026, 10, 19);
        var window = new TimeWindow(new TimeOnly(10, 0), new TimeOnly(15, 0));
        string[] padel1 =
        [
            "09:00 - 10:00",
            "10:00 - 11:00",
            "11:00 - 12:00",
            "13:00 - 14:00",
            "14:00 - 15:00"
        ];

        var kept = padel1
            .Select(fromTo => BookingSlotParser.TryCreateSlot(
                "BETVAM Padel Club",
                day,
                window,
                "Падел 1",
                "Изкуствена трева | Двоен",
                fromTo,
                "36.00",
                "€",
                "19.10.2026"))
            .Where(s => s is not null)
            .Select(s => s!.ToLogLine())
            .ToList();

        Assert.Equal(4, kept.Count);
        Assert.All(kept, line => Assert.Contains("BETVAM Padel Club", line, StringComparison.Ordinal));
        Assert.Contains(kept, line => line.Contains("10:00–11:00", StringComparison.Ordinal));
        Assert.Contains(kept, line => line.Contains("14:00–15:00", StringComparison.Ordinal));
        Assert.Contains(kept, line => line.Contains("19.10.2026", StringComparison.Ordinal));
        Assert.DoesNotContain(kept, line => line.Contains("09:00–10:00", StringComparison.Ordinal));
        Assert.Null(BookingSlotParser.TryCreateSlot(
            "BETVAM Padel Club",
            day,
            window,
            "Падел 1",
            null,
            "10:00 - 11:00",
            "36.00",
            "€",
            "18.10.2026"));
        Assert.Null(BookingSlotParser.TryCreateSlot(
            "BETVAM Padel Club",
            day,
            window,
            "Падел 1",
            null,
            null,
            null,
            null));
    }

    [Fact]
    public void ClubProfile_ItsfreeSlot_BecomesLogLine()
    {
        var window = new TimeWindow(new TimeOnly(18, 0), new TimeOnly(23, 59));
        var slot = BookingSlotParser.TryCreateSlot(
            "BETVAM Padel Club",
            new DateOnly(2026, 10, 18),
            window,
            "Падел 1",
            "Изкуствена трева | Двоен | Стандартен | Осветено | Закрито съоръжение",
            "23:00 - 00:00",
            "36.00",
            "€",
            "18.10.2026");

        Assert.NotNull(slot);
        Assert.Equal(
            "BETVAM Padel Club | Падел 1 | 18.10.2026 23:00–00:00 | 36.00 € | Изкуствена трева | Двоен | Стандартен | Осветено | Закрито съоръжение",
            slot!.ToLogLine());
    }

    [Fact]
    public void EarliestHourInWindow_PicksEarlierMatchOrNull()
    {
        var window = new TimeWindow(new TimeOnly(18, 0), new TimeOnly(21, 0));
        TimeOnly[] hours =
        [
            new(17, 0),
            new(18, 0),
            new(19, 0),
            new(23, 0)
        ];
        Assert.Equal(new TimeOnly(18, 0), BookingSlotParser.EarliestHourInWindow(window, hours));
        Assert.Null(BookingSlotParser.EarliestHourInWindow(window, [new TimeOnly(23, 0)]));
        Assert.Null(BookingSlotParser.EarliestHourInWindow(window, []));
    }

    [Fact]
    public void Listing_After17_UsesTomorrowAt10()
    {
        var after = new DateTime(2026, 10, 18, 17, 1, 0);
        var before = new DateTime(2026, 10, 18, 17, 0, 0);
        Assert.True(ListingTimePlanner.ShouldUseTomorrowMorning(after));
        Assert.False(ListingTimePlanner.ShouldUseTomorrowMorning(before));
        Assert.Equal(new DateOnly(2026, 10, 19), ListingTimePlanner.ListingDate(after));
        Assert.Equal(new DateOnly(2026, 10, 18), ListingTimePlanner.ListingDate(before));
        Assert.Equal(new TimeOnly(10, 0), ListingTimePlanner.MorningHour);
    }

    [Fact]
    public void ClockDropdown_IgnoresZeroAndClear()
    {
        Assert.True(BookingSlotParser.IsIgnoredClockOption("0.00"));
        Assert.True(BookingSlotParser.IsIgnoredClockOption("00:00"));
        Assert.True(BookingSlotParser.IsIgnoredClockOption("Изчисти"));
        Assert.False(BookingSlotParser.IsIgnoredClockOption("18:00"));
        Assert.Empty(BookingSlotParser.ParseSelectableHours(["0.00", "Изчисти"]));
        Assert.Equal(
            new TimeOnly(18, 0),
            BookingSlotParser.ParseSelectableHours(["0.00", "Изчисти", "18:00"])[0]);
    }

    [Fact]
    public void ClosedCourt_HasNoFromTo_IsNotASlot()
    {
        var window = new TimeWindow(new TimeOnly(18, 0), new TimeOnly(23, 59));
        Assert.Null(BookingSlotParser.TryCreateSlot(
            "BETVAM Padel Club",
            new DateOnly(2026, 10, 18),
            window,
            "Падел 3",
            "Изкуствена трева | Двоен",
            null,
            null,
            null));
    }

    [Fact]
    public async Task Search_FillsResultsAndStopCancelsWait()
    {
        var session = new FakeClickToPlaySession();
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session))
        {
            SearchLoopDelayOverride = TimeSpan.FromSeconds(30)
        };

        await vm.LoadSportsCommand.ExecuteAsync(null);
        vm.SelectedSport = vm.Sports[0];
        await WaitUntil(() => vm.Cities.Count > 0);
        vm.SelectedCity = vm.Cities[0];
        await WaitUntil(() => vm.Locations.Count > 0);
        vm.Locations[0].IsSelected = true;

        var search = vm.SearchCommand.ExecuteAsync(null);
        await WaitUntil(() => vm.Results.Count > 0);

        Assert.Contains(vm.Results, line => line.Contains("Amik Sport", StringComparison.Ordinal));
        Assert.Contains(vm.Results, line => line.Contains("18:00", StringComparison.Ordinal));
        Assert.True(vm.CanStop);
        Assert.False(vm.CanSearch);
        Assert.Equal(1, session.CloseBrowserCount);

        vm.StopCommand.Execute(null);
        await search;

        Assert.False(vm.IsBusy);
        Assert.False(vm.IsSearching);
        Assert.False(vm.CanStop);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (var i = 0; i < 80; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.Fail("Timed out waiting for search cascade.");
    }
}
