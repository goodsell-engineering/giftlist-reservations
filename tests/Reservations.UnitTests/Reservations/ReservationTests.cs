using Reservations.Domain.Reservations;
using Reservations.Domain.Reservations.Events;

namespace Reservations.UnitTests.Reservations;

public sealed class ReservationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, 123, TimeSpan.Zero);

    [Fact]
    public void Create_ShouldRaiseExactlyOneGiftReserved_CarryingTheListIdItemIdAndNormalizedTimestamp()
    {
        // Arrange
        var listId = new GiftListId(Guid.NewGuid());
        var itemId = new GiftItemId(Guid.NewGuid());
        var releaseSecret = new ReleaseSecret(new string('a', ReleaseSecret.Length));

        // Act
        var reservation = Reservation.Create(ReservationId.New(), listId, itemId, releaseSecret, Now);

        // Assert
        var domainEvent = Assert.Single(reservation.DomainEvents);
        var giftReserved = Assert.IsType<GiftReserved>(domainEvent);
        Assert.Equal(listId, giftReserved.ListId);
        Assert.Equal(itemId, giftReserved.ItemId);
        Assert.Equal(reservation.ReservedAt, giftReserved.ReservedAt);
    }

    [Fact]
    public void Create_ShouldSetReleaseSecret_ToTheProvidedValue()
    {
        // Arrange
        var releaseSecret = new ReleaseSecret(new string('b', ReleaseSecret.Length));

        // Act
        var reservation = Reservation.Create(
            ReservationId.New(), new GiftListId(Guid.NewGuid()), new GiftItemId(Guid.NewGuid()), releaseSecret, Now);

        // Assert
        Assert.Equal(releaseSecret, reservation.ReleaseSecret);
    }

    [Fact]
    public void Rehydrate_ShouldRaiseNoDomainEvents()
    {
        // Arrange — none

        // Act
        var reservation = Reservation.Rehydrate(
            ReservationId.New(),
            new GiftListId(Guid.NewGuid()),
            new GiftItemId(Guid.NewGuid()),
            new ReleaseSecret(new string('c', ReleaseSecret.Length)),
            Now);

        // Assert
        Assert.Empty(reservation.DomainEvents);
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyTheCollection()
    {
        // Arrange
        var reservation = Reservation.Create(
            ReservationId.New(),
            new GiftListId(Guid.NewGuid()),
            new GiftItemId(Guid.NewGuid()),
            new ReleaseSecret(new string('d', ReleaseSecret.Length)),
            Now);

        // Act
        reservation.ClearDomainEvents();

        // Assert
        Assert.Empty(reservation.DomainEvents);
    }
}
