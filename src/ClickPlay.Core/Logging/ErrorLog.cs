using System.Globalization;

namespace ClickPlay.Core.Logging;

/// <summary>
/// Plain-text log of errors only (<c>errors-log.txt</c>). The first line records when the file was started;
/// once that is more than <see cref="MaxAge"/> ago, the file is deleted and a new one begins.
/// Writing never throws: a log problem must not stop the app.
/// </summary>
/// <remarks>
/// The start time is kept in the file, not taken from the file's creation time: Windows can give a file
/// recreated right after deletion the old creation time ("tunneling"), which would delete it again and again.
/// </remarks>
public sealed class ErrorLog
{
    /// <summary>First-line prefix; the start time follows in <see cref="StampFormat"/>.</summary>
    public const string HeaderPrefix = "Errors log started ";

    /// <summary>Time format used in the header and in every entry.</summary>
    public const string StampFormat = "dd.MM.yyyy HH:mm:ss";

    /// <summary>How long one file is kept before it is deleted and restarted.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromDays(7);

    private readonly Func<DateTime> _clock;

    /// <summary>
    /// Creates a log for one file.
    /// </summary>
    /// <param name="filePath">Full path of <c>errors-log.txt</c>.</param>
    /// <param name="clock">Local time source; null uses <see cref="DateTime.Now"/>. Tests pass a fixed clock.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty.</exception>
    /// <example>
    /// <code>
    /// var log = new ErrorLog(AppPaths.ErrorsLogFile());
    /// </code>
    /// </example>
    public ErrorLog(string filePath, Func<DateTime>? clock = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = filePath;
        _clock = clock ?? (() => DateTime.Now);
    }

    /// <summary>Full path of the log file.</summary>
    public string FilePath { get; }

    /// <summary>
    /// Appends one error as <c>[dd.MM.yyyy HH:mm:ss] message</c>, after deleting the file if it is a week old.
    /// Creates the file (with its start header) when missing. Never throws.
    /// </summary>
    /// <param name="message">Error text; empty is ignored.</param>
    /// <returns>No return value.</returns>
    /// <example>
    /// <code>
    /// log.Append("Search failed (Checking Supersport, 23.10.2026): TimeoutException: …");
    /// </code>
    /// </example>
    public void Append(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            var now = _clock();
            DeleteIfExpired();
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var lines = new List<string>();
            if (!File.Exists(FilePath))
            {
                lines.Add(HeaderPrefix + now.ToString(StampFormat, CultureInfo.InvariantCulture));
                lines.Add(string.Empty);
            }

            lines.Add($"[{now.ToString(StampFormat, CultureInfo.InvariantCulture)}] {message.Trim()}");
            File.AppendAllLines(FilePath, lines);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// Deletes the file when its start header is <see cref="MaxAge"/> old or more, or when it has no valid header.
    /// Called before every write and once at app start. Never throws.
    /// </summary>
    /// <returns>True when the file was deleted.</returns>
    /// <example>
    /// <code>
    /// new ErrorLog(AppPaths.ErrorsLogFile()).DeleteIfExpired();
    /// </code>
    /// </example>
    public bool DeleteIfExpired()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return false;
            }

            var started = ReadStart();
            if (started is { } start && _clock() - start < MaxAge)
            {
                return false;
            }

            File.Delete(FilePath);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the start time from the first line.
    /// </summary>
    /// <returns>The start time, or null when the header is missing or unreadable.</returns>
    /// <example>
    /// <code>
    /// var started = ReadStart(); // 22.10.2026 21:00:00
    /// </code>
    /// </example>
    private DateTime? ReadStart()
    {
        var first = File.ReadLines(FilePath).FirstOrDefault();
        if (first is null || !first.StartsWith(HeaderPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return DateTime.TryParseExact(
            first[HeaderPrefix.Length..].Trim(),
            StampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var start)
            ? start
            : null;
    }
}
