namespace ClickPlay.Core.Validation;

/// <summary>
/// Splits a To field into individual recipient addresses. Prefer one address per line.
/// </summary>
public static class EmailRecipientParser
{
    /// <summary>
    /// Parses recipient addresses from multiline (preferred) or comma/semicolon-separated text.
    /// </summary>
    /// <param name="text">Raw To field. One address per line is preferred.</param>
    /// <returns>Distinct trimmed addresses; empty entries are dropped.</returns>
    /// <example>
    /// <code>
    /// var to = EmailRecipientParser.Parse("a@x.com\nb@y.com");
    /// </code>
    /// </example>
    public static IReadOnlyList<string> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(address => address.Contains('@', StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
