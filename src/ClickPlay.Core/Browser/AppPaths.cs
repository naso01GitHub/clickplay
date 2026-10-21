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
    /// Per-user settings folder (<c>%AppData%/ClickPlay</c> on Windows, <c>~/.config/ClickPlay</c> on Mac).
    /// </summary>
    /// <example>
    /// <code>
    /// var dir = AppPaths.SettingsDirectory();
    /// </code>
    /// </example>
    public static string SettingsDirectory()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClickPlay");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Text file next to the running exe. Created on first write; never deleted by Search.
    /// </summary>
    /// <example>
    /// <code>
    /// var path = AppPaths.ResultsLogFile();
    /// </code>
    /// </example>
    public static string ResultsLogFile()
    {
        var dir = AppContext.BaseDirectory;
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "results-log.txt");
    }
}
