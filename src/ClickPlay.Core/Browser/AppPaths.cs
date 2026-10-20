namespace ClickPlay.Core.Browser;

/// <summary>
/// Cross-platform application data folders (not machine-specific hardcoded paths).
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// Directory for overlay-failure screenshots and page-state logs.
    /// </summary>
    /// <example>
    /// <code>
    /// var dir = AppPaths.DiagnosticsDirectory();
    /// </code>
    /// </example>
    public static string DiagnosticsDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClickPlay",
            "diagnostics");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Text file for the Results window log (app data, not the repo).
    /// </summary>
    /// <example>
    /// <code>
    /// var path = AppPaths.ResultsLogFile();
    /// </code>
    /// </example>
    public static string ResultsLogFile()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClickPlay");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "results-log.txt");
    }
}
