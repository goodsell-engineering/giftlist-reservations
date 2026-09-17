using BuildingBlocks.Testing;
using Testcontainers.MongoDb;

namespace Reservations.IntegrationTests.Fixtures;

/// <summary>
/// The one real, containerised dependency this suite needs today (CONVENTIONS.md "Testing"),
/// started once for the whole assembly — never per test, never restarted between tests. Per-test
/// isolation is the responsibility of <see cref="ReservationsFixture"/> (drop the database), not
/// this fixture. Mirrors <c>GiftLists.IntegrationTests.Fixtures.InfrastructureFixture</c> (GL-16)
/// except it starts no RabbitMQ container: GL-35's scope is the persistence shape and the unique
/// (listId, itemId) index, not a use case, and there is no Rebus handler in this repo yet for a
/// broker to exercise (GL-36 owns that and should add one back here when it does).
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

    // GL-93: ReliableReadiness replaces the builder's default wait strategy, which reads
    // container history rather than probing the live server and so is not safe across the
    // restarts a `.WithReuse(true)` container accumulates locally — see its doc comment.
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7")
        .WithReuse(Reuse)
        .WithLabel("giftlist.suite", SuiteLabel)
        .WithReliableWaitStrategy()
        .Build();

    public string MongoConnectionString => _mongo.GetConnectionString();

    public Task InitializeAsync() => _mongo.StartReliablyAsync(SuiteLabel);

    public Task DisposeAsync() => _mongo.DisposeAsync().AsTask();
}
