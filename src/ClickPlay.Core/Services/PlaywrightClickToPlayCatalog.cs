using ClickPlay.Core.Browser;
using ClickPlay.Core.Models;

namespace ClickPlay.Core.Services;

/// <summary>
/// Catalog backed by a long-lived Playwright session (one page until dispose).
/// </summary>
public sealed class PlaywrightClickToPlayCatalog : IClickToPlayCatalog
{
    private readonly IClickToPlaySession _session;

    public PlaywrightClickToPlayCatalog(IClickToPlaySession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public Task StartAsync(BrowserLaunchOptions options, CancellationToken cancellationToken)
    {
        return _session.StartAsync(options, cancellationToken);
    }

    public Task<IReadOnlyList<Sport>> GetSportsAsync(CancellationToken cancellationToken)
    {
        return _session.GetSportsAsync(cancellationToken);
    }

    public Task<IReadOnlyList<City>> GetCitiesAsync(Sport sport, CancellationToken cancellationToken)
    {
        return _session.SelectSportAsync(sport, cancellationToken);
    }

    public Task<IReadOnlyList<Venue>> GetVenuesAsync(Sport sport, City city, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sport);
        return _session.SelectCityAsync(city, cancellationToken);
    }
}
