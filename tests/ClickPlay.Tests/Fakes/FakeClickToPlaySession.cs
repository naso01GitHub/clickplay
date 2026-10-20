using ClickPlay.Core.Models;
using ClickPlay.Core.Services;

namespace ClickPlay.Tests.Fakes;

public sealed class FakeClickToPlaySession : IClickToPlaySession
{
    public BrowserLaunchOptions? LastStartOptions { get; private set; }

    public Sport? LastSport { get; private set; }

    public City? LastCity { get; private set; }

    public IReadOnlyList<Sport> Sports { get; init; } = [new("Падел"), new("Тенис")];

    public IReadOnlyList<City> Cities { get; init; } = [new("Sofia"), new("Varna")];

    public IReadOnlyList<Venue> Venues { get; init; } =
    [
        new("Amik Sport", "Sofia"),
        new("Sport Olympic", "Sofia")
    ];

    public Task StartAsync(BrowserLaunchOptions options, CancellationToken cancellationToken)
    {
        LastStartOptions = options;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Sport>> GetSportsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Sports);
    }

    public Task<IReadOnlyList<City>> SelectSportAsync(Sport sport, CancellationToken cancellationToken)
    {
        LastSport = sport;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Cities);
    }

    public Task<IReadOnlyList<Venue>> SelectCityAsync(Sport sport, City city, CancellationToken cancellationToken)
    {
        LastSport = sport;
        LastCity = city;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Venues);
    }

    public SearchRequest? LastSearchRequest { get; private set; }

    public int SearchCallCount { get; private set; }

    public int CloseBrowserCount { get; private set; }

    public IReadOnlyList<AvailabilitySlot> Availability { get; init; } =
    [
        new("Amik Sport", new DateOnly(2026, 10, 19), new TimeOnly(18, 0), new TimeOnly(19, 0))
    ];

    public Task<IReadOnlyList<AvailabilitySlot>> SearchAvailabilityAsync(
        SearchRequest request,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        LastSearchRequest = request;
        SearchCallCount++;
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report("Checking locations…");
        return Task.FromResult(Availability);
    }

    public Task CloseBrowserAsync(CancellationToken cancellationToken)
    {
        CloseBrowserCount++;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
