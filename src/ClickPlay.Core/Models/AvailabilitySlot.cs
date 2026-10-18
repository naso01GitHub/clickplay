namespace ClickPlay.Core.Models;

/// <summary>
/// A reservable hour on one court or field at a location.
/// </summary>
/// <param name="LocationName">Club name.</param>
/// <param name="Date">Day of the slot.</param>
/// <param name="From">Slot start.</param>
/// <param name="To">Slot end when the site shows a range.</param>
/// <param name="CourtName">Label of the <c>facility active</c> court or field.</param>
/// <param name="FacilityDetails">Other text and data attributes from that court block.</param>
public sealed record AvailabilitySlot(
    string LocationName,
    DateOnly Date,
    TimeOnly From,
    TimeOnly? To,
    string? CourtName = null,
    string? FacilityDetails = null)
{
    /// <summary>
    /// Line for the Results window: club, court, time, and extra court details.
    /// </summary>
    /// <example>
    /// <code>
    /// var line = slot.ToLogLine();
    /// </code>
    /// </example>
    public string ToLogLine()
    {
        var range = To is { } end ? $"{From:HH:mm}–{end:HH:mm}" : $"{From:HH:mm}";
        var court = string.IsNullOrWhiteSpace(CourtName) ? null : CourtName.Trim();
        var extra = string.IsNullOrWhiteSpace(FacilityDetails) ? null : FacilityDetails.Trim();
        var parts = new List<string> { LocationName };
        if (court is not null)
        {
            parts.Add(court);
        }

        parts.Add($"{Date:yyyy-MM-dd} {range}");
        if (extra is not null)
        {
            parts.Add(extra);
        }

        return string.Join(" | ", parts);
    }
}
