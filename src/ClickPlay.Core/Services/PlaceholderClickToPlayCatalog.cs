using ClickPlay.Core.Models;

namespace ClickPlay.Core.Services;

/// <summary>
/// In-memory catalog so the GUI can be used before Playwright scraping is wired.
/// </summary>
public sealed class PlaceholderClickToPlayCatalog : IClickToPlayCatalog
{
    private static readonly IReadOnlyList<Sport> Sports =
    [
        new("Тенис"),
        new("Падел"),
        new("Пикълбол"),
        new("Бадминтон"),
        new("Футбол"),
        new("Тенис на маса"),
        new("Баскетбол"),
        new("Волейбол"),
        new("Плажен тенис"),
        new("Плажен волейбол"),
        new("Плуване"),
        new("Скуош"),
        new("Голф")
    ];

    private static readonly IReadOnlyList<City> Cities =
    [
        new("Sofia"),
        new("Varna"),
        new("Plovdiv"),
        new("Bankya")
    ];

    public Task StartAsync(BrowserLaunchOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Sport>> GetSportsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Sports);
    }

    public Task<IReadOnlyList<City>> GetCitiesAsync(Sport sport, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sport);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Cities);
    }

    public Task<IReadOnlyList<Venue>> GetVenuesAsync(Sport sport, City city, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sport);
        ArgumentNullException.ThrowIfNull(city);
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<Venue> venues =
        [
            new($"Amik Sport ({sport.Name})", $"{city.Name}"),
            new($"Sport Olympic ({sport.Name})", $"{city.Name}"),
            new($"Supersport ({sport.Name})", $"{city.Name}")
        ];

        return Task.FromResult(venues);
    }

    public Task<AvailabilityScan> SearchAvailabilityAsync(
        SearchRequest request,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Placeholder catalog has no live availability.");
        return Task.FromResult(AvailabilityScan.Complete([]));
    }

    public Task CloseBrowserAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
