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

    public Task<IReadOnlyList<Venue>> SelectCityAsync(City city, CancellationToken cancellationToken)
    {
        LastCity = city;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Venues);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
