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
