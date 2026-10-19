using System.Globalization;
using ClickPlay.Core.Calendar;
using ClickPlay.Core.Models;

namespace ClickPlay.Core.Browser;

/// <summary>
/// Reads free hours from a club profile <c>facility active</c> block.
/// </summary>
public static class BookingSlotParser
{
    /// <summary>
    /// Parses <c>data-from-to</c> such as <c>23:00 - 00:00</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// BookingSlotParser.TryParseFromTo("23:00 - 00:00", out var from, out var to);
    /// </code>
    /// </example>
    public static bool TryParseFromTo(string? dataFromTo, out TimeOnly from, out TimeOnly? to)
    {
        from = default;
        to = null;
        if (string.IsNullOrWhiteSpace(dataFromTo))
        {
            return false;
        }

        var matches = System.Text.RegularExpressions.Regex.Matches(dataFromTo, @"\b([01]?\d|2[0-3]):([0-5]\d)\b");
        if (matches.Count == 0 || !TimeOnly.TryParse(matches[0].Value, out from))
        {
            return false;
        }

        if (matches.Count > 1 && TimeOnly.TryParse(matches[1].Value, out var parsedTo))
        {
            to = parsedTo;
        }

        return true;
    }

    /// <summary>
    /// Parses site <c>data-df</c> values such as <c>19.10.2026</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// BookingSlotParser.TryParseSiteDate("19.10.2026", out var date);
    /// </code>
    /// </example>
    public static bool TryParseSiteDate(string? dataDf, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(dataDf))
        {
            return false;
        }

        var text = dataDf.Trim();
        string[] formats = ["dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy"];
        return DateOnly.TryParseExact(
            text,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    /// <summary>
    /// Builds the extra log fragment from court extras and slot price.
    /// </summary>
    /// <example>
    /// <code>
    /// var extra = BookingSlotParser.FormatDetails("Изкуствена трева | Двоен", "36.00", "€");
    /// </code>
    /// </example>
    public static string FormatDetails(string? extras, string? price, string? currency)
    {
        var paid = string.Join(" ", new[] { price, currency }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        var bits = new[] { paid, extras?.ReplaceLineEndings(" ").Trim() }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => System.Text.RegularExpressions.Regex.Replace(s!, @"\s+", " ").Trim());
        return string.Join(" | ", bits);
    }

    /// <summary>
    /// Maps one free cell into an <see cref="AvailabilitySlot"/> when the start is inside the window.
    /// </summary>
    /// <example>
    /// <code>
    /// var slot = BookingSlotParser.TryCreateSlot("Club", day, window, "Падел 1", extras, "23:00 - 00:00", "36.00", "€");
    /// </code>
    /// </example>
    public static AvailabilitySlot? TryCreateSlot(
        string locationName,
        DateOnly day,
        TimeWindow window,
        string? courtName,
        string? extras,
        string? dataFromTo,
        string? price,
        string? currency,
        string? dataDf = null)
    {
        if (!TryParseFromTo(dataFromTo, out var from, out var to))
        {
            return null;
        }

        if (!SearchDatePlanner.IsInWindow(window, from))
        {
            return null;
        }

        string? siteDateLabel = null;
        if (!string.IsNullOrWhiteSpace(dataDf))
        {
            if (!TryParseSiteDate(dataDf, out var siteDate) || siteDate != day)
            {
                return null;
            }

            siteDateLabel = dataDf.Trim();
        }

        var details = FormatDetails(extras, price, currency);
        return new AvailabilitySlot(
            locationName,
            day,
            from,
            to,
            string.IsNullOrWhiteSpace(courtName) ? null : courtName.Trim(),
            string.IsNullOrWhiteSpace(details) ? null : details,
            siteDateLabel);
    }

    /// <summary>
    /// True for empty clock rows, midnight placeholders (<c>0.00</c> / <c>00:00</c>), and <c>Изчисти</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// var skip = BookingSlotParser.IsIgnoredClockOption("0.00");
    /// </code>
    /// </example>
    public static bool IsIgnoredClockOption(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var text = raw.Trim();
        if (text.Contains("Изчисти", StringComparison.OrdinalIgnoreCase)
            || text.Equals("Clear", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var compact = text.Replace(" ", "", StringComparison.Ordinal);
        return compact is "0.00" or "00.00" or "0,00" or "00,00" or "0:00" or "00:00";
    }

    /// <summary>
    /// Hours from the clock dropdown, skipping <c>0.00</c> and <c>Изчисти</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// var hours = BookingSlotParser.ParseSelectableHours(labels);
    /// </code>
    /// </example>
    public static IReadOnlyList<TimeOnly> ParseSelectableHours(IEnumerable<string?> labels)
    {
        var hours = new List<TimeOnly>();
        foreach (var label in labels)
        {
            if (IsIgnoredClockOption(label))
            {
                continue;
            }

            if (TryParseFromTo(label, out var hour, out _))
            {
                hours.Add(hour);
            }
        }

        return hours;
    }

    /// <summary>
    /// Earliest site hour that sits inside the GUI weekday or weekend window.
    /// </summary>
    /// <example>
    /// <code>
    /// var hour = BookingSlotParser.EarliestHourInWindow(window, hours);
    /// </code>
    /// </example>
    public static TimeOnly? EarliestHourInWindow(TimeWindow window, IReadOnlyList<TimeOnly> siteHours)
    {
        if (siteHours is null || siteHours.Count == 0)
        {
            return null;
        }

        var match = siteHours
            .Where(h => SearchDatePlanner.IsInWindow(window, h))
            .OrderBy(h => h)
            .ToList();
        return match.Count == 0 ? null : match[0];
    }
}
