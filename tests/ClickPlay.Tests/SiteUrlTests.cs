using ClickPlay.Core.Browser;

namespace ClickPlay.Tests;

public sealed class SiteUrlTests
{
    [Fact]
    public void ClubUrl_HasSlugAndDay_NoHour()
    {
        var url = ClickToPlayBrowserSession.BuildClubUrl("supersport", new DateOnly(2026, 10, 23));

        Assert.Equal("https://clicktoplay.io/club/?club=supersport&date=2026-10-23", url);
    }

    [Fact]
    public void ClubUrl_EscapesCyrillicSlug()
    {
        var url = ClickToPlayBrowserSession.BuildClubUrl("падел-арена-варна", new DateOnly(2026, 10, 24));

        Assert.StartsWith("https://clicktoplay.io/club/?club=%D0%BF", url, StringComparison.Ordinal);
        Assert.Equal("падел-арена-варна", Uri.UnescapeDataString(new Uri(url).Query.Split('&')[0]["?club=".Length..]));
        Assert.EndsWith("&date=2026-10-24", url, StringComparison.Ordinal);
    }

    [Fact]
    public void ListingUrl_BeforeCutoff_CityOnly()
    {
        var url = ClickToPlayBrowserSession.BuildListingUrl("42", new DateTime(2026, 10, 21, 12, 0, 0));

        Assert.Equal("https://clicktoplay.io/clubs/index.php?city=42", url);
    }

    [Fact]
    public void ListingUrl_AfterCutoff_TomorrowAtTen()
    {
        var url = ClickToPlayBrowserSession.BuildListingUrl("42", new DateTime(2026, 10, 21, 20, 0, 0));

        Assert.Equal("https://clicktoplay.io/clubs/index.php?city=42&date=2026-10-22&hour=10:00", url);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void ClubUrl_RejectsEmptySlug(string slug)
    {
        Assert.Throws<ArgumentException>(() => ClickToPlayBrowserSession.BuildClubUrl(slug, new DateOnly(2026, 10, 23)));
    }
}
