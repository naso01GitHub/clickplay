namespace ClickPlay.App.ViewModels;

/// <summary>
/// How the status banner at the top of the window is styled.
/// </summary>
public enum StatusKind
{
    /// <summary>Neutral information or a prompt, e.g. "Select a city.".</summary>
    Info,

    /// <summary>Something is in progress, e.g. "Checking Supersport, 23.10.2026…".</summary>
    Working,

    /// <summary>Something good happened, e.g. "Found 3 new free hour(s)…" or "Mailed …".</summary>
    Success,

    /// <summary>Not an error, but worth noticing, e.g. "Skipped …" or "Search stopped.".</summary>
    Warning,

    /// <summary>Something failed or input is invalid, e.g. "Search failed (…)" or "Days ahead must be …".</summary>
    Error,
}

/// <summary>
/// Picks a <see cref="StatusKind"/> from the status text, so every place that sets a status
/// gets the right banner style without extra code.
/// </summary>
public static class StatusClassifier
{
    private static readonly string[] ErrorStarts = ["Search failed", "Could not", "Mail failed", "Email:"];
    private static readonly string[] ErrorParts =
        ["busy", "layout changed", "not clickable", "did not switch", " must ", "No club schedule"];

    private static readonly string[] WarningStarts = ["Skipped", "Mail skipped", "Stopping", "Search stopped"];
    private static readonly string[] WarningParts = ["cancelled", "has no club link", "at least one location"];

    private static readonly string[] SuccessStarts = ["Found ", "Mailed ", "Saved login erased"];
    private static readonly string[] SuccessParts = [" sports loaded"];

    /// <summary>
    /// Classifies a status message. Errors win over warnings, warnings over success,
    /// success over "working" (text ending with …); everything else is info.
    /// </summary>
    /// <param name="message">Status text; null or empty is <see cref="StatusKind.Info"/>.</param>
    /// <returns>The banner style for this message.</returns>
    /// <example>
    /// <code>
    /// StatusClassifier.Classify("Search failed (…): TimeoutException"); // Error
    /// StatusClassifier.Classify("Checking Supersport, 23.10.2026…");   // Working
    /// StatusClassifier.Classify("Found 3 new free hour(s). Closing browser…"); // Success
    /// </code>
    /// </example>
    public static StatusKind Classify(string? message)
    {
        var text = message?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return StatusKind.Info;
        }

        if (Matches(text, ErrorStarts, ErrorParts))
        {
            return StatusKind.Error;
        }

        if (Matches(text, WarningStarts, WarningParts))
        {
            return StatusKind.Warning;
        }

        if (Matches(text, SuccessStarts, SuccessParts))
        {
            return StatusKind.Success;
        }

        return text.EndsWith('…') || text.EndsWith("...", StringComparison.Ordinal)
            ? StatusKind.Working
            : StatusKind.Info;
    }

    /// <summary>
    /// Icon shown at the left of the banner.
    /// </summary>
    /// <param name="kind">Banner style.</param>
    /// <returns>One symbol: ✖ error, ⚠ warning, ✔ success, ⏳ working, ℹ info.</returns>
    /// <example>
    /// <code>
    /// var icon = StatusClassifier.Icon(StatusKind.Error); // "✖"
    /// </code>
    /// </example>
    public static string Icon(StatusKind kind) => kind switch
    {
        StatusKind.Error => "✖",
        StatusKind.Warning => "⚠",
        StatusKind.Success => "✔",
        StatusKind.Working => "⏳",
        _ => "ℹ",
    };

    /// <summary>
    /// True when the text starts with one of <paramref name="starts"/> or contains one of <paramref name="parts"/> (ignoring case).
    /// </summary>
    /// <param name="text">Trimmed status text.</param>
    /// <param name="starts">Prefixes to test.</param>
    /// <param name="parts">Substrings to test.</param>
    /// <returns>True on any match.</returns>
    /// <example>
    /// <code>
    /// Matches("Skipped Supersport…", ["Skipped"], []); // true
    /// </code>
    /// </example>
    private static bool Matches(string text, string[] starts, string[] parts) =>
        starts.Any(s => text.StartsWith(s, StringComparison.OrdinalIgnoreCase))
        || parts.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
}
