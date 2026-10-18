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

    [Fact]
    public void TimeWindow_WeekdayFromMustBeBeforeTo()
    {
        Assert.True(SearchInputValidator.TryParseTime("18:00", out var from, out _));
        Assert.True(SearchInputValidator.TryParseTime("21:00", out var to, out _));
        Assert.True(new TimeWindow(from, to).IsValid);
        Assert.False(new TimeWindow(to, from).IsValid);
    }
}
