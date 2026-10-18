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

    public static bool TryParseTime(string? text, out TimeOnly time, out string error)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text) || !TimeOnly.TryParse(text.Trim(), out time))
        {
            error = "Enter a valid time (for example 18:00).";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
