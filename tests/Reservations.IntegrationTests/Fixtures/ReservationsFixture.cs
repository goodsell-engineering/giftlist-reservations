using BuildingBlocks.Persistence;
using Reservations.Infrastructure.Platform;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Reservations.IntegrationTests.Fixtures;

/// <summary>
/// Builds the real Reservations composition root — the exact same
/// <c>AddBuildingBlocksMongo</c>/<c>AddReservationsInfrastructure</c> calls
/// <c>Reservations.Host</c>'s <c>Program.cs</c> makes — against the container from
/// <see cref="InfrastructureFixture"/>. GL-35 has no interactor and no Rebus handler yet, so there
/// is no wire-level entry point to send a command through at all; every test in this suite
/// resolves <see cref="Application.Reservations.IReservationRepository"/> straight out of the
/// container instead, via <see cref="CreateReservationsScope"/> — the same route
/// <c>GiftLists.IntegrationTests.GiftLists.GiftListRepositoryTests</c> uses for exactly the cases
/// its own fixture's doc comment says the wire-level tests genuinely cannot reach: forcing a
/// specific duplicate-key collision deterministically. Here that is not the exception, it is the
/// only thing this suite proves — GL-36's ReserveGift handler is what gives this a wire-level
/// entry point later.
/// </summary>
public sealed class ReservationsFixture : IAsyncLifetime
{
    private const string DatabaseName = "reservation";

    private readonly InfrastructureFixture _infrastructure = new();
    private IHost _reservationsHost = null!;

    public IMongoDatabase Database { get; private set; } = null!;

    public IServiceScope CreateReservationsScope() => _reservationsHost.Services.CreateScope();

    public async Task InitializeAsync()
    {
        await _infrastructure.InitializeAsync();

        var reservationsConfig = new Dictionary<string, string?>
        {
            [MongoConfigurationExtensions.ConnectionStringConfigKey] = _infrastructure.MongoConnectionString,
        };
        var reservationsBuilder = Host.CreateApplicationBuilder();
        reservationsBuilder.Logging.ClearProviders();
        reservationsBuilder.Configuration.AddInMemoryCollection(reservationsConfig);
        reservationsBuilder.Services.AddBuildingBlocksMongo(reservationsBuilder.Configuration, DatabaseName);
        reservationsBuilder.Services.AddReservationsInfrastructure();
        _reservationsHost = reservationsBuilder.Build();
        await _reservationsHost.StartAsync();
        await ReservationsInfrastructureServiceCollectionExtensions.EnsureIndexesAsync(
            _reservationsHost.Services, CancellationToken.None);

        Database = _reservationsHost.Services.GetRequiredService<IMongoDatabase>();
    }

    public async Task DisposeAsync()
    {
        await _reservationsHost.StopAsync();
        _reservationsHost.Dispose();
        await _infrastructure.DisposeAsync();
    }

    /// <summary>
    /// CONVENTIONS.md "Testing": isolate by dropping the database between tests, never by
    /// restarting a container. Re-applies the unique (listId, itemId) index afterwards — dropping
    /// the database drops it too, and a test relying on it running right after a reset would
    /// otherwise pass for the wrong reason.
    /// </summary>
    public async Task ResetAsync()
    {
        await Database.Client.DropDatabaseAsync(DatabaseName);
        await ReservationsInfrastructureServiceCollectionExtensions.EnsureIndexesAsync(
            _reservationsHost.Services, CancellationToken.None);
    }
}
