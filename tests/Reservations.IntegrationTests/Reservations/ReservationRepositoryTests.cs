using MongoDB.Bson;
using MongoDB.Driver;
using Reservations.Application.Reservations;
using Reservations.Domain.Reservations;
using Reservations.IntegrationTests.Fixtures;
using Reservations.IntegrationTests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Reservations.IntegrationTests.Reservations;

/// <summary>
/// GL-35: proves the persistence shape and, specifically, that the unique compound index on
/// (listId, itemId) actually rejects a duplicate insert against a real MongoDB — not an
/// in-memory stand-in, which cannot enforce a unique index at all (CONVENTIONS.md "Testing").
/// This is "first reserver wins" itself, not a proxy for it: GL-36's <c>ReserveGiftHandler</c>
/// decides what a client sees when <see cref="IReservationRepository.AddAsync"/> returns
/// <see cref="ReservationErrors.AlreadyReserved"/> (see <c>ReserveGiftTests</c> for that,
/// end-to-end, through the real request/reply bridge), but which of two racing inserts gets that
/// answer is decided entirely here, by Mongo.
/// </summary>
[Collection(ReservationsCollection.Name)]
public sealed class ReservationRepositoryTests(ReservationsFixture fixture) : IAsyncLifetime
{
    /// <summary>
    /// The literal collection name, restated here rather than reached for on the internal
    /// <c>ReservationRepository</c> (CONVENTIONS.md "Reaching an internal from a test": neither
    /// extractable nor genuinely public), kept in sync by hand — the same choice
    /// <c>ReleaseSecretPrivacyTests</c> makes for this exact collection.
    /// </summary>
    private const string ReservationsCollectionName = "reservations";

    public Task InitializeAsync() => fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task AddAsync_ShouldReturnAlreadyReserved_WhenTheUniqueListIdItemIdIndexRejectsTheInsert()
    {
        // Arrange — CONVENTIONS.md "Testing": only a real Mongo collection enforces the unique
        // index; an in-memory repository would let both inserts silently succeed. Same
        // (listId, itemId) pair, different reservation ids and timestamps — the two fields the
        // index is actually declared on are what must collide, nothing else.
        var now = DateTimeOffset.UtcNow;
        var listId = new GiftListId(Guid.NewGuid());
        var itemId = new GiftItemId(Guid.NewGuid());
        var first = Reservation.Create(ReservationId.New(), listId, itemId, new ReleaseSecret(ReleaseSecrets.New()), now);
        var second = Reservation.Create(ReservationId.New(), listId, itemId, new ReleaseSecret(ReleaseSecrets.New()), now.AddSeconds(1));

        using var scope = fixture.CreateReservationsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var firstSaved = await repository.AddAsync(first, CancellationToken.None);
        Assert.True(firstSaved.IsSuccess);

        // Act
        var secondSaved = await repository.AddAsync(second, CancellationToken.None);

        // Assert
        Assert.True(secondSaved.IsFailure);
        Assert.Equal("reservation.already_reserved", secondSaved.Error.Code);
        var reloadedFirst = await repository.FindByIdAsync(first.Id, CancellationToken.None);
        Assert.NotNull(reloadedFirst);
        var reloadedSecond = await repository.FindByIdAsync(second.Id, CancellationToken.None);
        Assert.Null(reloadedSecond);
    }

    /// <summary>
    /// The Phase 5 gate's "concurrent-reserve load test shows no double reservation" criterion.
    /// Everything else in this file races two inserts <em>sequentially</em> — await the first,
    /// then send the second — which proves the index rejects a duplicate but never puts two
    /// inserts in flight at once. That is the one arrangement where a pre-check cannot help and
    /// only the unique index can arbitrate, and it is the arrangement the guarantee is actually
    /// about, so it is worth stating separately.
    /// </summary>
    /// <remarks>
    /// Falsifiable by construction: drop the unique (listId, itemId) index and this test fails on
    /// the success count, not on a timeout or a flake — verified by doing exactly that (all 16
    /// inserts succeed and the collection ends up holding 16 documents). The document count is
    /// asserted as well as the result tally because they can disagree: a repository that swallowed
    /// the duplicate-key error and reported failure while still writing would satisfy the tally
    /// alone.
    /// </remarks>
    [Fact]
    public async Task AddAsync_ShouldPersistExactlyOneReservation_WhenManyInsertsRaceForTheSameItem()
    {
        // Arrange — one item, many would-be reservers, all started before any of them finishes.
        const int Racers = 16;
        var now = DateTimeOffset.UtcNow;
        var listId = new GiftListId(Guid.NewGuid());
        var itemId = new GiftItemId(Guid.NewGuid());

        using var scope = fixture.CreateReservationsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();

        // A gate every racer waits on, so the inserts are genuinely simultaneous rather than
        // merely started in a loop — without it the first insert typically completes before the
        // last is even issued, and the test silently degrades to the sequential case above.
        using var start = new SemaphoreSlim(0, Racers);
        var attempts = Enumerable.Range(0, Racers).Select(async _ =>
        {
            await start.WaitAsync();
            var reservation = Reservation.Create(
                ReservationId.New(), listId, itemId, new ReleaseSecret(ReleaseSecrets.New()), now);
            return await repository.AddAsync(reservation, CancellationToken.None);
        }).ToArray();

        // Act
        start.Release(Racers);
        var results = await Task.WhenAll(attempts);

        // Assert — exactly one winner, and every loser told the same thing.
        Assert.Equal(1, results.Count(result => result.IsSuccess));
        Assert.All(
            results.Where(result => result.IsFailure),
            result => Assert.Equal("reservation.already_reserved", result.Error.Code));

        // ...and the collection agrees with the tally.
        var stored = await fixture.Database
            .GetCollection<BsonDocument>(ReservationsCollectionName)
            .CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("itemId", itemId.Value));
        Assert.Equal(1, stored);
    }

    [Fact]
    public async Task AddAsync_ShouldSucceed_WhenTheSameItemIsReservedOnDifferentLists()
    {
        // Arrange — the index is compound: colliding on ItemId alone must not conflict. Proves
        // the index was declared on the PAIR, not accidentally on ItemId alone.
        var now = DateTimeOffset.UtcNow;
        var itemId = new GiftItemId(Guid.NewGuid());
        var first = Reservation.Create(ReservationId.New(), new GiftListId(Guid.NewGuid()), itemId, new ReleaseSecret(ReleaseSecrets.New()), now);
        var second = Reservation.Create(ReservationId.New(), new GiftListId(Guid.NewGuid()), itemId, new ReleaseSecret(ReleaseSecrets.New()), now);

        using var scope = fixture.CreateReservationsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();

        // Act
        var firstSaved = await repository.AddAsync(first, CancellationToken.None);
        var secondSaved = await repository.AddAsync(second, CancellationToken.None);

        // Assert
        Assert.True(firstSaved.IsSuccess);
        Assert.True(secondSaved.IsSuccess);
    }

    [Fact]
    public async Task FindByIdAsync_ShouldRoundTripEveryField_ThroughRealMongo()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var listId = new GiftListId(Guid.NewGuid());
        var itemId = new GiftItemId(Guid.NewGuid());
        var releaseSecret = new ReleaseSecret(ReleaseSecrets.New());
        var reservation = Reservation.Create(ReservationId.New(), listId, itemId, releaseSecret, now);

        using var scope = fixture.CreateReservationsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var saved = await repository.AddAsync(reservation, CancellationToken.None);
        Assert.True(saved.IsSuccess);

        // Act
        var reloaded = await repository.FindByIdAsync(reservation.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Equal(reservation.Id, reloaded.Id);
        Assert.Equal(listId, reloaded.ListId);
        Assert.Equal(itemId, reloaded.ItemId);
        Assert.Equal(reservation.ReservedAt, reloaded.ReservedAt);
        Assert.Equal(releaseSecret, reloaded.ReleaseSecret);
    }

    [Fact]
    public async Task FindByIdAsync_ShouldReturnNull_WhenNoReservationExists()
    {
        // Arrange — none

        // Act
        using var scope = fixture.CreateReservationsScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
        var reloaded = await repository.FindByIdAsync(ReservationId.New(), CancellationToken.None);

        // Assert
        Assert.Null(reloaded);
    }
}
