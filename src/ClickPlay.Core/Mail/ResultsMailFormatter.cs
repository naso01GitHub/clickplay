using ClickPlay.Core.Models;

namespace ClickPlay.Core.Mail;

/// <summary>
/// Builds the subject and body of the results mail sent after a scan with new free hours.
/// </summary>
public static class ResultsMailFormatter
{
    /// <summary>Subject prefix used when the Subject field is empty.</summary>
    public const string DefaultSubject = "ClickPlay";

    /// <summary>
    /// Subject = Subject field + location of the first new slot + its date and time.
    /// Adds <c>(+N more)</c> when the mail has more than one slot. Line breaks are removed.
    /// </summary>
    /// <param name="prefix">Subject field text; empty uses <see cref="DefaultSubject"/>.</param>
    /// <param name="newSlots">New slots of this scan, in Results order. Must not be empty.</param>
    /// <returns>One-line subject.</returns>
    /// <example>
    /// <code>
    /// var subject = ResultsMailFormatter.Subject("Free padel", slots);
    /// // Free padel Supersport 23.10.2026 18:00–19:00 (+4 more)
    /// </code>
    /// </example>
    public static string Subject(string? prefix, IReadOnlyList<AvailabilitySlot> newSlots)
    {
        ArgumentNullException.ThrowIfNull(newSlots);
        if (newSlots.Count == 0)
        {
            throw new ArgumentException("At least one slot is required.", nameof(newSlots));
        }

        var first = newSlots[0];
        var date = string.IsNullOrWhiteSpace(first.SiteDateLabel)
            ? first.Date.ToString("dd.MM.yyyy")
            : first.SiteDateLabel.Trim();
        var time = first.To is { } end ? $"{first.From:HH:mm}–{end:HH:mm}" : $"{first.From:HH:mm}";
        var head = string.IsNullOrWhiteSpace(prefix) ? DefaultSubject : prefix.Trim();
        var subject = $"{head} {first.LocationName} {date} {time}";
        if (newSlots.Count > 1)
        {
            subject += $" (+{newSlots.Count - 1} more)";
        }

        return OneLine(subject);
    }

    /// <summary>
    /// Body = scan stamp, then one Results line per new slot (same text as the Results window and results log),
    /// with an empty line between records.
    /// </summary>
    /// <param name="scanStamp">Scan header, e.g. <c>21.10.2026 21:05:17</c>.</param>
    /// <param name="newSlots">New slots of this scan.</param>
    /// <returns>Plain-text body.</returns>
    /// <example>
    /// <code>
    /// var body = ResultsMailFormatter.Body("21.10.2026 21:05:17", slots);
    /// </code>
    /// </example>
    public static string Body(string scanStamp, IReadOnlyList<AvailabilitySlot> newSlots)
    {
        ArgumentNullException.ThrowIfNull(newSlots);
        var records = newSlots.Select(slot => slot.ToLogLine());
        var separator = Environment.NewLine + Environment.NewLine;
        return scanStamp + separator + string.Join(separator, records) + Environment.NewLine;
    }

    /// <summary>
    /// Joins the text into one line: removes CR, LF, and tabs (header-injection guard) and collapses the gaps to single spaces.
    /// </summary>
    /// <param name="text">Text that may contain line breaks.</param>
    /// <returns>Single-line text.</returns>
    /// <example>
    /// <code>
    /// var line = OneLine("Free\r\nBcc: x@evil.com"); // "Free Bcc: x@evil.com"
    /// </code>
    /// </example>
    private static string OneLine(string text) =>
        string.Join(' ', text.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
