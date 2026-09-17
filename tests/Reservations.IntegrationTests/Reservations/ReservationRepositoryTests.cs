using Reservations.Application.Reservations;
using Reservations.Domain.Reservations;
using Reservations.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Reservations.IntegrationTests.Reservations;

/// <summary>
/// GL-35: proves the persistence shape and, specifically, that the unique compound index on
/// (listId, itemId) actually rejects a duplicate insert against a real MongoDB — not an
/// in-memory stand-in, which cannot enforce a unique index at all (CONVENTIONS.md "Testing").
/// This is "first reserver wins" itself, not a proxy for it: GL-36's ReserveGift handler will
/// decide what a client sees when <see cref="IReservationRepository.AddAsync"/> returns
/// <see cref="ReservationErrors.AlreadyReserved"/>, but which of two racing inserts gets that
/// answer is decided entirely here, by Mongo, before any handler exists.
/// </summary>
[Collection(ReservationsCollection.Name)]
public sealed class ReservationRepositoryTests(ReservationsFixture fixture) : IAsyncLifetime
{
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
        var first = Reservation.Create(ReservationId.New(), listId, itemId, now);
        var second = Reservation.Create(ReservationId.New(), listId, itemId, now.AddSeconds(1));

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

    [Fact]
    public async Task AddAsync_ShouldSucceed_WhenTheSameItemIsReservedOnDifferentLists()
    {
        // Arrange — the index is compound: colliding on ItemId alone must not conflict. Proves
        // the index was declared on the PAIR, not accidentally on ItemId alone.
        var now = DateTimeOffset.UtcNow;
        var itemId = new GiftItemId(Guid.NewGuid());
        var first = Reservation.Create(ReservationId.New(), new GiftListId(Guid.NewGuid()), itemId, now);
        var second = Reservation.Create(ReservationId.New(), new GiftListId(Guid.NewGuid()), itemId, now);

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
        var reservation = Reservation.Create(ReservationId.New(), listId, itemId, now);

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
