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
/// <param name="SiteDateLabel"><c>data-df</c> from the free slot, e.g. 19.10.2026.</param>
public sealed record AvailabilitySlot(
    string LocationName,
    DateOnly Date,
    TimeOnly From,
    TimeOnly? To,
    string? CourtName = null,
    string? FacilityDetails = null,
    string? SiteDateLabel = null)
{
    /// <summary>
    /// Line for the Results window: club, court, site date, time, extras.
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
        var dateLabel = string.IsNullOrWhiteSpace(SiteDateLabel)
            ? Date.ToString("dd.MM.yyyy")
            : SiteDateLabel.Trim();
        var parts = new List<string> { LocationName };
        if (court is not null)
        {
            parts.Add(court);
        }

        parts.Add($"{dateLabel} {range}");
        if (extra is not null)
        {
            parts.Add(extra);
        }

        return string.Join(" | ", parts);
    }
}
