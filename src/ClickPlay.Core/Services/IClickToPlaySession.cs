using ClickPlay.Core.Models;

namespace ClickPlay.Core.Services;

/// <summary>
/// Long-lived Playwright session against clicktoplay.io. The page stays open until dispose.
/// </summary>
public interface IClickToPlaySession : IAsyncDisposable
{
    Task StartAsync(BrowserLaunchOptions options, CancellationToken cancellationToken);

    Task<IReadOnlyList<Sport>> GetSportsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<City>> SelectSportAsync(Sport sport, CancellationToken cancellationToken);

    Task<IReadOnlyList<Venue>> SelectCityAsync(City city, CancellationToken cancellationToken);
}
