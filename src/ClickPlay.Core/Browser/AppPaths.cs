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
    /// Errors-only log next to the running exe (<c>errors-log.txt</c>). Created on the first error;
    /// deleted and restarted weekly by <see cref="Logging.ErrorLog"/>. The old <c>results-log.txt</c>
    /// in the same folder is no longer written and is left as it is.
    /// </summary>
    /// <returns>Full path of <c>errors-log.txt</c>.</returns>
    /// <example>
    /// <code>
    /// var log = new ErrorLog(AppPaths.ErrorsLogFile());
    /// </code>
    /// </example>
    public static string ErrorsLogFile() => Path.Combine(AppContext.BaseDirectory, "errors-log.txt");
}
