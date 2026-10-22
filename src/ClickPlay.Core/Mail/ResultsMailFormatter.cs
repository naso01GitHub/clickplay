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
    /// Subject = Subject field + sport selected in the GUI + number of records in this mail, in parentheses.
    /// Line breaks are removed.
    /// </summary>
    /// <param name="prefix">Subject field text; empty uses <see cref="DefaultSubject"/>.</param>
    /// <param name="sport">Sport name selected in the GUI, e.g. "Падел"; empty is left out.</param>
    /// <param name="recordCount">Number of free-hour records in the mail body; must be at least 1.</param>
    /// <returns>One-line subject.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="recordCount"/> is below 1.</exception>
    /// <example>
    /// <code>
    /// var subject = ResultsMailFormatter.Subject("Free padel", "Падел", 3);
    /// // Free padel Падел (3)
    /// </code>
    /// </example>
    public static string Subject(string? prefix, string? sport, int recordCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(recordCount, 1);
        var head = string.IsNullOrWhiteSpace(prefix) ? DefaultSubject : prefix.Trim();
        var subject = string.IsNullOrWhiteSpace(sport)
            ? $"{head} ({recordCount})"
            : $"{head} {sport.Trim()} ({recordCount})";
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
