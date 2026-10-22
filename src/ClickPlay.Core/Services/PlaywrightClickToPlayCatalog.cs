using ClickPlay.Core.Browser;
using ClickPlay.Core.Models;

namespace ClickPlay.Core.Services;

/// <summary>
/// Catalog backed by Playwright. The GUI starts and closes the browser per cascade step.
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
        return _session.SelectCityAsync(sport, city, cancellationToken);
    }

    public Task<AvailabilityScan> SearchAvailabilityAsync(
        SearchRequest request,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        return _session.SearchAvailabilityAsync(request, progress, cancellationToken);
    }

    public Task CloseBrowserAsync(CancellationToken cancellationToken)
    {
        return _session.CloseBrowserAsync(cancellationToken);
    }
}
