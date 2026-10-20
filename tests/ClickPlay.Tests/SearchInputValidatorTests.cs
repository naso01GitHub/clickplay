using ClickPlay.Core.Models;
using ClickPlay.Core.Validation;

namespace ClickPlay.Tests;

public sealed class SearchInputValidatorTests
{
    [Theory]
    [InlineData("7", 7)]
    [InlineData("1", 1)]
    public void DaysAhead_ParsesPositiveInteger(string text, int expected)
    {
        Assert.True(SearchInputValidator.TryParseDaysAhead(text, out var days, out var error));
        Assert.Equal(expected, days);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("abc")]
    public void DaysAhead_RejectsEmptyZeroAndNonNumeric(string text)
    {
        Assert.False(SearchInputValidator.TryParseDaysAhead(text, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("60", 60)]
    public void Repeat_ParsesValuesFromOneToSixty(string text, int expected)
    {
        Assert.True(SearchInputValidator.TryParseRepeat(text, out var repeat, out var error));
        Assert.Equal(expected, repeat);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("61")]
    [InlineData("abc")]
    public void Repeat_RejectsOutOfRangeAndNonNumeric(string text)
    {
        Assert.False(SearchInputValidator.TryParseRepeat(text, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("00:00", 0, 0)]
    [InlineData("09:05", 9, 5)]
    [InlineData("18:00", 18, 0)]
    [InlineData("23:59", 23, 59)]
    public void Time_ParsesHhMm(string text, int hour, int minute)
    {
        Assert.True(SearchInputValidator.TryParseTime(text, out var time, out var error));
        Assert.Equal(new TimeOnly(hour, minute), time);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("9:00")]
    [InlineData("18:0")]
    [InlineData("24:00")]
    [InlineData("18:60")]
    [InlineData("18.00")]
    [InlineData("abc")]
    public void Time_RejectsNonHhMm(string text)
    {
        Assert.False(SearchInputValidator.TryParseTime(text, out _, out var error));
        Assert.Contains("hh:mm", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("9", 9)]
    [InlineData("23", 23)]
    public void Hour_ParsesZeroToTwentyThree(string text, int expected)
    {
        Assert.True(SearchInputValidator.TryParseHour(text, out var hour, out var error));
        Assert.Equal(expected, hour);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("24")]
    [InlineData("ab")]
    public void Hour_RejectsOutOfRange(string text)
    {
        Assert.False(SearchInputValidator.TryParseHour(text, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("59", 59)]
    public void Minute_ParsesZeroToFiftyNine(string text, int expected)
    {
        Assert.True(SearchInputValidator.TryParseMinute(text, out var minute, out var error));
        Assert.Equal(expected, minute);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("60")]
    public void Minute_RejectsOutOfRange(string text)
    {
        Assert.False(SearchInputValidator.TryParseMinute(text, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("24", 0, 23, "23")]
    [InlineData("-1", 0, 23, "00")]
    [InlineData("9", 0, 23, "09")]
    [InlineData("99", 0, 59, "59")]
    [InlineData("", 0, 59, "00")]
    public void ClampClockPart_ForcesRangeAndTwoDigits(string text, int min, int max, string expected)
    {
        Assert.Equal(expected, SearchInputValidator.ClampClockPart(text, min, max));
    }

    [Fact]
    public void ClampClockNumber_CapsHourAndMinute()
    {
        Assert.Equal(23, SearchInputValidator.ClampClockNumber(55, 23));
        Assert.Equal(0, SearchInputValidator.ClampClockNumber(-4, 23));
        Assert.Equal(59, SearchInputValidator.ClampClockNumber(90, 59));
    }

    [Fact]
    public void HourAndMinute_BuildTime()
    {
        Assert.True(SearchInputValidator.TryParseHourAndMinute("18", "00", out var time, out _));
        Assert.Equal(new TimeOnly(18, 0), time);
    }

    [Fact]
    public void TimeWindow_WeekdayFromMustBeBeforeTo()
    {
        Assert.True(SearchInputValidator.TryParseTime("18:00", out var from, out _));
        Assert.True(SearchInputValidator.TryParseTime("21:00", out var to, out _));
        Assert.True(new TimeWindow(from, to).IsValid);
        Assert.False(new TimeWindow(to, from).IsValid);
    }
}
