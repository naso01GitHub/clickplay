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
        Assert.DoesNotContain("36.00", slot!.ToLogLine(), StringComparison.Ordinal);
        Assert.DoesNotContain("€", slot.ToLogLine(), StringComparison.Ordinal);
        Assert.Equal(
            "BETVAM Padel Club | Падел 1 | 18.10.2026 23:00–00:00 | Изкуствена трева | Двоен | Стандартен | Осветено | Закрито съоръжение",
            slot.ToLogLine());
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
    public void WaitStatus_UsesMinutesThenSeconds()
    {
        Assert.Equal(
            "Waiting 5 minute(s) before the next search…",
            MainViewModel.FormatWaitStatus(TimeSpan.FromMinutes(5)));
        Assert.Equal(
            "Waiting 1 minute(s) before the next search…",
            MainViewModel.FormatWaitStatus(TimeSpan.FromMinutes(1)));
        Assert.Equal(
            "Waiting 45 second(s) before the next search…",
            MainViewModel.FormatWaitStatus(TimeSpan.FromSeconds(45)));
    }

    [Fact]
    public void SearchFailure_IncludesStepAndExceptionType()
    {
        var line = MainViewModel.FormatSearchFailure(
            "Checking Amik Sport, 19.10.2026…",
            new InvalidOperationException("clicktoplay.io’s database is full"));
        Assert.StartsWith("Search failed (Checking Amik Sport, 19.10.2026)", line);
        Assert.Contains("InvalidOperationException", line, StringComparison.Ordinal);
        Assert.Contains("database is full", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Search_FillsResultsAndStopCancelsWait()
    {
        var session = new FakeClickToPlaySession();
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session))
        {
            SearchLoopDelayOverride = TimeSpan.FromSeconds(30),
            ScanClock = () => new DateTime(2026, 10, 20, 21, 0, 0),
            ResultsLogPath = TempLogPath()
        };

        await vm.LoadSportsCommand.ExecuteAsync(null);
        vm.SelectedSport = vm.Sports[0];
        await WaitUntil(() => vm.Cities.Count > 0);
        vm.SelectedCity = vm.Cities[0];
        await WaitUntil(() => vm.Locations.Count > 0);
        vm.Locations[0].IsSelected = true;

        var search = vm.SearchCommand.ExecuteAsync(null);
        await WaitUntil(() => vm.Results.Count > 0);

        Assert.Equal("20.10.2026 21:00:00", vm.Results[0]);
        Assert.Contains(vm.Results, line => line.Contains("Amik Sport", StringComparison.Ordinal));
        Assert.Contains(vm.Results, line => line.Contains("18:00", StringComparison.Ordinal));
        Assert.Equal(string.Empty, vm.Results[^1]);
        Assert.Contains("Amik Sport", File.ReadAllText(vm.ResultsLogPath), StringComparison.Ordinal);
        Assert.True(vm.CanStop);
        Assert.False(vm.CanSearch);
        Assert.Equal(4, session.CloseBrowserCount);

        vm.StopCommand.Execute(null);
        await search;

        Assert.Equal(5, session.CloseBrowserCount);
        Assert.False(vm.IsBusy);
        Assert.False(vm.IsSearching);
        Assert.False(vm.CanStop);
    }

    [Fact]
    public async Task Search_RereadsDateDaysAndWindowsEachCycle()
    {
        var session = new FakeClickToPlaySession();
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session))
        {
            SearchLoopDelayOverride = TimeSpan.FromMilliseconds(80),
            ResultsLogPath = TempLogPath()
        };

        await vm.LoadSportsCommand.ExecuteAsync(null);
        vm.SelectedSport = vm.Sports[0];
        await WaitUntil(() => vm.Cities.Count > 0);
        vm.SelectedCity = vm.Cities[0];
        await WaitUntil(() => vm.Locations.Count > 0);
        vm.Locations[0].IsSelected = true;

        var search = vm.SearchCommand.ExecuteAsync(null);
        await WaitUntil(() => session.SearchCallCount >= 1);

        var nextDate = DateTime.Today.AddDays(2);
        vm.DaysAheadText = "3";
        vm.SelectedDate = nextDate;
        vm.WeekdayFromHour = 17;
        vm.WeekdayFromMinute = 0;
        vm.WeekdayToHour = 20;
        vm.WeekdayToMinute = 0;

        await WaitUntil(() => session.SearchCallCount >= 2);

        Assert.Equal(3, session.LastSearchRequest!.DaysAhead);
        Assert.Equal(DateOnly.FromDateTime(nextDate), session.LastSearchRequest.StartDate);
        Assert.Equal(new TimeOnly(17, 0), session.LastSearchRequest.WeekdayWindow.From);
        Assert.Equal(new TimeOnly(20, 0), session.LastSearchRequest.WeekdayWindow.To);

        vm.StopCommand.Execute(null);
        await search;
    }

    [Fact]
    public async Task Search_ShowsOnlyNewSlotsOnLaterCycles()
    {
        var first = new AvailabilitySlot(
            "Amik Sport",
            new DateOnly(2026, 10, 19),
            new TimeOnly(18, 0),
            new TimeOnly(19, 0));
        var added = new AvailabilitySlot(
            "Amik Sport",
            new DateOnly(2026, 10, 20),
            new TimeOnly(19, 0),
            new TimeOnly(20, 0));
        var session = new FakeClickToPlaySession
        {
            AvailabilityForCall = call => call == 1 ? [first] : [first, added]
        };
        var stamp = 0;
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session))
        {
            SearchLoopDelayOverride = TimeSpan.FromMilliseconds(80),
            ScanClock = () => new DateTime(2026, 10, 20, 21, 0, 0).AddMinutes(stamp++),
            ResultsLogPath = TempLogPath()
        };

        await vm.LoadSportsCommand.ExecuteAsync(null);
        vm.SelectedSport = vm.Sports[0];
        await WaitUntil(() => vm.Cities.Count > 0);
        vm.SelectedCity = vm.Cities[0];
        await WaitUntil(() => vm.Locations.Count > 0);
        vm.Locations[0].IsSelected = true;

        var search = vm.SearchCommand.ExecuteAsync(null);
        await WaitUntil(() => session.SearchCallCount >= 1 && vm.Results.Count >= 3);
        Assert.Equal("20.10.2026 21:00:00", vm.Results[0]);
        Assert.Contains(vm.Results, line => line.Contains("18:00", StringComparison.Ordinal));

        await WaitUntil(() => session.SearchCallCount >= 2
            && vm.Results.Count(line => line.StartsWith("20.10.2026", StringComparison.Ordinal)) >= 2
            && vm.Results.Any(line => line.Contains("19:00", StringComparison.Ordinal)));
        Assert.Contains(vm.Results, line => line.Contains("18:00", StringComparison.Ordinal));
        Assert.Contains(vm.Results, line => line.Contains("19:00", StringComparison.Ordinal));
        Assert.Single(vm.Results, line => line.Contains("18:00", StringComparison.Ordinal));
        Assert.Equal(string.Empty, vm.Results[^1]);

        vm.StopCommand.Execute(null);
        await search;
    }

    [Fact]
    public async Task Search_AfterCrash_ClearsLogAndSearchesAgain()
    {
        var logPath = TempLogPath();
        var slot = new AvailabilitySlot(
            "Amik Sport",
            new DateOnly(2026, 10, 19),
            new TimeOnly(18, 0),
            new TimeOnly(19, 0));
        var session = new FakeClickToPlaySession
        {
            Availability = [slot],
            ThrowOnSearchCallNumber = 2,
            SearchException = new InvalidOperationException("site busy")
        };
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session))
        {
            SearchLoopDelayOverride = TimeSpan.FromMilliseconds(80),
            ResultsLogPath = logPath
        };

        await vm.LoadSportsCommand.ExecuteAsync(null);
        vm.SelectedSport = vm.Sports[0];
        await WaitUntil(() => vm.Cities.Count > 0);
        vm.SelectedCity = vm.Cities[0];
        await WaitUntil(() => vm.Locations.Count > 0);
        vm.Locations[0].IsSelected = true;

        var search = vm.SearchCommand.ExecuteAsync(null);
        await WaitUntil(() => session.SearchCallCount >= 3 && vm.Results.Any(line => line.Contains("Amik Sport", StringComparison.Ordinal)));
        Assert.True(vm.IsSearching);
        Assert.Contains(vm.Results, line => line.Contains("Search failed", StringComparison.Ordinal));
        Assert.Contains(vm.Results, line => line.Contains("18:00", StringComparison.Ordinal));
        var log = File.ReadAllText(logPath);
        Assert.Contains("Amik Sport", log, StringComparison.Ordinal);
        Assert.Contains("Search failed", log, StringComparison.Ordinal);
        Assert.True(log.Split("Amik Sport", StringSplitOptions.None).Length - 1 >= 2);

        vm.StopCommand.Execute(null);
        await search;
    }

    [Fact]
    public async Task Search_CalendarInPast_MovesToToday_ScanStartsToday()
    {
        var session = new FakeClickToPlaySession();
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session))
        {
            SearchLoopDelayOverride = TimeSpan.FromSeconds(30),
            CalendarClock = () => new DateTime(2026, 10, 22, 9, 0, 0),
            ResultsLogPath = TempLogPath(),
            SelectedDate = new DateTime(2026, 10, 19),
            DaysAheadText = "3"
        };

        var search = await StartSearchAsync(vm);
        await WaitUntil(() => session.SearchCallCount >= 1);

        Assert.Equal(new DateTime(2026, 10, 22), vm.SelectedDate);
        Assert.Equal(new DateOnly(2026, 10, 22), session.LastSearchRequest!.StartDate);
        Assert.Equal(3, session.LastSearchRequest.DaysAhead);

        vm.StopCommand.Execute(null);
        await search;
    }

    [Fact]
    public async Task Search_CalendarInFuture_IsKept()
    {
        var session = new FakeClickToPlaySession();
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session))
        {
            SearchLoopDelayOverride = TimeSpan.FromSeconds(30),
            CalendarClock = () => new DateTime(2026, 10, 22, 9, 0, 0),
            ResultsLogPath = TempLogPath(),
            SelectedDate = new DateTime(2026, 10, 25)
        };

        var search = await StartSearchAsync(vm);
        await WaitUntil(() => session.SearchCallCount >= 1);

        Assert.Equal(new DateTime(2026, 10, 25), vm.SelectedDate);
        Assert.Equal(new DateOnly(2026, 10, 25), session.LastSearchRequest!.StartDate);

        vm.StopCommand.Execute(null);
        await search;
    }

    [Fact]
    public async Task Search_PastMidnight_NextCycleStartsOnNewDay()
    {
        var session = new FakeClickToPlaySession();
        var now = new DateTime(2026, 10, 22, 23, 59, 0);
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session))
        {
            SearchLoopDelayOverride = TimeSpan.FromMilliseconds(80),
            CalendarClock = () => now,
            ResultsLogPath = TempLogPath(),
            SelectedDate = new DateTime(2026, 10, 22)
        };

        var search = await StartSearchAsync(vm);
        await WaitUntil(() => session.SearchCallCount >= 1);
        Assert.Equal(new DateOnly(2026, 10, 22), session.LastSearchRequest!.StartDate);

        now = new DateTime(2026, 10, 23, 0, 1, 0);
        await WaitUntil(() => session.LastSearchRequest!.StartDate == new DateOnly(2026, 10, 23));
        Assert.Equal(new DateTime(2026, 10, 23), vm.SelectedDate);

        vm.StopCommand.Execute(null);
        await search;
    }

    private static async Task<Task> StartSearchAsync(MainViewModel vm)
    {
        await vm.LoadSportsCommand.ExecuteAsync(null);
        vm.SelectedSport = vm.Sports[0];
        await WaitUntil(() => vm.Cities.Count > 0);
        vm.SelectedCity = vm.Cities[0];
        await WaitUntil(() => vm.Locations.Count > 0);
        vm.Locations[0].IsSelected = true;
        return vm.SearchCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task Search_RereadsLocationCheckboxesEachCycle()
    {
        var session = new FakeClickToPlaySession();
        var vm = new MainViewModel(new PlaywrightClickToPlayCatalog(session))
        {
            SearchLoopDelayOverride = TimeSpan.FromMilliseconds(80),
            ResultsLogPath = TempLogPath()
        };

        await vm.LoadSportsCommand.ExecuteAsync(null);
        vm.SelectedSport = vm.Sports[0];
        await WaitUntil(() => vm.Cities.Count > 0);
        vm.SelectedCity = vm.Cities[0];
        await WaitUntil(() => vm.Locations.Count > 1);
        vm.Locations[0].IsSelected = true;
        vm.Locations[1].IsSelected = false;

        var search = vm.SearchCommand.ExecuteAsync(null);
        await WaitUntil(() => session.SearchCallCount >= 1);
        Assert.Single(session.LastSearchRequest!.Locations);

        vm.Locations[1].IsSelected = true;
        await WaitUntil(() => session.SearchCallCount >= 2
            && session.LastSearchRequest!.Locations.Count == 2);

        vm.StopCommand.Execute(null);
        await search;
    }

    private static string TempLogPath() =>
        Path.Combine(Path.GetTempPath(), $"clickplay-results-{Guid.NewGuid():N}.txt");

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
