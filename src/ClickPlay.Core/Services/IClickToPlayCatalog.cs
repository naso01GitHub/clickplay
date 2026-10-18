using ClickPlay.Core.Models;

namespace ClickPlay.Core.Services;

/// <summary>
/// Reads sports, cities, and venues from clicktoplay.io.
/// </summary>
public interface IClickToPlayCatalog
{
    Task StartAsync(BrowserLaunchOptions options, CancellationToken cancellationToken);

    Task<IReadOnlyList<Sport>> GetSportsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<City>> GetCitiesAsync(Sport sport, CancellationToken cancellationToken);

    Task<IReadOnlyList<Venue>> GetVenuesAsync(Sport sport, City city, CancellationToken cancellationToken);
}
