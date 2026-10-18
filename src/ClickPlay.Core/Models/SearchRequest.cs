namespace ClickPlay.Core.Models;

/// <summary>
/// One availability pass over selected clubs and a date range.
/// </summary>
public sealed record SearchRequest(
    Sport Sport,
    City City,
    IReadOnlyList<Venue> Locations,
    DateOnly StartDate,
    int DaysAhead,
    TimeWindow WeekdayWindow,
    TimeWindow WeekendWindow);
