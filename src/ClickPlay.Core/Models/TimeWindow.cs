namespace ClickPlay.Core.Models;

/// <summary>
/// Inclusive search window on a calendar day.
/// </summary>
public sealed record TimeWindow(TimeOnly From, TimeOnly To)
{
    public bool IsValid => From < To;
}
