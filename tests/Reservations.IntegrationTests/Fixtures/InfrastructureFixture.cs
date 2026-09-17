using BuildingBlocks.Testing;
using Testcontainers.MongoDb;
using Testcontainers.RabbitMq;

namespace Reservations.IntegrationTests.Fixtures;

/// <summary>
/// The two real, containerised dependencies (CONVENTIONS.md "Testing"), started once for the
/// whole assembly — never per test, never restarted between tests. Per-test isolation is the
/// responsibility of <see cref="ReservationsFixture"/> (drop the database, give every test its
/// own Rebus queues), not this fixture. Mirrors
/// <c>Identity.IntegrationTests.Fixtures.InfrastructureFixture</c> — GL-36 adds RabbitMQ back
/// here, alongside Mongo. GL-35 left it out because the only Rebus handlers in this repo at the
/// time (GL-34's four GiftLists projection handlers) had no wire-level entry point exercised from
/// this suite; the ReserveGift request/reply handler this batch adds is reached through the real
/// bridge, and needs a real broker to be reached through at all.
/// </summary>
public sealed class InfrastructureFixture : IAsyncLifetime
{
    private static readonly bool Reuse = Environment.GetEnvironmentVariable("CI") != "true";

    /// <summary>
    /// GL-92: every container this suite starts carries this label, and no other suite's does —
    /// see <c>GiftLists.IntegrationTests.Fixtures.InfrastructureFixture.SuiteLabel</c>'s own doc
    /// comment for the failure this prevents. The value is this project's own name without the
    /// <c>.IntegrationTests</c> suffix, lower-cased — <c>SuiteLabelRuleTests</c> derives it
    /// mechanically from the .csproj and fails if they disagree.
    /// </summary>
    private const string SuiteLabel = "reservations";

    // GL-93: ReliableReadiness replaces the builders' default wait strategies, which read
    // container history rather than probing the live server and so are not safe across the
    // restarts a `.WithReuse(true)` container accumulates locally — see its doc comment.
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7")
        .WithReuse(Reuse)
        .WithLabel("giftlist.suite", SuiteLabel)
        .WithReliableWaitStrategy()
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3.13-management")
        .WithReuse(Reuse)
        .WithLabel("giftlist.suite", SuiteLabel)
        .WithReliableWaitStrategy()
        .Build();

    public string MongoConnectionString => _mongo.GetConnectionString();

    public string RabbitMqConnectionString => _rabbitMq.GetConnectionString();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_mongo.StartReliablyAsync(SuiteLabel), _rabbitMq.StartReliablyAsync(SuiteLabel));
    }

    public async Task DisposeAsync()
    {
        await _mongo.DisposeAsync().AsTask();
        await _rabbitMq.DisposeAsync().AsTask();
    }
}
