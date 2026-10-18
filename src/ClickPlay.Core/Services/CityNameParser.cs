using ClickPlay.Core.Models;

namespace ClickPlay.Core.Services;

/// <summary>
/// Normalizes city labels scraped from clicktoplay.io (e.g. "София, BULGARIA").
/// </summary>
public static class CityNameParser
{
    /// <summary>
    /// Returns unique city names, dropping country suffix and placeholders.
    /// </summary>
    /// <param name="labels">Raw labels from the city popup.</param>
    /// <returns>Validated cities.</returns>
    /// <example>
    /// <code>
    /// var cities = CityNameParser.Parse(["София, BULGARIA", "Текуща локация"]);
    /// </code>
    /// </example>
    public static IReadOnlyList<City> Parse(IEnumerable<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cities = new List<City>();
        foreach (var label in labels)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                continue;
            }

            var name = label.Trim().Split('\n')[0].Trim();
            var comma = name.IndexOf(',');
            if (comma > 0)
            {
                name = name[..comma].Trim();
            }

            if (name.Length is < 2 or > 48 || !seen.Add(name) || IsPlaceholder(name))
            {
                continue;
            }

            cities.Add(new City(name));
        }

        return cities;
    }

    private static bool IsPlaceholder(string name)
    {
        return name.StartsWith("bulgaria", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("българия", StringComparison.OrdinalIgnoreCase)
            || name.Contains("текущ", StringComparison.OrdinalIgnoreCase)
            || name.Contains("current location", StringComparison.OrdinalIgnoreCase)
            || name.Contains("няма намерени", StringComparison.OrdinalIgnoreCase)
            || name.Contains("no result", StringComparison.OrdinalIgnoreCase);
    }
}
