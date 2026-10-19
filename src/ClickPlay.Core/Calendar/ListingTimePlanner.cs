namespace ClickPlay.Core.Calendar;

/// <summary>
/// When to shift the listing date/time before reading club cards.
/// </summary>
public static class ListingTimePlanner
{
    /// <summary>After this local time, listing uses tomorrow morning.</summary>
    public static readonly TimeOnly EveningCutoff = new(17, 0);

    /// <summary>Hour to set on the site when listing tomorrow.</summary>
    public static readonly TimeOnly MorningHour = new(10, 0);

    /// <summary>
    /// True when local time is after 17:00.
    /// </summary>
    /// <example>
    /// <code>
    /// var tomorrow = ListingTimePlanner.ShouldUseTomorrowMorning(DateTime.Now);
    /// </code>
    /// </example>
    public static bool ShouldUseTomorrowMorning(DateTime now) =>
        TimeOnly.FromDateTime(now) > EveningCutoff;

    /// <summary>
    /// Listing calendar day: tomorrow after 17:00, otherwise today.
    /// </summary>
    /// <example>
    /// <code>
    /// var day = ListingTimePlanner.ListingDate(DateTime.Now);
    /// </code>
    /// </example>
    public static DateOnly ListingDate(DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        return ShouldUseTomorrowMorning(now) ? today.AddDays(1) : today;
    }
}
