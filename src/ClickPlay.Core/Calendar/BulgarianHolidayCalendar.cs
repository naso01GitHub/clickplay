namespace ClickPlay.Core.Calendar;

/// <summary>
/// Bulgarian public holidays (fixed dates plus Orthodox Easter-related days).
/// </summary>
public static class BulgarianHolidayCalendar
{
    /// <summary>
    /// True when the date is a Bulgarian public holiday (weekend or weekday).
    /// </summary>
    /// <param name="date">Calendar date.</param>
    /// <returns>True for a public holiday.</returns>
    /// <example>
    /// <code>
    /// var nye = BulgarianHolidayCalendar.IsPublicHoliday(new DateOnly(2026, 1, 1));
    /// </code>
    /// </example>
    public static bool IsPublicHoliday(DateOnly date)
    {
        if (IsFixedHoliday(date.Month, date.Day))
        {
            return true;
        }

        var easter = OrthodoxEaster(date.Year);
        return date == easter.AddDays(-2)
            || date == easter.AddDays(-1)
            || date == easter
            || date == easter.AddDays(1);
    }

    /// <summary>
    /// True when the date uses the weekend/holiday time window.
    /// </summary>
    /// <param name="date">Calendar date.</param>
    /// <returns>True for Saturday, Sunday, or a public holiday.</returns>
    /// <example>
    /// <code>
    /// var weekend = BulgarianHolidayCalendar.UsesWeekendWindow(new DateOnly(2026, 1, 3));
    /// </code>
    /// </example>
    public static bool UsesWeekendWindow(DateOnly date)
    {
        return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            || IsPublicHoliday(date);
    }

    /// <summary>
    /// Orthodox Easter Sunday (revised Julian / civil date used in Bulgaria).
    /// </summary>
    /// <param name="year">Year.</param>
    /// <returns>Easter Sunday.</returns>
    /// <example>
    /// <code>
    /// var easter = BulgarianHolidayCalendar.OrthodoxEaster(2025);
    /// </code>
    /// </example>
    public static DateOnly OrthodoxEaster(int year)
    {
        var a = year % 19;
        var b = year % 7;
        var c = year % 4;
        var d = (19 * a + 16) % 30;
        var e = (2 * c + 4 * b + 6 * d) % 7;
        var f = d + e;
        var march21 = new DateTime(year, 3, 21);
        return DateOnly.FromDateTime(march21.AddDays(f + 13));
    }

    private static bool IsFixedHoliday(int month, int day) =>
        (month, day) is
            (1, 1) or
            (3, 3) or
            (5, 1) or
            (5, 6) or
            (5, 24) or
            (9, 6) or
            (9, 22) or
            (12, 24) or
            (12, 25) or
            (12, 26);
}
