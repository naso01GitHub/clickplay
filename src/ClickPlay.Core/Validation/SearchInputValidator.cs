using System.Globalization;
using System.Text.RegularExpressions;

namespace ClickPlay.Core.Validation;

/// <summary>
/// Validates user-entered search parameters before they are used.
/// </summary>
public static class SearchInputValidator
{
    public static bool TryParseDaysAhead(string? text, out int daysAhead, out string error)
    {
        daysAhead = 0;
        if (string.IsNullOrWhiteSpace(text) || !int.TryParse(text.Trim(), out daysAhead) || daysAhead < 1)
        {
            error = "Days ahead must be a positive integer.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Wait minutes between search cycles; integer from 1 to 60.
    /// </summary>
    /// <param name="text">User text.</param>
    /// <param name="repeat">Parsed value.</param>
    /// <param name="error">User-visible error when parsing fails.</param>
    /// <returns>True when the value is in range.</returns>
    /// <example>
    /// <code>
    /// SearchInputValidator.TryParseRepeat("12", out var n, out _);
    /// </code>
    /// </example>
    public static bool TryParseRepeat(string? text, out int repeat, out string error)
    {
        repeat = 0;
        if (string.IsNullOrWhiteSpace(text) || !int.TryParse(text.Trim(), out repeat) || repeat is < 1 or > 60)
        {
            error = "Repeat (minutes) must be a number from 1 to 60.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static readonly Regex HhMm = new(
        @"^(?<h>[01]\d|2[0-3]):(?<m>[0-5]\d)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// Hour part of a GUI time box: integer 0–23.
    /// </summary>
    public static bool TryParseHour(string? text, out int hour, out string error)
    {
        hour = 0;
        if (string.IsNullOrWhiteSpace(text) || !int.TryParse(text.Trim(), out hour) || hour is < 0 or > 23)
        {
            error = "Hour must be 0–23.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Minute part of a GUI time box: integer 0–59.
    /// </summary>
    public static bool TryParseMinute(string? text, out int minute, out string error)
    {
        minute = 0;
        if (string.IsNullOrWhiteSpace(text) || !int.TryParse(text.Trim(), out minute) || minute is < 0 or > 59)
        {
            error = "Minutes must be 0–59.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Forces a clock part into <paramref name="min"/>–<paramref name="max"/> as two digits (00–23 or 00–59).
    /// Values below min become min; above max become max; empty or non-numeric become min.
    /// </summary>
    /// <example>
    /// <code>
    /// SearchInputValidator.ClampClockPart("24", 0, 23); // "23"
    /// SearchInputValidator.ClampClockPart("-1", 0, 59); // "00"
    /// </code>
    /// </example>
    /// <summary>
    /// Clamps a NumericUpDown clock part to 0–<paramref name="max"/>.
    /// </summary>
    public static decimal ClampClockNumber(decimal value, int max)
    {
        if (max < 0)
        {
            max = 0;
        }

        var n = decimal.Truncate(value);
        if (n < 0)
        {
            n = 0;
        }

        if (n > max)
        {
            n = max;
        }

        return n;
    }

    /// <summary>
    /// Builds a <see cref="TimeOnly"/> from hour and minute numeric boxes.
    /// </summary>
    public static TimeOnly ToClockTime(decimal hour, decimal minute)
    {
        var h = (int)ClampClockNumber(hour, 23);
        var m = (int)ClampClockNumber(minute, 59);
        return new TimeOnly(h, m);
    }

    public static string ClampClockPart(string? text, int min, int max)
    {
        if (min > max)
        {
            (min, max) = (max, min);
        }

        var raw = (text ?? string.Empty).Trim();
        if (raw.StartsWith('-'))
        {
            return min.ToString("00", CultureInfo.InvariantCulture);
        }

        var digits = new string(raw.Where(char.IsDigit).Take(3).ToArray());
        if (digits.Length == 0 || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            return min.ToString("00", CultureInfo.InvariantCulture);
        }

        if (n < min)
        {
            n = min;
        }

        if (n > max)
        {
            n = max;
        }

        return n.ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Builds a time from separate hour and minute boxes.
    /// </summary>
    /// <example>
    /// <code>
    /// SearchInputValidator.TryParseHourAndMinute("18", "00", out var t, out _);
    /// </code>
    /// </example>
    public static bool TryParseHourAndMinute(string? hourText, string? minuteText, out TimeOnly time, out string error)
    {
        time = default;
        if (!TryParseHour(hourText, out var hour, out error) || !TryParseMinute(minuteText, out var minute, out error))
        {
            return false;
        }

        time = new TimeOnly(hour, minute);
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Parses a GUI time as <c>hh:mm</c> with hour 00–23 and minutes 00–59.
    /// </summary>
    /// <example>
    /// <code>
    /// SearchInputValidator.TryParseTime("18:00", out var t, out _);
    /// </code>
    /// </example>
    public static bool TryParseTime(string? text, out TimeOnly time, out string error)
    {
        time = default;
        var value = (text ?? string.Empty).Trim();
        var match = HhMm.Match(value);
        if (!match.Success)
        {
            error = "Time must be hh:mm (hour 00–23, minutes 00–59).";
            return false;
        }

        var hour = int.Parse(match.Groups["h"].Value, CultureInfo.InvariantCulture);
        var minute = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
        time = new TimeOnly(hour, minute);
        error = string.Empty;
        return true;
    }
}
