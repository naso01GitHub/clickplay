namespace ClickPlay.Core.Models;

/// <summary>
/// A club or venue for a sport and city.
/// </summary>
/// <param name="Name">Club name from <c>h3.club-name</c>.</param>
/// <param name="Address">Club address from <c>.club-address</c>.</param>
/// <param name="ClubSlug">The <c>club</c> query value of the card link (<c>/club/?club=supersport</c>). Search opens the club page by this slug.</param>
/// <example>
/// <code>
/// var venue = new Venue("Supersport", "Варна", "supersport");
/// </code>
/// </example>
public sealed record Venue(string Name, string Address, string? ClubSlug = null);
