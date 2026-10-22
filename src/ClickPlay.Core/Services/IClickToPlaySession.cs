using ClickPlay.Core.Models;

namespace ClickPlay.Core.Services;

/// <summary>
/// Playwright session against clicktoplay.io. The GUI closes the browser after sports, cities, and locations; Search opens it again for each cycle.
/// </summary>
public interface IClickToPlaySession : IAsyncDisposable
{
    Task StartAsync(BrowserLaunchOptions options, CancellationToken cancellationToken);

    Task<IReadOnlyList<Sport>> GetSportsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<City>> SelectSportAsync(Sport sport, CancellationToken cancellationToken);

    Task<IReadOnlyList<Venue>> SelectCityAsync(Sport sport, City city, CancellationToken cancellationToken);

    Task<AvailabilityScan> SearchAvailabilityAsync(
        SearchRequest request,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    Task CloseBrowserAsync(CancellationToken cancellationToken);
}
