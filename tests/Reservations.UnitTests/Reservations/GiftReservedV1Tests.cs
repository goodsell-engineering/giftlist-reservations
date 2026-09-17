using Reservations.Contracts.Reservations.Events;

namespace Reservations.UnitTests.Reservations;

/// <summary>
/// A static shape guard for the wire contract (ARCHITECTURE.md "Domain events are not integration
/// events" — integration events, never domain events, leave the process; ARCHITECTURE.md
/// "Reservation privacy" — the guarantee is enforced by the ABSENCE of fields). Mirrors
/// <c>Identity.UnitTests.Users.UserRegisteredV1Tests</c> exactly: reflecting over the type itself
/// is enough to catch a reservation id, a release secret, or any other correlatable field added
/// later, and needs no broker to prove it.
/// </summary>
public sealed class GiftReservedV1Tests
{
    [Fact]
    public void GiftReservedV1_ShouldExposeExactlyItsThreeKnownFields_AndNeverAReservationIdOrReleaseSecret()
    {
        // Arrange — none

        // Act
        var propertyNames = typeof(GiftReservedV1).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        // Assert
        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal) { "ListId", "ItemId", "ReservedAt" },
            propertyNames);
    }
}
