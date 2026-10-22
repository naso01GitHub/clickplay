using ClickPlay.Core.Logging;

namespace ClickPlay.Tests;

public sealed class ErrorLogTests
{
    [Fact]
    public void Append_NewFile_WritesHeaderThenStampedLine()
    {
        var path = TempPath();
        var log = new ErrorLog(path, () => new DateTime(2026, 10, 22, 21, 5, 0));

        log.Append("Search failed (x): TimeoutException");

        var lines = File.ReadAllLines(path);
        Assert.Equal("Errors log started 22.10.2026 21:05:00", lines[0]);
        Assert.Equal(string.Empty, lines[1]);
        Assert.Equal("[22.10.2026 21:05:00] Search failed (x): TimeoutException", lines[2]);
    }

    [Fact]
    public void Append_WithinAWeek_KeepsOldEntries()
    {
        var path = TempPath();
        var now = new DateTime(2026, 10, 22, 9, 0, 0);
        var log = new ErrorLog(path, () => now);
        log.Append("first");

        now = now.AddDays(6).AddHours(23);
        log.Append("second");

        var text = File.ReadAllText(path);
        Assert.Contains("first", text, StringComparison.Ordinal);
        Assert.Contains("second", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Append_AfterAWeek_StartsNewFile()
    {
        var path = TempPath();
        var now = new DateTime(2026, 10, 22, 9, 0, 0);
        var log = new ErrorLog(path, () => now);
        log.Append("old error");

        now = now.AddDays(7);
        log.Append("new error");

        var lines = File.ReadAllLines(path);
        Assert.Equal("Errors log started 29.10.2026 09:00:00", lines[0]);
        Assert.DoesNotContain(lines, l => l.Contains("old error", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("new error", StringComparison.Ordinal));
    }

    [Fact]
    public void DeleteIfExpired_OnlyDeletesWeekOldOrHeaderlessFiles()
    {
        var path = TempPath();
        var now = new DateTime(2026, 10, 22, 9, 0, 0);
        var log = new ErrorLog(path, () => now);
        log.Append("x");

        Assert.False(log.DeleteIfExpired());
        Assert.True(File.Exists(path));

        now = now.AddDays(8);
        Assert.True(log.DeleteIfExpired());
        Assert.False(File.Exists(path));

        File.WriteAllText(path, "no header here");
        Assert.True(log.DeleteIfExpired());
        Assert.False(log.DeleteIfExpired());
    }

    [Fact]
    public void Append_EmptyMessage_WritesNothing_AndBadPathDoesNotThrow()
    {
        var path = TempPath();
        new ErrorLog(path).Append("  ");
        Assert.False(File.Exists(path));

        var dirAsFile = TempPath();
        File.WriteAllText(dirAsFile, "a file, not a folder");
        new ErrorLog(Path.Combine(dirAsFile, "errors-log.txt")).Append("x");
    }

    private static string TempPath() =>
        Path.Combine(Path.GetTempPath(), $"clickplay-errors-{Guid.NewGuid():N}.txt");
}
