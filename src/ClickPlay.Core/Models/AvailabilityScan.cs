namespace ClickPlay.Core.Models;

/// <summary>
/// One location on one day of a scan.
/// </summary>
/// <param name="Venue">Location name, the same as <see cref="AvailabilitySlot.LocationName"/>.</param>
/// <param name="Day">Schedule day, the same as <see cref="AvailabilitySlot.Date"/>.</param>
/// <example>
/// <code>
/// var key = new VenueDay("Supersport", new DateOnly(2026, 10, 23));
/// </code>
/// </example>
public readonly record struct VenueDay(string Venue, DateOnly Day)
{
    /// <summary>
    /// The location/day a slot belongs to.
    /// </summary>
    /// <param name="slot">A free slot.</param>
    /// <returns>Its location name and date.</returns>
    /// <example>
    /// <code>
    /// var key = VenueDay.Of(slot);
    /// </code>
    /// </example>
    public static VenueDay Of(AvailabilitySlot slot)
    {
        ArgumentNullException.ThrowIfNull(slot);
        return new VenueDay(slot.LocationName, slot.Date);
    }
}

/// <summary>
/// Result of one scan: the free slots found, and the location/days that could not be read
/// (site busy, timeout, no schedule). For a skipped location/day nothing is known, so the
/// previous scan's records for it are kept.
/// </summary>
/// <param name="Slots">Free slots inside the GUI time windows.</param>
/// <param name="Skipped">Location/days that were not read in this scan.</param>
/// <example>
/// <code>
/// var scan = new AvailabilityScan(slots, [new VenueDay("Supersport", day)]);
/// </code>
/// </example>
public sealed record AvailabilityScan(IReadOnlyList<AvailabilitySlot> Slots, IReadOnlyCollection<VenueDay> Skipped)
{
    /// <summary>
    /// A scan where every location/day was read.
    /// </summary>
    /// <param name="slots">Free slots found.</param>
    /// <returns>A scan with no skipped location/days.</returns>
    /// <example>
    /// <code>
    /// var scan = AvailabilityScan.Complete(slots);
    /// </code>
    /// </example>
    public static AvailabilityScan Complete(IReadOnlyList<AvailabilitySlot> slots) => new(slots, []);
}
