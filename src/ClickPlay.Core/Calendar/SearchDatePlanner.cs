using ClickPlay.Core.Models;

namespace ClickPlay.Core.Calendar;

/// <summary>
/// Builds the date list and picks weekday vs weekend/holiday windows.
/// </summary>
public static class SearchDatePlanner
{
    /// <summary>
    /// Consecutive dates from start, length daysAhead (inclusive start).
    /// </summary>
    /// <param name="start">First day.</param>
    /// <param name="daysAhead">How many days to include.</param>
    /// <returns>Dates to search.</returns>
    /// <example>
    /// <code>
    /// var days = SearchDatePlanner.Days(new DateOnly(2026, 10, 18), 3);
    /// </code>
    /// </example>
    public static IReadOnlyList<DateOnly> Days(DateOnly start, int daysAhead)
    {
        if (daysAhead < 1)
        {
            return [];
        }

        var list = new DateOnly[daysAhead];
        for (var i = 0; i < daysAhead; i++)
        {
            list[i] = start.AddDays(i);
        }

        return list;
    }

    /// <summary>
    /// Weekday window unless the date is a weekend or Bulgarian holiday.
    /// </summary>
    /// <param name="date">Day.</param>
    /// <param name="weekday">Mon–Fri non-holiday window.</param>
    /// <param name="weekend">Weekend and holiday window.</param>
    /// <returns>Window to apply.</returns>
    /// <example>
    /// <code>
    /// var w = SearchDatePlanner.WindowFor(date, weekday, weekend);
    /// </code>
    /// </example>
    public static TimeWindow WindowFor(DateOnly date, TimeWindow weekday, TimeWindow weekend)
    {
        return BulgarianHolidayCalendar.UsesWeekendWindow(date) ? weekend : weekday;
    }

    /// <summary>
    /// True when the slot start sits inside the window (inclusive).
    /// </summary>
    /// <param name="window">From/to.</param>
    /// <param name="slotStart">Slot start.</param>
    /// <returns>True if the slot is in range.</returns>
    /// <example>
    /// <code>
    /// var ok = SearchDatePlanner.IsInWindow(window, new TimeOnly(18, 0));
    /// </code>
    /// </example>
    public static bool IsInWindow(TimeWindow window, TimeOnly slotStart)
    {
        return slotStart >= window.From && slotStart <= window.To;
    }
}
