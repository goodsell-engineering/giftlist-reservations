using BuildingBlocks.Messaging;
using BuildingBlocks.Messaging.RequestReply;
using BuildingBlocks.Persistence;
using Reservations.Contracts.Reservations;
using Reservations.Infrastructure.Platform;
using Reservations.IntegrationTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Routing.TypeBased;

namespace Reservations.IntegrationTests.Fixtures;

/// <summary>
/// Builds the real Reservations composition root — the exact same
/// <c>AddBuildingBlocksMongo</c>/<c>AddBuildingBlocksRebus</c>/<c>AddReservationsInfrastructure</c>
/// calls <c>Reservations.Host</c>'s <c>Program.cs</c> makes — against the containers from
/// <see cref="InfrastructureFixture"/>, plus one "requester" bus standing in for both other
/// services this one ever talks to in production: the Gateway (the only sender of
/// <see cref="ReserveGift"/>, reached through <see cref="RequestReplyBridge"/>) and GiftLists (the
/// only publisher of the events <see cref="GiftListsBus"/> lets a test publish). Tests therefore
/// enter through the real Rebus handlers (CONVENTIONS.md "Testing"'s "entered at its real entry
/// point"), never by calling an interactor directly — except
/// <c>ReservationRepositoryTests</c>, which <see cref="CreateReservationsScope"/> exists for; see
/// its own doc comment for why that one case genuinely cannot be reached deterministically through
/// the bridge (forcing a specific duplicate-key collision).
/// </summary>
public sealed class ReservationsFixture : IAsyncLifetime
{
    private const string DatabaseName = "reservation";
    private const string ReservationQueueName = "reservation";

    private readonly InfrastructureFixture _infrastructure = new();
    private IHost _reservationsHost = null!;
    private IHost _requesterHost = null!;

    public IMongoDatabase Database { get; private set; } = null!;

    /// <summary>
    /// Log entries written by the real Reservations host — see <see cref="LogCapture"/>. GL-37:
    /// the backstop behind "the <c>Logging&lt;,&gt;</c> decorator never logs a release secret",
    /// since nothing about the type system prevents SOME future log call from doing it.
    /// </summary>
    public LogCapture Logs { get; } = new();

    public IRequestReplyBridge RequestReplyBridge => _requesterHost.Services.GetRequiredService<IRequestReplyBridge>();

    /// <summary>
    /// The GiftLists integration events this fixture can publish onto the real broker — the ACL
    /// boundary (ARCHITECTURE.md "Consuming other services' events: anti-corruption layer") this
    /// repo's own four handlers (GL-34) translate on the way in.
    /// </summary>
    public IBus GiftListsBus => _requesterHost.Services.GetRequiredService<IBus>();

    public string RabbitMqConnectionString => _infrastructure.RabbitMqConnectionString;

    public IServiceScope CreateReservationsScope() => _reservationsHost.Services.CreateScope();

    public async Task InitializeAsync()
    {
        await _infrastructure.InitializeAsync();

        var reservationsConfig = new Dictionary<string, string?>
        {
            [MongoConfigurationExtensions.ConnectionStringConfigKey] = _infrastructure.MongoConnectionString,
            [RebusConfigurationExtensions.ConnectionStringConfigKey] = _infrastructure.RabbitMqConnectionString,
        };
        var reservationsBuilder = Host.CreateApplicationBuilder();
        reservationsBuilder.Logging.ClearProviders();
        // Kept after ClearProviders so this is the only provider (mirrors GiftLists'/Identity's
        // own fixtures) — test output stays quiet while GL-37's log-capture assertions still work.
        reservationsBuilder.Logging.AddProvider(Logs);
        reservationsBuilder.Configuration.AddInMemoryCollection(reservationsConfig);
        reservationsBuilder.Services.AddBuildingBlocksMongo(reservationsBuilder.Configuration, DatabaseName);
        reservationsBuilder.Services.AddBuildingBlocksRebus(reservationsBuilder.Configuration, ReservationQueueName);
        reservationsBuilder.Services.AddReservationsInfrastructure();
        _reservationsHost = reservationsBuilder.Build();
        await _reservationsHost.StartAsync();
        await ReservationsInfrastructureServiceCollectionExtensions.EnsureIndexesAsync(
            _reservationsHost.Services, CancellationToken.None);
        await ReservationsInfrastructureServiceCollectionExtensions.SubscribeToGiftListsEventsAsync(
            _reservationsHost.Services, CancellationToken.None);

        Database = _reservationsHost.Services.GetRequiredService<IMongoDatabase>();

        var requesterBuilder = Host.CreateApplicationBuilder();
        requesterBuilder.Logging.ClearProviders();
        requesterBuilder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [RebusConfigurationExtensions.ConnectionStringConfigKey] = _infrastructure.RabbitMqConnectionString,
        });
        requesterBuilder.Services.AddBuildingBlocksRebus(
            requesterBuilder.Configuration,
            $"reservation-tests.{Guid.NewGuid():N}",
            configure: (configurer, _) => configurer.Routing(r => r.TypeBased()
                .Map<ReserveGift>(ReservationQueueName)));
        _requesterHost = requesterBuilder.Build();
        await _requesterHost.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _requesterHost.StopAsync();
        _requesterHost.Dispose();
        await _reservationsHost.StopAsync();
        _reservationsHost.Dispose();
        await _infrastructure.DisposeAsync();
    }

    /// <summary>
    /// CONVENTIONS.md "Testing": isolate by dropping the database between tests, never by
    /// restarting a container. Re-applies the unique (listId, itemId) index afterwards — dropping
    /// the database drops it too, and a test relying on it running right after a reset would
    /// otherwise pass for the wrong reason. Deliberately does NOT re-subscribe to GiftLists'
    /// events: a Rebus subscription is a durable binding on the broker, not a row this database
    /// drop touches, so re-subscribing on every reset would just be redundant work against an
    /// already-live consumer.
    /// </summary>
    public async Task ResetAsync()
    {
        await Database.Client.DropDatabaseAsync(DatabaseName);
        await ReservationsInfrastructureServiceCollectionExtensions.EnsureIndexesAsync(
            _reservationsHost.Services, CancellationToken.None);
    }
}
