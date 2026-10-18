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
}
