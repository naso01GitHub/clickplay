using ClickPlay.Core.Services;

namespace ClickPlay.Tests;

public sealed class CityNameParserTests
{
    [Fact]
    public void Parse_StripsCountryAndPlaceholders()
    {
        var cities = CityNameParser.Parse(
        [
            "София, BULGARIA",
            "Варна, BULGARIA",
            "София, BULGARIA",
            "Текуща локация",
            "Няма намерени резултати",
            "Bulgaria"
        ]);

        Assert.Equal(["София", "Варна"], cities.Select(c => c.Name));
    }
}
